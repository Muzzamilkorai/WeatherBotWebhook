using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WeatherBotWebhook.Services;

var builder = WebApplication.CreateBuilder(args);

// JSON + HttpClient + DI
builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddHttpClient<OpenWeatherService>(); // absolute URLs used in service
builder.Services.AddScoped<OpenWeatherService>();

var app = builder.Build();
app.UseHttpsRedirection();
app.MapControllers();

app.Run();

