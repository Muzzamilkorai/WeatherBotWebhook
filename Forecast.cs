using System;

public class Forcast
{
	
}
public class ForecastResponse
{
    [JsonPropertyName("list")] public List<ForecastItem>? List { get; set; }
    [JsonPropertyName("city")] public ForecastCity? City { get; set; }
}
public class ForecastCity
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("timezone")] public int TimezoneSeconds { get; set; } // seconds offset from UTC
}
public class ForecastItem
{
    [JsonPropertyName("dt")] public long Dt { get; set; }
    [JsonPropertyName("main")] public ForecastMain? Main { get; set; }
    [JsonPropertyName("weather")] public List<WeatherDesc>? Weather { get; set; }
    [JsonPropertyName("wind")] public ForecastWind? Wind { get; set; }
}
public class ForecastMain
{
    [JsonPropertyName("temp")] public double Temp { get; set; }
    [JsonPropertyName("feels_like")] public double FeelsLike { get; set; }
    [JsonPropertyName("humidity")] public int Humidity { get; set; }
    [JsonPropertyName("temp_min")] public double TempMin { get; set; }
    [JsonPropertyName("temp_max")] public double TempMax { get; set; }
}
public class ForecastWind { [JsonPropertyName("speed")] public double Speed { get; set; } }
public class WeatherDesc { [JsonPropertyName("description")] public string? Description { get; set; } }
public class CurrentWeatherByCity
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("main")] public ForecastMain? Main { get; set; }
    [JsonPropertyName("wind")] public ForecastWind? Wind { get; set; }
    [JsonPropertyName("weather")] public List<WeatherDesc>? Weather { get; set; }
}
