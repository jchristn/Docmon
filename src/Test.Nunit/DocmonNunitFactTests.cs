namespace Test.Nunit
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using global::NUnit.Framework;
    using Test.Shared;
    using Touchstone.Core;
    using Touchstone.NunitAdapter;

    /// <summary>
    /// Runs every Docmon test descriptor sequentially in a single NUnit test, honoring suite lifecycle hooks.
    /// </summary>
    [TestFixture]
    public sealed class DocmonNunitFactTests : TouchstoneNunitBase
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
        [Test]
        public async Task RunAll()
        {
            await RunAllAsync().ConfigureAwait(false);
        }
    }
}
