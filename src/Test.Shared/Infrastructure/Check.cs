namespace Test.Shared.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading.Tasks;

    /// <summary>
    /// Minimal assertion helpers for Touchstone descriptors. Every failure throws
    /// <see cref="TestAssertionException"/>; nothing is written to the console.
    /// </summary>
    internal static class Check
    {
        internal static void True(bool condition, string message)
        {
            if (!condition)
                throw new TestAssertionException(message);
        }

        internal static void False(bool condition, string message)
        {
            if (condition)
                throw new TestAssertionException(message);
        }

        internal static void Equal<T>(T expected, T actual, string what)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new TestAssertionException(what + ": expected '" + Describe(expected) + "' but got '" + Describe(actual) + "'.");
        }

        internal static void Close(double expected, double actual, double tolerance, string what)
        {
            if (Math.Abs(expected - actual) > tolerance)
                throw new TestAssertionException(what + ": expected " + expected.ToString(CultureInfo.InvariantCulture) + " but got " + actual.ToString(CultureInfo.InvariantCulture) + ".");
        }

        internal static void NotNull(object? value, string what)
        {
            if (value == null)
                throw new TestAssertionException(what + ": expected a value but got null.");
        }

        internal static void Null(object? value, string what)
        {
            if (value != null)
                throw new TestAssertionException(what + ": expected null but got '" + value + "'.");
        }

        internal static void Contains(string expected, IEnumerable<string> actual, string what)
        {
            foreach (string item in actual)
            {
                if (string.Equals(item, expected, StringComparison.Ordinal))
                    return;
            }

            throw new TestAssertionException(what + ": expected the sequence to contain '" + expected + "' but it was [" + string.Join(", ", actual) + "].");
        }

        internal static void SequenceEqual<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual, string what)
        {
            bool same = expected.Count == actual.Count;
            for (int i = 0; same && i < expected.Count; i++)
                same = EqualityComparer<T>.Default.Equals(expected[i], actual[i]);

            if (!same)
                throw new TestAssertionException(what + ": expected [" + string.Join(", ", expected) + "] but got [" + string.Join(", ", actual) + "].");
        }

        internal static TException Throws<TException>(Action action, string what)
            where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException ex)
            {
                return ex;
            }
            catch (Exception ex)
            {
                throw new TestAssertionException(what + ": expected " + typeof(TException).Name + " but got " + ex.GetType().Name + ": " + ex.Message);
            }

            throw new TestAssertionException(what + ": expected " + typeof(TException).Name + " but no exception was thrown.");
        }

        internal static async Task<TException> ThrowsAsync<TException>(Func<Task> action, string what)
            where TException : Exception
        {
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (TException ex)
            {
                return ex;
            }
            catch (Exception ex)
            {
                throw new TestAssertionException(what + ": expected " + typeof(TException).Name + " but got " + ex.GetType().Name + ": " + ex.Message);
            }

            throw new TestAssertionException(what + ": expected " + typeof(TException).Name + " but no exception was thrown.");
        }

        private static string Describe(object? value)
        {
            if (value == null)
                return "null";
            if (value is IFormattable formattable)
                return formattable.ToString(null, CultureInfo.InvariantCulture);
            return value.ToString() ?? string.Empty;
        }
    }
}
