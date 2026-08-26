namespace Docmon.Core.Helpers
{
    using System;
    using System.Globalization;

    /// <summary>
    /// Formats durations and timestamps for compact display in the terminal.
    /// </summary>
    public static class TimeFormatter
    {
        /// <summary>
        /// Formats an uptime duration compactly, for example <c>3d 4h</c>, <c>4h 12m</c>, or <c>45s</c>.
        /// </summary>
        /// <param name="uptime">The elapsed duration. Negative values are treated as zero.</param>
        /// <returns>A compact duration string. Never null.</returns>
        public static string FormatUptime(TimeSpan uptime)
        {
            if (uptime < TimeSpan.Zero)
                uptime = TimeSpan.Zero;

            if (uptime.TotalDays >= 1.0)
                return (int)uptime.TotalDays + "d " + uptime.Hours + "h";
            if (uptime.TotalHours >= 1.0)
                return uptime.Hours + "h " + uptime.Minutes + "m";
            if (uptime.TotalMinutes >= 1.0)
                return uptime.Minutes + "m " + uptime.Seconds + "s";

            return uptime.Seconds + "s";
        }

        /// <summary>
        /// Formats a timestamp as a local wall-clock time of day (<c>HH:mm:ss</c>).
        /// </summary>
        /// <param name="value">The timestamp. UTC values are converted to local time.</param>
        /// <returns>The formatted time. Never null.</returns>
        public static string FormatClock(DateTime value)
        {
            DateTime local = value.Kind == DateTimeKind.Utc ? value.ToLocalTime() : value;
            return local.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        }
    }
}
