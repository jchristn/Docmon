namespace Test.Shared.Infrastructure
{
    using System;
    using System.Reflection;

    /// <summary>Strict interface test double that rejects any call not handled by the test.</summary>
    public class StubServiceProxy : DispatchProxy
    {
        private Func<MethodInfo, object?[], object?>? _Handler;

        /// <summary>Creates a test double with explicit per-method behavior.</summary>
        /// <typeparam name="T">Service interface.</typeparam>
        /// <param name="handler">Call handler.</param>
        /// <returns>The proxy.</returns>
        public static T For<T>(Func<MethodInfo, object?[], object?> handler) where T : class
        {
            T proxy = Create<T, StubServiceProxy>();
            ((StubServiceProxy)(object)proxy)._Handler = handler;
            return proxy;
        }

        /// <inheritdoc/>
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return _Handler!(targetMethod!, args ?? Array.Empty<object?>());
        }
    }
}
