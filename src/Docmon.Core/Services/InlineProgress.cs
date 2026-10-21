namespace Docmon.Core.Services
{
    using System;
    /// <summary>Synchronous progress callback, preserving output order.</summary>
    /// <typeparam name="T">Reported value.</typeparam>
    public sealed class InlineProgress<T> : IProgress<T>
    {
        private readonly Action<T> _Callback;
        /// <summary>Creates a progress sink.</summary>
        /// <param name="callback">Receives values synchronously.</param>
        public InlineProgress(Action<T> callback) { _Callback = callback ?? throw new ArgumentNullException(nameof(callback)); }
        /// <summary>Reports a value.</summary>
        /// <param name="value">Value.</param>
        public void Report(T value) { _Callback(value); }
    }
}
