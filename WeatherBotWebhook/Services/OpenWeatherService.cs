using System.Text.Json;
using System.Text.Json.Serialization;

namespace WeatherBotWebhook.Services
{
    public class OpenWeatherService
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;
        private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

        public OpenWeatherService(HttpClient http, IConfiguration cfg)
        {
            _http = http;
            _apiKey = cfg["OpenWeather:ApiKey"] ?? throw new InvalidOperationException("OpenWeather:ApiKey missing");
        }

        public async Task<ForecastResponse?> GetFiveDayForecastByCityAsync(string city, string units = "metric")
        {
            var url = $"https://api.openweathermap.org/data/2.5/forecast?q={Uri.EscapeDataString(city)}&appid={_apiKey}&units={units}";
            using var resp = await _http.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ForecastResponse>(json, _json);
        }
        public async Task<ForecastResponse?> GetForecastThroughSimpleAsync(string city, DateTime localEndDate, string units = "metric")
        {
            // Get normal 5-day  3-hour forecast
            var resp = await GetFiveDayForecastByCityAsync(city, units);
            if (resp?.List == null || resp.List.Count == 0) return resp;

            var tz = TimeSpan.FromSeconds(resp.City?.TimezoneSeconds ?? 0);

            // What local date do we have up to?
            DateTime lastLocalDate = resp.List
                .Select(i => DateTimeOffset.FromUnixTimeSeconds(i.Dt).ToOffset(tz).Date)
                .DefaultIfEmpty(DateTime.UtcNow.Date)
                .Max();

            var targetEnd = localEndDate.Date;
            if (lastLocalDate >= targetEnd) return resp; 

            (double min, double max, int hum, double wind, string desc) GetDayStats(DateTime day)
            {
                var items = resp.List
                    .Where(i => DateTimeOffset.FromUnixTimeSeconds(i.Dt).ToOffset(tz).Date == day.Date)
                    .ToList();

                if (items.Count == 0)
                {
                    // Fallback to overall last day that exists
                    var lastDay = resp.List
                        .Select(i => DateTimeOffset.FromUnixTimeSeconds(i.Dt).ToOffset(tz).Date)
                        .Max();

                    items = resp.List
                        .Where(i => DateTimeOffset.FromUnixTimeSeconds(i.Dt).ToOffset(tz).Date == lastDay)
                        .ToList();
                }

                var min = items.Min(i => i.Main?.TempMin ?? i.Main?.Temp ?? 0);
                var max = items.Max(i => i.Main?.TempMax ?? i.Main?.Temp ?? 0);
                var hum = (int)Math.Round(items.Average(i => (double)(i.Main?.Humidity ?? 0)));
                var wind = items.Average(i => i.Wind?.Speed ?? 0);
                var desc = items.SelectMany(i => i.Weather ?? new())
                                .GroupBy(w => w.Description)
                                .OrderByDescending(g => g.Count())
                                .FirstOrDefault()?.Key ?? "forecast";

                return (min, max, hum, wind, desc);
            }

            long lastDt = resp.List.Max(i => i.Dt);
            const int threeHours = 3 * 3600;

            // We’ll copy the last available day’s pattern forward until we cover targetEnd
            var seed = GetDayStats(lastLocalDate);

            while (lastLocalDate < targetEnd)
            {
                lastLocalDate = lastLocalDate.AddDays(1);

                var dMin = seed.min;
                var dMax = Math.Max(dMin, seed.max);

                // Append 8 × 3h slices ≈ 24h
                for (int slot = 0; slot < 8; slot++)
                {
                    lastDt += threeHours;
                    double phase = slot / 8.0;              
                    double temp = DiurnalTemp(dMin, dMax, phase);

                    resp.List.Add(new ForecastItem
                    {
                        Dt = lastDt,
                        Main = new ForecastMain
                        {
                            Temp = Math.Round(temp, 1),
                            TempMin = Math.Round(Math.Min(temp, dMin), 1),
                            TempMax = Math.Round(Math.Max(temp, dMax), 1),
                            FeelsLike = Math.Round(temp, 1),
                            Humidity = seed.hum
                        },
                        Wind = new ForecastWind { Speed = Math.Round(seed.wind, 1) },
                        Weather = new List<WeatherDesc> { new WeatherDesc { Description = seed.desc } }
                    });
                }
            }

            return resp;

            static double DiurnalTemp(double tMin, double tMax, double phase)
            {
                double shift = 0.6; // peak ~ 15:00 local-ish
                double x = phase - shift;
                x -= Math.Floor(x);
                double cos = Math.Cos(2 * Math.PI * x);
                return tMin + (tMax - tMin) * (1 - cos) / 2.0;
            }
        }

        private static (double a, double b) FitLine(double[] x, double[] y)
        {
            int n = Math.Min(x.Length, y.Length);
            if (n == 0) return (0, 0);
            if (n == 1) return (0, y[0]);
            double sx = x.Sum(), sy = y.Sum();
            double sxx = x.Select(v => v * v).Sum();
            double sxy = x.Zip(y, (xi, yi) => xi * yi).Sum();
            double denom = n * sxx - sx * sx;
            if (Math.Abs(denom) < 1e-9) return (0, sy / n);
            double a = (n * sxy - sx * sy) / denom;
            double b = (sy - a * sx) / n;
            return (a, b);
        }

        private static double Clamp(double v, double lo, double hi) => Math.Max(lo, Math.Min(hi, v));

        private static double DiurnalTemp(double tMin, double tMax, double phase)
        {
            double shift = 0.6; // set daily max ~ 0.6 of the day
            double x = phase - shift;
            x -= Math.Floor(x);           // wrap 0..1
            double cos = Math.Cos(2 * Math.PI * x);
            return tMin + (tMax - tMin) * (1 - cos) / 2.0; // map cos [-1..1] -> [min..max]
        }

        public async Task<CurrentWeatherByCity?> GetCurrentWeatherByCityAsync(string city, string units = "metric")
        {
            var url = $"https://api.openweathermap.org/data/2.5/weather?q={Uri.EscapeDataString(city)}&appid={_apiKey}&units={units}";
            using var resp = await _http.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<CurrentWeatherByCity>(json, _json);
        }
    }



    public class Intent { [JsonPropertyName("displayName")] public string? DisplayName { get; set; } }
    public class DialogflowWebhookResponse { [JsonPropertyName("fulfillmentText")] public string? FulfillmentText { get; set; } }



}

