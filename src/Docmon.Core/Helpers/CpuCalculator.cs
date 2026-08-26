namespace Docmon.Core.Helpers
{
    /// <summary>
    /// Computes container CPU utilization from consecutive Docker stats samples using the same formula
    /// the Docker CLI applies: the container CPU delta divided by the system CPU delta, scaled by the
    /// number of online CPUs.
    /// </summary>
    public static class CpuCalculator
    {
        /// <summary>
        /// Computes CPU utilization as a percentage (0 through the online-CPU count times 100).
        /// </summary>
        /// <param name="containerUsage">The container's total CPU usage in the current sample.</param>
        /// <param name="previousContainerUsage">The container's total CPU usage in the prior sample.</param>
        /// <param name="systemUsage">The host's total CPU usage in the current sample.</param>
        /// <param name="previousSystemUsage">The host's total CPU usage in the prior sample.</param>
        /// <param name="onlineCpus">The number of online CPUs. Values below one are treated as one.</param>
        /// <returns>The CPU percentage, never negative.</returns>
        public static double Compute(
            ulong containerUsage,
            ulong previousContainerUsage,
            ulong systemUsage,
            ulong previousSystemUsage,
            uint onlineCpus)
        {
            double cpuDelta = (double)containerUsage - previousContainerUsage;
            double systemDelta = (double)systemUsage - previousSystemUsage;
            if (cpuDelta <= 0.0 || systemDelta <= 0.0)
                return 0.0;

            double cpus = onlineCpus < 1 ? 1.0 : onlineCpus;
            double percent = (cpuDelta / systemDelta) * cpus * 100.0;
            return percent < 0.0 ? 0.0 : percent;
        }
    }
}
