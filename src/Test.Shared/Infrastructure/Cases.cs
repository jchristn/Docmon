namespace Test.Shared.Infrastructure
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Touchstone.Core;

    /// <summary>
    /// Shorthand factories for building <see cref="TestCaseDescriptor"/> instances within one suite.
    /// </summary>
    internal sealed class Cases
    {
        private readonly string _SuiteId;
        private readonly string? _SkipReason;

        internal Cases(string suiteId, string? skipReason = null)
        {
            _SuiteId = suiteId;
            _SkipReason = skipReason;
        }

        internal TestCaseDescriptor Sync(string caseId, string displayName, Action body)
        {
            return Async(caseId, displayName, ct =>
            {
                body();
                return Task.CompletedTask;
            });
        }

        internal TestCaseDescriptor Async(string caseId, string displayName, Func<CancellationToken, Task> body)
        {
            return new TestCaseDescriptor(
                suiteId: _SuiteId,
                caseId: caseId,
                displayName: displayName,
                executeAsync: body,
                skip: _SkipReason != null,
                skipReason: _SkipReason);
        }
    }
}
