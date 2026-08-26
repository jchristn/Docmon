namespace Docmon.Core.Registries.DockerHub
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Docmon.Core.Models;

    /// <summary>
    /// An <see cref="IRegistryProvider"/> for Docker Hub, using the registry v2 HTTP API with an
    /// anonymous pull token from the Docker Hub token service. Thread safety: instances are safe for
    /// concurrent use.
    /// </summary>
    public sealed class DockerHubRegistryProvider : IRegistryProvider, IDisposable
    {
        private const string _TokenEndpoint = "https://auth.docker.io/token?service=registry.docker.io&scope=repository:";
        private const string _RegistryBase = "https://registry-1.docker.io/v2/";

        private static readonly string[] _ManifestAcceptTypes =
        {
            "application/vnd.docker.distribution.manifest.v2+json",
            "application/vnd.docker.distribution.manifest.list.v2+json",
            "application/vnd.oci.image.index.v1+json",
            "application/vnd.oci.image.manifest.v1+json"
        };

        private readonly HttpClient _Http;
        private readonly bool _OwnsHttpClient;
        private readonly IRegistryCredentialSource _Credentials;
        private readonly JsonSerializerOptions _JsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        private bool _Disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="DockerHubRegistryProvider"/> class.
        /// </summary>
        /// <param name="credentials">The credential source, or null for anonymous access.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to reuse, or null to create and own one.</param>
        public DockerHubRegistryProvider(IRegistryCredentialSource? credentials = null, HttpClient? httpClient = null)
        {
            _Credentials = credentials ?? new AnonymousCredentialSource();
            if (httpClient != null)
            {
                _Http = httpClient;
                _OwnsHttpClient = false;
            }
            else
            {
                _Http = new HttpClient();
                _Http.Timeout = TimeSpan.FromSeconds(15);
                _OwnsHttpClient = true;
            }
        }

        /// <inheritdoc/>
        public string RegistryHost
        {
            get { return "docker.io"; }
        }

        /// <inheritdoc/>
        public async Task<string?> ResolveDigestAsync(ImageReference reference, CancellationToken token)
        {
            if (reference == null) throw new ArgumentNullException(nameof(reference));

            string bearer = await GetTokenAsync(reference.Repository, token).ConfigureAwait(false);
            string url = _RegistryBase + reference.Repository + "/manifests/" + reference.Tag;

            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
                foreach (string accept in _ManifestAcceptTypes)
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));

                using (HttpResponseMessage response = await _Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false))
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        return null;
                    if (!response.IsSuccessStatusCode)
                        throw new RegistryException("Docker Hub returned " + (int)response.StatusCode + " resolving " + reference + ".");

                    if (response.Headers.TryGetValues("Docker-Content-Digest", out IEnumerable<string>? values))
                    {
                        foreach (string value in values)
                        {
                            if (!string.IsNullOrWhiteSpace(value))
                                return value.Trim();
                        }
                    }

                    return null;
                }
            }
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<string>> ListTagsAsync(ImageReference reference, CancellationToken token)
        {
            if (reference == null) throw new ArgumentNullException(nameof(reference));

            string bearer = await GetTokenAsync(reference.Repository, token).ConfigureAwait(false);
            string url = _RegistryBase + reference.Repository + "/tags/list";

            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
                using (HttpResponseMessage response = await _Http.SendAsync(request, token).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                        throw new RegistryException("Docker Hub returned " + (int)response.StatusCode + " listing tags for " + reference.Repository + ".");

                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    DockerHubTagsResponse? parsed = JsonSerializer.Deserialize<DockerHubTagsResponse>(body, _JsonOptions);
                    if (parsed?.Tags == null)
                        return new List<string>();

                    return parsed.Tags;
                }
            }
        }

        private async Task<string> GetTokenAsync(string repository, CancellationToken token)
        {
            string url = _TokenEndpoint + repository + ":pull";

            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                RegistryCredential? credential = _Credentials.GetCredential(RegistryHost);
                if (credential != null && !string.IsNullOrEmpty(credential.Username))
                {
                    string raw = credential.Username + ":" + credential.Secret;
                    string encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(raw));
                    request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encoded);
                }

                using (HttpResponseMessage response = await _Http.SendAsync(request, token).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                        throw new RegistryException("Could not obtain a Docker Hub pull token for '" + repository + "' (HTTP " + (int)response.StatusCode + ").");

                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    DockerHubTokenResponse? parsed = JsonSerializer.Deserialize<DockerHubTokenResponse>(body, _JsonOptions);
                    string? bearer = parsed?.Token ?? parsed?.AccessToken;
                    if (string.IsNullOrWhiteSpace(bearer))
                        throw new RegistryException("Docker Hub returned an empty token for '" + repository + "'.");

                    return bearer!;
                }
            }
        }

        /// <summary>
        /// Disposes the owned <see cref="HttpClient"/>, if this instance created it.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed)
                return;

            if (_OwnsHttpClient)
                _Http.Dispose();
            _Disposed = true;
        }
    }
}
