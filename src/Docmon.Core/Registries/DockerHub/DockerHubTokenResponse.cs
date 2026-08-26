namespace Docmon.Core.Registries.DockerHub
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// The bearer-token response returned by the Docker Hub token endpoint.
    /// </summary>
    internal sealed class DockerHubTokenResponse
    {
        /// <summary>
        /// Gets or sets the bearer token used to authorize registry manifest requests.
        /// </summary>
        [JsonPropertyName("token")]
        public string? Token { get; set; }

        /// <summary>
        /// Gets or sets the access token, an alternate field name some registries use.
        /// </summary>
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }
    }
}
