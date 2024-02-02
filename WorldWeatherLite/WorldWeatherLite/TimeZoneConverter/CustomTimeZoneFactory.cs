using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace MediaPortal.Plugins.WorldWeatherLite.TimeZoneConverter
{
    internal static class CustomTimeZoneFactory
    {
        private const string TrollTimeZoneId = "Antarctica/Troll";
        private static readonly Lazy<TimeZoneInfo> TrollTimeZone = new Lazy<TimeZoneInfo>(CreateTrollTimeZone);

        public static bool TryGetTimeZoneInfo(string timeZoneId, out TimeZoneInfo timeZoneInfo)
        {
            if (timeZoneId.Equals(TrollTimeZoneId, StringComparison.OrdinalIgnoreCase))
            {
                timeZoneInfo = TrollTimeZone.Value;
                return true;
            }

            timeZoneInfo = null;
            return false;
        }

        private static TimeZoneInfo CreateTrollTimeZone()
        {
            return TimeZoneInfo.CreateCustomTimeZone(
                id: TrollTimeZoneId,
                baseUtcOffset: TimeSpan.Zero,
                displayName: "(UTC+00:00) Troll Station, Antarctica",
                standardDisplayName: "Greenwich Mean Time",
                daylightDisplayName: "Central European Summer Time",
                adjustmentRules: new[]
            {
                // Like IANA, we will approximate with only UTC and CEST (UTC+2).
                // Handling the CET (UTC+1) period would require generating adjustment rules for each individual year.
                TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
                    DateTime.MinValue.Date,
                    DateTime.MaxValue.Date,
                    TimeSpan.FromHours(2), // Two hours DST gap
                    TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
                        new DateTime(1, 1, 1, 1, 0, 0), // 01:00 local, 01:00 UTC
                        3, // March
                        5, // the last week of the month
                        DayOfWeek.Sunday),
                    TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
                        new DateTime(1, 1, 1, 3, 0, 0), // 03:00 local, 01:00 UTC
                        10, // October
                        5, // the last week of the month
                        DayOfWeek.Sunday)
                )
            }
            );
        }
    }
}
