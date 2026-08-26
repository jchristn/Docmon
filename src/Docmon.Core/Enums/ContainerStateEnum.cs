namespace Docmon.Core.Enums
{
    /// <summary>
    /// The lifecycle state of a container, normalized from the Docker Engine state string.
    /// </summary>
    public enum ContainerStateEnum
    {
        /// <summary>
        /// The state could not be mapped to a known value.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The container has been created but not started.
        /// </summary>
        Created = 1,

        /// <summary>
        /// The container is running.
        /// </summary>
        Running = 2,

        /// <summary>
        /// The container is paused.
        /// </summary>
        Paused = 3,

        /// <summary>
        /// The container is restarting.
        /// </summary>
        Restarting = 4,

        /// <summary>
        /// The container has exited.
        /// </summary>
        Exited = 5,

        /// <summary>
        /// The container is dead and cannot be restarted without removal.
        /// </summary>
        Dead = 6,

        /// <summary>
        /// The container is being removed.
        /// </summary>
        Removing = 7
    }
}
