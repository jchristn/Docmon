namespace Docmon.Test
{
    using System;
    using System.Collections.Generic;
    using Docmon.Core.Helpers;
    using Docmon.Core.Models;

    /// <summary>
    /// Entry point for the Docmon automated tests. Exercises the pure helpers in Docmon.Core: byte
    /// formatting, CPU math, image-reference parsing, and uptime formatting.
    /// </summary>
    internal static class Program
    {
        private static int Main()
        {
            TestRunner runner = new TestRunner();

            Console.WriteLine("ByteFormatter");
            runner.Equal("zero", "0 B", ByteFormatter.Format(0));
            runner.Equal("kilobytes", "1.0 KB", ByteFormatter.Format(1024));
            runner.Equal("one and a half KB", "1.5 KB", ByteFormatter.Format(1536));
            runner.Equal("megabytes", "1.0 MB", ByteFormatter.Format(1024L * 1024L));

            Console.WriteLine("CpuCalculator");
            runner.Equal("standard formula", 200.0, CpuCalculator.Compute(2000, 1000, 2000, 1000, 2), 0.001);
            runner.Equal("no delta yields zero", 0.0, CpuCalculator.Compute(1000, 1000, 2000, 1000, 2), 0.001);
            runner.Equal("single cpu", 50.0, CpuCalculator.Compute(1500, 1000, 2000, 1000, 1), 0.001);

            Console.WriteLine("ImageReferenceParser");
            ImageReference? hub = ImageReferenceParser.TryParse("nginx:1.27");
            runner.Check("hub parsed", hub != null);
            if (hub != null)
            {
                runner.Equal("hub host", "docker.io", hub.RegistryHost);
                runner.Equal("hub repo", "library/nginx", hub.Repository);
                runner.Equal("hub tag", "1.27", hub.Tag);
            }

            ImageReference? ghcr = ImageReferenceParser.TryParse("ghcr.io/acme/api:1.4.2");
            runner.Check("ghcr parsed", ghcr != null);
            if (ghcr != null)
            {
                runner.Equal("ghcr host", "ghcr.io", ghcr.RegistryHost);
                runner.Equal("ghcr repo", "acme/api", ghcr.Repository);
                runner.Equal("ghcr tag", "1.4.2", ghcr.Tag);
            }

            ImageReference? defaultTag = ImageReferenceParser.TryParse("redis");
            runner.Check("default tag is latest", defaultTag != null && defaultTag.Tag == "latest");
            runner.Check("dangling placeholder is null", ImageReferenceParser.TryParse("<none>") == null);

            Console.WriteLine("TimeFormatter");
            runner.Equal("days", "3d 4h", TimeFormatter.FormatUptime(TimeSpan.FromHours(76)));
            runner.Equal("seconds", "45s", TimeFormatter.FormatUptime(TimeSpan.FromSeconds(45)));

            return runner.Complete();
        }
    }
}
