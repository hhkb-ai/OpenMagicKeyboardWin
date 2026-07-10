# MVP-A VM Preflight Checklist

**Status**: Pre-flight preparation (NOT execution approval)
**Date**: 2026-06-12
**Purpose**: Define prerequisites and procedures for isolated VM testing

---

## 1. Owner Gate Requirements

### 1.1 Required Approvals Before VM Testing

| # | Requirement | Status | Owner Sign-off |
|---|-------------|--------|----------------|
| 1 | PR #16 (watchdog) merged | ⬜ | |
| 2 | PR #21 (CI hardening) merged | ⬜ | |
| 3 | PR #22 (docs corrections) merged | ⬜ | |
| 4 | PR #24 (transform readiness) merged | ⬜ | |
| 5 | Owner explicitly approves VM testing | ⬜ | |
| 6 | Owner confirms VM environment ready | ⬜ | |

### 1.2 Owner Decision Template

```text
## Owner Decision: VM Testing Authorization

**Date**: YYYY-MM-DD
**Decision**: [ ] APPROVED / [ ] REJECTED

### Preconditions Verified
- [ ] All PRs merged (#16, #21, #22, #24)
- [ ] CI hardening passing
- [ ] Transform parity verified (47/47 tests)
- [ ] Documentation up to date

### VM Environment
- [ ] VM created (Windows 11 x64)
- [ ] VM snapshot taken (clean-install)
- [ ] Tools installed snapshot taken
- [ ] Spare USB keyboard available

### Safety Measures
- [ ] TESTSIGNING will only be enabled in VM
- [ ] Driver will only be loaded in VM
- [ ] No hardware binding on host
- [ ] Rollback plan confirmed

**Signature**: ________________
```

---

## 2. Prerequisites Verification

### 2.1 Code Prerequisites

| # | Prerequisite | Verification Command | Expected Result |
|---|--------------|---------------------|-----------------|
| 1 | All tests pass | `dotnet test` | 47/47 passed |
| 2 | WDK build succeeds (Debug) | `msbuild ... /p:Configuration=Debug` | 0 warnings, 0 errors |
| 3 | WDK build succeeds (Release) | `msbuild ... /p:Configuration=Release` | 0 warnings, 0 errors |
| 4 | .sys file generated | Check `bin/Debug/x64/` and `bin/Release/x64/` | .sys files exist |
| 5 | No forbidden patterns | CI forbidden-path-scan | PASS |
| 6 | Static safety scan | CI static-safety-scan | PASS |

### 2.2 Documentation Prerequisites

| # | Document | Status |
|---|----------|--------|
| 1 | `docs/mvp-a-vm-test-plan.md` | ✅ Exists |
| 2 | `docs/transform-parity.md` | ✅ Exists |
| 3 | `docs/driver-build-and-test-safety.md` | ✅ Exists |
| 4 | `docs/vm-preflight-checklist.md` | ✅ This document |

### 2.3 Infrastructure Prerequisites

| # | Prerequisite | Status |
|---|--------------|--------|
| 1 | VM software installed (VMware/Hyper-V/VirtualBox) | ⬜ |
| 2 | Windows 11 x64 ISO available | ⬜ |
| 3 | Spare USB keyboard for host | ⬜ |
| 4 | A2450 keyboard with USB cable | ⬜ |
| 5 | DebugView downloaded | ⬜ |

---

## 3. VM Environment Setup

### 3.1 VM Configuration

```text
Operating System: Windows 11 x64 (22H2 or later)
Memory: 4 GB or more
Disk: 60 GB
Network: Optional (for downloading tools)
USB Controller: USB 3.0
Secure Boot: Disabled (required for TESTSIGNING)
```

### 3.2 VM Snapshots

| Snapshot Name | When to Create | Purpose |
|---------------|----------------|---------|
| `clean-install` | After Windows installation | Cleanest rollback point |
| `tools-installed` | After installing DebugView, 7-Zip | Pre-test baseline |
| `testsigning-on` | After TESTSIGNING enabled and rebooted | Before driver installation |
| `driver-installed` | After driver successfully loaded | Functional test baseline |

---

## 4. Driver Files to Copy to VM

| File | Source Path | Purpose |
|------|------------|---------|
| `OpenMagicKeyboardA2450Filter.sys` | `driver/.../bin/{Config}/x64/` | Driver binary |
| `OpenMagicKeyboardA2450Filter.pdb` | `driver/.../bin/{Config}/x64/` | Debug symbols |
| `OpenMagicKeyboardA2450Filter.inf` | Generate from `.inf.template` | Driver installation |

---

## 5. Rollback Procedures

### 5.1 VM Snapshot Rollback

```text
1. Shut down or suspend VM
2. Select the appropriate snapshot
3. Restore snapshot
4. Start VM
5. Confirm system restored to pre-test state
```

### 5.2 Driver Uninstall (if needed)

```powershell
# In VM (Administrator PowerShell)
pnputil /enum-drivers | findstr "OpenMagicKeyboard"
pnputil /delete-driver oemXX.inf /uninstall /force
```

### 5.3 Disable TESTSIGNING (after testing)

```powershell
# In VM (Administrator PowerShell)
bcdedit /set testsigning off
shutdown /r /t 0
```

### 5.4 Nuclear Rollback

```text
If all else fails:
1. Shut down VM
2. Restore to clean-install snapshot
3. Start over
```

---

## 6. Stop Conditions

### 6.1 Immediate Stop (Rollback Required)

| Condition | Action |
|-----------|--------|
| Blue screen (BSOD) | Restore snapshot immediately |
| Keyboard completely unresponsive | Use spare keyboard, restore snapshot |
| Driver fails to load and cannot be uninstalled | Restore snapshot |
| VM cannot boot | Restore to clean-install snapshot |

### 6.2 Investigate Then Stop

| Condition | Action |
|-----------|--------|
| Test results不符合预期 | Investigate, then rollback if needed |
| Unexpected error messages | Capture logs, then rollback |
| Performance issues | Investigate, then rollback |

---

## 7. Test Cases

### 7.1 Basic Functionality

| TC | Test | Expected Result |
|----|------|-----------------|
| TC-01 | Fn alone → Left Ctrl | Windows sees Left Ctrl |
| TC-02 | Physical Left Ctrl alone → no output | Windows sees nothing |
| TC-03 | Ctrl + Backspace → Delete | Windows sees Delete |
| TC-04 | Ctrl + Up → PageUp | Windows sees Page Up |
| TC-05 | Ctrl + Down → PageDown | Windows sees Page Down |
| TC-06 | Ctrl + Left → Home | Windows sees Home |
| TC-07 | Ctrl + Right → End | Windows sees End |
| TC-08 | Fn + Ctrl → Left Ctrl | Windows sees Left Ctrl |
| TC-09 | Regular keys → unchanged | Windows sees normal keys |
| TC-10 | Other keyboard → unaffected | Other keyboard works normally |

### 7.2 Evidence Collection

| Evidence | How to Collect |
|----------|----------------|
| DebugView logs | Run DebugView as admin, capture kernel output |
| Keyboard test website | Use online keyboard tester |
| Device Manager screenshot | Show filter driver loaded |
| pnputil output | Show installed drivers |

---

## 8. Exit Criteria

### 8.1 VM Testing Complete

```text
[ ] All TC test cases passed
[ ] No blue screens or crashes
[ ] Rollback procedure verified
[ ] Evidence collected and archived
```

### 8.2 Ready for Real Hardware Testing

```text
[ ] VM testing all passed
[ ] Owner approves real hardware testing
[ ] Real A2450 test environment prepared
[ ] Risk assessment completed
```

---

## 9. Current Status Declaration

**This document is a pre-flight checklist only.**

- VM testing has NOT started
- Real A2450 testing has NOT started
- Driver is unsigned, not installed, not loaded
- Not production ready
- Requires explicit Owner approval before execution

---

## Appendix A: Command Reference

### VM Commands (Administrator)

```powershell
# Enable TESTSIGNING
bcdedit /set testsigning on
shutdown /r /t 0

# Disable TESTSIGNING
bcdedit /set testsigning off
shutdown /r /t 0

# Install driver
pnputil /add-driver OpenMagicKeyboardA2450Filter.inf /install

# Uninstall driver
pnputil /delete-driver oemXX.inf /uninstall /force

# List installed drivers
pnputil /enum-drivers

# List devices
Get-PnpDevice -Class HID
```

---

**Document End**

This is a pre-flight checklist only. Do not execute any VM operations without explicit Owner authorization.
