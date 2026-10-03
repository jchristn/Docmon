namespace Docmon.App
{
    using System;
    using System.Collections.Generic;
    using TUIKit.Ascii;
    using TUIKit.Ascii.Fonts;

    /// <summary>
    /// The Docmon ASCII-art wordmark and startup splash text. The wordmark is rendered with TUIKit's
    /// built-in FIGlet engine (the Small font, matching the house style) so it is always aligned.
    /// </summary>
    public static class DocmonBanner
    {
        /// <summary>
        /// The project home page shown on the splash and header.
        /// </summary>
        public const string ProjectUrl = "https://github.com/jchristn/Docmon";

        /// <summary>
        /// The one-line tagline shown beside the wordmark.
        /// </summary>
        public const string Tagline = "Manage and monitor your Docker stack";

        /// <summary>
        /// Builds the startup splash lines: the wordmark, a blank line, the version and copyright, a
        /// blank line, and the project URL. The modal appends its own "press any key" hint below these.
        /// </summary>
        /// <param name="version">The product version string, for example <c>0.1.1</c>. May be null.</param>
        /// <returns>The splash content lines. Never null.</returns>
        public static IReadOnlyList<string> SplashLines(string? version)
        {
            List<string> lines = new List<string>();
            foreach (string row in WordmarkLines())
                lines.Add(row);

            lines.Add(string.Empty);
            lines.Add(Tagline);
            lines.Add(string.Empty);
            lines.Add("v" + (string.IsNullOrEmpty(version) ? "0.1.1" : version) + " Alpha - (c)2026 Joel Christner");
            lines.Add(string.Empty);
            lines.Add(ProjectUrl);
            return lines;
        }

        /// <summary>
        /// Renders the "docmon" wordmark with the TUIKit Small font, trimmed of blank rows and padded to
        /// a uniform width so it centers and columns cleanly. Falls back to plain text if the font engine
        /// is unavailable.
        /// </summary>
        /// <returns>The wordmark rows. Never null.</returns>
        public static string[] WordmarkLines()
        {
            List<string> rows = new List<string>();
            try
            {
                foreach (string row in AsciiArt.Render("docmon", new SmallAsciiFont()))
                    rows.Add(row);
            }
            catch (Exception)
            {
                rows.Clear();
                rows.Add("d o c m o n");
            }

            while (rows.Count > 0 && rows[0].Trim().Length == 0)
                rows.RemoveAt(0);
            while (rows.Count > 0 && rows[rows.Count - 1].Trim().Length == 0)
                rows.RemoveAt(rows.Count - 1);
            if (rows.Count == 0)
                rows.Add("d o c m o n");

            int width = 0;
            foreach (string row in rows)
                width = Math.Max(width, row.Length);
            for (int i = 0; i < rows.Count; i++)
                rows[i] = rows[i].PadRight(width);

            return rows.ToArray();
        }
    }
}
