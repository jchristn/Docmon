namespace Docmon.Core.Services.Interfaces
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Docmon.Core.Enums;
    using Docmon.Core.Models;

    /// <summary>
    /// Determines whether locally present images have newer versions available on their registry, by
    /// comparing local and remote digests through the registered registry providers.
    /// </summary>
    public interface IRegistryService
    {
        /// <summary>
        /// Checks a single image against its registry and returns the resulting update status. The
        /// image's <see cref="ImageInfo.RemoteDigest"/> is populated as a side effect when resolvable.
        /// </summary>
        /// <param name="image">The image to check. Must not be null.</param>
        /// <param name="token">A token to observe for cancellation.</param>
        /// <returns>The update status.</returns>
        Task<UpdateStatusEnum> CheckAsync(ImageInfo image, CancellationToken token);

        /// <summary>
        /// Lists the tags available on the registry for an image's repository.
        /// </summary>
        /// <param name="image">The image whose repository should be queried. Must not be null.</param>
        /// <param name="token">A token to observe for cancellation.</param>
        /// <returns>The available tags, newest first where supported. Never null.</returns>
        Task<IReadOnlyList<string>> ListTagsAsync(ImageInfo image, CancellationToken token);
    }
}
