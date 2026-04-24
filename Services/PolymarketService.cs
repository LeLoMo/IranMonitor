using System.Text.Json;
using Barometer.Models;
using Microsoft.Extensions.Caching.Memory;

namespace Barometer.Services;

public interface IPolymarketService
{
    Task<PolymarketDataDto> GetMarketDataAsync();
}

public class PolymarketService : IPolymarketService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _config;
    private readonly ILogger<PolymarketService> _logger;
    private const string CacheKey = "PolymarketData";

    public PolymarketService(HttpClient httpClient, IMemoryCache cache, IConfiguration config, ILogger<PolymarketService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _config = config;
        _logger = logger;
    }

    public async Task<PolymarketDataDto> GetMarketDataAsync()
    {
        if (_cache.TryGetValue(CacheKey, out PolymarketDataDto? cached) && cached != null)
        {
            cached.IsFromCache = true;
            return cached;
        }

        var slug = _config["ApiSettings:PolymarketSlug"] ?? "will-the-us-invade-iran-before-2027";
        var cacheMinutes = int.Parse(_config["ApiSettings:PolymarketCacheMinutes"] ?? "5");
        var bigTradeThreshold = double.Parse(_config["ApiSettings:BigTradeThreshold"] ?? "10000");

        // Use the /events endpoint - this returns the event with all its markets
        var url = $"https://gamma-api.polymarket.com/events?slug={slug}";

        try
        {
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            var result = new PolymarketDataDto
            {
                Slug = slug,
                CachedAt = DateTime.UtcNow,
                IsFromCache = false
            };

            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            {
                var eventData = root[0];
                
                result.MarketTitle = eventData.TryGetProperty("title", out var title) ? title.GetString() ?? "US Invades Iran" : "US Invades Iran";
                result.Volume = eventData.TryGetProperty("volume", out var vol) ? vol.GetDouble() : 0;

                // Get the markets array from the event
                if (eventData.TryGetProperty("markets", out var markets) && markets.ValueKind == JsonValueKind.Array)
                {
                    // This is a single-market event — take the first active (non-closed) market
                    JsonElement? targetMarket = null;

                    foreach (var market in markets.EnumerateArray())
                    {
                        var isClosed = market.TryGetProperty("closed", out var closed) && closed.GetBoolean();
                        if (isClosed) continue;

                        targetMarket = market;
                        _logger.LogInformation("Found active market: {Question}", 
                            market.TryGetProperty("question", out var q) ? q.GetString() : "unknown");
                        break;
                    }

                    // If all markets are closed, fall back to the first one
                    if (!targetMarket.HasValue && markets.GetArrayLength() > 0)
                    {
                        targetMarket = markets[0];
                        _logger.LogWarning("All markets are closed, using first market as fallback");
                    }

                    // Parse the market's outcome prices
                    if (targetMarket.HasValue)
                    {
                        var market = targetMarket.Value;
                        
                        if (market.TryGetProperty("question", out var question))
                        {
                            result.MarketTitle = question.GetString() ?? result.MarketTitle;
                        }

                        if (market.TryGetProperty("outcomePrices", out var outcomesPrices))
                        {
                            var pricesStr = outcomesPrices.GetString();
                            if (!string.IsNullOrEmpty(pricesStr))
                            {
                                // Parse ["0.255", "0.745"] format
                                var prices = JsonSerializer.Deserialize<string[]>(pricesStr, options);
                                if (prices != null && prices.Length >= 2)
                                {
                                    if (double.TryParse(prices[0], out var yesPrice))
                                        result.YesPercentage = yesPrice * 100;
                                    if (double.TryParse(prices[1], out var noPrice))
                                        result.NoPercentage = noPrice * 100;
                                }
                            }
                        }

                        if (market.TryGetProperty("volumeNum", out var volNum))
                        {
                            result.Volume = volNum.GetDouble();
                        }

                        // Big trade detection: The Gamma API doesn't provide individual trade data
                        // We use a combination of high 24hr volume AND significant 1hr price movement
                        // as a proxy for detecting large trades
                        var hasHighVolume = false;
                        var hasSignificantPriceMove = false;
                        
                        if (market.TryGetProperty("volume24hr", out var vol24hr))
                        {
                            var volume24 = vol24hr.GetDouble();
                            // If hourly average volume exceeds threshold, that's significant
                            hasHighVolume = (volume24 / 24.0) > bigTradeThreshold;
                        }
                        
                        if (market.TryGetProperty("oneHourPriceChange", out var priceChange))
                        {
                            var hourlyChange = Math.Abs(priceChange.GetDouble());
                            // If price moved more than 1% in an hour, large trades likely occurred
                            hasSignificantPriceMove = hourlyChange > 0.01;
                        }
                        
                        // Flag big trade if both conditions are met
                        result.BigTradeDetected = hasHighVolume && hasSignificantPriceMove;
                        
                        if (result.BigTradeDetected)
                        {
                            _logger.LogInformation("Big trade activity detected on {Market}", result.MarketTitle);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("No markets found in event '{Slug}'", slug);
                    }
                }
            }

            // Validate we got real data
            if (result.YesPercentage == 0 && result.NoPercentage == 0)
            {
                _logger.LogWarning("Could not parse market prices from Polymarket response");
            }

            _cache.Set(CacheKey, result, TimeSpan.FromMinutes(cacheMinutes));
            _logger.LogInformation("Polymarket data fetched: Yes={Yes}%, No={No}%, cached for {Minutes}min", 
                result.YesPercentage, result.NoPercentage, cacheMinutes);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch data from Polymarket/Gamma API");
            
            // Return a degraded response instead of throwing
            return new PolymarketDataDto
            {
                MarketTitle = "US Invades Iran (Service Unavailable)",
                Slug = slug,
                YesPercentage = 0,
                NoPercentage = 0,
                CachedAt = DateTime.UtcNow,
                IsFromCache = false
            };
        }
    }
}

