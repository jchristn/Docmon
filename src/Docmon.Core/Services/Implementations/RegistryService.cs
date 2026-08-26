namespace Docmon.Core.Services.Implementations
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Docmon.Core.Enums;
    using Docmon.Core.Helpers;
    using Docmon.Core.Models;
    using Docmon.Core.Registries;
    using Docmon.Core.Services.Interfaces;

    /// <summary>
    /// Default <see cref="IRegistryService"/> implementation. It parses each image reference, selects a
    /// provider through <see cref="RegistryProviderFactory"/>, resolves the remote digest, and compares
    /// it against the image's local repo-digests. Thread safety: safe for concurrent use.
    /// </summary>
    public sealed class RegistryService : IRegistryService
    {
        private readonly RegistryProviderFactory _Factory;

        /// <summary>
        /// Initializes a new instance of the <see cref="RegistryService"/> class.
        /// </summary>
        /// <param name="factory">The provider factory. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> is null.</exception>
        public RegistryService(RegistryProviderFactory factory)
        {
            _Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        /// <inheritdoc/>
        public async Task<UpdateStatusEnum> CheckAsync(ImageInfo image, CancellationToken token)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            if (image.IsDangling)
                return UpdateStatusEnum.Unknown;

            ImageReference? reference = ImageReferenceParser.TryParse(image.Repository + ":" + image.Tag);
            if (reference == null)
                return UpdateStatusEnum.Unknown;

            IRegistryProvider? provider = _Factory.Resolve(reference.RegistryHost);
            if (provider == null)
                return UpdateStatusEnum.Unknown;

            try
            {
                string? remote = await provider.ResolveDigestAsync(reference, token).ConfigureAwait(false);
                image.RemoteDigest = remote;
                if (string.IsNullOrEmpty(remote))
                    return UpdateStatusEnum.Unknown;

                if (image.RepoDigests.Count == 0)
                    return UpdateStatusEnum.Unknown;

                foreach (string repoDigest in image.RepoDigests)
                {
                    string localDigest = ExtractDigest(repoDigest);
                    if (string.Equals(localDigest, remote, StringComparison.OrdinalIgnoreCase))
                        return UpdateStatusEnum.Current;
                }

                return UpdateStatusEnum.UpdateAvailable;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (RegistryException)
            {
                return UpdateStatusEnum.Error;
            }
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<string>> ListTagsAsync(ImageInfo image, CancellationToken token)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));

            ImageReference? reference = ImageReferenceParser.TryParse(image.Repository + ":" + image.Tag);
            if (reference == null)
                return new List<string>();

            IRegistryProvider? provider = _Factory.Resolve(reference.RegistryHost);
            if (provider == null)
                return new List<string>();

            return await provider.ListTagsAsync(reference, token).ConfigureAwait(false);
        }

        private static string ExtractDigest(string repoDigest)
        {
            int atIndex = repoDigest.IndexOf('@');
            return atIndex >= 0 ? repoDigest.Substring(atIndex + 1) : repoDigest;
        }
    }
}
