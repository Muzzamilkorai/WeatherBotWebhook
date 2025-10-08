using System;

public class QueryResult
{
    [JsonPropertyName("parameters")] public Dictionary<string, object>? Parameters { get; set; }
    [JsonPropertyName("intent")] public Intent? Intent { get; set; }
}