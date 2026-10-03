namespace Test.Nunit
{
    using System.Collections;
    using System.Threading;
    using System.Threading.Tasks;
    using global::NUnit.Framework;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.NunitAdapter;

    /// <summary>
    /// Runs each Docmon test descriptor as its own NUnit test case.
    /// </summary>
    [TestFixture]
    public sealed class DocmonNunitTests
    {
        private static IEnumerable TestCases()
        {
            return new TouchstoneTestCaseSource(DocmonSuites.All);
        }

        /// <summary>
        /// Runs a single descriptor.
        /// </summary>
        /// <param name="testCase">The descriptor to run.</param>
        /// <returns>A task that completes when the descriptor has run.</returns>
        [Test]
        [TestCaseSource(nameof(TestCases))]
        public async Task RunTest(TestCaseDescriptor testCase)
        {
            await testCase.ExecuteAsync(CancellationToken.None);
        }
    }
}
