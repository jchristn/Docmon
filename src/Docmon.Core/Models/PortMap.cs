namespace Docmon.Core.Models
{
    /// <summary>
    /// A single published-port mapping from the host to a container.
    /// </summary>
    public class PortMap
    {
        /// <summary>
        /// Gets or sets the host IP the port is bound on, or an empty string when unspecified.
        /// </summary>
        public string HostIp { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the host port, or zero when the container port is not published to the host.
        /// </summary>
        public int HostPort { get; set; }

        /// <summary>
        /// Gets or sets the container port.
        /// </summary>
        public int ContainerPort { get; set; }

        /// <summary>
        /// Gets or sets the protocol, for example <c>tcp</c> or <c>udp</c>.
        /// </summary>
        public string Protocol { get; set; } = "tcp";

        /// <summary>
        /// Returns the mapping formatted as <c>host-&gt;container/proto</c>, or <c>container/proto</c>
        /// when the port is not published.
        /// </summary>
        /// <returns>The formatted mapping.</returns>
        public override string ToString()
        {
            if (HostPort > 0)
                return HostPort + "->" + ContainerPort + "/" + Protocol;

            return ContainerPort + "/" + Protocol;
        }
    }
}
