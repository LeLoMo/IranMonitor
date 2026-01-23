namespace Barometer.Models;

public class WeatherForecastDto
{
    public DateTime Timestamp { get; set; }
    public double Temperature { get; set; }
    public double FeelsLike { get; set; }
    public int Humidity { get; set; }
    public double WindSpeed { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
}

public class WeatherResponseDto
{
    public List<WeatherForecastDto> Forecasts { get; set; } = new();
    public DateTime CachedAt { get; set; }
    public string Location { get; set; } = "Tehran, Iran";
    public bool IsFromCache { get; set; }
}

// OpenWeatherMap API Response Models
public class OpenWeatherResponse
{
    public List<OpenWeatherItem>? List { get; set; }
}

public class OpenWeatherItem
{
    public long Dt { get; set; }
    public OpenWeatherMain? Main { get; set; }
    public List<OpenWeatherWeather>? Weather { get; set; }
    public OpenWeatherWind? Wind { get; set; }
}

public class OpenWeatherMain
{
    public double Temp { get; set; }
    public double Feels_like { get; set; }
    public int Humidity { get; set; }
}

public class OpenWeatherWeather
{
    public string? Description { get; set; }
    public string? Icon { get; set; }
}

public class OpenWeatherWind
{
    public double Speed { get; set; }
}
