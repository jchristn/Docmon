namespace Docmon.App
{
    using System;

    /// <summary>
    /// An <see cref="IProgress{T}"/> that invokes a delegate synchronously on the reporting thread,
    /// unlike <see cref="Progress{T}"/> which marshals through a synchronization context. Used so
    /// Docmon can decide explicitly when to marshal onto the UI thread.
    /// </summary>
    /// <typeparam name="T">The progress value type.</typeparam>
    public sealed class DelegateProgress<T> : IProgress<T>
    {
        private readonly Action<T> _Handler;

        /// <summary>
        /// Initializes a new instance of the <see cref="DelegateProgress{T}"/> class.
        /// </summary>
        /// <param name="handler">The delegate invoked for each reported value. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="handler"/> is null.</exception>
        public DelegateProgress(Action<T> handler)
        {
            _Handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        /// <inheritdoc/>
        public void Report(T value)
        {
            _Handler(value);
        }
    }
}
