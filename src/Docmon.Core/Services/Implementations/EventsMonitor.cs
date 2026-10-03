namespace Docmon.Core.Services.Implementations
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Docker.DotNet;
    using Docker.DotNet.Models;
    using Docmon.Core.Models;
    using Docmon.Core.Services.Interfaces;

    /// <summary>
    /// Default <see cref="IEventsMonitor"/> implementation. It subscribes to the Docker daemon event
    /// stream and flattens each message into a <see cref="DockerEventInfo"/>. Thread safety: safe for
    /// concurrent use.
    /// </summary>
    public sealed class EventsMonitor : IEventsMonitor
    {
        private readonly IDockerClient _Client;

        /// <summary>
        /// Initializes a new instance of the <see cref="EventsMonitor"/> class.
        /// </summary>
        /// <param name="client">The connected Docker client. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="client"/> is null.</exception>
        public EventsMonitor(IDockerClient client)
        {
            _Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        /// <inheritdoc/>
        public async Task MonitorAsync(IProgress<DockerEventInfo> progress, CancellationToken token)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));

            Progress<Message> relay = new Progress<Message>(message =>
            {
                progress.Report(Convert(message));
            });

            try
            {
                await _Client.System.MonitorEventsAsync(new ContainerEventsParameters(), relay, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown of the event stream.
            }
        }

        internal static DockerEventInfo Convert(Message message)
        {
            DockerEventInfo info = new DockerEventInfo();
            info.Type = message.Type ?? string.Empty;
            info.Action = message.Action ?? string.Empty;
            info.Actor = ExtractActor(message);
            info.TimeUtc = message.Time > 0 ? DateTimeOffset.FromUnixTimeSeconds(message.Time).UtcDateTime : DateTime.UtcNow;
            return info;
        }

        internal static string ExtractActor(Message message)
        {
            if (message.Actor?.Attributes != null)
            {
                if (message.Actor.Attributes.TryGetValue("name", out string? name) && !string.IsNullOrEmpty(name))
                    return name;
                if (message.Actor.Attributes.TryGetValue("image", out string? image) && !string.IsNullOrEmpty(image))
                    return image;
            }

            if (!string.IsNullOrEmpty(message.Actor?.ID))
            {
                string id = message.Actor!.ID;
                return id.Length > 12 ? id.Substring(0, 12) : id;
            }

            return !string.IsNullOrEmpty(message.ID) ? message.ID : string.Empty;
        }
    }
}
