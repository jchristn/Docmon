namespace Test.Xunit
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.XunitAdapter;
    using global::Xunit;

    /// <summary>
    /// Runs every Docmon test descriptor sequentially in a single xUnit fact, honoring suite lifecycle hooks.
    /// </summary>
    public sealed class DocmonFactTests : TouchstoneFactBase
    {
        /// <summary>
        /// Gets the suites to execute.
        /// </summary>
        protected override IReadOnlyList<TestSuiteDescriptor> Suites
        {
            get { return DocmonSuites.All; }
        }

        /// <summary>
        /// Runs all suites.
        /// </summary>
        /// <returns>A task that completes when every suite has run.</returns>
        [Fact]
        public async Task RunAll()
        {
            await RunAllAsync();
        }
    }
}
