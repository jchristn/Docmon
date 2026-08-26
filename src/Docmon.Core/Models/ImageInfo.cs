namespace Docmon.Core.Models
{
    using System;
    using System.Collections.Generic;
    using Docmon.Core.Enums;

    /// <summary>
    /// A summary view of a locally present image and, once checked, its update status against the
    /// registry.
    /// </summary>
    public class ImageInfo
    {
        /// <summary>
        /// Gets or sets the image content ID (digest).
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the repository portion of the primary tag, for example <c>ghcr.io/acme/api</c>.
        /// </summary>
        public string Repository { get; set; } = "&lt;none&gt;";

        /// <summary>
        /// Gets or sets the tag portion of the primary tag, for example <c>1.4.2</c>.
        /// </summary>
        public string Tag { get; set; } = "&lt;none&gt;";

        /// <summary>
        /// Gets or sets the local repo-digests reported by Docker (used to compare against the registry).
        /// </summary>
        public IReadOnlyList<string> RepoDigests { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the on-disk size of the image, in bytes.
        /// </summary>
        public long SizeBytes { get; set; }

        /// <summary>
        /// Gets or sets the UTC creation time of the image.
        /// </summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>
        /// Gets or sets the registry digest resolved for this image's repository and tag, or null when
        /// not yet checked or not resolvable.
        /// </summary>
        public string? RemoteDigest { get; set; }

        /// <summary>
        /// Gets or sets the update status determined by comparing the local and remote digests.
        /// </summary>
        public UpdateStatusEnum Status { get; set; } = UpdateStatusEnum.Unknown;

        /// <summary>
        /// Gets a value indicating whether this is a dangling image (no repository or tag).
        /// </summary>
        public bool IsDangling
        {
            get { return Repository == "&lt;none&gt;" || string.IsNullOrEmpty(Repository); }
        }
    }
}
