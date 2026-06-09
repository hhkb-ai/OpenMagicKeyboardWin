# Driver Signing Notes

During development, Windows kernel drivers usually require test signing mode.

For public distribution, a production Windows driver requires proper signing through Microsoft's driver signing process. Plan for this early if the project is intended for non-technical users.

## Development mode

Typical development flow:

```powershell
bcdedit /set testsigning on
# reboot
```

Disable test mode after testing:

```powershell
bcdedit /set testsigning off
# reboot
```

## Public release caution

Unsigned or test-signed drivers are not suitable for normal public users.
