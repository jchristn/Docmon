namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// An in-memory <see cref="HttpMessageHandler"/> that answers requests from a delegate and records
    /// every request it receives, so registry providers can be exercised without network access.
    /// </summary>
    internal sealed class FakeHttpHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _Responder;
        private readonly List<HttpRequestMessage> _Requests = new List<HttpRequestMessage>();
        private readonly object _Lock = new object();

        internal FakeHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _Responder = responder ?? throw new ArgumentNullException(nameof(responder));
        }

        internal IReadOnlyList<HttpRequestMessage> Requests
        {
            get
            {
                lock (_Lock)
                {
                    return new List<HttpRequestMessage>(_Requests);
                }
            }
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_Lock)
            {
                _Requests.Add(request);
            }

            return Task.FromResult(_Responder(request));
        }
    }
}
