# A2450 Bluetooth Fn → Left Ctrl: minimal path

Status: **discovery / not a functioning driver** (2026-10-12).

## User requirement

- Physical device: Apple Magic Keyboard A2450.
- Host: Windows 11; keyboard connected by **Bluetooth**.
- Goal: map the physical **Fn/Globe** key to **Left Ctrl** for Fn+C, Fn+V, Fn+Z and Fn+A.
- Retain physical Ctrl as Ctrl; do not implement key swapping, FnLayer, media keys, UI, or paid dependencies for this MVP.

## What this PR proves and what it does not

The existing A2450 USB report structure has been confirmed by USBPcap, including Fn in byte 9 bit 1. The Bluetooth PID is `029C`, but on Windows the Bluetooth device ID often uses `VID&0001004C_PID&029C` instead of `VID_05AC&PID_029C`. Before this PR the descriptor dump searched only for the USB form and could skip Bluetooth HID collections.

This PR expands device recognition only. It **does not** prove that the Bluetooth Fn bit uses the USB layout, expose that bit to an ordinary user-mode input hook, or deliver Fn→Ctrl.

## Read-only validation on Windows 11 (with keyboard connected over Bluetooth)

From a checkout of this branch:

```powershell
dotnet test .\tests\OpenMagicKeyboard.TransformTests\OpenMagicKeyboard.TransformTests.csproj

dotnet run --project .\tools\A2450DescriptorDump\A2450DescriptorDump.csproj
```

Inspect `logs/a2450-descriptor-dump.json` locally:
- Confirm at least one collection with `Transport: "Bluetooth"` and PID `029C`.
- Record only report lengths, usage page/usage and collection label in shared summaries.
- Device paths may contain unique instance identifiers: **redact those before sharing publicly**.
- No typing, key events, key contents, or credentials should be collected during this discovery step.

If there are no matching collections, first compare the real Device Manager Hardware IDs. Do not guess the ID or modify Windows drivers.

## Fn observability gate

The next PR must establish the **actual Bluetooth report bytes** for idle, Fn-down and Fn-up from the physical A2450, collected in an explicitly authorized read-only manner. Avoid routine keyboard logging. USB Fn bit layout must not be applied to Bluetooth before confirmation.

Only then implement a Bluetooth-specific parser and offline report transformation tests. Require:
- Fn down → Left Ctrl down; Fn up → Left Ctrl up.
- Fn+C/V/Z/A output expected Ctrl shortcuts.
- Physical Ctrl remains Ctrl.
- No stuck Ctrl after Fn release, Bluetooth disconnect, reconnect, or sleep/wake.
- Only A2450 changes; other keyboards unchanged.

## Driver safety gate

The existing WDK build is USB-only, unsigned, and **not tested in VM or on hardware**. No Windows system configuration changes, filter binding, TESTSIGNING, registry edits, Secure Boot changes, host driver installation or unsigned .sys release are authorized by this document. Any future driver loading requires an isolated VM and explicit owner approval; real-machine install requires an independently reviewed rollback and trusted signing path.

## Architecture references

The open-source project `sheehanmunim/apple-peripherals-for-windows` documents a separate Bluetooth HID lower-filter implementation converting hidden Fn to F23 then mapping to Ctrl. Its listed Bluetooth IDs **do not include A2450 PID 029C**; it is only an architectural reference. That project is GPL-2.0-or-later while this repository is MIT: do not copy GPL-covered source into this MIT repository without resolving the license incompatibility.

Related task: #28.
