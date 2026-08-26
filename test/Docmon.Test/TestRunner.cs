namespace Docmon.Test
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A tiny dependency-free assertion runner: it collects named checks, runs them, prints results, and
    /// returns a process exit code (zero when every check passes).
    /// </summary>
    internal sealed class TestRunner
    {
        private readonly List<string> _Failures = new List<string>();
        private int _Passed;

        internal void Check(string name, bool condition)
        {
            if (condition)
            {
                _Passed++;
                Console.WriteLine("  PASS  " + name);
            }
            else
            {
                _Failures.Add(name);
                Console.WriteLine("  FAIL  " + name);
            }
        }

        internal void Equal(string name, string expected, string actual)
        {
            Check(name + " (expected '" + expected + "', got '" + actual + "')", string.Equals(expected, actual, StringComparison.Ordinal));
        }

        internal void Equal(string name, double expected, double actual, double tolerance)
        {
            Check(name + " (expected " + expected + ", got " + actual + ")", Math.Abs(expected - actual) <= tolerance);
        }

        internal int Complete()
        {
            Console.WriteLine();
            Console.WriteLine(_Passed + " passed, " + _Failures.Count + " failed.");
            return _Failures.Count == 0 ? 0 : 1;
        }
    }
}
