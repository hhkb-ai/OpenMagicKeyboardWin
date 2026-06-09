# OpenMagicKeyboardA2450Filter

Apple Magic Keyboard A2450 的 HID Filter Driver 设计目录。

## 当前状态

**设计 + 骨架阶段**。已完成 HID Report 抓包分析、驱动 MVP 设计、KMDF 驱动骨架代码。尚未编译或安装。

## 已完成

1. USBPcap + Wireshark 抓包确认 A2450 USB HID Report 结构
2. 确认 Byte 9 = 0x02 表示 Fn / Globe 按下
3. 确认 Windows 用户态 API（Raw Input / HID API）无法看到 Byte 9
4. 完成 Fn/Ctrl 交换状态机设计
5. 完成驱动 MVP 设计文档
6. 编写 KMDF 驱动骨架代码（不编译、不安装）

## 关键发现

A2450 USB 模式下 HID Report 为 10 字节：

| Byte | 字段 | 说明 |
|------|------|------|
| 0 | Report ID | 固定 0x01 |
| 1 | Modifier | bit 0 = Left Ctrl（A2450 实测） |
| 2 | Reserved | 0x00 |
| 3-8 | Key slots | HID Usage Codes |
| 9 | Apple Fn | 0x00 = 释放, 0x02 = 按下 |

**注意：A2450 的 Left Ctrl 是 bit 0 (0x01)，不是标准 HID 定义的 bit 1。**

## HID Collection 总览（真实设备验证）

| 接口 | UsagePage | Usage | InputReportLen | 说明 |
|------|-----------|-------|---------------|------|
| COL01 | 0x0001 | 0x0006 | 10 | 标准键盘 |
| **COL02** | **0x000C** | 0x0001 | **2** | **Consumer Control（媒体键通道）** |
| COL03 | 0xFF00 | 0x0006 | 65 | Vendor Defined（暂不处理） |

## 驱动目标

- HIDClass lower filter driver
- KMDF 框架
- 仅绑定 VID_05AC & PID_029C（A2450 USB 模式）
- COL01：物理 Fn → 输出 Left Ctrl，物理 Left Ctrl → 内部 FnLayer
- COL01：FnLayer + 方向键/Backspace → Home/End/PageUp/PageDown/Delete
- COL02：FnLayer + F7~F12 → 媒体键（通过 Consumer Control 通道）

## 文件说明

| 文件 | 说明 |
|------|------|
| `A2450Report.h` | HID Report 结构定义和常量 |
| `ReportTransform.h` | 转换函数接口和状态结构 |
| `ReportTransform.c` | 核心转换逻辑实现 |
| `Driver.c` | KMDF DriverEntry 和 DeviceAdd |
| `Device.c` | 设备上下文和初始化 |
| `Filter.c` | HID IOCTL 拦截设计（伪代码） |
| `FnCtrlStateMachine.md` | 状态机设计、伪代码、边界情况、测试用例 |
| `OpenMagicKeyboardA2450.inf.template` | INF 文件模板（待完善） |
| `README.md` | 本文件 |

## 相关文档

- `docs/a2450-usb-hid-report-structure.md` — HID Report 结构详解
- `docs/a2450-filter-driver-mvp-plan.md` — 驱动 MVP 设计文档
- `docs/driver-plan.md` — 驱动方案选型

## 用户态测试

转换逻辑已在用户态用 xUnit 验证：

```
tests/OpenMagicKeyboard.TransformTests/
```

运行测试：`dotnet test tests/OpenMagicKeyboard.TransformTests/`

## 当前不做

- 不安装驱动
- 不开启 TESTSIGNING
- 不关闭 Secure Boot
- 不修改注册表
- 不编译驱动代码
