namespace Barometer.Models;

public class PolymarketDataDto
{
    public string MarketTitle { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public double YesPercentage { get; set; }
    public double NoPercentage { get; set; }
    public bool BigTradeDetected { get; set; }
    public List<TradeDto> RecentTrades { get; set; } = new();
    public DateTime CachedAt { get; set; }
    public bool IsFromCache { get; set; }
    public double Volume { get; set; }
}

public class TradeDto
{
    public double Amount { get; set; }
    public string Side { get; set; } = string.Empty; // "Yes" or "No"
    public DateTime Timestamp { get; set; }
    public bool IsBigTrade { get; set; }
}

// Polymarket/Gamma API Response Models
public class GammaMarketResponse
{
    public string? Title { get; set; }
    public string? Slug { get; set; }
    public List<GammaOutcome>? Outcomes { get; set; }
    public double Volume { get; set; }
    public List<GammaTrade>? RecentTrades { get; set; }
}

public class GammaOutcome
{
    public string? Outcome { get; set; }
    public double Price { get; set; }
}

public class GammaTrade
{
    public double Amount { get; set; }
    public string? Side { get; set; }
    public long Timestamp { get; set; }
}
