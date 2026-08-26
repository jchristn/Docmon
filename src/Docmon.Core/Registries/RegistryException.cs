namespace Docmon.Core.Registries
{
    using System;

    /// <summary>
    /// Raised when a registry provider fails to resolve image information because of a transport,
    /// authentication, or protocol error.
    /// </summary>
    public class RegistryException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RegistryException"/> class.
        /// </summary>
        /// <param name="message">A message describing the failure.</param>
        public RegistryException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RegistryException"/> class.
        /// </summary>
        /// <param name="message">A message describing the failure.</param>
        /// <param name="innerException">The underlying exception.</param>
        public RegistryException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
