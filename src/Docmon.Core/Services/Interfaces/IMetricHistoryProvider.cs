namespace Docmon.Core.Services.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Docmon.Core.Models;

    /// <summary>Historical metrics boundary shared by local storage and authenticated remote clients.</summary>
    public interface IMetricHistoryProvider
    {
        /// <summary>Reads chronological buckets, including gaps. Empty container ID selects all Docker workloads.</summary>
        /// <param name="timeframe">hour, day, week, or month (rolling thirty days).</param>
        /// <param name="containerId">Container ID, or empty for host Docker workloads.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The complete fixed-size window.</returns>
        Task<IReadOnlyList<MetricHistoryBucket>> GetMetricHistoryAsync(string timeframe, string containerId, CancellationToken token);
    }
}
