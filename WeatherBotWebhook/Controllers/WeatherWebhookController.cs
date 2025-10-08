using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json; 
using System.Collections.Generic; 
using System.Linq; 
using System;
using WeatherBotWebhook.Services; 


namespace WeatherBotWebhook.Controllers
{
    [ApiController]
 
    public class WeatherWebhookController : ControllerBase
    {
        private readonly OpenWeatherService _openWeatherService;

        public WeatherWebhookController(OpenWeatherService openWeatherService) 
        {
            _openWeatherService = openWeatherService;
        }

        [HttpPost("api/webhook")] 
        public async Task<IActionResult> HandleWebhook([FromBody] DialogflowWebhookRequest req)
        {
            if (req?.QueryResult?.Intent is null)
            {
                Console.WriteLine("Received invalid Dialogflow request: Missing QueryResult or Intent.");
                return BadRequest("Invalid Dialogflow request: Missing intent information.");
            }

            Console.WriteLine($"Received Dialogflow request for intent: {req.QueryResult.Intent.DisplayName}");

            var parameters = req.QueryResult.Parameters ?? new Dictionary<string, object>(); 
            string? city = GetParameterValue<string>(parameters, "city"); 
            string? dateStr = GetParameterValue<string>(parameters, "date"); 

            // Intent Based Routing
            string intentDisplayName = req.QueryResult.Intent.DisplayName;
            string fulfillmentText = "I'm sorry, I couldn't process your request. Please try again.";

            switch (intentDisplayName)
            {
                case "CurrentWeather": 
                    fulfillmentText = await GetCurrentWeatherResponse(city);
                    break;

                case "WeatherForecast": 
                    fulfillmentText = await GetWeatherForecastResponse(city, dateStr);
                    break;

              
                default:
                    Console.WriteLine($"Unhandled intent: {intentDisplayName}");
                    fulfillmentText = "I'm not sure how to handle that. Can you ask about current weather or a forecast?";
                    break;
            }

            return Ok(new DialogflowWebhookResponse { FulfillmentText = fulfillmentText });
        }

        private T? GetParameterValue<T>(Dictionary<string, object> parameters, string paramName) where T : class
        {
            if (parameters.TryGetValue(paramName, out var value) && value != null)
            {
                if (value is JsonElement jsonElement)
                {

                    if (typeof(T) == typeof(string))
                        return jsonElement.GetString() as T;
                }
                else if (value is T typedValue)
                {
                    return typedValue;
                }
                else if (value is IConvertible convertibleValue)
                {
                    try
                    {
                        return (T)Convert.ChangeType(value, typeof(T));
                    }
                    catch (InvalidCastException) { /* log or ignore */ }
                }
            }
            return null;
        }


        private async Task<string> GetCurrentWeatherResponse(string? city)
        {
            if (string.IsNullOrWhiteSpace(city))
            {
                return "I need a city to get the current weather. Which city are you interested in?";
            }

            var cw = await _openWeatherService.GetCurrentWeatherByCityAsync(city);
            if (cw?.Main == null)
            {
                return $"Could not retrieve current weather for {city}. Please check the city name.";
            }

            var desc = cw.Weather?.FirstOrDefault()?.Description ?? "n/a";
            return $"Current weather in {cw.Name ?? city}: {Math.Round(cw.Main.Temp)}°C, {desc}. " +
                   $"Feels like {Math.Round(cw.Main.FeelsLike)}°C. Humidity {cw.Main.Humidity}% and wind {Math.Round(cw.Wind?.Speed ?? 0)} m/s.";
        }

        private async Task<string> GetWeatherForecastResponse(string? city, string? dateString)
        {
            if (string.IsNullOrWhiteSpace(city))
            {
                return "I need a city to provide the weather forecast. Which city are you interested in?";
            }

            DateTime startDate;
            if (!TryParseDate(dateString, out startDate))
            {
                startDate = DateTime.UtcNow; 
                Console.WriteLine($"No valid date provided, defaulting forecast start date to {startDate.ToShortDateString()}.");
            }
            else
            {
                startDate = startDate.Date;
            }

            var endDate = startDate.AddDays(7);

            var fc = await _openWeatherService.GetForecastThroughSimpleAsync(city, endDate);

            if (fc?.List == null || !fc.List.Any())
            {
                return $"Could not retrieve forecast for {city} for the period starting {startDate.ToShortDateString()}.";
            }

            // Converting timezone to local time
            var tz = TimeSpan.FromSeconds(fc.City?.TimezoneSeconds ?? 0);
            var groups = fc.List
                .Select(x => new { Item = x, Day = DateTimeOffset.FromUnixTimeSeconds(x.Dt).ToOffset(tz).Date })
                .GroupBy(x => x.Day)
                .ToDictionary(g => g.Key, g => g.Select(v => v.Item).ToList());

            var availableFrom = groups.Keys.Any() ? groups.Keys.Min() : DateTime.MinValue;
            var availableTo = groups.Keys.Any() ? groups.Keys.Max() : DateTime.MinValue;

            var actualStart = startDate.Date;
            var actualEnd = endDate.Date;

            if (actualStart > availableTo || actualEnd < availableFrom)
            {
                if (actualStart > availableTo && actualStart > availableFrom)
                {                    return $"I can provide an 8-day forecast for {city} starting from {availableFrom.ToShortDateString()}. Your requested date ({startDate.ToShortDateString()}) is too far in the future.";
                }
            }

            // Build the forecast summary string
            var sb = new StringBuilder();
            sb.AppendLine($"8-day forecast for {fc.City?.Name ?? city} from {actualStart.ToShortDateString()} to {actualEnd.ToShortDateString()}:");

            // Loop through the 8-day window (startDate to endDate)
            for (var day = actualStart; day <= actualEnd; day = day.AddDays(1))
            {
                if (groups.TryGetValue(day, out var items) && items.Any())
                {
                    var hi = Math.Round(items.Max(i => i.Main?.TempMax ?? i.Main?.Temp ?? 0));
                    var lo = Math.Round(items.Min(i => i.Main?.TempMin ?? i.Main?.Temp ?? 0));
                    var humidityAvg = (int)Math.Round(items.Average(i => (double)(i.Main?.Humidity ?? 0)));

                    var rep = items.OrderBy(i =>
                    {
                        var h = DateTimeOffset.FromUnixTimeSeconds(i.Dt).ToOffset(tz).Hour;
                        return Math.Min(Math.Abs(h - 12), Math.Abs(h - 15));
                    }).FirstOrDefault();
                    var desc = rep?.Weather?.FirstOrDefault()?.Description
                               ?? items.SelectMany(i => i.Weather ?? new()).GroupBy(w => w.Description).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key
                               ?? "n/a";

                    sb.AppendLine($"{day.ToShortDateString()}: {desc}, high {hi}°C, low {lo}°C, avg humidity {humidityAvg}%.");
                }
                else
                {
                    sb.AppendLine($"{day.ToShortDateString()}: No data available for this day.");
                }
            }

            return sb.ToString().Trim(); // Trim any trailing newlines
        }

        private static bool TryParseDate(string? s, out DateTime date)
        {
            date = default;
            if (string.IsNullOrWhiteSpace(s)) return false;


            if (DateTime.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out date))
            {
                return true;
            }

          
            var fmts = new[] { "dd/MM/yyyy", "yyyy-MM-dd", "dd-MM-yyyy" };
            return DateTime.TryParseExact(s, fmts, System.Globalization.CultureInfo.InvariantCulture,
                                          System.Globalization.DateTimeStyles.None, out date);
        }
    }
}