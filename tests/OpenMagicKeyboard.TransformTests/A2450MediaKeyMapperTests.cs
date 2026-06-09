using OpenMagicKeyboard.Shared;
using Xunit;

namespace OpenMagicKeyboard.TransformTests;

/// <summary>
/// Tests for A2450MediaKeyMapper — FnLayer + F7~F12 → Consumer Control Usage mapping.
///
/// Trigger condition: Physical Left Ctrl (FnLayer) + F7~F12.
/// NOT Physical Fn + F7~F12 (Fn is mapped to Left Ctrl, not FnLayer).
///
/// Consumer Control reports go via COL02 (UsagePage 0x000C), not COL01.
/// </summary>
public class A2450MediaKeyMapperTests
{
    // TC-01: F7 → Previous Track
    [Fact]
    public void F7_MapsToPreviousTrack()
    {
        var result = A2450MediaKeyMapper.MapFnLayerFunctionKeyToConsumerUsage(0x40);
        Assert.Equal((ushort)0x00B6, result);
    }

    // TC-02: F8 → Play/Pause
    [Fact]
    public void F8_MapsToPlayPause()
    {
        var result = A2450MediaKeyMapper.MapFnLayerFunctionKeyToConsumerUsage(0x41);
        Assert.Equal((ushort)0x00CD, result);
    }

    // TC-03: F9 → Next Track
    [Fact]
    public void F9_MapsToNextTrack()
    {
        var result = A2450MediaKeyMapper.MapFnLayerFunctionKeyToConsumerUsage(0x42);
        Assert.Equal((ushort)0x00B5, result);
    }

    // TC-04: F10 → Mute
    [Fact]
    public void F10_MapsToMute()
    {
        var result = A2450MediaKeyMapper.MapFnLayerFunctionKeyToConsumerUsage(0x43);
        Assert.Equal((ushort)0x00E2, result);
    }

    // TC-05: F11 → Volume Down
    [Fact]
    public void F11_MapsToVolumeDown()
    {
        var result = A2450MediaKeyMapper.MapFnLayerFunctionKeyToConsumerUsage(0x44);
        Assert.Equal((ushort)0x00EA, result);
    }

    // TC-06: F12 → Volume Up
    [Fact]
    public void F12_MapsToVolumeUp()
    {
        var result = A2450MediaKeyMapper.MapFnLayerFunctionKeyToConsumerUsage(0x45);
        Assert.Equal((ushort)0x00E9, result);
    }

    // TC-07: Non-media key returns null
    [Fact]
    public void NonMediaKey_ReturnsNull()
    {
        Assert.Null(A2450MediaKeyMapper.MapFnLayerFunctionKeyToConsumerUsage(0x2A)); // Backspace
        Assert.Null(A2450MediaKeyMapper.MapFnLayerFunctionKeyToConsumerUsage(0x04)); // A
        Assert.Null(A2450MediaKeyMapper.MapFnLayerFunctionKeyToConsumerUsage(0x3A)); // F1
        Assert.Null(A2450MediaKeyMapper.MapFnLayerFunctionKeyToConsumerUsage(0x3F)); // F6
        Assert.Null(A2450MediaKeyMapper.MapFnLayerFunctionKeyToConsumerUsage(0x46)); // F13
        Assert.Null(A2450MediaKeyMapper.MapFnLayerFunctionKeyToConsumerUsage(0x00)); // Empty
    }

    // TC-08: LeftCtrl (FnLayer) + F8 → Play/Pause via TransformWithConsumerUsage
    [Fact]
    public void FnLayer_F8_ReturnsPlayPause()
    {
        // Input: Left Ctrl (0x01) + F8 (0x41)
        var input = new byte[] { 0x01, 0x01, 0x00, 0x41, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        var result = A2450ReportTransformer.TransformWithConsumerUsage(input);

        Assert.Equal((ushort)0x00CD, result.ConsumerUsage);
    }

    // TC-09: Plain F8 (no Ctrl) → no ConsumerUsage
    [Fact]
    public void PlainF8_NoConsumerUsage()
    {
        // Input: F8 (0x41) alone, no modifier
        var input = new byte[] { 0x01, 0x00, 0x00, 0x41, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        var result = A2450ReportTransformer.TransformWithConsumerUsage(input);

        Assert.Null(result.ConsumerUsage);
    }

    // TC-10: Fn + F8 → no ConsumerUsage (Fn maps to Ctrl, not FnLayer)
    [Fact]
    public void FnPlusF8_NoConsumerUsage()
    {
        // Input: F8 (0x41) + Fn (byte 9 = 0x02), no physical Left Ctrl
        var input = new byte[] { 0x01, 0x00, 0x00, 0x41, 0x00, 0x00, 0x00, 0x00, 0x00, 0x02 };
        var result = A2450ReportTransformer.TransformWithConsumerUsage(input);

        // Fn is NOT FnLayer — FnLayer only activates via physical Left Ctrl.
        Assert.Null(result.ConsumerUsage);
    }

    // TC-11: LeftCtrl + all F7-F12 produce correct consumer usages
    [Theory]
    [InlineData(0x40, 0x00B6)] // F7 → Previous Track
    [InlineData(0x41, 0x00CD)] // F8 → Play/Pause
    [InlineData(0x42, 0x00B5)] // F9 → Next Track
    [InlineData(0x43, 0x00E2)] // F10 → Mute
    [InlineData(0x44, 0x00EA)] // F11 → Volume Down
    [InlineData(0x45, 0x00E9)] // F12 → Volume Up
    public void FnLayer_FKey_MapsToCorrectConsumerUsage(byte fKey, ushort expectedUsage)
    {
        var input = new byte[] { 0x01, 0x01, 0x00, fKey, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        var result = A2450ReportTransformer.TransformWithConsumerUsage(input);

        Assert.Equal(expectedUsage, result.ConsumerUsage);
    }

    // TC-12: LeftCtrl + non-media key → null ConsumerUsage
    [Fact]
    public void FnLayer_NonMediaKey_NoConsumerUsage()
    {
        // Left Ctrl + A (0x04)
        var input = new byte[] { 0x01, 0x01, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        var result = A2450ReportTransformer.TransformWithConsumerUsage(input);

        Assert.Null(result.ConsumerUsage);
    }
}
