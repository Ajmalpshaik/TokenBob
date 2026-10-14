using TokenBob.Core.Physics;

namespace TokenBob.Core.Screens;

public readonly record struct ScreenRect(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;
    public double Bottom => Top + Height;

    public bool Contains(Vec2 p) => p.X >= Left && p.X < Right && p.Y >= Top && p.Y < Bottom;
}

/// <summary>A monitor: bounds in device pixels and its DPI scale (dpi / 96).</summary>
public sealed record MonitorInfo(string DeviceName, ScreenRect BoundsDevice, double Scale, bool IsPrimary);

/// <summary>How a monitor is remembered in settings.</summary>
public sealed record MonitorId(string DeviceName, ScreenRect BoundsDevice);

/// <summary>Where the widget hangs: a monitor, the anchor's x in device px, and rope length in logical px.</summary>
public sealed record PlacementResult(MonitorInfo Monitor, double AnchorXDevice, double RopeLength);

public static class Placement
{
    public const double EdgeMargin = 40;          // logical px
    public const double DefaultRightInset = 120;  // logical px
    public const double MinRope = 60;
    public const double MaxRope = 400;

    public static MonitorId IdOf(MonitorInfo monitor) => new(monitor.DeviceName, monitor.BoundsDevice);

    public static PlacementResult Resolve(IReadOnlyList<MonitorInfo> monitors, MonitorId? saved,
        double? savedAnchorXDevice, double ropeLength)
    {
        if (monitors.Count == 0) throw new ArgumentException("At least one monitor is required.", nameof(monitors));

        var rope = Math.Clamp(ropeLength, MinRope, MaxRope);
        var match = saved is null ? null : monitors.FirstOrDefault(m => IdOf(m) == saved);
        if (match is not null && savedAnchorXDevice is { } x)
            return new PlacementResult(match, ClampAnchor(match, x), rope);

        var primary = monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors[0];
        var defaultX = primary.BoundsDevice.Right - DefaultRightInset * primary.Scale;
        return new PlacementResult(primary, ClampAnchor(primary, defaultX), rope);
    }

    /// <summary>
    /// Shift + drag: the monitor under the cursor (else the current one) gets the anchor;
    /// <paramref name="grabOffset"/> is the logical distance from the rope end to where the ornament was grabbed.
    /// </summary>
    public static PlacementResult Edit(IReadOnlyList<MonitorInfo> monitors, PlacementResult current,
        Vec2 cursorDevice, double grabOffset)
    {
        var monitor = monitors.FirstOrDefault(m => m.BoundsDevice.Contains(cursorDevice)) ?? current.Monitor;
        var rope = (cursorDevice.Y - monitor.BoundsDevice.Top) / monitor.Scale - grabOffset;
        return new PlacementResult(monitor, ClampAnchor(monitor, cursorDevice.X), Math.Clamp(rope, MinRope, MaxRope));
    }

    /// <summary>The widget window in device px: centered on the anchor, kept inside the monitor.</summary>
    public static ScreenRect WindowRect(PlacementResult placement, double widthLogical, double heightLogical)
    {
        var bounds = placement.Monitor.BoundsDevice;
        var scale = placement.Monitor.Scale;
        var width = Math.Min(widthLogical * scale, bounds.Width);
        var height = Math.Min(heightLogical * scale, bounds.Height);
        var left = Math.Clamp(placement.AnchorXDevice - width / 2, bounds.Left, bounds.Right - width);
        return new ScreenRect(left, bounds.Top, width, height);
    }

    private static double ClampAnchor(MonitorInfo monitor, double x)
    {
        var margin = EdgeMargin * monitor.Scale;
        return Math.Clamp(x, monitor.BoundsDevice.Left + margin, monitor.BoundsDevice.Right - margin);
    }
}
