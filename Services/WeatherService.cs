using System.Text.Json;
using Barometer.Models;
using Microsoft.Extensions.Caching.Memory;

namespace Barometer.Services;

public interface IWeatherService
{
    Task<WeatherResponseDto> GetTehranForecastAsync();
}

public class WeatherService : IWeatherService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _config;
    private readonly ILogger<WeatherService> _logger;
    private const string CacheKey = "TehranWeather";

    public WeatherService(HttpClient httpClient, IMemoryCache cache, IConfiguration config, ILogger<WeatherService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _config = config;
        _logger = logger;
    }

    public async Task<WeatherResponseDto> GetTehranForecastAsync()
    {
        if (_cache.TryGetValue(CacheKey, out WeatherResponseDto? cached) && cached != null)
        {
            cached.IsFromCache = true;
            return cached;
        }

        var apiKey = _config["ApiSettings:OpenWeatherMapApiKey"];
        var lat = _config["ApiSettings:TehranLatitude"];
        var lon = _config["ApiSettings:TehranLongitude"];
        var cacheMinutes = int.Parse(_config["ApiSettings:WeatherCacheMinutes"] ?? "60");

        var url = $"https://api.openweathermap.org/data/2.5/forecast?lat={lat}&lon={lon}&appid={apiKey}&units=metric&cnt=8";

        try
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var data = JsonSerializer.Deserialize<OpenWeatherResponse>(json, options);

            var result = new WeatherResponseDto
            {
                CachedAt = DateTime.UtcNow,
                IsFromCache = false,
                Location = "Tehran, Iran"
            };

            if (data?.List != null)
            {
                foreach (var item in data.List)
                {
                    var forecast = new WeatherForecastDto
                    {
                        Timestamp = DateTimeOffset.FromUnixTimeSeconds(item.Dt).UtcDateTime,
                        Temperature = item.Main?.Temp ?? 0,
                        FeelsLike = item.Main?.Feels_like ?? 0,
                        Humidity = item.Main?.Humidity ?? 0,
                        WindSpeed = item.Wind?.Speed ?? 0,
                        Description = item.Weather?.FirstOrDefault()?.Description ?? "Unknown",
                        Icon = item.Weather?.FirstOrDefault()?.Icon ?? "01d"
                    };
                    result.Forecasts.Add(forecast);
                }
            }

            _cache.Set(CacheKey, result, TimeSpan.FromMinutes(cacheMinutes));
            _logger.LogInformation("Weather data fetched and cached for {Minutes} minutes", cacheMinutes);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch weather data from OpenWeatherMap");
            throw;
        }
    }
}
