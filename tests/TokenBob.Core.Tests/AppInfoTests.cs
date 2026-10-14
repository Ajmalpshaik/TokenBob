using TokenBob.Core;

namespace TokenBob.Core.Tests;

public class AppInfoTests
{
    [Fact]
    public void AppInfo_constants_match_spec()
    {
        Assert.Equal("TokenBob", AppInfo.Name);
        Assert.Equal("https://github.com/Ajmalpshaik/TokenBob", AppInfo.RepoUrl);
        Assert.Equal("https://claude.ai/settings/usage", AppInfo.UsagePageUrl);
    }
}
