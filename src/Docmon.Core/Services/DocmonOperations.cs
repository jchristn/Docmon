namespace Docmon.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Docmon.Core.Models;

    /// <summary>Shared application operations for REST, MCP, and future remote TUI adapters.</summary>
    public sealed class DocmonOperations
    {
        private readonly DocmonRuntime _Runtime;
        /// <summary>Creates the application service.</summary>
        /// <param name="runtime">Shared Docker runtime.</param>
        public DocmonOperations(DocmonRuntime runtime) { _Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime)); }

        /// <summary>Executes a named operation. Does not perform transport authentication.</summary>
        /// <param name="operation">Operation from the published API catalog.</param>
        /// <param name="request">Typed operation parameters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Typed resource or completed operation result.</returns>
        public async Task<object> ExecuteAsync(string operation, OperationRequest request, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(request);
            token.ThrowIfCancellationRequested();
            Queue<string> lines = new Queue<string>();
            InlineProgress<string> progress = new InlineProgress<string>(line => { lock (lines) { if (lines.Count >= 2000) lines.Dequeue(); lines.Enqueue(line); } });
            OperationResult result = new OperationResult();
            switch (operation)
            {
                case "containers_list": return await _Runtime.Docker.ListContainersAsync(true, token).ConfigureAwait(false);
                case "containers_inspect": return await _Runtime.Docker.InspectAsync(Required(request.Id, "id"), token).ConfigureAwait(false);
                case "containers_logs":
                    if (request.Tail < 1 || request.Tail > 10000) throw new ArgumentException("tail must be between 1 and 10000.");
                    return await _Runtime.Docker.GetLogsAsync(Required(request.Id, "id"), request.Tail, token).ConfigureAwait(false);
                case "containers_start": await _Runtime.Docker.StartAsync(Required(request.Id, "id"), token).ConfigureAwait(false); break;
                case "containers_stop": await _Runtime.Docker.StopAsync(Required(request.Id, "id"), token).ConfigureAwait(false); break;
                case "containers_restart": await _Runtime.Docker.RestartAsync(Required(request.Id, "id"), token).ConfigureAwait(false); break;
                case "containers_kill": await _Runtime.Docker.KillAsync(Required(request.Id, "id"), token).ConfigureAwait(false); break;
                case "containers_pause": await _Runtime.Docker.PauseAsync(Required(request.Id, "id"), token).ConfigureAwait(false); break;
                case "containers_unpause": await _Runtime.Docker.UnpauseAsync(Required(request.Id, "id"), token).ConfigureAwait(false); break;
                case "containers_remove": await _Runtime.Docker.RemoveAsync(Required(request.Id, "id"), request.Force, token).ConfigureAwait(false); break;
                case "containers_exec":
                    if (request.Command == null || request.Command.Count == 0 || request.Command.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("command must contain an executable and valid arguments.");
                    ExecResult execution = await _Runtime.Exec.RunAsync(Required(request.Id, "id"), request.Command, progress, token).ConfigureAwait(false);
                    result.ExitCode = execution.ExitCode; break;
                case "containers_copy_in": await _Runtime.Transfer.CopyInAsync(Required(request.Id, "id"), Required(request.HostPath, "hostPath"), Required(request.Path, "path"), token).ConfigureAwait(false); break;
                case "containers_copy_out": await _Runtime.Transfer.CopyOutAsync(Required(request.Id, "id"), Required(request.Path, "path"), Required(request.HostPath, "hostPath"), token).ConfigureAwait(false); break;
                case "containers_update":
                    ContainerInfo target = (await _Runtime.Docker.ListContainersAsync(true, token).ConfigureAwait(false)).FirstOrDefault(c => c.Id == Required(request.Id, "id") || c.Name == request.Id) ?? throw new KeyNotFoundException("Container not found: " + request.Id);
                    await foreach (string line in _Runtime.Docker.PullAsync(target.Image, token).ConfigureAwait(false)) { if (line.StartsWith("error:", StringComparison.Ordinal)) throw new DockerConnectionException(line); progress.Report(line); }
                    if (!string.IsNullOrWhiteSpace(target.ComposeProject))
                    {
                        ComposeStack project = (await _Runtime.Compose.DiscoverAsync(token).ConfigureAwait(false)).First(s => s.Project == target.ComposeProject);
                        result.ExitCode = await _Runtime.Compose.UpAsync(project.Project, project.ConfigFilePath, target.ComposeService, progress, token).ConfigureAwait(false);
                    }
                    else progress.Report("Image pulled. Recreate the standalone container to apply it; restarting preserves the original image.");
                    break;
                case "images_list": return await _Runtime.Docker.ListImagesAsync(token).ConfigureAwait(false);
                case "images_pull":
                    string image = request.Image;
                    if (string.IsNullOrWhiteSpace(image)) { ImageInfo existing = await FindImageAsync(request.Id, token).ConfigureAwait(false); image = Reference(existing); }
                    await foreach (string line in _Runtime.Docker.PullAsync(image, token).ConfigureAwait(false)) { if (line.StartsWith("error:", StringComparison.Ordinal)) throw new DockerConnectionException(line); progress.Report(line); }
                    break;
                case "images_remove": await _Runtime.Docker.RemoveImageAsync(Required(request.Id, "id"), request.Force, token).ConfigureAwait(false); break;
                case "images_prune": result.ReclaimedBytes = await _Runtime.Docker.PruneImagesAsync(token).ConfigureAwait(false); break;
                case "images_check":
                    ImageInfo check = await FindImageAsync(request.Id, token).ConfigureAwait(false);
                    check.Status = await _Runtime.Registry.CheckAsync(check, token).ConfigureAwait(false); return check;
                case "images_tags": return await _Runtime.Registry.ListTagsAsync(await FindImageAsync(request.Id, token).ConfigureAwait(false), token).ConfigureAwait(false);
                case "system_usage": return await _Runtime.Docker.GetSystemUsageAsync(token).ConfigureAwait(false);
                case "stacks_list": return await _Runtime.Compose.DiscoverAsync(token).ConfigureAwait(false);
                case "stacks_up": case "stacks_down": case "stacks_restart": case "stacks_pull":
                    ComposeStack stack = (await _Runtime.Compose.DiscoverAsync(token).ConfigureAwait(false)).FirstOrDefault(s => s.Project == Required(request.Project, "project")) ?? throw new KeyNotFoundException("Compose project not found: " + request.Project);
                    if (request.Service != null && !stack.Services.Any(s => s.Name == request.Service)) throw new ArgumentException("Unknown service for this project.");
                    if (string.IsNullOrWhiteSpace(stack.ConfigFilePath)) throw new ArgumentException("Compose configuration path is unavailable. Mount the original compose files at their host paths.");
                    switch (operation)
                    {
                        case "stacks_up": result.ExitCode = await _Runtime.Compose.UpAsync(stack.Project, stack.ConfigFilePath, request.Service, progress, token).ConfigureAwait(false); break;
                        case "stacks_down": result.ExitCode = await _Runtime.Compose.DownAsync(stack.Project, stack.ConfigFilePath, progress, token).ConfigureAwait(false); break;
                        case "stacks_restart": result.ExitCode = await _Runtime.Compose.RestartAsync(stack.Project, stack.ConfigFilePath, progress, token).ConfigureAwait(false); break;
                        case "stacks_pull": result.ExitCode = await _Runtime.Compose.PullAsync(stack.Project, stack.ConfigFilePath, progress, token).ConfigureAwait(false); break;
                    }
                    break;
                default: throw new ArgumentException("Unknown operation: " + operation);
            }
            lock (lines) result.Output = lines.ToArray();
            return result;
        }

        private async Task<ImageInfo> FindImageAsync(string id, CancellationToken token)
        {
            Required(id, "id");
            return (await _Runtime.Docker.ListImagesAsync(token).ConfigureAwait(false)).FirstOrDefault(i => i.Id == id || (!i.IsDangling && Reference(i) == id)) ?? throw new KeyNotFoundException("Image not found: " + id);
        }
        private static string Reference(ImageInfo image)
        {
            if (image.IsDangling) throw new ArgumentException("An untagged image cannot be pulled. Supply an image name and tag.");
            return image.Repository + ":" + image.Tag;
        }
        private static string Required(string? value, string name)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException(name + " is required.");
            return value;
        }
    }
}
