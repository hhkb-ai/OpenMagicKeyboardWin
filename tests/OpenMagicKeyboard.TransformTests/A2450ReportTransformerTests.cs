using OpenMagicKeyboard.Shared;
using Xunit;

namespace OpenMagicKeyboard.TransformTests;

/// <summary>
/// Unit tests for A2450ReportTransformer based on USBPcap-confirmed HID report structure.
///
/// A2450 10-byte HID Report:
///   Byte 0 = Report ID (0x01)
///   Byte 1 = Modifier
///   Byte 2 = Reserved (0x00)
///   Byte 3-8 = Key usage slots
///   Byte 9 = Apple Fn state (0x02 = Fn pressed)
///
/// Modifier bits (as observed via USBPcap on A2450):
///   Bit 0 (0x01) = Left Ctrl
/// </summary>
public class A2450ReportTransformerTests
{
    private static byte[] MakeReport(
        byte modifier = 0x00,
        byte key1 = 0x00, byte key2 = 0x00, byte key3 = 0x00,
        byte key4 = 0x00, byte key5 = 0x00, byte key6 = 0x00,
        byte appleFn = 0x00)
    {
        return [0x01, modifier, 0x00, key1, key2, key3, key4, key5, key6, appleFn];
    }

    // TC-01: Idle report should not change.
    [Fact]
    public void Idle_Report_Unchanged()
    {
        var input = MakeReport();
        var output = A2450ReportTransformer.Transform(input);

        Assert.Equal(input, output);
    }

    // TC-02: Fn pressed alone → output Left Ctrl, clear byte 9.
    [Fact]
    public void FnAlone_OutputsLeftCtrl_ClearsByte9()
    {
        // USBPcap: 01 00 00 00 00 00 00 00 00 02
        var input = MakeReport(appleFn: 0x02);
        var output = A2450ReportTransformer.Transform(input);

        // Expect: 01 01 00 00 00 00 00 00 00 00
        Assert.Equal(0x01, output[1]); // Left Ctrl bit set
        Assert.Equal(0x00, output[9]); // Apple Fn byte cleared
        Assert.Equal((byte)0x00, output[3]); // no key
    }

    // TC-03: Left Ctrl pressed alone → no Ctrl in output, only activates FnLayer.
    [Fact]
    public void LeftCtrlAlone_NoOutputCtrl()
    {
        // USBPcap: 01 01 00 00 00 00 00 00 00 00
        var input = MakeReport(modifier: 0x01);
        var output = A2450ReportTransformer.Transform(input);

        // Expect: 01 00 00 00 00 00 00 00 00 00
        Assert.Equal(0x00, output[1]); // Ctrl removed
        Assert.Equal(0x00, output[9]); // no Fn
    }

    // TC-04: Fn + Left Ctrl → output Left Ctrl (from Fn), FnLayer active, no cross-contamination.
    [Fact]
    public void FnPlusCtrl_OutputsCtrlFromFn_FnLayerActive()
    {
        // USBPcap: 01 01 00 00 00 00 00 00 00 02
        var input = MakeReport(modifier: 0x01, appleFn: 0x02);
        var output = A2450ReportTransformer.Transform(input);

        // Expect: 01 01 00 00 00 00 00 00 00 00
        // Fn sets Ctrl, physical Ctrl removes it, Fn restores it.
        Assert.Equal(0x01, output[1]); // Left Ctrl present (from Fn)
        Assert.Equal(0x00, output[9]); // Apple Fn cleared
    }

    // TC-05: Backspace alone → unchanged.
    [Fact]
    public void BackspaceAlone_Unchanged()
    {
        // USBPcap: 01 00 00 2A 00 00 00 00 00 00
        var input = MakeReport(key1: 0x2A);
        var output = A2450ReportTransformer.Transform(input);

        Assert.Equal(0x2A, output[3]); // Backspace unchanged
        Assert.Equal(0x00, output[1]); // no modifier
    }

    // TC-06: LeftCtrl (FnLayer) + Backspace → Delete.
    [Fact]
    public void FnLayer_Backspace_MapsToDelete()
    {
        // USBPcap: 01 01 00 2A 00 00 00 00 00 00
        var input = MakeReport(modifier: 0x01, key1: 0x2A);
        var output = A2450ReportTransformer.Transform(input);

        Assert.Equal(0x4C, output[3]); // Delete
        Assert.Equal(0x00, output[1]); // no Ctrl in output
    }

    // TC-07: LeftCtrl (FnLayer) + Up → PageUp.
    [Fact]
    public void FnLayer_Up_MapsToPageUp()
    {
        // USBPcap: 01 01 00 52 00 00 00 00 00 00
        var input = MakeReport(modifier: 0x01, key1: 0x52);
        var output = A2450ReportTransformer.Transform(input);

        Assert.Equal(0x4B, output[3]); // PageUp
    }

    // TC-08: LeftCtrl (FnLayer) + Down → PageDown.
    [Fact]
    public void FnLayer_Down_MapsToPageDown()
    {
        var input = MakeReport(modifier: 0x01, key1: 0x51);
        var output = A2450ReportTransformer.Transform(input);

        Assert.Equal(0x4E, output[3]); // PageDown
    }

    // TC-09: LeftCtrl (FnLayer) + Left → Home.
    [Fact]
    public void FnLayer_Left_MapsToHome()
    {
        var input = MakeReport(modifier: 0x01, key1: 0x50);
        var output = A2450ReportTransformer.Transform(input);

        Assert.Equal(0x4A, output[3]); // Home
    }

    // TC-10: LeftCtrl (FnLayer) + Right → End.
    [Fact]
    public void FnLayer_Right_MapsToEnd()
    {
        var input = MakeReport(modifier: 0x01, key1: 0x4F);
        var output = A2450ReportTransformer.Transform(input);

        Assert.Equal(0x4D, output[3]); // End
    }

    // TC-11: Ctrl + letter A → output A only, no Ctrl.
    [Fact]
    public void CtrlPlusA_OutputA_NoCtrl()
    {
        // USBPcap: 01 01 00 04 00 00 00 00 00 00  (0x04 = A)
        var input = MakeReport(modifier: 0x01, key1: 0x04);
        var output = A2450ReportTransformer.Transform(input);

        Assert.Equal(0x00, output[1]); // Ctrl removed
        Assert.Equal(0x04, output[3]); // A unchanged
    }

    // TC-12: Fn + Backspace → Ctrl + Backspace (Fn maps to Ctrl, no FnLayer).
    [Fact]
    public void FnPlusBackspace_CtrlBackspace()
    {
        // Fn is NOT Ctrl → it's FnLayer trigger. But Fn maps TO Ctrl.
        // Fn+Backspace: Fn down → Ctrl output, Backspace is NOT in FnLayer (FnLayer = physical Ctrl).
        var input = MakeReport(key1: 0x2A, appleFn: 0x02);
        var output = A2450ReportTransformer.Transform(input);

        Assert.Equal(0x01, output[1]); // Left Ctrl (from Fn)
        Assert.Equal(0x2A, output[3]); // Backspace unchanged (FnLayer not active)
        Assert.Equal(0x00, output[9]); // Fn cleared
    }

    // TC-13: Input array is not modified.
    [Fact]
    public void Transform_DoesNotModifyInput()
    {
        var input = MakeReport(modifier: 0x01, key1: 0x2A, appleFn: 0x02);
        var original = (byte[])input.Clone();

        A2450ReportTransformer.Transform(input);

        Assert.Equal(original, input);
    }

    // TC-14: Wrong report ID passes through unchanged.
    [Fact]
    public void WrongReportId_PassThrough()
    {
        var input = new byte[] { 0x02, 0x01, 0x00, 0x2A, 0x00, 0x00, 0x00, 0x00, 0x00, 0x02 };
        var output = A2450ReportTransformer.Transform(input);

        Assert.Equal(input, output);
    }

    // TC-15: Fn + Ctrl + Backspace → Delete with Ctrl.
    [Fact]
    public void FnPlusCtrlPlusBackspace_DeleteWithCtrl()
    {
        // Fn sets Ctrl, physical Ctrl activates FnLayer and removes Ctrl, Fn restores Ctrl.
        // Backspace → Delete via FnLayer.
        var input = MakeReport(modifier: 0x01, key1: 0x2A, appleFn: 0x02);
        var output = A2450ReportTransformer.Transform(input);

        // FnLayer is active (physical Ctrl), so Backspace → Delete.
        // Fn sets Ctrl, physical Ctrl removes it, Fn restores it.
        Assert.Equal(0x01, output[1]); // Left Ctrl from Fn
        Assert.Equal(0x4C, output[3]); // Delete
        Assert.Equal(0x00, output[9]); // Fn cleared
    }

    // TC-16: SwapFnAndLeftCtrl=false → no transformation.
    [Fact]
    public void SwapDisabled_NoTransformation()
    {
        var input = MakeReport(modifier: 0x01, key1: 0x2A, appleFn: 0x02);
        var output = A2450ReportTransformer.Transform(input, new A2450TransformOptions { SwapFnAndLeftCtrl = false });

        Assert.Equal(input, output);
    }

    // TC-17: ClearAppleFnByte=false → byte 9 preserved.
    [Fact]
    public void ClearFnByteDisabled_PreservesByte9()
    {
        var input = MakeReport(appleFn: 0x02);
        var output = A2450ReportTransformer.Transform(input, new A2450TransformOptions { ClearAppleFnByte = false });

        Assert.Equal(0x01, output[1]); // Left Ctrl from Fn
        Assert.Equal(0x02, output[9]); // Fn byte preserved
    }

    // TC-18: EnableFnLayer=false → no key remapping.
    [Fact]
    public void FnLayerDisabled_NoRemap()
    {
        var input = MakeReport(modifier: 0x01, key1: 0x2A);
        var output = A2450ReportTransformer.Transform(input, new A2450TransformOptions { EnableFnLayer = false });

        Assert.Equal(0x2A, output[3]); // Backspace NOT remapped
        Assert.Equal(0x00, output[1]); // Ctrl still removed
    }

    // TC-19: Null input throws.
    [Fact]
    public void NullInput_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => A2450ReportTransformer.Transform(null!));
    }

    // TC-20: Wrong length throws.
    [Fact]
    public void WrongLength_Throws()
    {
        Assert.Throws<ArgumentException>(() => A2450ReportTransformer.Transform(new byte[5]));
    }

    // TC-21: Fn release → no Ctrl in output.
    [Fact]
    public void FnRelease_ClearsRemappedCtrl()
    {
        // Simulate: first report has Fn down, second report has Fn released
        var fnDown = MakeReport(appleFn: 0x02);
        var fnReleased = MakeReport(); // Fn not pressed

        var output1 = A2450ReportTransformer.Transform(fnDown);
        var output2 = A2450ReportTransformer.Transform(fnReleased);

        Assert.Equal(0x01, output1[1]); // Ctrl present when Fn down
        Assert.Equal(0x00, output2[1]); // Ctrl absent when Fn released
    }

    // TC-22: Fn + Ctrl + Up → PageUp with Ctrl.
    [Fact]
    public void FnPlusCtrlPlusUp_PageUpWithCtrl()
    {
        var input = MakeReport(modifier: 0x01, key1: 0x52, appleFn: 0x02);
        var output = A2450ReportTransformer.Transform(input);

        Assert.Equal(0x01, output[1]); // Left Ctrl from Fn
        Assert.Equal(0x4B, output[3]); // PageUp
    }

    // TC-23: Fn + Ctrl + Down → PageDown with Ctrl.
    [Fact]
    public void FnPlusCtrlPlusDown_PageDownWithCtrl()
    {
        var input = MakeReport(modifier: 0x01, key1: 0x51, appleFn: 0x02);
        var output = A2450ReportTransformer.Transform(input);

        Assert.Equal(0x01, output[1]); // Left Ctrl from Fn
        Assert.Equal(0x4E, output[3]); // PageDown
    }

    // TC-24: Fn + Ctrl + Left → Home with Ctrl.
    [Fact]
    public void FnPlusCtrlPlusLeft_HomeWithCtrl()
    {
        var input = MakeReport(modifier: 0x01, key1: 0x50, appleFn: 0x02);
        var output = A2450ReportTransformer.Transform(input);

        Assert.Equal(0x01, output[1]); // Left Ctrl from Fn
        Assert.Equal(0x4A, output[3]); // Home
    }

    // TC-25: Fn + Ctrl + Right → End with Ctrl.
    [Fact]
    public void FnPlusCtrlPlusRight_EndWithCtrl()
    {
        var input = MakeReport(modifier: 0x01, key1: 0x4F, appleFn: 0x02);
        var output = A2450ReportTransformer.Transform(input);

        Assert.Equal(0x01, output[1]); // Left Ctrl from Fn
        Assert.Equal(0x4D, output[3]); // End
    }

    // TC-26: Multiple FnLayer keys simultaneously (Ctrl + Backspace + Up).
    [Fact]
    public void FnLayer_MultipleKeys_AllRemapped()
    {
        // Ctrl + Backspace + Up → Delete + PageUp
        var input = MakeReport(modifier: 0x01, key1: 0x2A, key2: 0x52);
        var output = A2450ReportTransformer.Transform(input);

        Assert.Equal(0x00, output[1]); // Ctrl removed (no Fn)
        Assert.Equal(0x4C, output[3]); // Delete (Backspace remapped)
        Assert.Equal(0x4B, output[4]); // PageUp (Up remapped)
    }

    // TC-27: FnLayer key in non-key1 slot (key3).
    [Fact]
    public void FnLayer_KeyInSlot3_Remapped()
    {
        var input = MakeReport(modifier: 0x01, key3: 0x50); // Left arrow in slot 3
        var output = A2450ReportTransformer.Transform(input);

        Assert.Equal(0x4A, output[5]); // Home (Left remapped in slot 3)
    }

    // TC-28: FnLayer with Shift modifier preserved.
    [Fact]
    public void FnLayer_WithShift_ShiftPreserved()
    {
        // Shift (0x02) + LeftCtrl (0x01) + Backspace
        var input = MakeReport(modifier: 0x03, key1: 0x2A);
        var output = A2450ReportTransformer.Transform(input);

        Assert.Equal(0x02, output[1]); // Shift preserved, Ctrl removed
        Assert.Equal(0x4C, output[3]); // Delete
    }

    // TC-29: Full 6-key slots with mixed remapping.
    [Fact]
    public void FullKeySlots_MixedRemapping()
    {
        // Ctrl + A, Backspace, Up, Down, Left, Right
        var input = MakeReport(modifier: 0x01, key1: 0x04, key2: 0x2A, key3: 0x52, key4: 0x51, key5: 0x50, key6: 0x4F);
        var output = A2450ReportTransformer.Transform(input);

        Assert.Equal(0x00, output[1]); // Ctrl removed
        Assert.Equal(0x04, output[3]); // A unchanged
        Assert.Equal(0x4C, output[4]); // Delete (Backspace remapped)
        Assert.Equal(0x4B, output[5]); // PageUp (Up remapped)
        Assert.Equal(0x4E, output[6]); // PageDown (Down remapped)
        Assert.Equal(0x4A, output[7]); // Home (Left remapped)
        Assert.Equal(0x4D, output[8]); // End (Right remapped)
    }

    // TC-30: Fn + Ctrl + letter → Ctrl + letter, FnLayer active.
    [Fact]
    public void FnPlusCtrlPlusLetter_CtrlLetter_FnLayerActive()
    {
        // Fn + Ctrl + A → Ctrl + A (FnLayer active but A not remapped)
        var input = MakeReport(modifier: 0x01, key1: 0x04, appleFn: 0x02);
        var output = A2450ReportTransformer.Transform(input);

        Assert.Equal(0x01, output[1]); // Left Ctrl from Fn
        Assert.Equal(0x04, output[3]); // A unchanged (not in FnLayer map)
    }
}
