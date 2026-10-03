namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Docmon.Core.Models;
    using Docmon.Core.Registries;

    /// <summary>
    /// A scriptable <see cref="IRegistryProvider"/> used to drive <c>RegistryService</c> through every
    /// outcome without a real registry.
    /// </summary>
    internal sealed class FakeRegistryProvider : IRegistryProvider
    {
        private readonly string _Host;
        private readonly List<ImageReference> _Resolved = new List<ImageReference>();

        internal FakeRegistryProvider(string host)
        {
            _Host = host;
        }

        public string RegistryHost
        {
            get { return _Host; }
        }

        internal string? Digest { get; set; }

        internal Exception? Failure { get; set; }

        internal List<string> Tags { get; } = new List<string>();

        internal IReadOnlyList<ImageReference> Resolved
        {
            get { return _Resolved; }
        }

        public Task<string?> ResolveDigestAsync(ImageReference reference, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            _Resolved.Add(reference);
            if (Failure != null)
                throw Failure;

            return Task.FromResult(Digest);
        }

        public Task<IReadOnlyList<string>> ListTagsAsync(ImageReference reference, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            _Resolved.Add(reference);
            if (Failure != null)
                throw Failure;

            IReadOnlyList<string> tags = new List<string>(Tags);
            return Task.FromResult(tags);
        }
    }
}
