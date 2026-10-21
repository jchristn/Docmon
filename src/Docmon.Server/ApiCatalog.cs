namespace Docmon.Server
{
    using System.Collections.Generic;
    /// <summary>Single endpoint inventory used by REST, MCP, and documentation generation.</summary>
    public static class ApiCatalog
    {
        /// <summary>Published operations.</summary>
        public static IReadOnlyList<ApiOperation> Operations { get; } = new List<ApiOperation>
        {
            new ApiOperation("containers_list", "GET", "/containers", "List all containers"),
            new ApiOperation("containers_inspect", "GET", "/containers/{id}", "Inspect a container", "id"),
            new ApiOperation("containers_logs", "GET", "/containers/{id}/logs", "Read container logs", "id"),
            new ApiOperation("containers_start", "POST", "/containers/{id}/start", "Start container", "id"),
            new ApiOperation("containers_stop", "POST", "/containers/{id}/stop", "Stop container", "id"),
            new ApiOperation("containers_restart", "POST", "/containers/{id}/restart", "Restart container", "id"),
            new ApiOperation("containers_kill", "POST", "/containers/{id}/kill", "Kill container", "id"),
            new ApiOperation("containers_pause", "POST", "/containers/{id}/pause", "Pause container", "id"),
            new ApiOperation("containers_unpause", "POST", "/containers/{id}/unpause", "Unpause container", "id"),
            new ApiOperation("containers_remove", "POST", "/containers/{id}/remove", "Remove container", "id"),
            new ApiOperation("containers_exec", "POST", "/containers/{id}/exec", "Exec container", "id", "command"),
            new ApiOperation("containers_copy_in", "POST", "/containers/{id}/copy-in", "Copy in container", "id", "hostPath", "path"),
            new ApiOperation("containers_copy_out", "POST", "/containers/{id}/copy-out", "Copy out container", "id", "hostPath", "path"),
            new ApiOperation("containers_update", "POST", "/containers/{id}/update", "Update container", "id"),
            new ApiOperation("images_list", "GET", "/images", "List local images"),
            new ApiOperation("images_pull", "POST", "/images/pull", "Pull an image by name/tag or existing image ID"),
            new ApiOperation("images_prune", "POST", "/images/prune", "Prune dangling images"),
            new ApiOperation("images_remove", "POST", "/images/{id}/remove", "Remove image", "id"),
            new ApiOperation("images_check", "POST", "/images/{id}/check", "Check image", "id"),
            new ApiOperation("images_tags", "GET", "/images/{id}/tags", "Tags image", "id"),
            new ApiOperation("stacks_list", "GET", "/stacks", "Discover Compose projects"),
            new ApiOperation("stacks_up", "POST", "/stacks/{project}/up", "Up Compose project", "project"),
            new ApiOperation("stacks_down", "POST", "/stacks/{project}/down", "Down Compose project", "project"),
            new ApiOperation("stacks_restart", "POST", "/stacks/{project}/restart", "Restart Compose project", "project"),
            new ApiOperation("stacks_pull", "POST", "/stacks/{project}/pull", "Pull Compose project", "project"),
            new ApiOperation("system_usage", "GET", "/system/usage", "Docker disk and resource usage"),
            new ApiOperation("system_info", "GET", "/system/info", "Server connection and version"),
            new ApiOperation("events_list", "GET", "/events", "Recent daemon events"),
            new ApiOperation("metrics_summary", "GET", "/metrics/summary", "Persistent historical metrics: hour/day/week/month buckets; CPU and memory means, last byte counters, gaps marked by hasData"),
            new ApiOperation("metrics_history", "GET", "/metrics/history", "Bounded recent container metric history"),
            new ApiOperation("metrics_list", "GET", "/metrics", "Latest live container metrics"),
            new ApiOperation("history_summary", "GET", "/history/summary", "Request history time buckets across the retained journal"),
            new ApiOperation("history_inspect", "GET", "/history/{id}", "Inspect a redacted request record", "id"),
            new ApiOperation("history_list", "GET", "/history", "Recent API requests without credentials"),
        };
    }
}
