namespace Docmon.Core.Models
{
    using System;

    /// <summary>
    /// A parsed container image reference broken into its registry host, repository path, and tag.
    /// </summary>
    public class ImageReference
    {
        private string _RegistryHost = "docker.io";
        private string _Repository = string.Empty;
        private string _Tag = "latest";

        /// <summary>
        /// Gets or sets the registry host, for example <c>docker.io</c> or <c>ghcr.io</c>. Never null or empty.
        /// Defaults to <c>docker.io</c>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null or empty.</exception>
        public string RegistryHost
        {
            get
            {
                return _RegistryHost;
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(RegistryHost));
                _RegistryHost = value;
            }
        }

        /// <summary>
        /// Gets or sets the repository path, for example <c>library/nginx</c> or <c>acme/api</c>. Never
        /// null or empty.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null or empty.</exception>
        public string Repository
        {
            get
            {
                return _Repository;
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Repository));
                _Repository = value;
            }
        }

        /// <summary>
        /// Gets or sets the tag, for example <c>1.4.2</c> or <c>latest</c>. Never null or empty. Defaults
        /// to <c>latest</c>.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when set to null or empty.</exception>
        public string Tag
        {
            get
            {
                return _Tag;
            }
            set
            {
                if (string.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Tag));
                _Tag = value;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this reference targets Docker Hub (<c>docker.io</c>).
        /// </summary>
        public bool IsDockerHub
        {
            get { return string.Equals(_RegistryHost, "docker.io", StringComparison.OrdinalIgnoreCase); }
        }

        /// <summary>
        /// Returns the canonical <c>host/repository:tag</c> string.
        /// </summary>
        /// <returns>The canonical reference.</returns>
        public override string ToString()
        {
            return _RegistryHost + "/" + _Repository + ":" + _Tag;
        }
    }
}
