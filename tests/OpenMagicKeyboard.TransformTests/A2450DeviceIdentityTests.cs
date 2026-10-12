using OpenMagicKeyboard.Shared;
using Xunit;

namespace OpenMagicKeyboard.TransformTests;

public class A2450DeviceIdentityTests
{
    [Theory]
    [InlineData(@"HID\VID_05AC&PID_029C&MI_01&COL01", "USB")]
    [InlineData(@"\\?\hid#vid_05ac&pid_029c&mi_01&col02#part", "USB")]
    [InlineData(@"BTHENUM\{00001124-0000-1000-8000-00805F9B34FB}_VID&0001004C_PID&029C", "Bluetooth")]
    [InlineData(@"\\?\hid#{00001124-0000-1000-8000-00805f9b34fb}_vid&0001004c_pid&029c&col01#part", "Bluetooth")]
    public void RecognizesA2450AcrossTransports(string id, string expectedTransport)
    {
        Assert.True(A2450DeviceIdentity.IsA2450(id));
        Assert.Equal(expectedTransport, A2450DeviceIdentity.GetTransport(id));
    }

    [Theory]
    [InlineData(@"HID\VID_05AC&PID_026C")]
    [InlineData(@"HID\VID_05AC&PID_0320")]
    [InlineData(@"BTHENUM\{00001124-0000-1000-8000-00805F9B34FB}_VID&0001004C_PID&0267")]
    [InlineData(@"HID\VID_046D&PID_029C")]
    [InlineData("")]
    [InlineData(null)]
    public void DoesNotIdentifyOtherDevicesAsA2450(string? id)
    {
        Assert.False(A2450DeviceIdentity.IsA2450(id));
        Assert.Null(A2450DeviceIdentity.GetTransport(id));
    }
}
