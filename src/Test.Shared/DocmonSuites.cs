namespace Test.Shared
{
    using System.Collections.Generic;
    using Test.Shared.Suites;
    using Touchstone.Core;

    /// <summary>
    /// The single source of truth for Docmon tests. Every runner (console, xUnit, NUnit) executes the
    /// suites returned by <see cref="All"/>.
    /// </summary>
    public static class DocmonSuites
    {
        /// <summary>
        /// Gets every Docmon test suite, offline suites first and live-daemon suites last.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get
            {
                return new List<TestSuiteDescriptor>
                {
                    HelperSuites.ByteFormatterSuite(),
                    HelperSuites.CpuCalculatorSuite(),
                    HelperSuites.ImageReferenceParserSuite(),
                    HelperSuites.TimeFormatterSuite(),
                    ModelSuites.ModelSuite(),
                    RegistrySuites.ProviderFactorySuite(),
                    RegistrySuites.RegistryServiceSuite(),
                    RegistrySuites.DockerHubProviderSuite(),
                    MappingSuites.DockerServiceMappingSuite(),
                    MappingSuites.ComposeMappingSuite(),
                    MappingSuites.StreamConversionSuite(),
                    GuardSuites.ServiceGuardSuite(),
                    GuardSuites.ClientProviderSuite(),
                    MonitoringSuites.MetricsSuite(),
                    MonitoringSuites.TableStateSuite(),
                    RenderingSuites.WidgetRenderingSuite(),
                    DockerIntegrationSuites.EngineSuite(),
                    DockerIntegrationSuites.ContainerSuite(),
                    DockerIntegrationSuites.RuntimeSuite(),
                    DockerIntegrationSuites.ComposeSuite()
                };
            }
        }
    }
}
