using System.Text.Json;

namespace DraCode.KoboldLair.Server.Models
{
    public class WebSocketCommand
    {
        public string? Id { get; set; }
        public string? Command { get; set; }
        public JsonElement? Data { get; set; }
    }
}
