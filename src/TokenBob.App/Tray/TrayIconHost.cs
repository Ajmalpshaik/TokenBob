using System.Windows.Controls;
using System.Windows.Media.Imaging;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using TokenBob.Core;

namespace TokenBob.App.Tray;

/// <summary>The notification-area icon: tooltip with usage, right-click menu, Windows notifications.</summary>
internal sealed class TrayIconHost : IDisposable
{
    private readonly TaskbarIcon _icon;
    private bool _widgetVisible = true;
    private bool _signedIn;

    public TrayIconHost()
    {
        _icon = new TaskbarIcon
        {
            IconSource = new BitmapImage(new Uri("pack://application:,,,/Assets/TokenBob.ico")),
            ToolTipText = AppInfo.Name,
            NoLeftClickDelay = true,
        };
        _icon.ContextMenu = BuildMenu();
        _icon.TrayMouseDoubleClick += (_, _) => SettingsClicked?.Invoke();
        _icon.ForceCreate(enablesEfficiencyMode: false);
    }

    public event Action? SettingsClicked;
    public event Action? ToggleClicked;
    public event Action? RefreshClicked;
    public event Action? SignInOutClicked;
    public event Action? QuitClicked;

    public void SetTooltip(string text) => _icon.ToolTipText = text;

    public void SetWidgetVisible(bool visible) => _widgetVisible = visible;

    public void SetSignedIn(bool signedIn) => _signedIn = signedIn;

    public void Notify(string title, string body) => _icon.ShowNotification(title, body, NotificationIcon.None);

    /// <summary>A fresh copy of the menu (also used for right-click on the widget).</summary>
    public ContextMenu BuildMenu()
    {
        var toggle = Item("Hide widget", () => ToggleClicked?.Invoke());
        var signInOut = Item("Sign in…", () => SignInOutClicked?.Invoke());
        var menu = new ContextMenu
        {
            Items =
            {
                Item("Settings", () => SettingsClicked?.Invoke()),
                toggle,
                Item("Refresh now", () => RefreshClicked?.Invoke()),
                signInOut,
                new Separator(),
                Item("Quit TokenBob", () => QuitClicked?.Invoke()),
            },
        };
        menu.Opened += (_, _) =>
        {
            toggle.Header = _widgetVisible ? "Hide widget" : "Show widget";
            signInOut.Header = _signedIn ? "Sign out" : "Sign in…";
        };
        return menu;
    }

    public void Dispose() => _icon.Dispose();

    private static MenuItem Item(string header, Action onClick)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => onClick();
        return item;
    }
}
