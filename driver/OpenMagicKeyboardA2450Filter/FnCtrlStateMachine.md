# Fn/Ctrl State Machine

## 输入报告字段说明

A2450 USB HID Report 为 10 字节：

| 偏移 | 长度 | 字段 | 说明 |
|------|------|------|------|
| 0 | 1 | reportId | 固定 0x01 |
| 1 | 1 | modifier | 标准 HID 修饰键位掩码 |
| 2 | 1 | reserved | 固定 0x00 |
| 3 | 1 | keySlot[0] | 第一个按键 HID Usage Code |
| 4 | 1 | keySlot[1] | 第二个按键 |
| 5 | 1 | keySlot[2] | 第三个按键 |
| 6 | 1 | keySlot[3] | 第四个按键 |
| 7 | 1 | keySlot[4] | 第五个按键 |
| 8 | 1 | keySlot[5] | 第六个按键 |
| 9 | 1 | appleFnState | Apple 私有 Fn/Globe 状态 |

## Modifier 位定义（A2450 USBPcap 实际观察值）

**注意：A2450 的 modifier 映射与标准 HID 规范不同。**

标准 HID 定义 bit 0 = Left Shift，bit 1 = Left Ctrl。
但 USBPcap 抓包显示 A2450 按下 Left Ctrl 时 Byte 1 = 0x01（bit 0）。

以下为 A2450 实际值：

| Bit | 掩码 | 含义 |
|-----|------|------|
| 0 | 0x01 | **Left Ctrl**（A2450 实测） |
| 1 | 0x02 | Left Shift |
| 2 | 0x04 | Left Alt (Option) |
| 3 | 0x08 | Left GUI (Command) |
| 4 | 0x10 | Right Ctrl |
| 5 | 0x20 | Right Shift |
| 6 | 0x40 | Right Alt |
| 7 | 0x80 | Right GUI |

## 状态提取

```c
bool physicalFnDown       = (report[9] & 0x02) != 0;
bool physicalLeftCtrlDown  = (report[1] & 0x01) != 0;  // bit 0 on A2450!
bool physicalLeftShiftDown = (report[1] & 0x02) != 0;
bool physicalLeftAltDown   = (report[1] & 0x04) != 0;
bool physicalLeftGUIDown   = (report[1] & 0x08) != 0;
```

## 驱动状态结构

```c
typedef struct {
    bool fnLayerActive;       // 内部 FnLayer 是否激活
    bool swapEnabled;         // Fn/Ctrl 交换是否启用
    bool prevPhysicalFnDown;  // 上一次 Fn 状态（用于边缘检测）
    bool prevLeftCtrlDown;    // 上一次 Ctrl 状态
} A2450_DRIVER_STATE;
```

## 完整变换伪代码

```c
void transformReport(A2450_DRIVER_STATE* state, uint8_t* report, size_t len) {
    if (len < 10) return;
    if (report[0] != 0x01) return;  // 只处理键盘 Report ID 0x01

    bool physicalFnDown      = (report[9] & 0x02) != 0;
    bool physicalLeftCtrlDown = (report[1] & 0x01) != 0;  // bit 0 on A2450

    if (!state->swapEnabled) {
        // 交换未启用，原样传递
        return;
    }

    // --- Step 1: Physical Fn → Left Ctrl ---
    if (physicalFnDown) {
        report[1] |= 0x01;   // 设置 Left Ctrl bit (bit 0 on A2450)
        report[9] &= ~0x02;  // 清除 Apple Fn 标志
    }

    // --- Step 2: Physical Left Ctrl → FnLayer ---
    if (physicalLeftCtrlDown) {
        report[1] &= ~0x01;  // 移除 Left Ctrl modifier (bit 0 on A2450)
        state->fnLayerActive = true;
    } else {
        state->fnLayerActive = false;
    }

    // --- Step 3: FnLayer key remapping ---
    if (state->fnLayerActive) {
        for (int i = 3; i <= 8; i++) {
            if (report[i] != 0x00) {
                report[i] = remapFnLayerKey(report[i]);
            }
        }
    }

    state->prevPhysicalFnDown = physicalFnDown;
    state->prevLeftCtrlDown   = physicalLeftCtrlDown;
}
```

## FnLayer 键位映射表

```c
uint8_t remapFnLayerKey(uint8_t usage) {
    switch (usage) {
        case 0x2A: return 0x4C;  // Backspace → Delete
        case 0x52: return 0x4B;  // Up        → Page Up
        case 0x51: return 0x4E;  // Down      → Page Down
        case 0x50: return 0x4A;  // Left      → Home
        case 0x4F: return 0x4D;  // Right     → End
        default:   return usage; // 其他键不变
    }
}
```

### FnLayer 媒体键（通过 COL02 Consumer Control）

真实设备 DescriptorDump 确认：

- **COL02** = Consumer Control, UsagePage 0x000C, Usage 0x0001, InputReportByteLength 2
- **COL03** = Vendor Defined (0xFF00), 不是 Consumer Control

媒体键应通过 COL02 的 Consumer Control 通道输出，不能塞进 COL01 的 10 字节键盘 Report。

| 物理键 | Key Usage | Consumer Usage | Consumer Code |
|--------|-----------|---------------|---------------|
| F7 | 0x40 | Previous Track | 0x00B6 |
| F8 | 0x41 | Play/Pause | 0x00CD |
| F9 | 0x42 | Next Track | 0x00B5 |
| F10 | 0x43 | Mute | 0x00E2 |
| F11 | 0x44 | Volume Down | 0x00EA |
| F12 | 0x45 | Volume Up | 0x00E9 |

触发条件：Physical Left Ctrl（FnLayer）+ F7~F12。
注意：Physical Fn 被映射为 Left Ctrl，不是 FnLayer，所以 Fn+F7 不触发媒体键。

原因：标准键盘 Report（Usage Page 0x07）无法输出媒体键。媒体键属于 Consumer Control Usage Page（0x0C），需要额外的 HID Report 或虚拟设备。

## 边界情况处理

### 1. Fn + Ctrl 同时按下

输入报告：

```
01 02 00 00 00 00 00 00 00 02
```

处理逻辑：

```c
// physicalFnDown = true, physicalLeftCtrlDown = true

// Step 0: 保存原始状态
bool origFnDown       = (report[9] & 0x02) != 0;
bool origLeftCtrlDown = (report[1] & 0x01) != 0;  // bit 0 on A2450

// Step 1: Fn → Left Ctrl
report[1] |= 0x01;   // 设置 Left Ctrl (bit 0)
report[9] &= ~0x02;  // 清除 Fn 标志

// Step 2: Left Ctrl → FnLayer（移除物理 Ctrl）
report[1] &= ~0x01;  // 移除 Left Ctrl

// Step 2b: Re-apply Fn-mapped Ctrl
report[1] |= 0x01;   // 恢复 Fn 映射的 Ctrl

// 结果：FnLayer 激活，输出 Left Ctrl（来自 Fn）
// 两个键的功能：物理 Ctrl = FnLayer，物理 Fn = Ctrl
```

修正后的完整伪代码：

```c
void transformReport(A2450_DRIVER_STATE* state, uint8_t* report, size_t len) {
    if (len < 10 || report[0] != 0x01) return;

    // 保存原始状态
    bool origFnDown      = (report[9] & 0x02) != 0;
    bool origLeftCtrlDown = (report[1] & 0x01) != 0;  // bit 0 on A2450

    if (!state->swapEnabled) return;

    // Step 1: Physical Fn → Left Ctrl（设置 Ctrl bit）
    if (origFnDown) {
        report[1] |= 0x01;   // bit 0 on A2450
        report[9] &= ~0x02;
    }

    // Step 2: Physical Left Ctrl → FnLayer（移除 Ctrl bit）
    if (origLeftCtrlDown) {
        report[1] &= ~0x01;  // bit 0 on A2450
        state->fnLayerActive = true;
    } else {
        state->fnLayerActive = false;
    }

    // Step 2b: Re-apply Fn-mapped Ctrl (if Fn also pressed)
    if (origFnDown) {
        report[1] |= 0x01;
    }

    // Step 3: FnLayer key remapping
    if (state->fnLayerActive) {
        for (int i = 3; i <= 8; i++) {
            if (report[i] != 0x00) {
                report[i] = remapFnLayerKey(report[i]);
            }
        }
    }
}
```

### 2. Ctrl + 普通字母（如 Ctrl+C）

输入报告：

```
01 02 00 06 00 00 00 00 00 00
```

处理：

- `origLeftCtrlDown = true` → FnLayer 激活
- `origFnDown = false` → 不设置额外 Ctrl
- Key Slot 1 = 0x06 (C) → 在 FnLayer 映射中无匹配 → 保持 0x06
- 输出：`01 00 00 06 00 00 00 00 00 00`（无 Ctrl 修饰，只有 C）

**结果：Ctrl+C 不再触发，因为物理 Ctrl 被当作 FnLayer。**

这是设计意图：物理 Ctrl 不再是 Ctrl。如果用户需要 Ctrl+C，应该按物理 Fn + C（因为 Fn 被映射为 Ctrl）。

### 3. 多键同时按下（如 Fn + Ctrl + Backspace）

输入报告：

```
01 01 00 2A 00 00 00 00 00 02
```

处理：

- `origFnDown = true` → 设置 Ctrl bit
- `origLeftCtrlDown = true` → FnLayer 激活，移除 Ctrl bit
- `origFnDown` → 重新设置 Ctrl bit（来自 Fn 的 Ctrl）
- Key Slot 1 = 0x2A (Backspace) → FnLayer 映射为 0x4C (Delete)
- 输出：`01 01 00 4C 00 00 00 00 00 00`（Ctrl 来自 Fn，Delete）

**结果：Fn + Ctrl + Backspace = Ctrl + Delete。**

Fn 设置的 Ctrl 通过 Step 2b 恢复，FnLayer 映射 Backspace → Delete。

### 4. Key Release（按键释放）

释放报告中对应 key slot 变为 0x00：

```
01 00 00 00 00 00 00 00 00 00  // 所有键释放
```

或部分释放：

```
01 02 00 00 00 00 00 00 00 02  // 释放 keySlot，但 Ctrl 和 Fn 仍按住
```

处理逻辑不需要特殊处理释放事件，因为：

- 每个报告是完整状态快照，不是增量事件
- key slot = 0x00 表示该位置无按键
- modifier bit = 0 表示该修饰键已释放
- FnLayer 状态通过 `origLeftCtrlDown` 实时判断，不依赖边缘检测

### 5. 是否清除 Byte 9

**MVP 决定：清除。**

```c
report[9] &= ~0x02;  // 清除 Apple Fn 标志
```

理由：

1. kbdhid 不解析 Byte 9，清除无副作用
2. 保持输出报告"干净"
3. 避免后续驱动或工具对非标准字节产生意外行为
4. 已在清除前提取 `origFnDown` 状态，不影响逻辑

如果后续需要保留 Byte 9（例如调试用途），可通过配置开关控制。

### 6. Fn + Shift 组合

输入报告（Fn + Shift + A）：

```
01 01 00 04 00 00 00 00 00 02
```

处理：

- `origFnDown = true` → 设置 Ctrl bit → report[1] = 0x02 | 0x01 = 0x03
- `origLeftCtrlDown = false` → FnLayer 不激活
- Key Slot 1 = 0x04 (A) → 无 FnLayer 映射 → 保持 0x04
- 输出：`01 03 00 04 00 00 00 00 00 00`（Left Shift + Left Ctrl + A）

**结果：Fn + Shift + A = Ctrl + Shift + A。**

### 7. Fn + Alt 组合

输入报告（Fn + Alt + Tab）：

```
01 04 00 2B 00 00 00 00 00 02
```

处理：

- `origFnDown = true` → 设置 Ctrl bit → report[1] = 0x04 | 0x01 = 0x05
- `origLeftCtrlDown = false` → FnLayer 不激活
- Key Slot 1 = 0x2B (Tab) → 无 FnLayer 映射 → 保持 0x2B
- 输出：`01 05 00 2B 00 00 00 00 00 00`（Left Alt + Left Ctrl + Tab）

**结果：Fn + Alt + Tab = Ctrl + Alt + Tab。**

### 8. 仅 FnLayer 激活但无按键

输入报告（仅按住 Ctrl）：

```
01 01 00 00 00 00 00 00 00 00
```

处理：

- `origLeftCtrlDown = true` → FnLayer 激活
- 无 key slots → 无映射
- 输出：`01 00 00 00 00 00 00 00 00 00`（空报告）

**结果：按住物理 Ctrl 不产生任何系统输入。**

## 状态转换图

```
                    ┌─────────────┐
                    │   IDLE      │
                    │ (无键按下)   │
                    └──────┬──────┘
                           │
              ┌────────────┼────────────┐
              ▼            ▼            ▼
     ┌────────────┐ ┌────────────┐ ┌────────────┐
     │ Fn DOWN    │ │ Ctrl DOWN  │ │ 其他键 DOWN │
     │ → 输出 Ctrl│ │ → FnLayer  │ │ → 原样输出  │
     └──────┬─────┘ └──────┬─────┘ └────────────┘
            │              │
            │    ┌─────────┴─────────┐
            │    ▼                   ▼
            │  ┌─────────────┐  ┌─────────────┐
            │  │ FnLayer +   │  │ FnLayer +   │
            │  │ 映射键      │  │ 非映射键    │
            │  │ → 重映射输出│  │ → 原样输出  │
            │  └─────────────┘  └─────────────┘
            │
            ▼
     ┌────────────┐
     │ Fn UP      │
     │ → 释放 Ctrl│
     └────────────┘
```

## 测试用例

### TC-01: Fn 单独按下

- 输入: `01 00 00 00 00 00 00 00 00 02`
- 输出: `01 01 00 00 00 00 00 00 00 00`（Left Ctrl, bit 0）

### TC-02: Fn 释放

- 输入: `01 00 00 00 00 00 00 00 00 00`
- 输出: `01 00 00 00 00 00 00 00 00 00`（空）

### TC-03: Left Ctrl 单独按下

- 输入: `01 01 00 00 00 00 00 00 00 00`
- 输出: `01 00 00 00 00 00 00 00 00 00`（空，Ctrl 被吞掉）

### TC-04: Fn + Backspace

- 输入: `01 00 00 2A 00 00 00 00 00 02`
- 输出: `01 01 00 2A 00 00 00 00 00 00`（Ctrl + Backspace）
- 说明: Fn 层不是 FnLayer，Fn 被映射为 Ctrl，Backspace 不变

### TC-05: Ctrl + Backspace（FnLayer + Backspace）

- 输入: `01 01 00 2A 00 00 00 00 00 00`
- 输出: `01 00 00 4C 00 00 00 00 00 00`（Delete）
- 说明: Ctrl 进入 FnLayer，Backspace 重映射为 Delete

### TC-06: Fn + Ctrl + Backspace

- 输入: `01 01 00 2A 00 00 00 00 00 02`
- 输出: `01 01 00 4C 00 00 00 00 00 00`（Ctrl + Delete）
- 说明: Fn 设置 Ctrl，物理 Ctrl 进入 FnLayer，Backspace → Delete

### TC-07: Ctrl + Up

- 输入: `01 01 00 52 00 00 00 00 00 00`
- 输出: `01 00 00 4B 00 00 00 00 00 00`（Page Up）

### TC-08: Ctrl + Left

- 输入: `01 01 00 50 00 00 00 00 00 00`
- 输出: `01 00 00 4A 00 00 00 00 00 00`（Home）

### TC-09: Ctrl + C（Ctrl 被吞掉）

- 输入: `01 01 00 06 00 00 00 00 00 00`
- 输出: `01 00 00 06 00 00 00 00 00 00`（仅 C，无 Ctrl）
- 说明: 物理 Ctrl 不再是系统 Ctrl，这是设计意图

### TC-10: Fn + C（Fn → Ctrl）

- 输入: `01 00 00 06 00 00 00 00 00 02`
- 输出: `01 01 00 06 00 00 00 00 00 00`（Ctrl + C）

### TC-11: 快速连按（无状态残留）

- 输入序列:
  1. `01 01 00 2A 00 00 00 00 00 00` (Ctrl+Backspace → FnLayer+Delete)
  2. `01 00 00 2A 00 00 00 00 00 00` (Backspace 释放)
  3. `01 00 00 00 00 00 00 00 00 00` (空闲)
- 输出序列:
  1. `01 00 00 4C 00 00 00 00 00 00` (Delete)
  2. `01 00 00 00 00 00 00 00 00 00` (空)
  3. `01 00 00 00 00 00 00 00 00 00` (空)
