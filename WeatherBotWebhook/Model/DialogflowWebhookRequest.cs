using System;
using System.Text.Json.Serialization;

public class DialogflowWebhookRequest
{
    [JsonPropertyName("queryResult")] public QueryResult? QueryResult { get; set; }
}