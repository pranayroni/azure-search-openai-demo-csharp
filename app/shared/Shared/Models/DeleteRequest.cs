using System.Text.Json.Serialization;

namespace Shared.Models;

public class DeleteRequest
{
    [property: JsonPropertyName("file")] public string file { get; set; } = "";

}
