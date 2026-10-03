namespace Docmon.Core.Services.Implementations
{
    using System;
    using System.Formats.Tar;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Docker.DotNet;
    using Docker.DotNet.Models;
    using Docmon.Core.Services.Interfaces;

    /// <summary>
    /// Default <see cref="ITransferService"/> implementation using the Docker archive API and
    /// <see cref="System.Formats.Tar"/> for packing and unpacking. Thread safety: safe for concurrent use.
    /// </summary>
    public sealed class TransferService : ITransferService
    {
        private readonly IDockerClient _Client;

        /// <summary>
        /// Initializes a new instance of the <see cref="TransferService"/> class.
        /// </summary>
        /// <param name="client">The connected Docker client. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="client"/> is null.</exception>
        public TransferService(IDockerClient client)
        {
            _Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        /// <inheritdoc/>
        public async Task CopyOutAsync(string containerId, string containerPath, string hostDirectory, CancellationToken token)
        {
            if (containerId == null) throw new ArgumentNullException(nameof(containerId));
            if (containerPath == null) throw new ArgumentNullException(nameof(containerPath));
            if (hostDirectory == null) throw new ArgumentNullException(nameof(hostDirectory));

            Directory.CreateDirectory(hostDirectory);

            GetArchiveFromContainerParameters parameters = new GetArchiveFromContainerParameters { Path = containerPath };
            GetArchiveFromContainerResponse response = await _Client.Containers.GetArchiveFromContainerAsync(containerId, parameters, false, token).ConfigureAwait(false);

            // Spool the archive to a temporary file before extracting. Reading the tar directly from the
            // Docker.DotNet chunked response stream fails with EndOfStreamException on current engines,
            // and a file (rather than memory) keeps large copies off the heap.
            string spoolPath = Path.GetTempFileName();
            using (Stream tar = response.Stream)
            using (FileStream spool = new FileStream(spoolPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.DeleteOnClose))
            {
                await tar.CopyToAsync(spool, 81920, token).ConfigureAwait(false);
                spool.Seek(0, SeekOrigin.Begin);
                await TarFile.ExtractToDirectoryAsync(spool, hostDirectory, true, token).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async Task CopyInAsync(string containerId, string hostPath, string containerDirectory, CancellationToken token)
        {
            if (containerId == null) throw new ArgumentNullException(nameof(containerId));
            if (hostPath == null) throw new ArgumentNullException(nameof(hostPath));
            if (containerDirectory == null) throw new ArgumentNullException(nameof(containerDirectory));

            using (MemoryStream archive = new MemoryStream())
            {
                await PackAsync(hostPath, archive, token).ConfigureAwait(false);
                archive.Seek(0, SeekOrigin.Begin);

                ContainerPathStatParameters parameters = new ContainerPathStatParameters { Path = containerDirectory };
                await _Client.Containers.ExtractArchiveToContainerAsync(containerId, parameters, archive, token).ConfigureAwait(false);
            }
        }

        private static async Task PackAsync(string hostPath, Stream destination, CancellationToken token)
        {
            using (TarWriter writer = new TarWriter(destination, TarEntryFormat.Pax, true))
            {
                if (File.Exists(hostPath))
                {
                    string name = Path.GetFileName(hostPath);
                    await writer.WriteEntryAsync(hostPath, name, token).ConfigureAwait(false);
                    return;
                }

                if (!Directory.Exists(hostPath))
                    throw new FileNotFoundException("The host path does not exist.", hostPath);

                string root = hostPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string rootName = Path.GetFileName(root);
                foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    token.ThrowIfCancellationRequested();
                    string relative = Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
                    string entryName = rootName + "/" + relative;
                    await writer.WriteEntryAsync(file, entryName, token).ConfigureAwait(false);
                }
            }
        }
    }
}
