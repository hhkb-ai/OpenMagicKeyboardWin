# OpenMagicKeyboardA2450Filter

This folder is reserved for the A2450 Windows driver prototype.

The first driver goal is not a full product. It is a minimal proof of concept:

- Bind only to Apple Magic Keyboard A2450.
- Convert original Fn into Left Ctrl.
- Treat original Left Ctrl as an internal FnLayer modifier.
- Translate FnLayer shortcuts inside the driver.

## Current status

Design notes only. Do not install anything from this folder yet.

## Driver model under consideration

- HIDClass lower filter driver
- KMDF where possible
- A2450-specific device profile

## Required before implementation

Complete `tools/A2450HidLogger` testing and collect:

- Bluetooth hardware ID
- USB hardware ID, if available
- Fn report behavior
- Left Ctrl report behavior
- Fn + F-row behavior
- Fn + Backspace behavior
- Fn + Arrow behavior
