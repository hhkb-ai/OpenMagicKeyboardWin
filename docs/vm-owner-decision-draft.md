# VM Owner Decision Draft

**Status**: DRAFT — NOT APPROVED
**Date**: 2026-06-13
**Author**: Agent A

---

## 1. Purpose

This document is a draft for Owner Decision on isolated VM driver-load testing. It does NOT represent approval. The Owner must explicitly approve before any VM testing begins.

**Request**: Approve isolated VM driver-load test only.

---

## 2. Post-Merge Verification Results

### 2.1 Merged PRs

| PR | Title | Status |
|----|-------|--------|
| #16 | Owner Decision watchdog workflow | ✅ Merged |
| #21 | CI hardening workflow | ✅ Merged |
| #22 | Docs factual corrections | ✅ Merged |
| #24 | Fn-to-Ctrl transform readiness validation | ✅ Merged |

### 2.2 File Verification

| File | Status |
|------|--------|
| `.github/workflows/ci-hardening.yml` | ✅ Present |
| `.github/workflows/owner-decision-watchdog.yml` | ✅ Present |
| `docs/transform-parity.md` | ✅ Present |
| `tests/.../A2450ReportTransformerTests.cs` (TC-21 to TC-30) | ✅ 10 tests present |

### 2.3 Test Results

| Test | Result |
|------|--------|
| dotnet test | ✅ **47/47 passed** (0 failed, 0 skipped) |
| Workflow tests | ✅ 19/19 passed |

### 2.4 CI Hardening Results

| CI Job | Result | Notes |
|--------|--------|-------|
| Unit Tests | ✅ PASS | 47/47 |
| Forbidden Path Scan | ✅ PASS | No forbidden patterns |
| Static Safety Scan | ✅ PASS | No unsafe patterns in driver source |
| WDK Build | ❌ FAIL | GitHub Actions runner missing WDK (`ntddk.h` not found) |

**WDK CI Failure Analysis**:
- Error: `Cannot open include file: 'ntddk.h': No such file or directory`
- Cause: GitHub Actions `windows-latest` runner does not have WDK installed
- Impact: Cannot verify WDK build in CI
- Mitigation: Local WDK build verified (see below)

### 2.5 Local WDK Build Verification

#### Debug x64

| Item | Value |
|------|-------|
| Command | `msbuild driver/OpenMagicKeyboardA2450Filter/OpenMagicKeyboardA2450Filter.vcxproj /p:Configuration=Debug /p:Platform=x64 /m /verbosity:minimal` |
| MSBuild Version | 18.6.3+84d3e95b4 (.NET Framework) |
| Toolset | WindowsKernelModeDriver10.0 |
| Result | ✅ Success (0 warnings, 0 errors) |
| Output | `driver/OpenMagicKeyboardA2450Filter/bin/Debug/x64/OpenMagicKeyboardA2450Filter.sys` |
| Size | 11,264 bytes |
| SHA256 | `c51868a28ba544e6e264a6e3dd4ddfa2dcd85646a9942a65945f8ba3c99b3ed8` |

#### Release x64

| Item | Value |
|------|-------|
| Command | `msbuild driver/OpenMagicKeyboardA2450Filter/OpenMagicKeyboardA2450Filter.vcxproj /p:Configuration=Release /p:Platform=x64 /m /verbosity:minimal` |
| MSBuild Version | 18.6.3+84d3e95b4 (.NET Framework) |
| Toolset | WindowsKernelModeDriver10.0 |
| Result | ✅ Success (0 warnings, 0 errors) |
| Output | `driver/OpenMagicKeyboardA2450Filter/bin/Release/x64/OpenMagicKeyboardA2450Filter.sys` |
| Size | 10,752 bytes |
| SHA256 | `30a4ff03459f04c7976331eff3b744d858beae25639f3a7cb167b1ec6c95d974` |

---

## 3. VM Test Scope

### 3.1 What Will Be Tested

| Test | Description |
|------|-------------|
| Driver load | Load `.sys` in isolated VM |
| Driver unload | Unload `.sys` and verify clean removal |
| Rollback | Restore VM snapshot after test |
| Logs | Collect DebugView logs during test |

### 3.2 What Will NOT Be Tested

| Item | Reason |
|------|--------|
| Real A2450 hardware | Not authorized for this phase |
| Host system changes | VM only |
| Bluetooth | MVP-D, not in scope |
| Media keys | MVP-B, not in scope |
| Production deployment | Not authorized |

### 3.3 VM Environment Requirements

| Requirement | Specification |
|-------------|---------------|
| VM Software | VMware Workstation Pro / Hyper-V / VirtualBox |
| OS | Windows 11 x64 (22H2+) |
| Memory | 4 GB+ |
| Disk | 60 GB |
| USB Controller | USB 3.0 |
| Secure Boot | Disabled (for TESTSIGNING) |

### 3.4 Snapshot Strategy

| Snapshot | When | Purpose |
|----------|------|---------|
| `clean-install` | After Windows install | Cleanest rollback |
| `tools-installed` | After DebugView, 7-Zip | Pre-test baseline |
| `testsigning-on` | After TESTSIGNING enabled | Before driver load |
| `driver-loaded` | After driver loaded | Functional test baseline |

---

## 4. Stop Conditions

### 4.1 Immediate Stop (Rollback Required)

| Condition | Action |
|-----------|--------|
| Blue Screen (BSOD) | Restore snapshot immediately |
| Keyboard completely unresponsive | Use spare keyboard, restore snapshot |
| Driver cannot be unloaded | Restore snapshot |
| VM cannot boot | Restore to clean-install snapshot |

### 4.2 Investigate Then Stop

| Condition | Action |
|-----------|--------|
| Unexpected error messages | Capture logs, then rollback |
| Performance degradation | Investigate, then rollback |
| Test results不符合预期 | Document, then rollback |

---

## 5. Safety Measures

### 5.1 What Will Be Done

| Measure | Description |
|---------|-------------|
| TESTSIGNING | Enabled only in VM, never on host |
| Driver load | Only in VM, never on host |
| Snapshot before each phase | Rollback capability at every step |
| Spare keyboard ready | For host if A2450 is passthrough |

### 5.2 What Will NOT Be Done

| Action | Status |
|--------|--------|
| Host TESTSIGNING | ❌ Never |
| Host driver installation | ❌ Never |
| Host registry changes | ❌ Never |
| LowerFilters modification | ❌ Never |
| Real A2450 binding | ❌ Never |
| Unsigned .sys release | ❌ Never |

---

## 6. Owner Decision Required

### 6.1 Decision Statement

```text
## Owner Decision: VM Testing Authorization

**Date**: _______________
**Decision**: [ ] APPROVE / [ ] REJECT

### Scope Approved
- [ ] Isolated VM driver-load test only
- [ ] No real A2450 hardware
- [ ] No host system changes
- [ ] No production deployment

### Preconditions Verified
- [ ] All PRs merged (#16, #21, #22, #24)
- [ ] dotnet test 47/47 passed
- [ ] Local WDK build verified
- [ ] Documentation complete
- [ ] Rollback plan confirmed

### Safety Measures Confirmed
- [ ] TESTSIGNING only in VM
- [ ] Driver load only in VM
- [ ] Snapshots taken before each phase
- [ ] Spare keyboard available

**Signature**: ________________
```

### 6.2 After Approval

If Owner approves:

1. Create VM environment
2. Take snapshots
3. Copy driver files to VM
4. Enable TESTSIGNING in VM
5. Load driver in VM
6. Run test cases (TC-01 to TC-10)
7. Collect evidence
8. Unload driver
9. Restore snapshot
10. Report results

---

## 7. Current Status Declaration

**This document is a DRAFT only.**

- VM testing has NOT started
- Driver has NOT been loaded
- TESTSIGNING has NOT been enabled
- No host system changes have been made
- No unsigned .sys has been released
- Requires explicit Owner approval before execution

---

## Appendix: Evidence Summary

| Evidence | Location |
|----------|----------|
| dotnet test result | 47/47 passed |
| Local WDK Debug build | `bin/Debug/x64/OpenMagicKeyboardA2450Filter.sys` |
| Local WDK Release build | `bin/Release/x64/OpenMagicKeyboardA2450Filter.sys` |
| Transform parity document | `docs/transform-parity.md` |
| VM preflight checklist | `docs/vm-preflight-checklist.md` |
| CI hardening workflow | `.github/workflows/ci-hardening.yml` |
| Watchdog workflow | `.github/workflows/owner-decision-watchdog.yml` |

---

**Document End**

This is a DRAFT. Do not execute any VM operations without explicit Owner authorization.
