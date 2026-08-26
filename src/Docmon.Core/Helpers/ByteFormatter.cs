namespace Docmon.Core.Helpers
{
    using System;
    using System.Globalization;

    /// <summary>
    /// Formats byte counts and byte-per-second rates into compact, human-readable strings.
    /// </summary>
    public static class ByteFormatter
    {
        private static readonly string[] _Units = { "B", "KB", "MB", "GB", "TB", "PB" };

        /// <summary>
        /// Formats a byte count using binary (1024-based) units, for example <c>340.0 MB</c>.
        /// </summary>
        /// <param name="bytes">The number of bytes. Negative values are treated as zero.</param>
        /// <returns>A formatted string. Never null.</returns>
        public static string Format(long bytes)
        {
            if (bytes <= 0)
                return "0 B";

            double value = bytes;
            int unit = 0;
            while (value >= 1024.0 && unit < _Units.Length - 1)
            {
                value /= 1024.0;
                unit++;
            }

            string format = unit == 0 ? "0" : "0.0";
            return value.ToString(format, CultureInfo.InvariantCulture) + " " + _Units[unit];
        }

        /// <summary>
        /// Formats a transfer rate expressed in bytes per second, for example <c>2.1 MB/s</c>.
        /// </summary>
        /// <param name="bytesPerSecond">The rate in bytes per second. Negative values are treated as zero.</param>
        /// <returns>A formatted string. Never null.</returns>
        public static string FormatRate(double bytesPerSecond)
        {
            if (bytesPerSecond <= 0.0)
                return "0 B/s";

            return Format((long)Math.Round(bytesPerSecond)) + "/s";
        }
    }
}
