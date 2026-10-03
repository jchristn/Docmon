namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Docmon.Core.Enums;
    using Docmon.Core.Helpers;
    using Docmon.Core.Models;
    using Docmon.Core.Registries;
    using Docmon.Core.Registries.DockerHub;
    using Docmon.Core.Services.Implementations;
    using Test.Shared.Infrastructure;
    using Touchstone.Core;

    /// <summary>
    /// Suites for registry support: provider selection, update-status determination, and the Docker Hub
    /// provider (exercised offline through an in-memory HTTP handler).
    /// </summary>
    public static class RegistrySuites
    {
        private const string _Digest = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string _OtherDigest = "sha256:2222222222222222222222222222222222222222222222222222222222222222";

        /// <summary>
        /// Provider registration and resolution.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor ProviderFactorySuite()
        {
            Cases c = new Cases("RegistryProviderFactory");
            return new TestSuiteDescriptor("RegistryProviderFactory", "Registry provider factory", new List<TestCaseDescriptor>
            {
                c.Sync("AnonymousCredentials", "AnonymousCredentialSource always returns null", () =>
                    Check.Null(new AnonymousCredentialSource().GetCredential("docker.io"), "credential")),
                c.Sync("DockerHubRegistered", "Docker Hub is registered by default, case-insensitively", () =>
                {
                    using (RegistryProviderFactory factory = new RegistryProviderFactory())
                    {
                        IRegistryProvider? provider = factory.Resolve("docker.io");
                        Check.True(provider is DockerHubRegistryProvider, "docker.io resolves to the Docker Hub provider");
                        Check.True(ReferenceEquals(provider, factory.Resolve("DOCKER.IO")), "host lookup is case-insensitive");
                    }
                }),
                c.Sync("UnknownHost", "Unknown hosts resolve to null", () =>
                {
                    using (RegistryProviderFactory factory = new RegistryProviderFactory())
                    {
                        Check.Null(factory.Resolve("ghcr.io"), "ghcr.io");
                        Check.Null(factory.Resolve(string.Empty), "empty host");
                    }
                }),
                c.Sync("RegisterAndReplace", "Register adds a host and replaces an existing registration", () =>
                {
                    using (RegistryProviderFactory factory = new RegistryProviderFactory())
                    {
                        FakeRegistryProvider ghcr = new FakeRegistryProvider("ghcr.io");
                        factory.Register(ghcr);
                        Check.True(ReferenceEquals(ghcr, factory.Resolve("ghcr.io")), "ghcr registered");

                        FakeRegistryProvider hub = new FakeRegistryProvider("docker.io");
                        factory.Register(hub);
                        Check.True(ReferenceEquals(hub, factory.Resolve("docker.io")), "docker.io replaced");
                    }
                }),
                c.Sync("NullArguments", "Register(null) and Resolve(null) throw ArgumentNullException", () =>
                {
                    using (RegistryProviderFactory factory = new RegistryProviderFactory())
                    {
                        Check.Throws<ArgumentNullException>(() => factory.Register(null!), "Register(null)");
                        Check.Throws<ArgumentNullException>(() => factory.Resolve(null!), "Resolve(null)");
                    }
                }),
                c.Async("DisposeLeavesSharedClient", "Disposing the factory (twice) does not dispose a caller-supplied HttpClient", async ct =>
                {
                    FakeHttpHandler handler = new FakeHttpHandler(request => new HttpResponseMessage(HttpStatusCode.OK));
                    using (HttpClient shared = new HttpClient(handler))
                    {
                        RegistryProviderFactory factory = new RegistryProviderFactory(null, shared);
                        factory.Dispose();
                        factory.Dispose();
                        using (HttpResponseMessage response = await shared.GetAsync("http://127.0.0.1/ping", ct).ConfigureAwait(false))
                        {
                            Check.Equal(HttpStatusCode.OK, response.StatusCode, "shared client still usable");
                        }
                    }
                })
            });
        }

        /// <summary>
        /// Update-status determination through <see cref="RegistryService"/>.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor RegistryServiceSuite()
        {
            Cases c = new Cases("RegistryService");
            return new TestSuiteDescriptor("RegistryService", "Registry service", new List<TestCaseDescriptor>
            {
                c.Sync("NullFactory", "Constructor rejects a null factory", () =>
                    Check.Throws<ArgumentNullException>(() => new RegistryService(null!), "new RegistryService(null)")),
                c.Async("NullImage", "CheckAsync and ListTagsAsync reject a null image", async ct =>
                {
                    using (RegistryProviderFactory factory = new RegistryProviderFactory())
                    {
                        RegistryService service = new RegistryService(factory);
                        await Check.ThrowsAsync<ArgumentNullException>(() => service.CheckAsync(null!, ct), "CheckAsync(null)").ConfigureAwait(false);
                        await Check.ThrowsAsync<ArgumentNullException>(() => service.ListTagsAsync(null!, ct), "ListTagsAsync(null)").ConfigureAwait(false);
                    }
                }),
                c.Async("Current", "Matching local repo-digest (case-insensitive) yields Current and records the remote digest", async ct =>
                {
                    FakeRegistryProvider hub = new FakeRegistryProvider("docker.io") { Digest = _Digest };
                    ImageInfo image = Image("nginx", "1.27", "nginx@sha256:" + new string('A', 64));
                    Check.Equal(UpdateStatusEnum.Current, await Run(hub, image, ct).ConfigureAwait(false), "status");
                    Check.Equal(_Digest, image.RemoteDigest, "remote digest");
                    Check.Equal("library/nginx", hub.Resolved[0].Repository, "parsed repository");
                    Check.Equal("1.27", hub.Resolved[0].Tag, "parsed tag");
                }),
                c.Async("CurrentAmongMany", "Any matching repo-digest among several yields Current", async ct =>
                {
                    FakeRegistryProvider hub = new FakeRegistryProvider("docker.io") { Digest = _Digest };
                    ImageInfo image = Image("nginx", "1.27", "mirror/nginx@" + _OtherDigest, "nginx@" + _Digest);
                    Check.Equal(UpdateStatusEnum.Current, await Run(hub, image, ct).ConfigureAwait(false), "status");
                }),
                c.Async("UpdateAvailable", "Differing digest yields UpdateAvailable", async ct =>
                {
                    FakeRegistryProvider hub = new FakeRegistryProvider("docker.io") { Digest = _OtherDigest };
                    ImageInfo image = Image("nginx", "1.27", "nginx@" + _Digest);
                    Check.Equal(UpdateStatusEnum.UpdateAvailable, await Run(hub, image, ct).ConfigureAwait(false), "status");
                    Check.Equal(_OtherDigest, image.RemoteDigest, "remote digest");
                }),
                c.Async("NoLocalDigests", "A locally built image with no repo-digests yields Unknown", async ct =>
                {
                    FakeRegistryProvider hub = new FakeRegistryProvider("docker.io") { Digest = _Digest };
                    ImageInfo image = Image("nginx", "1.27");
                    Check.Equal(UpdateStatusEnum.Unknown, await Run(hub, image, ct).ConfigureAwait(false), "status");
                    Check.Equal(_Digest, image.RemoteDigest, "remote digest still recorded");
                }),
                c.Async("RemoteTagMissing", "A tag absent from the registry yields Unknown", async ct =>
                {
                    FakeRegistryProvider hub = new FakeRegistryProvider("docker.io") { Digest = null };
                    ImageInfo image = Image("nginx", "gone", "nginx@" + _Digest);
                    Check.Equal(UpdateStatusEnum.Unknown, await Run(hub, image, ct).ConfigureAwait(false), "status");
                    Check.Null(image.RemoteDigest, "remote digest");
                }),
                c.Async("Dangling", "Dangling images are Unknown without contacting the registry", async ct =>
                {
                    FakeRegistryProvider hub = new FakeRegistryProvider("docker.io") { Digest = _Digest };
                    ImageInfo image = new ImageInfo();
                    Check.Equal(UpdateStatusEnum.Unknown, await Run(hub, image, ct).ConfigureAwait(false), "status");
                    Check.Equal(0, hub.Resolved.Count, "provider calls");
                }),
                c.Async("UnsupportedRegistry", "Images on a registry with no provider are Unknown", async ct =>
                {
                    FakeRegistryProvider hub = new FakeRegistryProvider("docker.io") { Digest = _Digest };
                    ImageInfo image = Image("ghcr.io/acme/api", "1.0", "ghcr.io/acme/api@" + _Digest);
                    Check.Equal(UpdateStatusEnum.Unknown, await Run(hub, image, ct).ConfigureAwait(false), "status");
                    Check.Equal(0, hub.Resolved.Count, "provider calls");
                }),
                c.Async("CustomProviderUsed", "A registered third-party provider is selected by host", async ct =>
                {
                    FakeRegistryProvider ghcr = new FakeRegistryProvider("ghcr.io") { Digest = _Digest };
                    ImageInfo image = Image("ghcr.io/acme/api", "1.0", "ghcr.io/acme/api@" + _Digest);
                    Check.Equal(UpdateStatusEnum.Current, await Run(ghcr, image, ct).ConfigureAwait(false), "status");
                    Check.Equal("acme/api", ghcr.Resolved[0].Repository, "repository passed to provider");
                }),
                c.Async("RegistryFailure", "A RegistryException is reported as Error", async ct =>
                {
                    FakeRegistryProvider hub = new FakeRegistryProvider("docker.io") { Failure = new RegistryException("boom") };
                    ImageInfo image = Image("nginx", "1.27", "nginx@" + _Digest);
                    Check.Equal(UpdateStatusEnum.Error, await Run(hub, image, ct).ConfigureAwait(false), "status");
                }),
                c.Async("CancellationPropagates", "Cancellation is not swallowed as Error", async ct =>
                {
                    FakeRegistryProvider hub = new FakeRegistryProvider("docker.io") { Failure = new OperationCanceledException() };
                    ImageInfo image = Image("nginx", "1.27", "nginx@" + _Digest);
                    await Check.ThrowsAsync<OperationCanceledException>(() => Run(hub, image, ct), "CheckAsync").ConfigureAwait(false);
                }),
                c.Async("ListTags", "ListTagsAsync returns the provider's tags", async ct =>
                {
                    FakeRegistryProvider hub = new FakeRegistryProvider("docker.io");
                    hub.Tags.AddRange(new[] { "1.27", "1.26", "latest" });
                    using (RegistryProviderFactory factory = new RegistryProviderFactory())
                    {
                        factory.Register(hub);
                        IReadOnlyList<string> tags = await new RegistryService(factory).ListTagsAsync(Image("nginx", "1.27"), ct).ConfigureAwait(false);
                        Check.SequenceEqual(new List<string> { "1.27", "1.26", "latest" }, new List<string>(tags), "tags");
                    }
                }),
                c.Async("ListTagsUnsupported", "ListTagsAsync returns empty for a registry with no provider", async ct =>
                {
                    using (RegistryProviderFactory factory = new RegistryProviderFactory())
                    {
                        IReadOnlyList<string> tags = await new RegistryService(factory).ListTagsAsync(Image("quay.io/acme/app", "1"), ct).ConfigureAwait(false);
                        Check.Equal(0, tags.Count, "tag count");
                    }
                }),
                c.Sync("ExtractDigest", "Repo-digest parsing strips the repository prefix", () =>
                {
                    Check.Equal(_Digest, RegistryService.ExtractDigest("nginx@" + _Digest), "with prefix");
                    Check.Equal(_Digest, RegistryService.ExtractDigest(_Digest), "bare digest");
                })
            });
        }

        /// <summary>
        /// The Docker Hub provider's HTTP protocol handling, against an in-memory handler.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor DockerHubProviderSuite()
        {
            Cases c = new Cases("DockerHubProvider");
            return new TestSuiteDescriptor("DockerHubProvider", "Docker Hub registry provider", new List<TestCaseDescriptor>
            {
                c.Sync("RegistryHost", "Provider reports docker.io as its host", () =>
                {
                    using (DockerHubRegistryProvider provider = new DockerHubRegistryProvider())
                    {
                        Check.Equal("docker.io", provider.RegistryHost, "host");
                    }
                }),
                c.Async("NullReference", "ResolveDigestAsync and ListTagsAsync reject a null reference", async ct =>
                {
                    using (DockerHubRegistryProvider provider = new DockerHubRegistryProvider())
                    {
                        await Check.ThrowsAsync<ArgumentNullException>(() => provider.ResolveDigestAsync(null!, ct), "ResolveDigestAsync(null)").ConfigureAwait(false);
                        await Check.ThrowsAsync<ArgumentNullException>(() => provider.ListTagsAsync(null!, ct), "ListTagsAsync(null)").ConfigureAwait(false);
                    }
                }),
                c.Async("ResolveDigest", "Resolves the digest with a bearer token and manifest Accept headers", async ct =>
                {
                    FakeHttpHandler handler = new FakeHttpHandler(request => Route(request, Json("{\"token\":\"tkn\"}"), () => WithDigest(_Digest)));
                    string? digest = await Resolve(handler, null, "nginx:1.27", ct).ConfigureAwait(false);
                    Check.Equal(_Digest, digest, "digest");

                    IReadOnlyList<HttpRequestMessage> requests = handler.Requests;
                    Check.Equal(2, requests.Count, "request count");
                    string tokenUrl = requests[0].RequestUri!.ToString();
                    Check.True(tokenUrl.StartsWith("https://auth.docker.io/token", StringComparison.Ordinal), "token endpoint: " + tokenUrl);
                    Check.True(tokenUrl.Contains("repository:library/nginx:pull"), "token scope: " + tokenUrl);
                    Check.Null(requests[0].Headers.Authorization, "anonymous token request has no Authorization");

                    HttpRequestMessage manifest = requests[1];
                    Check.Equal("https://registry-1.docker.io/v2/library/nginx/manifests/1.27", manifest.RequestUri!.ToString(), "manifest url");
                    Check.Equal("Bearer", manifest.Headers.Authorization!.Scheme, "auth scheme");
                    Check.Equal("tkn", manifest.Headers.Authorization.Parameter, "bearer token");
                    List<string> accepts = new List<string>();
                    foreach (System.Net.Http.Headers.MediaTypeWithQualityHeaderValue accept in manifest.Headers.Accept)
                        accepts.Add(accept.MediaType ?? string.Empty);
                    Check.Contains("application/vnd.oci.image.index.v1+json", accepts, "Accept headers");
                    Check.Contains("application/vnd.docker.distribution.manifest.list.v2+json", accepts, "Accept headers");
                }),
                c.Async("AccessTokenField", "Accepts the alternate access_token field", async ct =>
                {
                    FakeHttpHandler handler = new FakeHttpHandler(request => Route(request, Json("{\"access_token\":\"alt\"}"), () => WithDigest(_Digest)));
                    Check.Equal(_Digest, await Resolve(handler, null, "nginx:1.27", ct).ConfigureAwait(false), "digest");
                    Check.Equal("alt", handler.Requests[1].Headers.Authorization!.Parameter, "bearer token");
                }),
                c.Async("BasicCredentials", "Configured credentials are sent as Basic auth to the token service", async ct =>
                {
                    FakeHttpHandler handler = new FakeHttpHandler(request => Route(request, Json("{\"token\":\"tkn\"}"), () => WithDigest(_Digest)));
                    StaticCredentials credentials = new StaticCredentials(new RegistryCredential { Username = "alice", Secret = "s3cret" });
                    await Resolve(handler, credentials, "nginx:1.27", ct).ConfigureAwait(false);
                    System.Net.Http.Headers.AuthenticationHeaderValue? auth = handler.Requests[0].Headers.Authorization;
                    Check.NotNull(auth, "token Authorization");
                    Check.Equal("Basic", auth!.Scheme, "scheme");
                    Check.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes("alice:s3cret")), auth.Parameter, "encoded credentials");
                    Check.Equal("docker.io", credentials.LastHost, "credential host requested");
                }),
                c.Async("EmptyUsernameIsAnonymous", "A credential with an empty username is treated as anonymous", async ct =>
                {
                    FakeHttpHandler handler = new FakeHttpHandler(request => Route(request, Json("{\"token\":\"tkn\"}"), () => WithDigest(_Digest)));
                    StaticCredentials credentials = new StaticCredentials(new RegistryCredential { Username = string.Empty, Secret = "x" });
                    await Resolve(handler, credentials, "nginx:1.27", ct).ConfigureAwait(false);
                    Check.Null(handler.Requests[0].Headers.Authorization, "token Authorization");
                }),
                c.Async("ManifestNotFound", "A 404 manifest resolves to null (tag absent)", async ct =>
                {
                    FakeHttpHandler handler = new FakeHttpHandler(request => Route(request, Json("{\"token\":\"tkn\"}"), () => new HttpResponseMessage(HttpStatusCode.NotFound)));
                    Check.Null(await Resolve(handler, null, "nginx:nope", ct).ConfigureAwait(false), "digest");
                }),
                c.Async("MissingDigestHeader", "A successful manifest without Docker-Content-Digest resolves to null", async ct =>
                {
                    FakeHttpHandler handler = new FakeHttpHandler(request => Route(request, Json("{\"token\":\"tkn\"}"), () => new HttpResponseMessage(HttpStatusCode.OK)));
                    Check.Null(await Resolve(handler, null, "nginx:1.27", ct).ConfigureAwait(false), "digest");
                }),
                c.Async("ManifestServerError", "A 5xx manifest response raises RegistryException", async ct =>
                {
                    FakeHttpHandler handler = new FakeHttpHandler(request => Route(request, Json("{\"token\":\"tkn\"}"), () => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
                    RegistryException ex = await Check.ThrowsAsync<RegistryException>(() => Resolve(handler, null, "nginx:1.27", ct), "ResolveDigestAsync").ConfigureAwait(false);
                    Check.True(ex.Message.Contains("503"), "message includes status: " + ex.Message);
                }),
                c.Async("TokenUnauthorized", "A rejected token request raises RegistryException", async ct =>
                {
                    FakeHttpHandler handler = new FakeHttpHandler(request => Route(request, new HttpResponseMessage(HttpStatusCode.Unauthorized), () => WithDigest(_Digest)));
                    await Check.ThrowsAsync<RegistryException>(() => Resolve(handler, null, "nginx:1.27", ct), "ResolveDigestAsync").ConfigureAwait(false);
                    Check.Equal(1, handler.Requests.Count, "manifest is not requested without a token");
                }),
                c.Async("TokenEmpty", "An empty token body raises RegistryException", async ct =>
                {
                    FakeHttpHandler handler = new FakeHttpHandler(request => Route(request, Json("{}"), () => WithDigest(_Digest)));
                    await Check.ThrowsAsync<RegistryException>(() => Resolve(handler, null, "nginx:1.27", ct), "ResolveDigestAsync").ConfigureAwait(false);
                }),
                c.Async("TokenMalformedJson", "A malformed token body raises RegistryException, not JsonException", async ct =>
                {
                    FakeHttpHandler handler = new FakeHttpHandler(request => Route(request, Json("<html>rate limited</html>"), () => WithDigest(_Digest)));
                    await Check.ThrowsAsync<RegistryException>(() => Resolve(handler, null, "nginx:1.27", ct), "ResolveDigestAsync").ConfigureAwait(false);
                }),
                c.Async("TransportFailure", "A network failure raises RegistryException so callers report Error", async ct =>
                {
                    FakeHttpHandler handler = new FakeHttpHandler(request => throw new HttpRequestException("No such host is known."));
                    RegistryException ex = await Check.ThrowsAsync<RegistryException>(() => Resolve(handler, null, "nginx:1.27", ct), "ResolveDigestAsync").ConfigureAwait(false);
                    Check.True(ex.InnerException is HttpRequestException, "inner exception preserved");
                }),
                c.Async("TransportFailureIsErrorStatus", "End to end, an unreachable Docker Hub yields UpdateStatus Error", async ct =>
                {
                    FakeHttpHandler handler = new FakeHttpHandler(request => throw new HttpRequestException("offline"));
                    using (HttpClient http = new HttpClient(handler))
                    using (RegistryProviderFactory factory = new RegistryProviderFactory(null, http))
                    {
                        UpdateStatusEnum status = await new RegistryService(factory).CheckAsync(Image("nginx", "1.27", "nginx@" + _Digest), ct).ConfigureAwait(false);
                        Check.Equal(UpdateStatusEnum.Error, status, "status");
                    }
                }),
                c.Async("Timeout", "An HTTP timeout raises RegistryException", async ct =>
                {
                    FakeHttpHandler handler = new FakeHttpHandler(request => throw new TaskCanceledException("timeout"));
                    await Check.ThrowsAsync<RegistryException>(() => Resolve(handler, null, "nginx:1.27", ct), "ResolveDigestAsync").ConfigureAwait(false);
                }),
                c.Async("CallerCancellation", "Caller cancellation propagates as OperationCanceledException", async ct =>
                {
                    FakeHttpHandler handler = new FakeHttpHandler(request => Route(request, Json("{\"token\":\"tkn\"}"), () => WithDigest(_Digest)));
                    using (CancellationTokenSource cts = new CancellationTokenSource())
                    {
                        cts.Cancel();
                        await Check.ThrowsAsync<OperationCanceledException>(() => Resolve(handler, null, "nginx:1.27", cts.Token), "ResolveDigestAsync").ConfigureAwait(false);
                    }
                }),
                c.Async("ListTags", "ListTagsAsync returns the tag list", async ct =>
                {
                    FakeHttpHandler handler = new FakeHttpHandler(request => Route(request, Json("{\"token\":\"tkn\"}"), () => Json("{\"name\":\"library/nginx\",\"tags\":[\"1.26\",\"1.27\"]}")));
                    IReadOnlyList<string> tags = await ListTags(handler, "nginx", ct).ConfigureAwait(false);
                    Check.SequenceEqual(new List<string> { "1.26", "1.27" }, new List<string>(tags), "tags");
                    Check.Equal("https://registry-1.docker.io/v2/library/nginx/tags/list", handler.Requests[1].RequestUri!.ToString(), "tags url");
                }),
                c.Async("ListTagsNull", "A tag list with null tags returns empty", async ct =>
                {
                    FakeHttpHandler handler = new FakeHttpHandler(request => Route(request, Json("{\"token\":\"tkn\"}"), () => Json("{\"name\":\"library/nginx\",\"tags\":null}")));
                    Check.Equal(0, (await ListTags(handler, "nginx", ct).ConfigureAwait(false)).Count, "tag count");
                }),
                c.Async("ListTagsNotFound", "A 404 tag list raises RegistryException", async ct =>
                {
                    FakeHttpHandler handler = new FakeHttpHandler(request => Route(request, Json("{\"token\":\"tkn\"}"), () => new HttpResponseMessage(HttpStatusCode.NotFound)));
                    await Check.ThrowsAsync<RegistryException>(() => ListTags(handler, "nope", ct), "ListTagsAsync").ConfigureAwait(false);
                }),
                c.Async("ListTagsMalformed", "A malformed tag list raises RegistryException", async ct =>
                {
                    FakeHttpHandler handler = new FakeHttpHandler(request => Route(request, Json("{\"token\":\"tkn\"}"), () => Json("{\"tags\": [1, ")));
                    await Check.ThrowsAsync<RegistryException>(() => ListTags(handler, "nginx", ct), "ListTagsAsync").ConfigureAwait(false);
                })
            });
        }

        private static ImageInfo Image(string repository, string tag, params string[] repoDigests)
        {
            ImageInfo image = new ImageInfo();
            image.Repository = repository;
            image.Tag = tag;
            image.RepoDigests = new List<string>(repoDigests);
            return image;
        }

        private static async Task<UpdateStatusEnum> Run(FakeRegistryProvider provider, ImageInfo image, CancellationToken token)
        {
            using (RegistryProviderFactory factory = new RegistryProviderFactory())
            {
                factory.Register(provider);
                return await new RegistryService(factory).CheckAsync(image, token).ConfigureAwait(false);
            }
        }

        private static async Task<string?> Resolve(FakeHttpHandler handler, IRegistryCredentialSource? credentials, string reference, CancellationToken token)
        {
            using (HttpClient http = new HttpClient(handler, false))
            using (DockerHubRegistryProvider provider = new DockerHubRegistryProvider(credentials, http))
            {
                return await provider.ResolveDigestAsync(ImageReferenceParser.TryParse(reference)!, token).ConfigureAwait(false);
            }
        }

        private static async Task<IReadOnlyList<string>> ListTags(FakeHttpHandler handler, string reference, CancellationToken token)
        {
            using (HttpClient http = new HttpClient(handler, false))
            using (DockerHubRegistryProvider provider = new DockerHubRegistryProvider(null, http))
            {
                return await provider.ListTagsAsync(ImageReferenceParser.TryParse(reference)!, token).ConfigureAwait(false);
            }
        }

        private static HttpResponseMessage Route(HttpRequestMessage request, HttpResponseMessage tokenResponse, Func<HttpResponseMessage> registryResponse)
        {
            if (request.RequestUri != null && request.RequestUri.Host == "auth.docker.io")
                return tokenResponse;

            return registryResponse();
        }

        private static HttpResponseMessage Json(string body)
        {
            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StringContent(body, Encoding.UTF8, "application/json");
            return response;
        }

        private static HttpResponseMessage WithDigest(string digest)
        {
            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Headers.Add("Docker-Content-Digest", digest);
            return response;
        }
    }
}
