namespace Docmon.Core.Models
{
    using Docmon.Core.Enums;

    /// <summary>
    /// A service within a compose project, correlating the running container (if any) with the image
    /// declared for that service.
    /// </summary>
    public class ComposeServiceInfo
    {
        /// <summary>
        /// Gets or sets the compose service name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the image the running container reports, or an empty string when not running.
        /// </summary>
        public string RunningImage { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the container ID backing this service, or an empty string when not running.
        /// </summary>
        public string ContainerId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the normalized state of the backing container.
        /// </summary>
        public ContainerStateEnum State { get; set; } = ContainerStateEnum.Unknown;

        /// <summary>
        /// Gets or sets the update status for the service's image.
        /// </summary>
        public UpdateStatusEnum Update { get; set; } = UpdateStatusEnum.Unknown;
    }
}
