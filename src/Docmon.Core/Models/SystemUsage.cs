namespace Docmon.Core.Models
{
    /// <summary>
    /// A roll-up of local Docker resource consumption shown on the Tools screen.
    /// </summary>
    public class SystemUsage
    {
        /// <summary>
        /// Gets or sets the total number of containers (running and stopped).
        /// </summary>
        public int ContainerCount { get; set; }

        /// <summary>
        /// Gets or sets the number of running containers.
        /// </summary>
        public int RunningCount { get; set; }

        /// <summary>
        /// Gets or sets the total number of local images.
        /// </summary>
        public int ImageCount { get; set; }

        /// <summary>
        /// Gets or sets the combined on-disk size of local images, in bytes.
        /// </summary>
        public long ImageSizeBytes { get; set; }

        /// <summary>
        /// Gets or sets the number of dangling images.
        /// </summary>
        public int DanglingImageCount { get; set; }

        /// <summary>
        /// Gets or sets the number of local volumes.
        /// </summary>
        public int VolumeCount { get; set; }

        /// <summary>
        /// Gets or sets the Docker Engine version string.
        /// </summary>
        public string EngineVersion { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the daemon's operating system, for example <c>linux</c>.
        /// </summary>
        public string OperatingSystem { get; set; } = string.Empty;
    }
}
