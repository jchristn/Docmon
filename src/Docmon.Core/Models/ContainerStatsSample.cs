namespace Docmon.Core.Models
{
    using System;

    /// <summary>
    /// A single point-in-time sample of a container's resource usage, derived from a Docker stats frame.
    /// </summary>
    public class ContainerStatsSample
    {
        /// <summary>
        /// Gets or sets the container ID this sample belongs to.
        /// </summary>
        public string ContainerId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the CPU utilization as a percentage (0 through the online-CPU count times 100).
        /// </summary>
        public double CpuPercent { get; set; }

        /// <summary>
        /// Gets or sets the memory currently in use, in bytes.
        /// </summary>
        public long MemoryUsage { get; set; }

        /// <summary>
        /// Gets or sets the memory limit, in bytes.
        /// </summary>
        public long MemoryLimit { get; set; }

        /// <summary>
        /// Gets or sets the cumulative bytes received over the network.
        /// </summary>
        public long NetworkRxBytes { get; set; }

        /// <summary>
        /// Gets or sets the cumulative bytes transmitted over the network.
        /// </summary>
        public long NetworkTxBytes { get; set; }

        /// <summary>
        /// Gets or sets the cumulative bytes read from block devices.
        /// </summary>
        public long BlockReadBytes { get; set; }

        /// <summary>
        /// Gets or sets the cumulative bytes written to block devices.
        /// </summary>
        public long BlockWriteBytes { get; set; }

        /// <summary>
        /// Gets or sets the UTC time the sample was taken.
        /// </summary>
        public DateTime AtUtc { get; set; }

        /// <summary>
        /// Gets the memory utilization as a percentage of the limit, or zero when no limit is reported.
        /// </summary>
        public double MemoryPercent
        {
            get { return MemoryLimit > 0 ? (double)MemoryUsage / MemoryLimit * 100.0 : 0.0; }
        }
    }
}
