using System.Net;
using TransPeek.Core.Update;
using Xunit;

namespace TransPeek.Tests;

public sealed class UpdateCheckerTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_respond(request));
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => throw new HttpRequestException("no network");
    }

    [Theory]
    [InlineData("v0.2.0", 0, 2, 0)]
    [InlineData("0.2.0", 0, 2, 0)]
    [InlineData("v1.0", 1, 0, 0)]
    [InlineData("v1.0.0-beta", 1, 0, 0)]
    [InlineData("v0.10.3", 0, 10, 3)]
    public void ParseVersion_ParsesTags(string tag, int major, int minor, int build)
    {
        var version = UpdateChecker.ParseVersion(tag);
        Assert.NotNull(version);
        Assert.Equal(new Version(major, minor, build), version);
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("")]
    [InlineData(null)]
    public void ParseVersion_Unparseable_ReturnsNull(string? tag)
    {
        Assert.Null(UpdateChecker.ParseVersion(tag));
    }

    [Fact]
    public async Task CheckAsync_NewerTag_ReturnsUpdateAvailable()
    {
        const string body = """
            {"tag_name":"v0.2.0","html_url":"https://github.com/TranManhTuanLabs/TransPeek-Windows/releases/tag/v0.2.0","name":"v0.2.0"}
            """;
        var http = new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body),
        }));
        var result = await new UpdateChecker(http).CheckAsync(new Version(0, 1, 0));
        Assert.Equal(UpdateCheckStatus.UpdateAvailable, result.Status);
        Assert.Equal("0.2.0", result.LatestVersion);
        Assert.Contains("/releases/tag/v0.2.0", result.ReleaseUrl);
    }

    [Fact]
    public async Task CheckAsync_SameTag_ReturnsUpToDate()
    {
        const string body = """
            {"tag_name":"v0.1.0","html_url":"https://example.com/r","name":"v0.1.0"}
            """;
        var http = new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body),
        }));
        var result = await new UpdateChecker(http).CheckAsync(new Version(0, 1, 0));
        Assert.Equal(UpdateCheckStatus.UpToDate, result.Status);
        Assert.Equal("0.1.0", result.LatestVersion);
    }

    [Fact]
    public async Task CheckAsync_NoReleasesYet_ReturnsFriendlyError()
    {
        var http = new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));
        var result = await new UpdateChecker(http).CheckAsync(new Version(0, 1, 0));
        Assert.Equal(UpdateCheckStatus.Error, result.Status);
        Assert.Contains("No releases", result.ErrorMessage);
    }

    [Fact]
    public async Task CheckAsync_NoNetwork_ReturnsFriendlyError_NotThrow()
    {
        var http = new HttpClient(new ThrowingHandler());
        var result = await new UpdateChecker(http).CheckAsync(new Version(0, 1, 0));
        Assert.Equal(UpdateCheckStatus.Error, result.Status);
        Assert.Contains("network", result.ErrorMessage);
    }

    [Fact]
    public async Task CheckAsync_SendsUserAgent()
    {
        string? userAgent = null;
        var http = new HttpClient(new FakeHandler(request =>
        {
            userAgent = request.Headers.UserAgent.ToString();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"tag_name":"v0.1.0","html_url":"https://example.com/r"}"""),
            };
        }));
        await new UpdateChecker(http).CheckAsync(new Version(0, 1, 0));
        Assert.Contains("TransPeek-Windows", userAgent);
    }
}
