
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using DraCode.KoboldLair.Client;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace DraCode.KoboldLair.Tests.Client;

/// <summary>
/// TASK-119: the client host's proxy forwards only the caller's own token, URL-encoded, validates the server
/// certificate outside an explicit Development opt-out, and relays the close status and description both ways.
/// </summary>
public class ServerWebSocketProxyTests
{
    [Fact]
    public void Caller_token_is_url_encoded_into_the_upstream_query()
    {
        var uri = ServerWebSocketProxy.BuildUpstreamUri("https://server:5001/", "/dragon", "a+b/c=&d");

        uri.AbsoluteUri.Should().Be("wss://server:5001/dragon?token=a%2Bb%2Fc%3D%26d");
    }

    [Fact]
    public void Without_a_caller_token_the_upstream_has_no_token()
    {
        var uri = ServerWebSocketProxy.BuildUpstreamUri("http://localhost:57087", "/wyvern", "");

        uri.AbsoluteUri.Should().Be("ws://localhost:57087/wyvern");
    }

    [Theory]
    [InlineData("Development", "true", true)]
    [InlineData("Development", null, false)]
    [InlineData("Production", "true", false)]
    [InlineData("Testing", "true", false)]
    public void Invalid_certificates_are_accepted_only_with_the_Development_opt_out(
        string environment, string? optOut, bool expected)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [ServerWebSocketProxy.AllowInvalidCertificateKey] = optOut })
            .Build();

        ServerWebSocketProxy.AllowsInvalidCertificate(configuration, new FakeEnvironment(environment))
            .Should().Be(expected);
    }

    [Fact]
    public async Task Messages_relay_and_a_browser_close_reaches_the_server_with_its_status()
    {
        var (browser, proxyClientSide) = await CreatePairAsync();
        var (proxyServerSide, server) = await CreatePairAsync();
        var relay = ServerWebSocketProxy.RelayAsync(proxyClientSide, proxyServerSide, CancellationToken.None);

        await browser.SendAsync(Encoding.UTF8.GetBytes("hello"), WebSocketMessageType.Text, true, CancellationToken.None);
        (await ReceiveTextAsync(server)).Should().Be("hello");

        await browser.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "user left", CancellationToken.None);
        var close = await server.ReceiveAsync(new byte[16], CancellationToken.None);
        close.MessageType.Should().Be(WebSocketMessageType.Close);
        close.CloseStatus.Should().Be(WebSocketCloseStatus.NormalClosure);
        close.CloseStatusDescription.Should().Be("user left");

        await server.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
        await relay.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task A_server_close_reaches_the_browser_with_its_status_and_description()
    {
        var (browser, proxyClientSide) = await CreatePairAsync();
        var (proxyServerSide, server) = await CreatePairAsync();
        var relay = ServerWebSocketProxy.RelayAsync(proxyClientSide, proxyServerSide, CancellationToken.None);

        await server.CloseOutputAsync((WebSocketCloseStatus)4001, "token expired", CancellationToken.None);
        var close = await browser.ReceiveAsync(new byte[16], CancellationToken.None);
        close.CloseStatus.Should().Be((WebSocketCloseStatus)4001);
        close.CloseStatusDescription.Should().Be("token expired");

        await browser.CloseOutputAsync((WebSocketCloseStatus)4001, "token expired", CancellationToken.None);
        await relay.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private static async Task<string> ReceiveTextAsync(WebSocket socket)
    {
        var buffer = new byte[64];
        var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
        return Encoding.UTF8.GetString(buffer, 0, result.Count);
    }

    /// <summary>Two connected WebSockets over a loopback TCP connection: (client end, server end).</summary>
    private static async Task<(WebSocket, WebSocket)> CreatePairAsync()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var tcpClient = new TcpClient();
        var accept = listener.AcceptTcpClientAsync();
        await tcpClient.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
        var tcpServer = await accept;
        return (
            WebSocket.CreateFromStream(tcpClient.GetStream(), isServer: false, subProtocol: null, TimeSpan.Zero),
            WebSocket.CreateFromStream(tcpServer.GetStream(), isServer: true, subProtocol: null, TimeSpan.Zero));
    }

    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
