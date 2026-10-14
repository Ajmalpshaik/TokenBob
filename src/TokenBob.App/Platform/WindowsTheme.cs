using Microsoft.Win32;

namespace TokenBob.App.Platform;

/// <summary>Whether Windows apps use the dark theme, and a signal when that changes.</summary>
internal static class WindowsTheme
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private static bool _lastDark = ReadIsDark();

    static WindowsTheme()
    {
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category != UserPreferenceCategory.General) return;
            var dark = ReadIsDark();
            if (dark == _lastDark) return;
            _lastDark = dark;
            Changed?.Invoke();
        };
    }

    public static event Action? Changed;

    public static bool IsDark => _lastDark;

    private static bool ReadIsDark()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
        return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
    }
}
