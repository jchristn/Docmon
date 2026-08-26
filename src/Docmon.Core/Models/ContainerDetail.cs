namespace Docmon.Core.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Detailed, inspect-level information about a single container, flattened to the fields Docmon
    /// presents on its detail and inspect views.
    /// </summary>
    public class ContainerDetail
    {
        /// <summary>
        /// Gets or sets the full container ID.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the container name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the image reference the container was created from.
        /// </summary>
        public string Image { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the raw state string reported by Docker.
        /// </summary>
        public string State { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the health string, or an empty string when no healthcheck is configured.
        /// </summary>
        public string Health { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the UTC creation time.
        /// </summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>
        /// Gets or sets the UTC start time, or <see cref="DateTime.MinValue"/> when not started.
        /// </summary>
        public DateTime StartedUtc { get; set; }

        /// <summary>
        /// Gets or sets the restart policy name, for example <c>unless-stopped</c>.
        /// </summary>
        public string RestartPolicy { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the entrypoint and command, joined for display.
        /// </summary>
        public string Command { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the published-port mappings.
        /// </summary>
        public IReadOnlyList<PortMap> Ports { get; set; } = new List<PortMap>();

        /// <summary>
        /// Gets or sets the environment variables, each as a <c>KEY=value</c> string.
        /// </summary>
        public IReadOnlyList<string> Environment { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the mounts, each formatted as <c>source -&gt; destination</c>.
        /// </summary>
        public IReadOnlyList<string> Mounts { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the network names the container is attached to.
        /// </summary>
        public IReadOnlyList<string> Networks { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the container labels.
        /// </summary>
        public IReadOnlyDictionary<string, string> Labels { get; set; } = new Dictionary<string, string>();
    }
}
