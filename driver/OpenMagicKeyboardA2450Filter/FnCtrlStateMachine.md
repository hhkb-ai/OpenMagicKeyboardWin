# Fn/Ctrl State Machine

## Practical swap model

Windows has no general-purpose `VK_FN`. Therefore the driver should not try to emit a universal Fn key.

Use this behavior instead:

| Original physical key | Driver behavior |
|---|---|
| Fn | Emit Left Ctrl down/up |
| Left Ctrl | Enter/leave internal FnLayer |

## FnLayer mappings

| Physical input while original Left Ctrl is held | Output |
|---|---|
| Backspace | Delete |
| Arrow Up | Page Up |
| Arrow Down | Page Down |
| Arrow Left | Home |
| Arrow Right | End |
| F7 | Previous Track |
| F8 | Play/Pause |
| F9 | Next Track |
| F10 | Mute |
| F11 | Volume Down |
| F12 | Volume Up |

## Pseudocode

```c
if (swapEnabled) {
    if (appleFnChanged) {
        emit_key(KEY_LEFTCTRL, appleFnDown);
        suppress_original_fn();
    }

    if (leftCtrlChanged) {
        state.fnLayerDown = leftCtrlDown;
        suppress_original_leftctrl();
    }

    if (state.fnLayerDown) {
        translate_fn_layer_key(input);
    }
}
```
