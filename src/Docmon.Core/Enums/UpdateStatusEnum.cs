namespace Docmon.Core.Enums
{
    /// <summary>
    /// The result of comparing a locally present image against its registry counterpart.
    /// </summary>
    public enum UpdateStatusEnum
    {
        /// <summary>
        /// The update state has not been determined yet.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// A registry check is currently in progress.
        /// </summary>
        Checking = 1,

        /// <summary>
        /// The local image matches the registry; nothing to pull.
        /// </summary>
        Current = 2,

        /// <summary>
        /// The registry has a newer image than the one present locally.
        /// </summary>
        UpdateAvailable = 3,

        /// <summary>
        /// The check failed (network, authentication, or an unsupported registry).
        /// </summary>
        Error = 4
    }
}
