namespace Docmon.Core.Helpers
{
    using System;
    using Docmon.Core.Models;

    /// <summary>
    /// Parses Docker image reference strings into their registry, repository, and tag components,
    /// applying Docker's default-registry, default-namespace, and default-tag conventions.
    /// </summary>
    public static class ImageReferenceParser
    {
        /// <summary>
        /// The canonical Docker Hub registry host.
        /// </summary>
        public const string DockerHubHost = "docker.io";

        /// <summary>
        /// Parses an image reference such as <c>nginx:1.27</c>, <c>ghcr.io/acme/api:1.4.2</c>, or
        /// <c>docker.io/library/redis</c> into its components.
        /// </summary>
        /// <param name="reference">The image reference string. Must not be null or empty.</param>
        /// <returns>The parsed reference, or null when the input cannot be parsed (for example a bare
        /// digest or the <c>&lt;none&gt;</c> placeholder Docker uses for dangling images).</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="reference"/> is null or empty.</exception>
        public static ImageReference? TryParse(string reference)
        {
            if (string.IsNullOrWhiteSpace(reference)) throw new ArgumentNullException(nameof(reference));

            string working = reference.Trim();
            if (working.StartsWith("<", StringComparison.Ordinal) || working.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                return null;

            // Strip any digest suffix; Docmon compares by tag, not by pinned digest.
            int atIndex = working.IndexOf('@');
            if (atIndex >= 0)
                working = working.Substring(0, atIndex);

            if (working.Length == 0)
                return null;

            string host = DockerHubHost;
            string remainder = working;

            int firstSlash = working.IndexOf('/');
            if (firstSlash > 0)
            {
                string candidate = working.Substring(0, firstSlash);
                if (LooksLikeRegistryHost(candidate))
                {
                    host = candidate;
                    remainder = working.Substring(firstSlash + 1);
                }
            }

            string repository;
            string tag = "latest";
            int colonIndex = remainder.LastIndexOf(':');
            if (colonIndex >= 0 && remainder.IndexOf('/', colonIndex) < 0)
            {
                repository = remainder.Substring(0, colonIndex);
                string parsedTag = remainder.Substring(colonIndex + 1);
                if (parsedTag.Length > 0)
                    tag = parsedTag;
            }
            else
            {
                repository = remainder;
            }

            if (repository.Length == 0)
                return null;

            // Docker Hub official images live under the implicit "library" namespace.
            if (string.Equals(host, DockerHubHost, StringComparison.OrdinalIgnoreCase) && repository.IndexOf('/') < 0)
                repository = "library/" + repository;

            ImageReference result = new ImageReference();
            result.RegistryHost = host;
            result.Repository = repository;
            result.Tag = tag;
            return result;
        }

        private static bool LooksLikeRegistryHost(string candidate)
        {
            if (string.Equals(candidate, "localhost", StringComparison.OrdinalIgnoreCase))
                return true;

            return candidate.IndexOf('.') >= 0 || candidate.IndexOf(':') >= 0;
        }
    }
}
