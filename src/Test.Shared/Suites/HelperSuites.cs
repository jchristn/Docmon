namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using Docmon.Core.Helpers;
    using Docmon.Core.Models;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Suites for the pure helpers in <c>Docmon.Core.Helpers</c>: byte formatting, CPU math, image
    /// reference parsing, and time formatting.
    /// </summary>
    public static class HelperSuites
    {
        /// <summary>
        /// Byte and rate formatting.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor ByteFormatterSuite()
        {
            Cases c = new Cases("ByteFormatter");
            return new TestSuiteDescriptor("ByteFormatter", "ByteFormatter", new List<TestCaseDescriptor>
            {
                c.Sync("Zero", "Zero bytes formats as 0 B", () => Check.Equal("0 B", ByteFormatter.Format(0), "Format(0)")),
                c.Sync("Negative", "Negative byte counts clamp to 0 B", () => Check.Equal("0 B", ByteFormatter.Format(-512), "Format(-512)")),
                c.Sync("Bytes", "Sub-kilobyte values have no decimal", () =>
                {
                    Check.Equal("1 B", ByteFormatter.Format(1), "Format(1)");
                    Check.Equal("1023 B", ByteFormatter.Format(1023), "Format(1023)");
                }),
                c.Sync("Kilobytes", "Kilobyte boundary and fraction", () =>
                {
                    Check.Equal("1.0 KB", ByteFormatter.Format(1024), "Format(1024)");
                    Check.Equal("1.5 KB", ByteFormatter.Format(1536), "Format(1536)");
                }),
                c.Sync("LargerUnits", "MB, GB, TB, and PB units", () =>
                {
                    Check.Equal("1.0 MB", ByteFormatter.Format(1024L * 1024L), "MB");
                    Check.Equal("340.0 MB", ByteFormatter.Format(340L * 1024L * 1024L), "340 MB");
                    Check.Equal("2.0 GB", ByteFormatter.Format(2L * 1024L * 1024L * 1024L), "GB");
                    Check.Equal("1.0 TB", ByteFormatter.Format(1L << 40), "TB");
                    Check.Equal("1.0 PB", ByteFormatter.Format(1L << 50), "PB");
                }),
                c.Sync("MaxValueCapsAtPetabytes", "long.MaxValue stays in PB (largest unit)", () =>
                    Check.Equal("8192.0 PB", ByteFormatter.Format(long.MaxValue), "Format(long.MaxValue)")),
                c.Sync("InvariantCulture", "Formatting ignores the current culture's decimal separator", () =>
                {
                    CultureInfo original = CultureInfo.CurrentCulture;
                    try
                    {
                        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                        Check.Equal("1.5 KB", ByteFormatter.Format(1536), "Format under de-DE");
                    }
                    finally
                    {
                        CultureInfo.CurrentCulture = original;
                    }
                }),
                c.Sync("RateZeroAndNegative", "Zero and negative rates format as 0 B/s", () =>
                {
                    Check.Equal("0 B/s", ByteFormatter.FormatRate(0.0), "FormatRate(0)");
                    Check.Equal("0 B/s", ByteFormatter.FormatRate(-10.0), "FormatRate(-10)");
                    Check.Equal("0 B/s", ByteFormatter.FormatRate(0.4), "FormatRate(0.4) rounds to zero");
                }),
                c.Sync("RatePositive", "Positive rates round and use units", () =>
                {
                    Check.Equal("512 B/s", ByteFormatter.FormatRate(512.0), "FormatRate(512)");
                    Check.Equal("2.0 KB/s", ByteFormatter.FormatRate(2048.4), "FormatRate(2048.4)");
                    Check.Equal("2.1 MB/s", ByteFormatter.FormatRate(2.1 * 1024 * 1024), "FormatRate(2.1 MB)");
                })
            });
        }

        /// <summary>
        /// CPU percentage math.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor CpuCalculatorSuite()
        {
            Cases c = new Cases("CpuCalculator");
            return new TestSuiteDescriptor("CpuCalculator", "CpuCalculator", new List<TestCaseDescriptor>
            {
                c.Sync("StandardFormula", "Container delta / system delta * cpus * 100", () =>
                    Check.Close(200.0, CpuCalculator.Compute(2000, 1000, 2000, 1000, 2), 0.001, "two cpus fully used")),
                c.Sync("SingleCpu", "Half of one CPU is 50%", () =>
                    Check.Close(50.0, CpuCalculator.Compute(1500, 1000, 2000, 1000, 1), 0.001, "single cpu")),
                c.Sync("FourCpus", "Full use of four CPUs is 400%", () =>
                    Check.Close(400.0, CpuCalculator.Compute(2000, 1000, 2000, 1000, 4), 0.001, "four cpus")),
                c.Sync("ZeroCpusTreatedAsOne", "An online CPU count of zero is treated as one", () =>
                    Check.Close(50.0, CpuCalculator.Compute(1500, 1000, 2000, 1000, 0), 0.001, "zero cpus")),
                c.Sync("NoContainerDelta", "No container delta yields zero", () =>
                    Check.Close(0.0, CpuCalculator.Compute(1000, 1000, 2000, 1000, 2), 0.001, "no container delta")),
                c.Sync("NoSystemDelta", "No system delta yields zero (no divide by zero)", () =>
                    Check.Close(0.0, CpuCalculator.Compute(2000, 1000, 1000, 1000, 2), 0.001, "no system delta")),
                c.Sync("CounterReset", "Counters going backwards (container restart) yield zero, never negative", () =>
                {
                    Check.Close(0.0, CpuCalculator.Compute(500, 1000, 2000, 1000, 2), 0.001, "container counter reset");
                    Check.Close(0.0, CpuCalculator.Compute(2000, 1000, 500, 1000, 2), 0.001, "system counter reset");
                }),
                c.Sync("LargeCounters", "Large (long-uptime) nanosecond counters compute correctly", () =>
                    Check.Close(50.0, CpuCalculator.Compute(1000000001000000000UL, 1000000000000000000UL, 1000000002000000000UL, 1000000000000000000UL, 1), 0.001, "large counters"))
            });
        }

        /// <summary>
        /// Image reference parsing, including Docker's default-registry, namespace, and tag conventions.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor ImageReferenceParserSuite()
        {
            Cases c = new Cases("ImageReferenceParser");
            return new TestSuiteDescriptor("ImageReferenceParser", "ImageReferenceParser", new List<TestCaseDescriptor>
            {
                c.Sync("OfficialHubImage", "Official Docker Hub image gets docker.io and library/", () =>
                    Expect(ImageReferenceParser.TryParse("nginx:1.27"), "docker.io", "library/nginx", "1.27")),
                c.Sync("DefaultTag", "Missing tag defaults to latest", () =>
                    Expect(ImageReferenceParser.TryParse("redis"), "docker.io", "library/redis", "latest")),
                c.Sync("EmptyTag", "Trailing colon with empty tag defaults to latest", () =>
                    Expect(ImageReferenceParser.TryParse("redis:"), "docker.io", "library/redis", "latest")),
                c.Sync("UserNamespace", "Namespaced Hub image keeps its namespace", () =>
                    Expect(ImageReferenceParser.TryParse("acme/api:2"), "docker.io", "acme/api", "2")),
                c.Sync("ExplicitHubHost", "Explicit docker.io/library prefix is preserved", () =>
                    Expect(ImageReferenceParser.TryParse("docker.io/library/redis"), "docker.io", "library/redis", "latest")),
                c.Sync("HubHostCaseInsensitive", "Mixed-case Docker.IO host still gets the library namespace", () =>
                {
                    ImageReference? parsed = ImageReferenceParser.TryParse("Docker.IO/nginx");
                    Expect(parsed, "Docker.IO", "library/nginx", "latest");
                    Check.True(parsed!.IsDockerHub, "IsDockerHub should be case-insensitive");
                }),
                c.Sync("ThirdPartyRegistry", "GHCR reference splits host, repo, and tag", () =>
                    Expect(ImageReferenceParser.TryParse("ghcr.io/acme/api:1.4.2"), "ghcr.io", "acme/api", "1.4.2")),
                c.Sync("ThirdPartyNoLibrary", "Non-Hub single-segment repos do not get library/", () =>
                    Expect(ImageReferenceParser.TryParse("quay.io/app"), "quay.io", "app", "latest")),
                c.Sync("RegistryWithPort", "Host with port is not mistaken for a tag", () =>
                {
                    Expect(ImageReferenceParser.TryParse("localhost:5000/team/app:v1"), "localhost:5000", "team/app", "v1");
                    Expect(ImageReferenceParser.TryParse("registry.local:5000/app"), "registry.local:5000", "app", "latest");
                }),
                c.Sync("Localhost", "Bare localhost is recognized as a registry host", () =>
                    Expect(ImageReferenceParser.TryParse("localhost/foo:1"), "localhost", "foo", "1")),
                c.Sync("DigestStripped", "Digest suffixes are stripped; tag preserved when present", () =>
                {
                    Expect(ImageReferenceParser.TryParse("nginx@sha256:abcdef"), "docker.io", "library/nginx", "latest");
                    Expect(ImageReferenceParser.TryParse("nginx:1.27@sha256:abcdef"), "docker.io", "library/nginx", "1.27");
                }),
                c.Sync("Whitespace", "Surrounding whitespace is trimmed", () =>
                    Expect(ImageReferenceParser.TryParse("  nginx:1.0  "), "docker.io", "library/nginx", "1.0")),
                c.Sync("ToStringCanonical", "ToString produces host/repository:tag", () =>
                    Check.Equal("docker.io/library/nginx:1.27", ImageReferenceParser.TryParse("nginx:1.27")!.ToString(), "canonical form")),
                c.Sync("DanglingPlaceholder", "Dangling <none> placeholders return null", () =>
                {
                    Check.Null(ImageReferenceParser.TryParse("<none>"), "<none>");
                    Check.Null(ImageReferenceParser.TryParse("<none>:<none>"), "<none>:<none>");
                }),
                c.Sync("BareDigest", "A bare sha256 digest returns null", () =>
                {
                    Check.Null(ImageReferenceParser.TryParse("sha256:0123456789abcdef"), "sha256:");
                    Check.Null(ImageReferenceParser.TryParse("SHA256:0123456789abcdef"), "SHA256: upper case");
                }),
                c.Sync("OnlyDigest", "Input that is only an @digest returns null", () =>
                    Check.Null(ImageReferenceParser.TryParse("@sha256:abc"), "@sha256:abc")),
                c.Sync("OnlyTag", "Input with a tag but no repository returns null", () =>
                    Check.Null(ImageReferenceParser.TryParse(":1.0"), ":1.0")),
                c.Sync("NullThrows", "Null input throws ArgumentNullException", () =>
                    Check.Throws<ArgumentNullException>(() => ImageReferenceParser.TryParse(null!), "TryParse(null)")),
                c.Sync("EmptyThrows", "Empty and whitespace input throw ArgumentNullException", () =>
                {
                    Check.Throws<ArgumentNullException>(() => ImageReferenceParser.TryParse(string.Empty), "TryParse(\"\")");
                    Check.Throws<ArgumentNullException>(() => ImageReferenceParser.TryParse("   "), "TryParse(whitespace)");
                })
            });
        }

        /// <summary>
        /// Uptime and clock formatting.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor TimeFormatterSuite()
        {
            Cases c = new Cases("TimeFormatter");
            return new TestSuiteDescriptor("TimeFormatter", "TimeFormatter", new List<TestCaseDescriptor>
            {
                c.Sync("Days", "Durations of a day or more show days and hours", () =>
                {
                    Check.Equal("3d 4h", TimeFormatter.FormatUptime(TimeSpan.FromHours(76)), "76h");
                    Check.Equal("1d 0h", TimeFormatter.FormatUptime(TimeSpan.FromDays(1)), "exactly one day");
                    Check.Equal("400d 0h", TimeFormatter.FormatUptime(TimeSpan.FromDays(400)), "over a year");
                }),
                c.Sync("Hours", "Durations under a day show hours and minutes", () =>
                    Check.Equal("2h 5m", TimeFormatter.FormatUptime(new TimeSpan(2, 5, 30)), "2h5m30s")),
                c.Sync("Minutes", "Durations under an hour show minutes and seconds", () =>
                    Check.Equal("1m 30s", TimeFormatter.FormatUptime(TimeSpan.FromSeconds(90)), "90s")),
                c.Sync("Seconds", "Durations under a minute show seconds", () =>
                    Check.Equal("45s", TimeFormatter.FormatUptime(TimeSpan.FromSeconds(45)), "45s")),
                c.Sync("ZeroAndNegative", "Zero and negative durations format as 0s", () =>
                {
                    Check.Equal("0s", TimeFormatter.FormatUptime(TimeSpan.Zero), "zero");
                    Check.Equal("0s", TimeFormatter.FormatUptime(TimeSpan.FromMinutes(-5)), "negative");
                }),
                c.Sync("ClockLocal", "Local timestamps format as HH:mm:ss unchanged", () =>
                    Check.Equal("13:05:09", TimeFormatter.FormatClock(new DateTime(2026, 1, 2, 13, 5, 9, DateTimeKind.Local)), "local clock")),
                c.Sync("ClockUnspecified", "Unspecified-kind timestamps are not shifted", () =>
                    Check.Equal("07:00:00", TimeFormatter.FormatClock(new DateTime(2026, 1, 2, 7, 0, 0, DateTimeKind.Unspecified)), "unspecified clock")),
                c.Sync("ClockUtcConverted", "UTC timestamps are converted to local time", () =>
                {
                    DateTime utc = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
                    string expected = utc.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
                    Check.Equal(expected, TimeFormatter.FormatClock(utc), "utc clock");
                })
            });
        }

        private static void Expect(ImageReference? parsed, string host, string repository, string tag)
        {
            Check.NotNull(parsed, "parsed reference");
            Check.Equal(host, parsed!.RegistryHost, "registry host");
            Check.Equal(repository, parsed.Repository, "repository");
            Check.Equal(tag, parsed.Tag, "tag");
        }
    }
}
