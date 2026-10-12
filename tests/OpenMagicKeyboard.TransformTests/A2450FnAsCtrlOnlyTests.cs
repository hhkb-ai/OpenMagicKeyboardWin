using OpenMagicKeyboard.Shared;
using Xunit;

namespace OpenMagicKeyboard.TransformTests;

/// <summary>
/// A2450 USB HID regression tests for the requested one-way Fn → Left Ctrl.
/// These are protocol-level offline tests, NOT Bluetooth hardware validation.
/// </summary>
public sealed class A2450FnAsCtrlOnlyTests
{
    private static readonly A2450TransformOptions Options = new()
    {
        FnAsAdditionalLeftCtrl = true,
        SwapFnAndLeftCtrl = false, // One-way mode takes priority over legacy swap.
        EnableFnLayer = false,
        ClearAppleFnByte = true,
    };

    private static byte[] Report(byte modifier = 0, byte key = 0, bool fn = false) =>
        [0x01, modifier, 0x00, key, 0, 0, 0, 0, 0, fn ? (byte)0x02 : (byte)0x00];

    [Fact]
    public void FnAlone_PressesLeftCtrl()
    {
        var result = A2450ReportTransformer.Transform(Report(fn: true), Options);
        Assert.Equal((byte)0x01, result[1]);
        Assert.Equal((byte)0x00, result[9]);
    }

    [Fact]
    public void PhysicalCtrlAlone_StillPressesLeftCtrl()
    {
        var result = A2450ReportTransformer.Transform(Report(modifier: 0x01), Options);
        Assert.Equal((byte)0x01, result[1]);
    }

    [Fact]
    public void FnAndPhysicalCtrl_PreserveOneLeftCtrlBit()
    {
        var result = A2450ReportTransformer.Transform(Report(modifier: 0x01, fn: true), Options);
        Assert.Equal((byte)0x01, result[1]);
    }

    [Theory]
    [InlineData(0x06)] // C
    [InlineData(0x19)] // V
    [InlineData(0x1D)] // Z
    [InlineData(0x04)] // A
    public void FnWithLetter_OutputsCtrlAndOriginalLetter(int key)
    {
        var output = A2450ReportTransformer.Transform(Report(key: checked((byte)key), fn: true), Options);
        Assert.Equal((byte)0x01, output[1]);
        Assert.Equal((byte)key, output[3]);
    }

    [Fact]
    public void FnRelease_DropsCtrlWhenPhysicalCtrlNotHeld()
    {
        var pressed = A2450ReportTransformer.Transform(Report(fn: true), Options);
        var released = A2450ReportTransformer.Transform(Report(), Options);
        Assert.Equal((byte)0x01, pressed[1]);
        Assert.Equal((byte)0x00, released[1]);
    }

    [Fact]
    public void FnRelease_PreservesPhysicalCtrlIfStillHeld()
    {
        var pressed = A2450ReportTransformer.Transform(Report(modifier: 0x01, fn: true), Options);
        var releasedFn = A2450ReportTransformer.Transform(Report(modifier: 0x01, fn: false), Options);
        Assert.Equal((byte)0x01, pressed[1]);
        Assert.Equal((byte)0x01, releasedFn[1]);
    }

    [Fact]
    public void FnRelease_AfterLetterRelease_DropsCtrl()
    {
        var start = A2450ReportTransformer.Transform(Report(fn: true), Options);
        var chord = A2450ReportTransformer.Transform(Report(key: 0x06, fn: true), Options);
        var letterReleased = A2450ReportTransformer.Transform(Report(fn: true), Options);
        var fnReleased = A2450ReportTransformer.Transform(Report(), Options);
        Assert.Equal((byte)0x01, start[1]);
        Assert.Equal((byte)0x01, chord[1]);
        Assert.Equal((byte)0x06, chord[3]);
        Assert.Equal((byte)0x01, letterReleased[1]);
        Assert.Equal((byte)0x00, fnReleased[1]);
    }

    [Fact]
    public void OtherModifiersAndKeys_ArePreserved()
    {
        var source = Report(modifier: 0x02 | 0x08, key: 0x06, fn: true);
        var output = A2450ReportTransformer.Transform(source, Options);
        Assert.Equal((byte)(0x01 | 0x02 | 0x08), output[1]);
        Assert.Equal((byte)0x06, output[3]);
        Assert.Equal((byte)0x00, output[9]);
        Assert.Equal((byte)(0x02 | 0x08), source[1]); // Input remains untouched.
    }
}
