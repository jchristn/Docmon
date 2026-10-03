namespace Test.Xunit
{
    using System.Threading;
    using System.Threading.Tasks;
    using Test.Shared;
    using Touchstone.Core;
    using global::Xunit;
    using global::Xunit.Abstractions;

    /// <summary>
    /// Runs each non-skipped Docmon test descriptor as its own xUnit theory row.
    /// </summary>
    public sealed class DocmonTheoryTests
    {
        private readonly ITestOutputHelper _Output;

        /// <summary>
        /// Initializes a new instance of the <see cref="DocmonTheoryTests"/> class.
        /// </summary>
        /// <param name="output">The xUnit output helper.</param>
        public DocmonTheoryTests(ITestOutputHelper output)
        {
            _Output = output;
        }

        /// <summary>
        /// Enumerates every non-skipped descriptor across all suites.
        /// </summary>
        /// <returns>The theory data.</returns>
        public static TheoryData<TestCaseDescriptor> TestCases()
        {
            TheoryData<TestCaseDescriptor> data = new TheoryData<TestCaseDescriptor>();

            foreach (TestSuiteDescriptor suite in DocmonSuites.All)
            {
                foreach (TestCaseDescriptor testCase in suite.Cases)
                {
                    if (!testCase.Skip)
                        data.Add(testCase);
                }
            }

            return data;
        }

        /// <summary>
        /// Runs a single descriptor.
        /// </summary>
        /// <param name="testCase">The descriptor to run.</param>
        /// <returns>A task that completes when the descriptor has run.</returns>
        [Theory]
        [MemberData(nameof(TestCases))]
        public async Task RunTest(TestCaseDescriptor testCase)
        {
            _Output.WriteLine($"Running: {testCase.DisplayName}");
            await testCase.ExecuteAsync(CancellationToken.None);
        }
    }
}
