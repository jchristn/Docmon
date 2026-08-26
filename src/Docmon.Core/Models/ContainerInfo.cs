namespace Docmon.Core.Models
{
    using System;
    using System.Collections.Generic;
    using Docmon.Core.Enums;

    /// <summary>
    /// A summary view of a container as listed by the Docker Engine, enriched with the display fields
    /// Docmon needs.
    /// </summary>
    public class ContainerInfo
    {
        /// <summary>
        /// Gets or sets the full container ID.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets the short (12-character) container ID.
        /// </summary>
        public string ShortId
        {
            get { return Id.Length > 12 ? Id.Substring(0, 12) : Id; }
        }

        /// <summary>
        /// Gets or sets the primary container name (without the leading slash Docker returns).
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the image reference (repository and tag) the container was created from.
        /// </summary>
        public string Image { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the image content ID (digest) the container is running.
        /// </summary>
        public string ImageId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the normalized lifecycle state.
        /// </summary>
        public ContainerStateEnum State { get; set; } = ContainerStateEnum.Unknown;

        /// <summary>
        /// Gets or sets the raw human-readable status string from Docker, for example <c>Up 3 days</c>.
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the health string when a healthcheck is configured (for example <c>healthy</c>),
        /// or an empty string when none is reported.
        /// </summary>
        public string Health { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the UTC creation time.
        /// </summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>
        /// Gets or sets the published-port mappings.
        /// </summary>
        public IReadOnlyList<PortMap> Ports { get; set; } = new List<PortMap>();

        /// <summary>
        /// Gets or sets the compose project name from the <c>com.docker.compose.project</c> label, or
        /// an empty string when the container is not compose-managed.
        /// </summary>
        public string ComposeProject { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the compose service name from the <c>com.docker.compose.service</c> label, or an
        /// empty string when not compose-managed.
        /// </summary>
        public string ComposeService { get; set; } = string.Empty;

        /// <summary>
        /// Gets a value indicating whether the container is currently running.
        /// </summary>
        public bool IsRunning
        {
            get { return State == ContainerStateEnum.Running; }
        }
    }
}
