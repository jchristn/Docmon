namespace Docmon.Server
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Docmon.Core.Models;
    using Docmon.Core.Services;

    internal sealed class MonitoringService
    {
        private readonly DocmonRuntime _Runtime;
        private readonly ConcurrentQueue<DockerEventInfo> _Events = new ConcurrentQueue<DockerEventInfo>();
        private readonly ConcurrentDictionary<string, ContainerStatsSample> _Metrics = new ConcurrentDictionary<string, ContainerStatsSample>();
        private readonly ConcurrentQueue<ContainerStatsSample> _History = new ConcurrentQueue<ContainerStatsSample>();
        internal ContainerStatsSample[] History => _History.ToArray();
        internal DockerEventInfo[] Events => _Events.ToArray();
        internal ContainerStatsSample[] Metrics => _Metrics.Values.ToArray();
        internal string? Error { get; private set; }
        internal MonitoringService(DocmonRuntime runtime) { _Runtime = runtime; }
        internal async Task RunAsync(CancellationToken token)
        {
            await Task.WhenAll(EventsAsync(token), MetricsAsync(token)).ConfigureAwait(false);
        }
        private async Task EventsAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await _Runtime.Events.MonitorAsync(new InlineProgress<DockerEventInfo>(item =>
                    {
                        _Events.Enqueue(item);
                        while (_Events.Count > 1000) _Events.TryDequeue(out _);
                    }), token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
                catch (Exception ex) { Error = ex.Message; }
                await Task.Delay(5000, token).ConfigureAwait(false);
            }
        }
        private async Task MetricsAsync(CancellationToken token)
        {
            Dictionary<string, CancellationTokenSource> streams = new Dictionary<string, CancellationTokenSource>();
            List<Task> tasks = new List<Task>();
            try
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        IReadOnlyList<ContainerInfo> containers = await _Runtime.Docker.ListContainersAsync(false, token).ConfigureAwait(false);
                        HashSet<string> active = containers.Select(c => c.Id).ToHashSet();
                        foreach (string stale in streams.Keys.Where(id => !active.Contains(id)).ToArray())
                        {
                            streams[stale].Cancel(); streams[stale].Dispose(); streams.Remove(stale); _Metrics.TryRemove(stale, out _);
                        }
                        foreach (ContainerInfo container in containers)
                        {
                            if (streams.ContainsKey(container.Id)) continue;
                            CancellationTokenSource stream = CancellationTokenSource.CreateLinkedTokenSource(token);
                            streams[container.Id] = stream;
                            tasks.Add(StreamAsync(container.Id, stream.Token));
                        }
                        tasks.RemoveAll(t => t.IsCompleted);
                        Error = null;
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
                    catch (Exception ex) { Error = ex.Message; }
                    await Task.Delay(5000, token).ConfigureAwait(false);
                }
            }
            finally
            {
                foreach (CancellationTokenSource source in streams.Values) { source.Cancel(); source.Dispose(); }
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
        }
        private async Task StreamAsync(string id, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try { await _Runtime.Stats.StreamAsync(id, new InlineProgress<ContainerStatsSample>(sample => { _Metrics[id] = sample; _History.Enqueue(sample); while (_History.Count > 10000) _History.TryDequeue(out _); }), token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
                catch (Exception ex) { Error = ex.Message; }
                try { await Task.Delay(5000, token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
            }
        }
    }
}
