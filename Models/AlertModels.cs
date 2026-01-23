namespace Barometer.Models;

public class AlertResponseDto
{
    public bool IsMajorAlert { get; set; }
    public bool HasAnyAlert { get; set; }
    public List<string> ActiveCities { get; set; } = new();
    public List<string> ActiveCitiesEnglish { get; set; } = new();
    public List<string> MajorCitiesInAlert { get; set; } = new();
    public DateTime CachedAt { get; set; }
    public bool IsFromCache { get; set; }
    public string Status { get; set; } = "Safe"; // "Safe", "Alert", "MajorAlert"
}

// Pikud Ha'oref API Response Models
public class OrefAlertResponse
{
    public string? Id { get; set; }
    public string? Cat { get; set; }
    public string? Title { get; set; }
    public string? Data { get; set; }
    public string? Desc { get; set; }
}

public static class MajorCities
{
    public static readonly Dictionary<string, string> HebrewToEnglish = new()
    {
        { "תל אביב", "Tel Aviv" },
        { "ירושלים", "Jerusalem" },
        { "חיפה", "Haifa" },
        { "תל אביב - מרכז העיר", "Tel Aviv - City Center" },
        { "תל אביב - יפו", "Tel Aviv - Jaffa" },
        { "תל אביב - דרום", "Tel Aviv - South" },
        { "תל אביב - צפון", "Tel Aviv - North" },
        { "ירושלים - מרכז", "Jerusalem - Center" },
        { "ירושלים - דרום", "Jerusalem - South" },
        { "ירושלים - מזרח", "Jerusalem - East" },
        { "חיפה - מרכז הכרמל", "Haifa - Carmel Center" },
        { "חיפה - קריות", "Haifa - Krayot" }
    };

    public static readonly string[] MajorCityPatterns = new[]
    {
        "תל אביב",
        "ירושלים",
        "חיפה"
    };

    public static string TranslateToEnglish(string hebrewCity)
    {
        // Try exact match first
        if (HebrewToEnglish.TryGetValue(hebrewCity, out var english))
            return english;

        // Try partial match for major cities
        foreach (var kvp in HebrewToEnglish)
        {
            if (hebrewCity.Contains(kvp.Key))
                return kvp.Value;
        }

        // Return transliterated or original if no match
        return hebrewCity;
    }
}
