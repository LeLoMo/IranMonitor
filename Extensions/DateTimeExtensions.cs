using System;

namespace Barometer.Extensions
{
    public static class DateTimeExtensions
    {
        /// <summary>
        /// Converts a UTC DateTime to US Eastern Time (America/New_York).
        /// Returns a DateTime with Kind=Unspecified (because DateTime cannot truly carry TZ info).
        /// </summary>
        public static DateTime ToEasternTime(this DateTime utc)
        {
            if (utc.Kind != DateTimeKind.Utc)
                utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);

            var et = GetEasternTimeZone();
            return TimeZoneInfo.ConvertTimeFromUtc(utc, et);
        }

        /// <summary>
        /// Converts any DateTime to US Eastern Time (treats Unspecified as local machine time).
        /// Prefer passing UTC for correctness.
        /// </summary>
        public static DateTime ToEasternTimeSafe(this DateTime dt)
        {
            var et = GetEasternTimeZone();

            if (dt.Kind == DateTimeKind.Utc)
                return TimeZoneInfo.ConvertTimeFromUtc(dt, et);

            if (dt.Kind == DateTimeKind.Local)
                return TimeZoneInfo.ConvertTime(dt, TimeZoneInfo.Local, et);

            // Unspecified: assume it's already UTC to avoid machine-local surprises
            // Change this behavior if you prefer treating Unspecified as Local.
            var assumedUtc = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(assumedUtc, et);
        }

        private static TimeZoneInfo GetEasternTimeZone()
        {
            // Windows: "Eastern Standard Time"
            // Linux/macOS: "America/New_York"
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
            }
        }
    }
}
