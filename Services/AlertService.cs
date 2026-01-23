using System.Text.Json;
using System.Text;
using Barometer.Models;
using Microsoft.Extensions.Caching.Memory;

namespace Barometer.Services;

public interface IAlertService
{
    Task<AlertResponseDto> GetAlertStatusAsync();
}

public class AlertService : IAlertService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _config;
    private readonly ILogger<AlertService> _logger;
    private const string CacheKey = "AlertStatus";

    // Major cities in Hebrew - must all be present for IsMajorAlert
    private static readonly string[] MajorCitiesHebrew = new[]
    {
        "תל אביב",
        "ירושלים", 
        "חיפה"
    };

    public AlertService(HttpClient httpClient, IMemoryCache cache, IConfiguration config, ILogger<AlertService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _config = config;
        _logger = logger;
    }

    public async Task<AlertResponseDto> GetAlertStatusAsync()
    {
        if (_cache.TryGetValue(CacheKey, out AlertResponseDto? cached) && cached != null)
        {
            cached.IsFromCache = true;
            return cached;
        }

        var cacheSeconds = int.Parse(_config["ApiSettings:AlertCacheSeconds"] ?? "15");

        // Pikud Ha'oref API endpoint
        var url = "https://www.oref.org.il/WarningMessages/alert/alerts.json";

        try
        {
            // Set required headers for Pikud Ha'oref API
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "he-IL,he;q=0.9,en-US;q=0.8,en;q=0.7");
            _httpClient.DefaultRequestHeaders.Add("Referer", "https://www.oref.org.il/");
            _httpClient.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            var response = await _httpClient.GetAsync(url);
            
            var result = new AlertResponseDto
            {
                CachedAt = DateTime.UtcNow,
                IsFromCache = false
            };

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                
                // Handle empty response (no active alerts)
                if (string.IsNullOrWhiteSpace(json) || json == "[]" || json == "{}")
                {
                    result.Status = "Safe";
                    result.IsMajorAlert = false;
                    result.HasAnyAlert = false;
                }
                else
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    
                    // Try to parse as array first, then as single object
                    List<OrefAlertResponse>? alerts = null;
                    try
                    {
                        alerts = JsonSerializer.Deserialize<List<OrefAlertResponse>>(json, options);
                    }
                    catch
                    {
                        try
                        {
                            var singleAlert = JsonSerializer.Deserialize<OrefAlertResponse>(json, options);
                            if (singleAlert != null)
                                alerts = new List<OrefAlertResponse> { singleAlert };
                        }
                        catch
                        {
                            _logger.LogWarning("Could not parse alert response: {Json}", json);
                        }
                    }

                    if (alerts != null && alerts.Count > 0)
                    {
                        result.HasAnyAlert = true;
                        
                        // Extract all cities from the alert data
                        var allCities = new HashSet<string>();
                        foreach (var alert in alerts)
                        {
                            if (!string.IsNullOrEmpty(alert.Data))
                            {
                                // Data field contains comma-separated city names
                                var cities = alert.Data.Split(new[] { ',', '،', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                                foreach (var city in cities)
                                {
                                    var trimmedCity = city.Trim();
                                    if (!string.IsNullOrEmpty(trimmedCity))
                                    {
                                        allCities.Add(trimmedCity);
                                    }
                                }
                            }
                        }

                        result.ActiveCities = allCities.ToList();
                        result.ActiveCitiesEnglish = allCities.Select(c => MajorCities.TranslateToEnglish(c)).ToList();

                        // Check if all three major cities are in the alert
                        var majorCitiesFound = new List<string>();
                        foreach (var majorCity in MajorCitiesHebrew)
                        {
                            // Robust string matching - check if any active city contains the major city name
                            bool found = allCities.Any(activeCity => 
                                ContainsHebrewCity(activeCity, majorCity) || 
                                ContainsHebrewCity(majorCity, activeCity));
                            
                            if (found)
                            {
                                majorCitiesFound.Add(MajorCities.TranslateToEnglish(majorCity));
                            }
                        }

                        result.MajorCitiesInAlert = majorCitiesFound;

                        // IsMajorAlert = TRUE only if ALL THREE major cities are present
                        result.IsMajorAlert = majorCitiesFound.Count >= 3;
                        result.Status = result.IsMajorAlert ? "MajorAlert" : "Alert";
                    }
                    else
                    {
                        result.Status = "Safe";
                        result.IsMajorAlert = false;
                        result.HasAnyAlert = false;
                    }
                }
            }
            else
            {
                // Non-success status code - treat as no alerts (safe)
                result.Status = "Safe";
                result.IsMajorAlert = false;
                result.HasAnyAlert = false;
                _logger.LogWarning("Alert API returned status {StatusCode}", response.StatusCode);
            }

            _cache.Set(CacheKey, result, TimeSpan.FromSeconds(cacheSeconds));
            _logger.LogInformation("Alert data fetched and cached for {Seconds} seconds. Status: {Status}", cacheSeconds, result.Status);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch alert data from Pikud Ha'oref");
            
            // Return safe status on error (fail-safe)
            return new AlertResponseDto
            {
                Status = "Safe",
                IsMajorAlert = false,
                HasAnyAlert = false,
                CachedAt = DateTime.UtcNow,
                IsFromCache = false
            };
        }
    }

    /// <summary>
    /// Robust Hebrew string matching that handles variations in city names
    /// </summary>
    private static bool ContainsHebrewCity(string text, string city)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(city))
            return false;

        // Normalize both strings
        var normalizedText = NormalizeHebrew(text);
        var normalizedCity = NormalizeHebrew(city);

        return normalizedText.Contains(normalizedCity, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Normalize Hebrew text for comparison
    /// </summary>
    private static string NormalizeHebrew(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // Remove common Hebrew punctuation and normalize whitespace
        var normalized = text
            .Replace("־", " ")  // Hebrew maqaf
            .Replace("-", " ")
            .Replace("–", " ")
            .Replace("\"", "")
            .Replace("'", "")
            .Replace(",", " ")
            .Trim();

        // Normalize multiple spaces to single space
        while (normalized.Contains("  "))
            normalized = normalized.Replace("  ", " ");

        return normalized;
    }
}
