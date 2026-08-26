namespace Docmon.Core.Models
{
    using System;

    /// <summary>
    /// A single Docker daemon event, flattened to the fields Docmon displays in its event stream.
    /// </summary>
    public class DockerEventInfo
    {
        /// <summary>
        /// Gets or sets the object type the event concerns, for example <c>container</c>, <c>image</c>,
        /// or <c>volume</c>.
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the action, for example <c>start</c>, <c>die</c>, or <c>pull</c>.
        /// </summary>
        public string Action { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the actor's short identifier or name (a container name or image reference).
        /// </summary>
        public string Actor { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the UTC time the event occurred.
        /// </summary>
        public DateTime TimeUtc { get; set; }
    }
}
