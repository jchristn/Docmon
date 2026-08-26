namespace Docmon.Core.Services.Implementations
{
    using System.Collections.Generic;
    using Docmon.Core.Models;

    /// <summary>
    /// Accumulates the services discovered for a single compose project and produces an immutable
    /// <see cref="ComposeStack"/>.
    /// </summary>
    internal sealed class ComposeStackBuilder
    {
        internal ComposeStackBuilder(string project)
        {
            Project = project;
        }

        internal string Project { get; }

        internal string ConfigFile { get; set; } = string.Empty;

        internal List<ComposeServiceInfo> Services { get; } = new List<ComposeServiceInfo>();

        internal ComposeStack Build()
        {
            Services.Sort((a, b) => string.Compare(a.Name, b.Name, System.StringComparison.OrdinalIgnoreCase));
            ComposeStack stack = new ComposeStack();
            stack.Project = Project;
            stack.ConfigFilePath = ConfigFile;
            stack.Services = Services;
            return stack;
        }
    }
}
