namespace TokenBob.Core.Config;

public enum StartupApprovalState { Missing, Enabled, Disabled }

/// <summary>
/// Windows keeps a per-app "approved" value next to each startup entry (the Startup apps page
/// in Settings and Task Manager edit it). Without it, newer Windows builds skip the entry; an
/// odd first byte means the user switched the app off there.
/// </summary>
public static class StartupApproval
{
    public static StartupApprovalState Read(byte[]? value) => value switch
    {
        null or { Length: 0 } => StartupApprovalState.Missing,
        _ when value[0] % 2 == 1 => StartupApprovalState.Disabled,
        _ => StartupApprovalState.Enabled,
    };

    /// <summary>The value Windows writes for an enabled startup app.</summary>
    public static byte[] EnabledValue()
    {
        var value = new byte[12];
        value[0] = 0x02;
        return value;
    }
}
