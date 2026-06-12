# MVP-A VM 测试计划

**版本**: 1.0
**日期**: 2026-06-12
**状态**: 设计阶段，等待 Owner 批准后执行
**作者**: Agent B

---

## 一、目的与范围

### 1.1 目的

本文档定义 MVP-A 阶段的 VM 验证流程，确保 A2450 filter driver 在受控环境中安全加载和运行。

### 1.2 范围

- VM 环境准备
- 驱动加载验证
- HID Report 拦截验证
- Fn/Ctrl 映射功能验证
- 回滚方案

### 1.3 范围外

- 媒体键支持（MVP-B）
- 蓝牙模式支持
- 生产环境部署
- 多设备兼容性测试

---

## 二、安全边界

### 2.1 严格禁止

| 禁止操作 | 原因 |
|----------|------|
| 在主机上安装驱动 | 防止系统损坏 |
| 在主机上开启 TESTSIGNING | 安全风险 |
| 在生产环境测试 | 数据安全 |
| 绑定非 A2450 设备 | 防止其他设备受影响 |
| 修改主机注册表 | 系统稳定性 |

### 2.2 允许操作

| 允许操作 | 环境 |
|----------|------|
| 安装驱动 | VM 专用 |
| 开启 TESTSIGNING | VM 专用 |
| 加载驱动 | VM 专用 |
| 功能测试 | VM 专用 |
| 快照回滚 | VM 专用 |

---

## 三、VM 前置条件

### 3.1 VM 软件要求

| 软件 | 推荐 | 备选 |
|------|------|------|
| VMware Workstation Pro | ⭐⭐⭐⭐⭐ | USB 3.0 透传稳定 |
| Hyper-V | ⭐⭐⭐⭐ | Windows 内置 |
| VirtualBox | ⭐⭐⭐ | 免费，USB 支持较弱 |

### 3.2 VM 配置要求

```
操作系统: Windows 11 x64 (22H2+)
内存: 4 GB+
磁盘: 60 GB
USB 控制器: USB 3.0
Secure Boot: 关闭（测试需要）
```

### 3.3 硬件要求

- A2450 键盘（USB 有线连接）
- 主机备用 USB 键盘（A2450 透传后主机无法使用）
- USB 线缆

---

## 四、快照与回滚要求

### 4.1 快照策略

| 快照名称 | 时机 | 用途 |
|----------|------|------|
| `clean-install` | Windows 安装完成 | 最干净回滚点 |
| `tools-installed` | 安装 DebugView 等工具 | 测试前基线 |
| `testsigning-on` | TESTSIGNING 开启并重启 | 驱动安装前 |
| `driver-installed` | 驱动安装成功 | 功能测试基线 |
| `after-test` | 测试完成 | 验证回滚可用 |

### 4.2 回滚触发条件

- 蓝屏或系统崩溃
- 驱动加载失败
- 键盘无响应
- 测试结果不符合预期
- 需要重新开始测试

---

## 五、测试前检查清单

### 5.1 环境检查

```text
[ ] VM 已创建并运行正常
[ ] VM 已创建 "tools-installed" 快照
[ ] A2450 已通过 USB 线连接到主机
[ ] 主机有备用键盘可用
[ ] DebugView 已在 VM 内安装
```

### 5.2 驱动文件检查

```text
[ ] .sys 文件存在: OpenMagicKeyboardA2450Filter.sys
[ ] .pdb 文件存在: OpenMagicKeyboardA2450Filter.pdb
[ ] .sys 文件大小合理（约 11 KB）
[ ] .sys 是 Debug 或 Release 版本（确认与 INF 一致）
```

### 5.3 安全检查

```text
[ ] 不在主机上执行任何操作
[ ] 不在生产环境执行
[ ] 已创建快照，可以随时回滚
[ ] 已阅读风险清单
[ ] 已准备好回滚方案
```

---

## 六、VM 验证计划

### 阶段 1：环境准备

1. 创建 VM 并安装 Windows 11
2. 安装 VMware Tools / Hyper-V Integration Services
3. 安装 DebugView
4. 创建快照 `tools-installed`

### 阶段 2：驱动加载

1. 开启 TESTSIGNING（VM 内）
   ```powershell
   bcdedit /set testsigning on
   shutdown /r /t 0
   ```
2. 创建快照 `testsigning-on`
3. 从 .inf.template 生成测试用 .inf
4. 复制 .sys 和 .pdb 到 VM
5. 使用 pnputil 安装驱动
   ```powershell
   pnputil /add-driver OpenMagicKeyboardA2450Filter.inf /install
   ```
6. 验证驱动加载
   ```powershell
   pnputil /enum-drivers | findstr "OpenMagicKeyboard"
   ```
7. 创建快照 `driver-installed`

### 阶段 3：HID Report 拦截验证

1. 将 A2450 透传到 VM
2. 打开 DebugView，启用内核日志捕获
3. 设置过滤器: `OpenMagicKeyboard*`
4. 在 A2450 上按下任意键
5. 观察 DebugView 输出
6. 验证 completion routine 触发

### 阶段 4：功能验证

#### TC-01: Fn → Left Ctrl

| 项目 | 内容 |
|------|------|
| 动作 | 按下 Fn/Globe 键 |
| 预期 | Windows 识别为 Left Ctrl |
| 验证 | 在线键盘测试网站显示 "Left Ctrl" |

#### TC-02: Physical Left Ctrl → 无输出

| 项目 | 内容 |
|------|------|
| 动作 | 按下物理 Left Ctrl |
| 预期 | Windows 不识别为任何按键 |
| 验证 | 在线键盘测试网站无显示 |

#### TC-03: Physical Left Ctrl + Backspace → Delete

| 项目 | 内容 |
|------|------|
| 动作 | 按住物理 Left Ctrl，按 Backspace |
| 预期 | Windows 识别为 Delete |
| 验证 | 删除光标后字符 |

#### TC-04: Physical Left Ctrl + ↑ → PageUp

| 项目 | 内容 |
|------|------|
| 动作 | 按住物理 Left Ctrl，按 ↑ |
| 预期 | Windows 识别为 Page Up |
| 验证 | 页面向上翻页 |

#### TC-05: Physical Left Ctrl + ↓ → PageDown

| 项目 | 内容 |
|------|------|
| 动作 | 按住物理 Left Ctrl，按 ↓ |
| 预期 | Windows 识别为 Page Down |
| 验证 | 页面向下翻页 |

#### TC-06: Physical Left Ctrl + ← → Home

| 项目 | 内容 |
|------|------|
| 动作 | 按住物理 Left Ctrl，按 ← |
| 预期 | Windows 识别为 Home |
| 验证 | 光标跳到行首 |

#### TC-07: Physical Left Ctrl + → → End

| 项目 | 内容 |
|------|------|
| 动作 | 按住物理 Left Ctrl，按 → |
| 预期 | Windows 识别为 End |
| 验证 | 光标跳到行尾 |

#### TC-08: 普通字母键 → 不受影响

| 项目 | 内容 |
|------|------|
| 动作 | 按下 A, B, C 等字母键 |
| 预期 | Windows 正常识别字母 |
| 验证 | 记事本正常输入 |

#### TC-09: 其他键盘 → 不受影响

| 项目 | 内容 |
|------|------|
| 动作 | 插入另一个 USB 键盘 |
| 预期 | 另一个键盘正常工作 |
| 验证 | filter driver 只绑定 A2450 |

---

## 七、预期结果

### 7.1 成功标准

- 驱动加载成功，无蓝屏
- DebugView 显示 completion routine 触发日志
- 所有 TC 测试用例通过
- 统计字段（ReportsTransformed/ReportsPassedThrough）正确递增

### 7.2 失败判断

- 蓝屏 → 回滚到快照，检查驱动代码
- 键盘无响应 → 检查 completion routine 逻辑
- 映射不正确 → 检查 A2450TransformKeyboardReport 函数
- 统计字段不递增 → 检查 IoTarget 和 IOCTL 拦截

---

## 八、失败处理与回滚标准

### 8.1 即时回滚

- 蓝屏或系统崩溃
- 键盘完全无响应
- 驱动加载失败且无法卸载

### 8.2 调查后回滚

- 测试结果不符合预期
- 需要修改驱动代码重新测试
- 测试环境异常

### 8.3 回滚步骤

```text
1. 关闭 VM（或挂起）
2. 选择对应快照
3. 恢复到快照
4. 启动 VM
5. 确认系统已恢复
```

---

## 九、证据收集

### 9.1 必须收集

- DebugView 日志截图
- 在线键盘测试网站截图
- 设备管理器截图（显示 filter driver）
- pnputil /enum-drivers 输出
- 统计字段值（WinDbg 或日志）

### 9.2 可选收集

- USBPcap 抓包
- 性能计数器
- 内存使用情况

---

## 十、退出标准

### 10.1 VM 测试完成

```text
[ ] 所有 TC 测试用例通过
[ ] 无蓝屏或系统崩溃
[ ] 回滚方案验证成功
[ ] 证据已收集并归档
```

### 10.2 进入下一阶段

```text
[ ] VM 测试全部通过
[ ] Owner 批准进入真实硬件测试
[ ] 真实 A2450 测试环境已准备
[ ] 风险评估已完成
```

---

## 十一、范围外事项

以下事项不在本测试计划范围内：

- 媒体键支持（MVP-B）
- 蓝牙模式支持
- 生产环境部署
- 多设备兼容性测试
- 驱动签名（EV 证书 + Microsoft 硬件仪表板）
- 性能优化
- 长期稳定性测试

---

## 十二、当前状态声明

**重要**：本文档是设计阶段产物。

- VM 测试尚未开始
- 真实 A2450 测试尚未开始
- 驱动未签名、未安装、未加载
- 驱动不可用于生产环境
- 本计划需 Owner 批准后方可执行

---

## 附录 A：命令速查表

### VM 内命令（管理员权限）

```powershell
# 开启 TESTSIGNING
bcdedit /set testsigning on
shutdown /r /t 0

# 关闭 TESTSIGNING
bcdedit /set testsigning off
shutdown /r /t 0

# 安装驱动
pnputil /add-driver OpenMagicKeyboardA2450Filter.inf /install

# 卸载驱动
pnputil /delete-driver oemXX.inf /uninstall /force

# 查看已安装驱动
pnputil /enum-drivers

# 查看设备
Get-PnpDevice -Class HID
```

### 主机命令（仅供参考，不执行）

```powershell
# 查看 USB 设备（不执行）
# Get-PnpDevice | Where-Object {$_.InstanceId -like "*VID_05AC*"}
```

---

## 附录 B：测试结果记录模板

```text
测试日期: YYYY-MM-DD
测试人员: [姓名]
VM 环境: [VMware/Hyper-V/VirtualBox]
Windows 版本: [版本号]
驱动版本: [Debug/Release]
TESTSIGNING: [开启/关闭]

测试结果:
| 用例 | 结果 | 备注 |
|------|------|------|
| TC-01 | PASS/FAIL | |
| TC-02 | PASS/FAIL | |
| TC-03 | PASS/FAIL | |
| TC-04 | PASS/FAIL | |
| TC-05 | PASS/FAIL | |
| TC-06 | PASS/FAIL | |
| TC-07 | PASS/FAIL | |
| TC-08 | PASS/FAIL | |
| TC-09 | PASS/FAIL | |

问题记录:
[描述遇到的问题和解决方法]
```

---

**文档结束**

本文档仅用于设计和规划，不执行任何操作。实际测试前请确认所有检查项并获得 Owner 批准。
