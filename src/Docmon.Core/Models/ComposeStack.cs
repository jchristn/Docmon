namespace Docmon.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// A compose project (stack): the set of services that share a
    /// <c>com.docker.compose.project</c> label, and the config file they came from when known.
    /// </summary>
    public class ComposeStack
    {
        /// <summary>
        /// Gets or sets the compose project name.
        /// </summary>
        public string Project { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the path to the compose config file, or an empty string when unknown.
        /// </summary>
        public string ConfigFilePath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the services in this stack.
        /// </summary>
        public IReadOnlyList<ComposeServiceInfo> Services { get; set; } = new List<ComposeServiceInfo>();

        /// <summary>
        /// Gets the number of services whose backing container is currently running.
        /// </summary>
        public int RunningCount
        {
            get
            {
                int count = 0;
                foreach (ComposeServiceInfo service in Services)
                {
                    if (service.State == Enums.ContainerStateEnum.Running)
                        count++;
                }

                return count;
            }
        }

        /// <summary>
        /// Gets the total number of services in the stack.
        /// </summary>
        public int ServiceCount
        {
            get { return Services.Count; }
        }
    }
}
