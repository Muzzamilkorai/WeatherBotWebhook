using System;

public class DialogflowWebhookRequest
{
    [JsonPropertyName("queryResult")] public QueryResult? QueryResult { get; set; }
}