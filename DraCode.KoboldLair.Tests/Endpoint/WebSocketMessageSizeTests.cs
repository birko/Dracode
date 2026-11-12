using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using DraCode.KoboldLair.Server.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace DraCode.KoboldLair.Tests.Endpoint;

/// <summary>
/// TASK-135: every WebSocket endpoint reads whole messages. A message over the old 64 KB buffer arrives whole (it used to
/// be parsed as a fragment), and one over <see cref="WebSocketLimits.MaxMessageBytes"/> closes the socket with
/// MessageTooBig instead of being parsed.
/// </summary>
public class WebSocketMessageSizeTests
{
    private const int OverOldBuffer = 100 * 1024;

    private static WebApplicationFactory<Program> CreateFactory()
    {
        var config = new Dictionary<string, string?>
        {
            ["Authentication:Jwt:Enabled"] = "false",
            ["KoboldLair:Data:DefaultBackend"] = "JsonFile",
            ["KoboldLair:ProjectsPath"] = Path.Combine(Path.GetTempPath(), "kl-ws-size-tests", Guid.NewGuid().ToString("N")),
            // A provider so a Dragon session can create its agents; nothing here calls it.
            ["KoboldLair:DefaultProvider"] = "test-ollama",
            ["KoboldLair:Providers:0:Name"] = "test-ollama",
            ["KoboldLair:Providers:0:Type"] = "ollama",
            ["KoboldLair:Providers:0:DefaultModel"] = "none",
            ["KoboldLair:Providers:0:RequiresApiKey"] = "false",
            ["KoboldLair:Providers:0:CompatibleAgents:0"] = "all",
            ["KoboldLair:Providers:0:Configuration:baseUrl"] = "http://127.0.0.1:9",
        };

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(config));
            builder.ConfigureTestServices(services => services.RemoveAll<IHostedService>());
        });
    }

    private static void SafeDispose(WebApplicationFactory<Program> factory)
    {
        try { factory.Dispose(); }
        catch (AggregateException) { }
        catch (OperationCanceledException) { }
    }

    private static async Task<WebSocket> ConnectAsync(WebApplicationFactory<Program> factory, string path)
    {
        var uri = new UriBuilder(factory.Server.BaseAddress) { Scheme = "ws", Path = path }.Uri;
        return await factory.Server.CreateWebSocketClient().ConnectAsync(uri, CancellationToken.None);
    }

    /// <summary>A JSON message of at least <paramref name="size"/> bytes: <paramref name="fields"/> plus a padding field.</summary>
    private static byte[] Padded(Dictionary<string, object> fields, int size)
    {
        fields["padding"] = new string('x', size);
        return JsonSerializer.SerializeToUtf8Bytes(fields);
    }

    /// <summary>Sends in 16 KB frames, as a browser or proxy may.</summary>
    private static async Task SendFramedAsync(WebSocket socket, byte[] message)
    {
        const int frame = 16 * 1024;
        for (var offset = 0; offset < message.Length; offset += frame)
        {
            var count = Math.Min(frame, message.Length - offset);
            await socket.SendAsync(new ArraySegment<byte>(message, offset, count), WebSocketMessageType.Text,
                endOfMessage: offset + count == message.Length, CancellationToken.None);
        }
    }

    /// <summary>The next text message, or null when the server closed; the close status is returned too.</summary>
    private static async Task<(string? Text, WebSocketCloseStatus? Close)> ReceiveAsync(WebSocket socket)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var buffer = new byte[16 * 1024];
        using var ms = new MemoryStream();
        while (true)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
            if (result.MessageType == WebSocketMessageType.Close)
                return (null, result.CloseStatus);
            ms.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
                return (Encoding.UTF8.GetString(ms.ToArray()), null);
        }
    }

    private static string? TypeOf(string json) =>
        JsonDocument.Parse(json).RootElement.TryGetProperty("type", out var t) ? t.GetString() : null;

    [Theory]
    [InlineData("/wyvern", "action")]
    [InlineData("/dragon", "type")]
    public async Task A_ping_larger_than_64_KB_arrives_whole(string path, string typeField)
    {
        var factory = CreateFactory();
        try
        {
            using var socket = await ConnectAsync(factory, path);

            await SendFramedAsync(socket, Padded(new() { [typeField] = "ping" }, OverOldBuffer));

            var (text, close) = await ReceiveAsync(socket);
            close.Should().BeNull();
            TypeOf(text!).Should().Be("pong");
        }
        finally { SafeDispose(factory); }
    }

    [Fact]
    public async Task A_kobold_initial_payload_larger_than_64_KB_arrives_whole()
    {
        var factory = CreateFactory();
        try
        {
            using var socket = await ConnectAsync(factory, "/kobold");

            await SendFramedAsync(socket, Padded(new() { ["mode"] = "no-such-mode" }, OverOldBuffer));

            var (text, _) = await ReceiveAsync(socket);
            JsonDocument.Parse(text!).RootElement.GetProperty("message").GetString()
                .Should().Be("unknown mode 'no-such-mode'");
        }
        finally { SafeDispose(factory); }
    }

    [Theory]
    [InlineData("/wyvern", "action")]
    [InlineData("/dragon", "type")]
    [InlineData("/kobold", "mode")]
    public async Task A_message_over_the_cap_closes_with_MessageTooBig(string path, string typeField)
    {
        var factory = CreateFactory();
        try
        {
            using var socket = await ConnectAsync(factory, path);

            try
            {
                await SendFramedAsync(socket, Padded(new() { [typeField] = "ping" }, WebSocketLimits.MaxMessageBytes + 1));
            }
            catch (WebSocketException) { } // the server may close before the last frame is written

            var (text, close) = await ReceiveAsync(socket);
            text.Should().BeNull("an over-cap message must not be parsed and answered");
            close.Should().Be(WebSocketCloseStatus.MessageTooBig);
        }
        finally { SafeDispose(factory); }
    }
}
