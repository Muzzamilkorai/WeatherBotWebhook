using Microsoft.AspNetCore.Mvc;
using System.Text;
using WeatherBotWebhook.Services;
namespace WeatherBotWebhook.Controllers
{
    [ApiController]
    public class WeatherWebhookController : ControllerBase
    {
        private readonly OpenWeatherService _svc;

        public WeatherWebhookController(OpenWeatherService svc)
        {
            _svc = svc;
        }

        [HttpPost("api/webhook")]
        public async Task<IActionResult> Webhook([FromBody] DialogflowWebhookRequest req)
        {
            if (req?.QueryResult is null)
                return BadRequest("Invalid Dialogflow request.");

            var p = req.QueryResult.Parameters ?? new();
            string? city = p.TryGetValue("city", out var cObj) ? cObj?.ToString() : null;
            string? dateStr = p.TryGetValue("date", out var dObj) ? dObj?.ToString() : null;

            if (string.IsNullOrWhiteSpace(city))
                return OkText("Please tell me the city name.");

            // If no date then show Current weather
            if (!TryParseDate(dateStr, out var startDate))
            {
                var cw = await _svc.GetCurrentWeatherByCityAsync(city);
                if (cw?.Main == null)
                    return OkText($"Could not retrieve current weather for {city}.");

                var desc = cw.Weather?.FirstOrDefault()?.Description ?? "n/a";
                var txt = $"Current weather in {cw.Name ?? city}: {Math.Round(cw.Main.Temp)}°C, {desc}. " +
                          $"Feels like {Math.Round(cw.Main.FeelsLike)}°C. Humidity {cw.Main.Humidity}% and wind {Math.Round(cw.Wind?.Speed ?? 0)} m/s.";
                return OkText(txt);
            }

            // 8 days
            var endDate = startDate.Date.AddDays(7); 
            var fc = await _svc.GetForecastThroughSimpleAsync(city, endDate); 

            if (fc?.List == null || fc.List.Count == 0)
                return OkText($"Could not retrieve forecast for {city}.");

            // Converting timezone to local time
            var tz = TimeSpan.FromSeconds(fc.City?.TimezoneSeconds ?? 0);
            var groups = fc.List
                .Select(x => new { Item = x, Day = DateTimeOffset.FromUnixTimeSeconds(x.Dt).ToOffset(tz).Date })
                .GroupBy(x => x.Day)
                .ToDictionary(g => g.Key, g => g.Select(v => v.Item).ToList());

            // available date range
            var availableFrom = groups.Keys.Min();
            var availableTo = groups.Keys.Max();

            // out of range request
            if (endDate < availableFrom || startDate > availableTo)
            {
                return OkText(
                    $"I can provide an 8-day forecast for {city} between {availableFrom:yyyy-MM-dd} and {availableTo:yyyy-MM-dd}. " +
                    $"Your requested window ({startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}) is outside this range."
                );
            }

           
            var actualStart = startDate < availableFrom ? availableFrom : startDate;
            var actualEnd = endDate > availableTo ? availableTo : endDate;

            //daily summarry for each day
            var sb = new StringBuilder();
            sb.AppendLine($"8-day forecast for {fc.City?.Name ?? city} from {actualStart:yyyy-MM-dd} to {actualEnd:yyyy-MM-dd}:");

            for (var day = actualStart.Date; day <= actualEnd.Date; day = day.AddDays(1))
            {
                if (!groups.TryGetValue(day, out var items) || items.Count == 0)
                {
                    sb.AppendLine($"{day:yyyy-MM-dd}: (no data)");
                    continue;
                }

                var hi = Math.Round(items.Max(i => i.Main?.TempMax ?? i.Main?.Temp ?? 0));
                var lo = Math.Round(items.Min(i => i.Main?.TempMin ?? i.Main?.Temp ?? 0));
                var humidityAvg = (int)Math.Round(items.Average(i => (double)(i.Main?.Humidity ?? 0)));

                // description at 12:00 or 15:00 if available, otherwise the most frequent one
                var rep = items.OrderBy(i =>
                {
                    var h = DateTimeOffset.FromUnixTimeSeconds(i.Dt).ToOffset(tz).Hour;
                    return Math.Min(Math.Abs(h - 12), Math.Abs(h - 15));
                }).FirstOrDefault();
                var desc = rep?.Weather?.FirstOrDefault()?.Description
                           ?? items.SelectMany(i => i.Weather ?? new()).GroupBy(w => w.Description).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key
                           ?? "n/a";

                sb.AppendLine($"{day:yyyy-MM-dd}: {desc}, high {hi}°C, low {lo}°C, avg humidity {humidityAvg}%.");
            }

            return OkText(sb.ToString());

           
            IActionResult OkText(string s) => Ok(new DialogflowWebhookResponse { FulfillmentText = s });

            static bool TryParseDate(string? s, out DateTime date)
            {
                date = default;
                if (string.IsNullOrWhiteSpace(s)) return false;
                var fmts = new[] { "dd/MM/yyyy", "yyyy-MM-dd", "dd-MM-yyyy", "yyyy-MM-ddTHH:mm:ssK" };
                return DateTime.TryParseExact(s, fmts, System.Globalization.CultureInfo.InvariantCulture,
                                              System.Globalization.DateTimeStyles.None, out date);
            }
        }
    }
}
