# A2450 Filter Driver MVP 设计

## 1. 当前已知 HID Report 结构

通过 USBPcap + Wireshark 抓包确认，A2450 USB 模式下键盘发送 **10 字节** HID Report：

```
Byte 0 = Report ID (0x01)
Byte 1 = Modifier (标准 HID 修饰键位掩码)
Byte 2 = Reserved (0x00)
Byte 3 = Key Slot 1 (HID Usage Code)
Byte 4 = Key Slot 2
Byte 5 = Key Slot 3
Byte 6 = Key Slot 4
Byte 7 = Key Slot 5
Byte 8 = Key Slot 6
Byte 9 = Apple Fn / Globe State (0x00=释放, 0x02=按下)
```

详细结构见 `docs/a2450-usb-hid-report-structure.md`。

## 2. 为什么普通用户态 API 不能解决

### 已尝试的方案

| 方案 | 结果 | 原因 |
|------|------|------|
| Raw Input API | ❌ 看不到 Fn | kbdhid 丢弃 Byte 9 |
| HidD_GetInputReport | ❌ 被 kbdhid 拦截 | 设备被键盘驱动独占 |
| HidSharp stream.Read() | ❌ 无数据 | 同上 |
| ReadFile (同步/异步) | ❌ 无数据 | 同上 |
| ReadFile Overlapped I/O | ❌ 无数据 | 同上 |

### 根本原因

Windows 键盘驱动栈：

```
应用程序 (Raw Input / GetAsyncKeyState)
    ↓
kbdclass.sys (键盘类驱动)
    ↓
kbdhid.sys (HID 小端口驱动) ← 只解析 Byte 1~8，丢弃 Byte 9
    ↓
HID USB 微型驱动 (hidusb.sys)
    ↓
USB 端点 0x82 (Interrupt IN)
```

`kbdhid.sys` 是 HID Keyboard miniport driver，它只处理标准 HID Usage Table 定义的字段（Modifier + 6 Key Slots）。Apple 私有的 Byte 9 被忽略，永远不会传递到上层。

### 为什么不能绕过 kbdhid

- `CreateFile` 打开 HID 设备时，kbdhid 已经绑定，设备处于独占状态
- 卸载 kbdhid 会失去标准键盘功能
- 用户态无法强制解绑 HID 设备与 kbdhid 的关联

## 3. 为什么需要 HID / Keyboard Filter Driver

### 方案选择

| 方案 | 可行性 | 说明 |
|------|--------|------|
| HIDClass lower filter | ✅ 推荐 | 在 kbdhid 之下拦截原始 HID Report |
| Keyboard class filter | ⚠️ 可行但粗暴 | 在 kbdclass 层拦截，拿不到 Byte 9 |
| 卸载 kbdhid + 自写驱动 | ❌ 过于复杂 | 需要完整实现键盘功能 |
| 用户态 HID 直读 | ❌ 已验证不可行 | kbdhid 独占设备 |

### HIDClass lower filter 方案

在 `hidusb.sys` 和 `kbdhid.sys` 之间插入一个 filter driver：

```
kbdhid.sys (键盘类驱动)
    ↓
[OpenMagicKeyboardA2450Filter.sys] ← 我们的 filter driver
    ↓
hidusb.sys (HID USB 微型驱动)
```

Filter driver 可以：

1. **读取原始 10 字节 HID Report**（包括 Byte 9）
2. **修改 Report 内容**后再传递给 kbdhid
3. **仅绑定 A2450 设备**，不影响其他键盘

### 驱动类型

- **KMDF**（Kernel-Mode Driver Framework）
- **HIDClass lower filter** driver
- 绑定条件：`VID_05AC & PID_029C`（A2450 USB 模式）

## 4. 驱动 MVP 目标

### 核心交换逻辑

```
物理 Fn / Globe  →  输出 Left Ctrl
物理 Left Ctrl   →  内部 FnLayer 状态（不输出 Ctrl）
```

### FnLayer 组合键映射

| 物理输入（Ctrl 按住时） | 输出 |
|------------------------|------|
| Backspace | Delete |
| ↑ | Page Up |
| ↓ | Page Down |
| ← | Home |
| → | End |
| F7 | Previous Track |
| F8 | Play/Pause |
| F9 | Next Track |
| F10 | Mute |
| F11 | Volume Down |
| F12 | Volume Up |

### 不在 MVP 范围内

- 蓝牙模式支持（需要额外抓包验证）
- 图形化配置界面
- 系统托盘应用
- 持久化配置
- 安装程序

## 5. 输入报告解析

### 读取方式

Filter driver 在 `IRP_MJ_READ` 或 `EvtIoRead` 回调中拦截 HID Report。

### 字段提取伪代码

```c
// inputReport 是从下层驱动读取的原始 10 字节 HID Report
uint8_t reportId    = inputReport[0];  // 0x01
uint8_t modifier    = inputReport[1];  // 标准修饰键
uint8_t reserved    = inputReport[2];  // 0x00
uint8_t keySlot1    = inputReport[3];  // HID Usage Code
uint8_t keySlot2    = inputReport[4];
uint8_t keySlot3    = inputReport[5];
uint8_t keySlot4    = inputReport[6];
uint8_t keySlot5    = inputReport[7];
uint8_t keySlot6    = inputReport[8];
uint8_t appleFnState = inputReport[9]; // Apple 私有 Fn 状态
```

### 状态提取

```c
bool physicalFnDown      = (appleFnState & 0x02) != 0;
bool physicalLeftCtrlDown = (modifier & 0x01) != 0;  // bit 0 on A2450!
```

## 6. 输出报告重写规则

### 总体策略

1. 复制输入报告到输出缓冲区
2. 根据状态机修改输出缓冲区
3. 将修改后的报告传递给上层驱动（kbdhid）

### 伪代码

```c
void transformReport(uint8_t* report, size_t len) {
    if (len < 10) return;

    bool physicalFnDown      = (report[9] & 0x02) != 0;
    bool physicalLeftCtrlDown = (report[1] & 0x01) != 0;  // bit 0 on A2450

    // --- Step 1: Physical Fn → Left Ctrl ---
    if (physicalFnDown) {
        report[1] |= 0x01;   // 设置 Left Ctrl bit (bit 0 on A2450)
        report[9] &= ~0x02;  // 清除 Apple Fn 标志
    }

    // --- Step 2: Physical Left Ctrl → internal FnLayer ---
    if (physicalLeftCtrlDown) {
        report[1] &= ~0x01;  // 移除 Left Ctrl（不发送给系统）
        // internalFnLayer = true;（在驱动全局状态中记录）
    }

    // --- Step 2b: Re-apply Fn-mapped Ctrl ---
    if (physicalFnDown) {
        report[1] |= 0x01;  // 恢复 Fn 映射的 Ctrl
    }

    // --- Step 3: FnLayer key remapping ---
    if (internalFnLayer) {
        for (int i = 3; i <= 8; i++) {
            report[i] = remapFnLayerKey(report[i]);
        }
    }
}
```

## 7. Fn → Left Ctrl 逻辑

### 触发条件

```c
bool physicalFnDown = (report[9] & 0x02) != 0;
```

### 输出修改

```c
if (physicalFnDown) {
    // 在 modifier 字节设置 Left Ctrl bit (bit 0 on A2450)
    report[1] |= 0x01;

    // 清除 Apple 私有 Fn 标志，避免上层看到非标准字节
    report[9] &= ~0x02;
}
```

### 是否必须清除 Byte 9

**建议清除**，理由：

1. kbdhid 不解析 Byte 9，清除与否对标准键盘行为无影响
2. 清除后保持输出报告的"干净"，避免潜在的兼容性问题
3. 如果后续需要检测 Fn 状态（例如组合键判断），在清除前已提取到 `physicalFnDown` 变量

**不清除的风险**：

1. 某些 HID 上层驱动可能对非零 Byte 9 有意外行为
2. 调试时可能混淆：看到 Byte 9 = 0x02 但系统已把它当作 Ctrl

## 8. Left Ctrl → FnLayer 逻辑

### 触发条件

```c
bool physicalLeftCtrlDown = (report[1] & 0x01) != 0;  // bit 0 on A2450
```

### 输出修改

```c
if (physicalLeftCtrlDown) {
    // 从 modifier 中移除 Left Ctrl
    report[1] &= ~0x01;

    // 设置内部 FnLayer 状态
    state->fnLayerActive = true;
} else {
    state->fnLayerActive = false;
}
```

### 为什么不输出 Left Ctrl

物理 Left Ctrl 被重新定义为 FnLayer 修饰键。如果同时输出 Ctrl，会导致：

- `Ctrl+C`、`Ctrl+V` 等系统快捷键被意外触发
- 用户按住 Ctrl（物理）+ 字母时，会同时产生 FnLayer 映射和 Ctrl 组合

## 9. FnLayer 键位映射表

### 标准键映射（通过 Key Code 替换）

| 物理键 | 原始 Usage | 输出 Usage | 输出键名 |
|--------|-----------|-----------|---------|
| Backspace | 0x2A | 0x4C | Delete |
| ↑ | 0x52 | 0x4B | Page Up |
| ↓ | 0x51 | 0x4E | Page Down |
| ← | 0x50 | 0x4A | Home |
| → | 0x4F | 0x4D | End |

### 注意：方向键的固件重映射

USBPcap 抓包发现，A2450 固件在 Fn 层对方向键做了交叉重映射：

| 物理键 | 纯按下 Usage | Fn+按下 Usage（固件输出） |
|--------|-------------|------------------------|
| ↑ | 0x52 | 0x50 |
| ↓ | 0x51 | 0x52 |
| ← | 0x50 | 0x4F |
| → | 0x4F | 0x51 |

**但这不影响驱动设计**，因为：

1. 驱动检测的是 `physicalFnDown`（Byte 9 bit），不是 Key Code 变化
2. 当 `physicalLeftCtrlDown`（我们的 FnLayer）激活时，键盘固件不会做重映射（因为物理 Fn 没有按下）
3. 驱动需要映射的是 **原始方向键 Usage**，不是 Fn 层重映射后的 Usage

映射逻辑：

```c
uint8_t remapFnLayerKey(uint8_t usage) {
    switch (usage) {
        case 0x2A: return 0x4C;  // Backspace → Delete
        case 0x52: return 0x4B;  // Up → Page Up
        case 0x51: return 0x4E;  // Down → Page Down
        case 0x50: return 0x4A;  // Left → Home
        case 0x4F: return 0x4D;  // Right → End
        default:   return usage; // 其他键不变
    }
}
```

## 10. 媒体键映射的风险和后续方案

### 问题

标准 HID Keyboard Report（Report ID 0x01）只能输出键盘 Usage Page（0x07）的键码。

媒体键（Play/Pause、Volume 等）属于 **Consumer Control Usage Page（0x0C）**，需要通过单独的 HID Report 输出。

### A2450 的 Consumer Control 接口（真实设备验证）

通过 `A2450DescriptorDump` 在真实 A2450 USB 设备上确认：

| 接口 | UsagePage | Usage | InputReportLen | 说明 |
|------|-----------|-------|---------------|------|
| COL01 | 0x0001 | 0x0006 | 10 | 标准键盘 |
| **COL02** | **0x000C** | 0x0001 | **2** | **Consumer Control** |
| COL03 | 0xFF00 | 0x0006 | 65 | Vendor Defined |

**关键修正：COL02 才是 Consumer Control，不是 COL03。**

### 媒体键方案

| 方案 | 可行性 | 说明 |
|------|--------|------|
| 驱动层合成 Consumer Control Input Report | ✅ 可行 | COL02 = UsagePage 0x0C, 2 字节输入；不要向物理 COL02 写入 Output Report |
| 模拟 Consumer Control 设备 | ❌ 过于复杂 | 需要虚拟 HID 设备驱动 |

### MVP-B 媒体键计划

FnLayer + F7~F12 映射为 Consumer Control Usage，通过 COL02 通道输出：

| 物理键 | Key Usage | Consumer Usage |
|--------|-----------|---------------|
| F7 | 0x40 | Previous Track (0x00B6) |
| F8 | 0x41 | Play/Pause (0x00CD) |
| F9 | 0x42 | Next Track (0x00B5) |
| F10 | 0x43 | Mute (0x00E2) |
| F11 | 0x44 | Volume Down (0x00EA) |
| F12 | 0x45 | Volume Up (0x00E9) |

触发条件：Physical Left Ctrl（FnLayer）+ F7~F12。
注意：Physical Fn 被映射为 Left Ctrl，不是 FnLayer，所以 Fn+F7 不触发媒体键。

后续版本需要：

1. 抓取 COL03 的 HID Report Descriptor（使用 `HidP_GetCaps` 或 USBPcap）
2. 确认是否有 Consumer Control Usage Page
3. 设计 Consumer Report 输出路径

## 11. 驱动安装风险

### 测试签名模式

开发阶段需要开启测试签名：

```powershell
bcdedit /set testsigning on
# 重启生效
```

**风险**：

- 桌面右下角显示"测试模式"水印
- 部分游戏反作弊系统可能拒绝运行
- 某些安全软件可能拦截测试签名驱动

### Secure Boot

测试签名驱动在 Secure Boot 开启时 **无法加载**。

选项：

1. 关闭 Secure Boot（降低系统安全性）
2. 使用正式签名（需要 EV 代码签名证书 + Microsoft 硬件仪表板）
3. 使用 Windows 开发人员模式（Windows 10/11）

### 系统稳定性

内核驱动 bug 可能导致：

- **蓝屏（BSOD）**：空指针、内存越界、IRQL 不当
- **键盘失真**：错误的 Report 修改导致按键混乱
- **设备挂起**：IRP 未正确完成导致设备无响应

### MVP 安全策略

1. **仅在开发机测试**，不在生产环境使用
2. **实现设备绑定检查**，只处理 VID_05AC & PID_029C
3. **添加调试日志**，所有 Report 修改可追踪
4. **设置卸载路径**，出问题时可快速移除驱动

## 12. 当前状态：Pre-VM hardening 已完成

一个最小 KMDF HID lower-filter 原型已存在，实现了 C 变换逻辑和 IOCTL_HID_READ_REPORT 拦截。Pre-VM report-length hardening 已合并到 main 并通过 WDK 验证（Debug x64 + Release x64，0 warnings，0 errors）。

但它仍然是构建阶段产物：
- 未签名
- 未安装
- 未加载
- 未绑定到真实硬件
- 未在 VM 中测试
- 未在真实 A2450 上验证
- 不可用 / 不可用于生产

当前阶段允许：

1. ✅ 完成 HID Report 结构文档
2. ✅ 完成状态机设计文档
3. ✅ 完成驱动 MVP 设计文档
4. ✅ 编写 KMDF 驱动框架代码
5. ✅ 编写 INF 文件模板
6. ✅ 编写 C# 单元测试
7. ✅ WDK Debug/Release 构建验证
8. ✅ 文档同步
9. ❌ 不安装驱动
10. ❌ 不开启 TESTSIGNING

下一步：

1. VM load test
2. Real A2450 functional test in controlled environment

## 13. 后续开发任务拆分

### Phase 1：设计完成 ✅

- [x] USBPcap 抓包分析
- [x] HID Report 结构确认
- [x] 状态机设计
- [x] 驱动 MVP 设计文档

### Phase 2：代码框架 ✅

- [x] KMDF 驱动项目骨架
- [x] INF 文件模板（设备绑定 VID_05AC & PID_029C）
- [x] EvtIoInternalDeviceControl 回调实现
- [x] Report 变换逻辑实现
- [x] 单元测试框架（C# 37/37 passed）

### Phase 3：编译验证 ✅ (WDK verified)

- [x] 安装 WDK
- [x] 编译驱动（Debug x64 + Release x64，0 warnings，0 errors）
- [x] Report-length hardening（merged to main）
- [ ] 开启 TESTSIGNING（VM 测试阶段）
- [ ] 加载驱动并测试（VM 测试阶段）

### Phase 4：功能验证 ⏳ (not started)

- [ ] Fn → Left Ctrl 验证（VM）
- [ ] Left Ctrl → FnLayer 验证（VM）
- [ ] FnLayer + Backspace → Delete 验证（VM）
- [ ] FnLayer + 方向键 → Home/End/PageUp/PageDown 验证（VM）
- [ ] 多键同时按下测试
- [ ] 长时间稳定性测试
- [ ] Real A2450 functional test in controlled environment

### Phase 5：完善 ⏳ (not started)

- [ ] 媒体键支持
- [ ] 蓝牙模式支持
- [ ] 用户态配置工具
- [ ] 系统托盘应用
- [ ] 正式签名
- [ ] 安装程序
