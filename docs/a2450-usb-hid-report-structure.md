# A2450 USB HID Report 结构

## 确认方式

通过 USBPcap + Wireshark 在 USB 传输层直接抓包确认。

抓包文件：

- `logs/a2450-usb-fn-test.pcapng`（完整测试，270 帧）
- `logs/a2450-usb-fn-test-small.pcapng`（精简测试，74 帧）

抓包时键盘处于 **USB 有线模式**，蓝牙已断开。

## HID Report 格式

A2450 USB 模式下，键盘通过 Interrupt IN 端点（0x82）发送 **10 字节** HID Report：

| Byte | 字段 | 说明 |
|------|------|------|
| 0 | Report ID | 固定 `0x01` |
| 1 | Modifier | 修饰键位掩码 |
| 2 | Reserved | 固定 `0x00` |
| 3 | Key Slot 1 | 第一个按键 HID Usage Code |
| 4 | Key Slot 2 | 第二个按键 HID Usage Code |
| 5 | Key Slot 3 | 第三个按键 HID Usage Code |
| 6 | Key Slot 4 | 第四个按键 HID Usage Code |
| 7 | Key Slot 5 | 第五个按键 HID Usage Code |
| 8 | Key Slot 6 | 第六个按键 HID Usage Code |
| 9 | **Apple Fn State** | **Apple 私有 Fn / Globe 状态字节** |

## Modifier 字节位定义（Byte 1）

**注意：A2450 的 modifier 映射与标准 HID 规范不同。**

标准 HID 定义 bit 0 = Left Shift，bit 1 = Left Ctrl。
但 USBPcap 抓包显示 A2450 按下 Left Ctrl 时 Byte 1 = 0x01（bit 0）。

以下为 A2450 实际观察值：

| Bit | 掩码 | 含义 |
|-----|------|------|
| 0 | 0x01 | **Left Ctrl**（A2450 实测） |
| 1 | 0x02 | Left Shift |
| 2 | 0x04 | Left Alt（Option） |
| 3 | 0x08 | Left GUI（Command） |
| 4 | 0x10 | Right Ctrl |
| 5 | 0x20 | Right Shift |
| 6 | 0x40 | Right Alt |
| 7 | 0x80 | Right GUI |

## Apple Fn State 字节（Byte 9）

这是 Apple 键盘的 **非标准扩展字段**，不在 HID Usage Tables 规范中。

| 值 | 含义 |
|----|------|
| 0x00 | Fn / Globe 未按下 |
| 0x02 | Fn / Globe 按下 |

Windows 标准键盘驱动（kbdhid）**不解析此字节**，导致 Fn 键在 Raw Input / HID API 层不可见。

## 抓包验证数据

### 空闲状态

```
01 00 00 00 00 00 00 00 00 00
```

### Fn / Globe 单独按下

```
01 00 00 00 00 00 00 00 00 02
```

Byte 9 = 0x02，其余全 0。

### Left Ctrl 单独按下

```
01 01 00 00 00 00 00 00 00 00
```

Byte 1 bit 0 = 1（Left Ctrl，A2450 实测），Byte 9 = 0x00。

### Fn + Left Ctrl 同时按下

```
01 01 00 00 00 00 00 00 00 02
```

Byte 1 bit 0 = 1，Byte 9 = 0x02。两个状态可同时检测。

### Backspace

```
01 00 00 2A 00 00 00 00 00 00
```

Usage 0x2A = Backspace。

### F1

```
01 00 00 3A 00 00 00 00 00 00
```

Usage 0x3A = F1。

### Fn + F1

```
01 00 00 3A 00 00 00 00 00 02
```

Key Code 不变（0x3A），Byte 9 = 0x02。Fn 不改变功能键的 Usage Code。

### Fn + 方向键（关键发现）

| 组合 | HID Report | Byte 3 | Byte 9 |
|------|-----------|--------|--------|
| ↑ 单独 | `01 00 00 52 00 00 00 00 00 00` | 0x52 (Up) | 0x00 |
| Fn+↑ | `01 00 00 50 00 00 00 00 00 02` | **0x50** (Left) | 0x02 |
| ↓ 单独 | `01 00 00 51 00 00 00 00 00 00` | 0x51 (Down) | 0x00 |
| Fn+↓ | `01 00 00 52 00 00 00 00 00 02` | **0x52** (Up) | 0x02 |
| ← 单独 | `01 00 00 50 00 00 00 00 00 00` | 0x50 (Left) | 0x00 |
| Fn+← | `01 00 00 4F 00 00 00 00 00 02` | **0x4F** (Right) | 0x02 |
| → 单独 | `01 00 00 4F 00 00 00 00 00 00` | 0x4F (Right) | 0x00 |
| Fn+→ | `01 00 00 51 00 00 00 00 00 02` | **0x51** (Down) | 0x02 |

**发现：Fn + 方向键时，键盘固件对 Key Code 做了交叉重映射。**

这说明 Apple 固件在 Fn 层对方向键做了硬件级重编码，而非仅设置 Byte 9 标志。

### F1~F12 与 Fn+F1~F12 对比

| 按键 | 纯按下 Key Code | Fn+按下 Key Code | 差异 |
|------|----------------|-----------------|------|
| F1 | 0x3A | 0x3A | 无变化 |
| F2 | 0x3B | 0x3B | 无变化 |
| F3 | 0x3C | 0x3C | 无变化 |
| F4 | 0x3D | 0x3D | 无变化 |
| F5 | 0x3E | 0x3E | 无变化 |
| F6 | 0x3F | 0x3F | 无变化 |
| F7 | 0x40 | 0x40 | 无变化 |
| F8 | 0x41 | 0x41 | 无变化 |
| F9 | 0x42 | 0x42 | 无变化 |
| F10 | 0x43 | 0x43 | 无变化 |
| F11 | 0x44 | 0x44 | 无变化 |
| F12 | 0x45 | 0x45 | 无变化 |

**结论：Fn + F1~F12 仅改变 Byte 9，Key Code 不变。驱动需要同时检测 Byte 9 和 Key Code 来判断是否触发媒体键映射。**

## USB 端点信息

| 属性 | 值 |
|------|-----|
| 端点地址 | 0x82 (Interrupt IN) |
| 传输类型 | Interrupt Transfer |
| HID Report ID | 0x01 |
| Report 长度 | 10 字节（含 Report ID） |

## A2450 HID Collection 总览（真实设备验证）

通过 `A2450DescriptorDump` 工具在真实 A2450 USB 设备上确认：

| 接口 | UsagePage | Usage | InputReportLen | 说明 |
|------|-----------|-------|---------------|------|
| **COL01** | 0x0001 | 0x0006 | **10** | 标准键盘（本项目主要处理对象） |
| **COL02** | **0x000C** | 0x0001 | **2** | **Consumer Control（媒体键通道）** |
| COL03 | 0xFF00 | 0x0006 | 65 | Apple / Vendor Defined（暂不处理） |
| MI_00 COL01 | 0xFF00 | 0x000B | 5 | Vendor Defined |
| MI_00 COL02 | 0xFF00 | 0x0014 | 3 | Vendor Defined |

**关键结论：**

- **COL01** 负责键盘 Report 转换（Fn→Ctrl、Ctrl→FnLayer、Backspace/方向键映射）
- **COL02** 是 Consumer Control 接口，可用于媒体键（Previous Track、Play/Pause、Volume 等）
- **COL03** 是供应商定义接口，暂不处理
- 之前假设 COL03 是 Consumer Control 是错误的，真实设备上 **COL02 才是 Consumer Control**

## Windows 用户态 API 不可见的原因

Windows 标准键盘驱动栈：

```
kbdhid.sys (HID miniport driver)
  ↓
kbdclass.sys (keyboard class driver)
  ↓
Windows Raw Input / GetAsyncKeyState / SendInput
```

`kbdhid.sys` 只解析标准 HID Keyboard Usage Table（Byte 1~8），**丢弃 Byte 9**。

因此：

- Raw Input 看到的 `KBDLLHOOKSTRUCT.scanCode` 不含 Byte 9
- `HidD_GetInputReport` 返回的 Report 被 kbdhid 消费，用户态拿不到原始 Byte 9
- HidSharp `stream.Read()` 同样被 kbdhid 拦截

## 关键结论

1. **Fn / Globe 在 USB 传输层完全可见**，编码为 Byte 9 = 0x02
2. **Windows 用户态 API 不可见**，因为 kbdhid 丢弃 Byte 9
3. **必须绕过 kbdhid** 才能读取 Byte 9，方案包括：
   - HID Filter Driver（拦截 IRP_MJ_READ）
   - 直接读取 HID 设备文件（需独占访问，需绕过 kbdhid 绑定）
4. **Fn + 方向键有固件级重映射**，驱动需要在重映射后的 Key Code 基础上再做翻译
5. **Byte 9 可用于判断 Fn 状态**，是实现 Fn/Ctrl 交换的核心依据

## 与蓝牙模式的关系

本次抓包基于 USB 有线模式。蓝牙模式下的 HID Report 结构 **可能不同**：

- 蓝牙 HID 可能使用不同的 Report ID
- Byte 9 的 Fn 标志位是否一致，需要蓝牙抓包验证
- 蓝牙模式下 kbdhid 同样会丢弃非标准字节

蓝牙抓包需要使用 Bluetooth HCI snoop 或专用工具，不在当前 USBPcap 范围内。
