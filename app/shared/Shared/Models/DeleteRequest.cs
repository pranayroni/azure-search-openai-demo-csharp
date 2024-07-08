using System.Text.Json.Serialization;

namespace MinimalApi.Models;

public class DeleteRequest
{
    [property: JsonPropertyName("file")] public string file { get; set; } = "";

}
