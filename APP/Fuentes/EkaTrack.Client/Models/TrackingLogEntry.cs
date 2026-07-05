using System.Text.Json;

namespace EkaTrack.Client.Models;

public class TrackingLogEntry
{
    public int Id { get; set; }
    public string Action { get; set; } = "";
    public JsonElement? Details { get; set; }
    public string CreatedAt { get; set; } = "";
}