namespace Docmon.Core.Services
{
    using System;

    /// <summary>
    /// Raised when Docmon cannot reach or communicate with the Docker daemon.
    /// </summary>
    public class DockerConnectionException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DockerConnectionException"/> class.
        /// </summary>
        /// <param name="message">A message describing the failure.</param>
        public DockerConnectionException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DockerConnectionException"/> class.
        /// </summary>
        /// <param name="message">A message describing the failure.</param>
        /// <param name="innerException">The underlying exception.</param>
        public DockerConnectionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
