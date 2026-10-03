namespace Test.Shared.Infrastructure
{
    using System;

    /// <summary>
    /// Raised by <see cref="Check"/> when a test expectation is not met.
    /// </summary>
    public sealed class TestAssertionException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TestAssertionException"/> class.
        /// </summary>
        /// <param name="message">A message describing the unmet expectation.</param>
        public TestAssertionException(string message)
            : base(message)
        {
        }
    }
}
