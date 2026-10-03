namespace Docmon.Core.Services.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Docker.DotNet;
    using Docker.DotNet.Models;
    using Docmon.Core.Helpers;
    using Docmon.Core.Models;
    using Docmon.Core.Services.Interfaces;

    /// <summary>
    /// Default <see cref="IStatsStreamer"/> implementation. It opens a streaming stats subscription and
    /// converts each Docker stats frame into a <see cref="ContainerStatsSample"/>, computing CPU usage
    /// from the frame's own current and prior CPU counters. Thread safety: safe for concurrent use.
    /// </summary>
    public sealed class StatsStreamer : IStatsStreamer
    {
        private readonly IDockerClient _Client;

        /// <summary>
        /// Initializes a new instance of the <see cref="StatsStreamer"/> class.
        /// </summary>
        /// <param name="client">The connected Docker client. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="client"/> is null.</exception>
        public StatsStreamer(IDockerClient client)
        {
            _Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        /// <inheritdoc/>
        public async Task StreamAsync(string containerId, IProgress<ContainerStatsSample> progress, CancellationToken token)
        {
            if (containerId == null) throw new ArgumentNullException(nameof(containerId));
            if (progress == null) throw new ArgumentNullException(nameof(progress));

            Progress<ContainerStatsResponse> relay = new Progress<ContainerStatsResponse>(response =>
            {
                ContainerStatsSample? sample = Convert(containerId, response);
                if (sample != null)
                    progress.Report(sample);
            });

            ContainerStatsParameters parameters = new ContainerStatsParameters { Stream = true };
            try
            {
                await _Client.Containers.GetContainerStatsAsync(containerId, parameters, relay, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown of the stream.
            }
            catch (DockerContainerNotFoundException)
            {
                // The container went away while streaming; end quietly.
            }
        }

        internal static ContainerStatsSample? Convert(string containerId, ContainerStatsResponse response)
        {
            if (response == null || response.CPUStats == null || response.PreCPUStats == null)
                return null;

            uint onlineCpus = response.CPUStats.OnlineCPUs;
            if (onlineCpus == 0 && response.CPUStats.CPUUsage?.PercpuUsage != null)
                onlineCpus = (uint)response.CPUStats.CPUUsage.PercpuUsage.Count;

            ulong containerUsage = response.CPUStats.CPUUsage?.TotalUsage ?? 0;
            ulong previousContainerUsage = response.PreCPUStats.CPUUsage?.TotalUsage ?? 0;

            double cpu = CpuCalculator.Compute(
                containerUsage,
                previousContainerUsage,
                response.CPUStats.SystemUsage,
                response.PreCPUStats.SystemUsage,
                onlineCpus);

            ContainerStatsSample sample = new ContainerStatsSample();
            sample.ContainerId = containerId;
            sample.CpuPercent = cpu;
            sample.MemoryUsage = (long)(response.MemoryStats?.Usage ?? 0);
            sample.MemoryLimit = (long)(response.MemoryStats?.Limit ?? 0);
            sample.AtUtc = DateTime.UtcNow;

            if (response.Networks != null)
            {
                long rx = 0;
                long tx = 0;
                foreach (KeyValuePair<string, NetworkStats> pair in response.Networks)
                {
                    rx += (long)pair.Value.RxBytes;
                    tx += (long)pair.Value.TxBytes;
                }
                sample.NetworkRxBytes = rx;
                sample.NetworkTxBytes = tx;
            }

            if (response.BlkioStats?.IoServiceBytesRecursive != null)
            {
                long read = 0;
                long write = 0;
                foreach (BlkioStatEntry entry in response.BlkioStats.IoServiceBytesRecursive)
                {
                    if (string.Equals(entry.Op, "Read", StringComparison.OrdinalIgnoreCase))
                        read += (long)entry.Value;
                    else if (string.Equals(entry.Op, "Write", StringComparison.OrdinalIgnoreCase))
                        write += (long)entry.Value;
                }
                sample.BlockReadBytes = read;
                sample.BlockWriteBytes = write;
            }

            return sample;
        }
    }
}
