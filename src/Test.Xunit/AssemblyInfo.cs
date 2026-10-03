// The shared suites drive a real Docker daemon and mutate process-wide state (environment variables),
// so the fact-style and theory-style classes must not run concurrently.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
