namespace Docmon.Core.Registries.DockerHub
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// The tags-list response returned by the registry v2 tags endpoint.
    /// </summary>
    internal sealed class DockerHubTagsResponse
    {
        /// <summary>
        /// Gets or sets the repository name.
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the available tags.
        /// </summary>
        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; }
    }
}
