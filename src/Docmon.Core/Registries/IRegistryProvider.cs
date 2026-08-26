namespace Docmon.Core.Registries
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Docmon.Core.Models;

    /// <summary>
    /// Abstraction over a container image registry for resolving the remote digest and tags of an image
    /// so update availability can be determined. Implementations are selected per image by
    /// <see cref="RegistryProviderFactory"/>. Thread safety: implementations must be safe for concurrent
    /// use.
    /// </summary>
    public interface IRegistryProvider
    {
        /// <summary>
        /// Gets the registry host this provider handles, for example <c>docker.io</c>.
        /// </summary>
        string RegistryHost { get; }

        /// <summary>
        /// Resolves the remote manifest digest for a repository and tag.
        /// </summary>
        /// <param name="reference">The parsed image reference. Must not be null.</param>
        /// <param name="token">A token to observe for cancellation.</param>
        /// <returns>The remote digest (for example <c>sha256:...</c>), or null when the tag is absent.</returns>
        /// <exception cref="RegistryException">Thrown on a registry transport, authentication, or protocol failure.</exception>
        Task<string?> ResolveDigestAsync(ImageReference reference, CancellationToken token);

        /// <summary>
        /// Lists available tags for a repository, newest first where the registry supports ordering.
        /// </summary>
        /// <param name="reference">The parsed image reference. Must not be null.</param>
        /// <param name="token">A token to observe for cancellation.</param>
        /// <returns>The available tags. Never null.</returns>
        /// <exception cref="RegistryException">Thrown on a registry transport, authentication, or protocol failure.</exception>
        Task<IReadOnlyList<string>> ListTagsAsync(ImageReference reference, CancellationToken token);
    }
}
