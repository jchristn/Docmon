namespace Test.Shared.Infrastructure
{
    using System;

    /// <summary>
    /// An <see cref="IProgress{T}"/> that invokes a callback synchronously on the reporting thread, so
    /// tests observe reports deterministically (unlike <see cref="Progress{T}"/>, which posts them).
    /// </summary>
    /// <typeparam name="T">The report type.</typeparam>
    internal sealed class CallbackProgress<T> : IProgress<T>
    {
        private readonly Action<T> _Callback;

        internal CallbackProgress(Action<T> callback)
        {
            _Callback = callback ?? throw new ArgumentNullException(nameof(callback));
        }

        public void Report(T value)
        {
            _Callback(value);
        }
    }
}
