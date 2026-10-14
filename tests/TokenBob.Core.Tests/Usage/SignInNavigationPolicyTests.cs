using TokenBob.Core.Usage;

namespace TokenBob.Core.Tests.Usage;

public class SignInNavigationPolicyTests
{
    [Theory]
    [InlineData("https://claude.ai/login")]
    [InlineData("https://claude.ai/")]
    [InlineData("https://www.claude.ai/x")]
    [InlineData("https://console.anthropic.com/")]
    [InlineData("https://accounts.google.com/o/oauth2/v2/auth")]
    [InlineData("https://accounts.google.com.qa/accounts/SetSID")]
    [InlineData("https://www.google.com.qa/accounts/SetSID")]
    [InlineData("https://accounts.google.co.uk/x")]
    [InlineData("https://www.google.de/accounts/SetSID")]
    [InlineData("https://accounts.youtube.com/accounts/SetSID")]
    public void Allows_claude_anthropic_and_google_sign_in_urls(string url)
    {
        Assert.True(SignInNavigationPolicy.IsAllowed(new Uri(url)));
    }

    [Theory]
    [InlineData("http://claude.ai/login")]
    [InlineData("https://claude.ai.evil.com/")]
    [InlineData("https://evilclaude.ai/")]
    [InlineData("https://example.com/")]
    [InlineData("file:///C:/x")]
    [InlineData("http://accounts.google.com/")]
    [InlineData("https://google.com.evil.io/")]
    [InlineData("https://evilgoogle.com/")]
    [InlineData("https://google.evil.com/")]
    [InlineData("https://www.youtube.com/watch")]
    [InlineData("https://mail.google.com/")]
    public void Blocks_http_and_look_alike_hosts(string url)
    {
        Assert.False(SignInNavigationPolicy.IsAllowed(new Uri(url)));
    }

    [Fact]
    public void Login_url_is_claude_login_page()
    {
        Assert.Equal("https://claude.ai/login", SignInNavigationPolicy.LoginUrl);
    }
}
