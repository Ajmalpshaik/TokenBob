using TokenBob.Core.Config;

namespace TokenBob.Core.Tests.Config;

public class StartupApprovalTests
{
    [Fact]
    public void Missing_value_is_not_yet_approved()
    {
        Assert.Equal(StartupApprovalState.Missing, StartupApproval.Read(null));
        Assert.Equal(StartupApprovalState.Missing, StartupApproval.Read([]));
    }

    [Theory]
    [InlineData((byte)0x02)]
    [InlineData((byte)0x06)]
    public void Even_first_byte_is_enabled(byte first)
    {
        Assert.Equal(StartupApprovalState.Enabled, StartupApproval.Read([first, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]));
    }

    [Theory]
    [InlineData((byte)0x03)]
    [InlineData((byte)0x01)]
    public void Odd_first_byte_means_the_user_turned_it_off(byte first)
    {
        Assert.Equal(StartupApprovalState.Disabled, StartupApproval.Read([first, 0, 0, 0, 0x10, 0x20, 0, 0, 0, 0, 0, 0]));
    }

    [Fact]
    public void Enabled_value_is_twelve_bytes_starting_with_02()
    {
        var value = StartupApproval.EnabledValue();
        Assert.Equal(12, value.Length);
        Assert.Equal(0x02, value[0]);
        Assert.All(value.Skip(1), b => Assert.Equal(0, b));
    }
}
