using TokenBob.Core.Install;

namespace TokenBob.Core.Tests.Install;

public class InstallerTests
{
    private static readonly InstallPaths Paths = InstallPaths.For(@"C:\Users\pat\AppData\Local", @"C:\Users\pat\AppData\Roaming");
    private const string Downloads = @"C:\Users\pat\Downloads\TokenBob.exe";

    private static LaunchMode Decide(string exe, bool installed = false, bool singleFile = true, params string[] args) =>
        Installer.Decide(args, exe, Paths, installed, singleFile);

    [Fact]
    public void Paths_are_per_user_with_a_start_menu_shortcut()
    {
        Assert.Equal(@"C:\Users\pat\AppData\Local\Programs\TokenBob", Paths.Dir);
        Assert.Equal(@"C:\Users\pat\AppData\Local\Programs\TokenBob\TokenBob.exe", Paths.Exe);
        Assert.Equal(@"C:\Users\pat\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\TokenBob.lnk", Paths.StartMenuShortcut);
        Assert.Equal(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\TokenBob", InstallPaths.UninstallKey);
    }

    [Fact]
    public void First_double_click_from_downloads_installs()
    {
        Assert.Equal(LaunchMode.Install, Decide(Downloads));
    }

    [Fact]
    public void Newer_copy_over_an_installed_one_updates()
    {
        Assert.Equal(LaunchMode.Update, Decide(Downloads, installed: true));
    }

    [Theory]
    [InlineData(@"C:\Users\pat\AppData\Local\Programs\TokenBob\TokenBob.exe")]
    [InlineData(@"c:\users\PAT\appdata\local\programs\tokenbob\TOKENBOB.EXE")]
    public void Running_the_installed_copy_just_runs(string exe)
    {
        Assert.Equal(LaunchMode.Run, Decide(exe, installed: true));
    }

    [Fact]
    public void Uninstall_flag_uninstalls()
    {
        Assert.Equal(LaunchMode.Uninstall, Decide(Paths.Exe, installed: true, singleFile: true, "--uninstall"));
    }

    [Fact]
    public void Developer_builds_always_run()
    {
        Assert.Equal(LaunchMode.Run, Decide(@"D:\dev\TokenBob\bin\Debug\TokenBob.exe", singleFile: false));
    }

    [Theory]
    [InlineData("--no-install")]
    [InlineData("--render-samples")]
    [InlineData("--demo-alerts")]
    public void Developer_options_always_run(string option)
    {
        Assert.Equal(LaunchMode.Run, Decide(Downloads, installed: false, singleFile: true, option, "x.png"));
    }
}
