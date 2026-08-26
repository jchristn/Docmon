namespace Docmon.App.Rendering
{
    using System;
    using System.Collections.Generic;
    using TUIKit;

    /// <summary>
    /// Low-level drawing primitives shared by Docmon's widgets and screens: clipped text, bordered
    /// panels, tables, gauges, sparklines, and time-series charts. Everything draws directly into a
    /// supplied <see cref="ISurface"/> at absolute coordinates, so screens can compose their own
    /// multi-region layouts without needing sub-surfaces.
    /// </summary>
    public static class Draw
    {
        private const string _VerticalBlocks = " ▁▂▃▄▅▆▇█";

        /// <summary>
        /// Clips a string to a maximum display width, appending an ellipsis when truncated.
        /// </summary>
        /// <param name="value">The text. Null is treated as empty.</param>
        /// <param name="width">The maximum width in cells.</param>
        /// <returns>The clipped text. Never null.</returns>
        public static string Clip(string? value, int width)
        {
            value ??= string.Empty;
            if (width <= 0)
                return string.Empty;
            if (value.Length <= width)
                return value;
            if (width == 1)
                return value.Substring(0, 1);
            return value.Substring(0, width - 1) + "…";
        }

        /// <summary>
        /// Draws clipped text at a position with a style.
        /// </summary>
        /// <param name="surface">The target surface.</param>
        /// <param name="x">The column.</param>
        /// <param name="y">The row.</param>
        /// <param name="text">The text.</param>
        /// <param name="style">The style.</param>
        /// <param name="maxWidth">The maximum width; text is clipped to fit.</param>
        public static void Text(ISurface surface, int x, int y, string text, CellStyle style, int maxWidth)
        {
            surface.DrawText(x, y, Clip(text, maxWidth), style);
        }

        /// <summary>
        /// Draws a bordered panel with an optional title and returns the inner content rectangle.
        /// </summary>
        /// <param name="surface">The target surface.</param>
        /// <param name="rect">The outer rectangle.</param>
        /// <param name="title">The title, or null for none.</param>
        /// <param name="borderStyle">The border and title style.</param>
        /// <returns>The inner rectangle (inset by one cell on each side), clamped to non-negative size.</returns>
        public static Rect Panel(ISurface surface, Rect rect, string? title, CellStyle borderStyle)
        {
            if (rect.Width < 2 || rect.Height < 2)
                return Rect.Empty;

            surface.DrawBox(rect, borderStyle, title);
            return new Rect(rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2);
        }

        /// <summary>
        /// Fills a rectangle with blank cells of a given style (used to clear a region before drawing).
        /// </summary>
        /// <param name="surface">The target surface.</param>
        /// <param name="rect">The rectangle to clear.</param>
        /// <param name="style">The style whose background is applied.</param>
        public static void Clear(ISurface surface, Rect rect, CellStyle style)
        {
            surface.Fill(rect, Cell.Blank(style));
        }

        /// <summary>
        /// Draws a horizontal meter bar filled to a fraction of its width.
        /// </summary>
        /// <param name="surface">The target surface.</param>
        /// <param name="x">The column.</param>
        /// <param name="y">The row.</param>
        /// <param name="width">The total width in cells.</param>
        /// <param name="fraction">The fill fraction, clamped to 0 through 1.</param>
        /// <param name="filled">The style for the filled portion.</param>
        /// <param name="empty">The style for the empty portion.</param>
        public static void Bar(ISurface surface, int x, int y, int width, double fraction, CellStyle filled, CellStyle empty)
        {
            if (width <= 0)
                return;

            fraction = Math.Clamp(fraction, 0.0, 1.0);
            int filledCells = (int)Math.Round(fraction * width);
            for (int i = 0; i < width; i++)
            {
                bool on = i < filledCells;
                surface.Set(x + i, y, Cell.Glyph(on ? "█" : "░", on ? filled : empty, 1));
            }
        }

        /// <summary>
        /// Builds a single-row sparkline string from a series of values using vertical block glyphs.
        /// </summary>
        /// <param name="values">The values, oldest first. Null yields an empty string.</param>
        /// <param name="width">The number of columns to render.</param>
        /// <param name="maximum">The value mapped to a full-height glyph; values above are clamped.</param>
        /// <returns>The sparkline string. Never null.</returns>
        public static string Sparkline(IReadOnlyList<double>? values, int width, double maximum)
        {
            if (values == null || values.Count == 0 || width <= 0)
                return string.Empty;

            char[] output = new char[width];
            double max = maximum <= 0 ? 1.0 : maximum;
            int start = Math.Max(0, values.Count - width);
            int count = values.Count - start;
            int pad = width - count;

            for (int i = 0; i < width; i++)
            {
                if (i < pad)
                {
                    output[i] = ' ';
                    continue;
                }

                double value = values[start + (i - pad)];
                double fraction = Math.Clamp(value / max, 0.0, 1.0);
                int level = (int)Math.Round(fraction * 8.0);
                output[i] = _VerticalBlocks[Math.Clamp(level, 0, 8)];
            }

            return new string(output);
        }

        /// <summary>
        /// Draws a filled area chart for a single series within a rectangle.
        /// </summary>
        /// <param name="surface">The target surface.</param>
        /// <param name="area">The chart plot area.</param>
        /// <param name="values">The values, oldest first.</param>
        /// <param name="maximum">The value mapped to full height; values above are clamped.</param>
        /// <param name="style">The fill style.</param>
        public static void AreaChart(ISurface surface, Rect area, IReadOnlyList<double> values, double maximum, CellStyle style)
        {
            if (area.Width <= 0 || area.Height <= 0 || values == null || values.Count == 0)
                return;

            double max = maximum <= 0 ? 1.0 : maximum;
            int width = area.Width;
            int height = area.Height;
            int start = Math.Max(0, values.Count - width);
            int count = values.Count - start;
            int pad = width - count;

            for (int column = 0; column < width; column++)
            {
                if (column < pad)
                    continue;

                double value = values[start + (column - pad)];
                double fraction = Math.Clamp(value / max, 0.0, 1.0);
                double cells = fraction * height;
                int fullCells = (int)Math.Floor(cells);
                double remainder = cells - fullCells;
                int partialLevel = (int)Math.Round(remainder * 8.0);

                int x = area.X + column;
                for (int row = 0; row < fullCells && row < height; row++)
                    surface.Set(x, area.Y + height - 1 - row, Cell.Glyph("█", style, 1));

                if (fullCells < height && partialLevel > 0)
                {
                    char glyph = _VerticalBlocks[Math.Clamp(partialLevel, 1, 8)];
                    surface.Set(x, area.Y + height - 1 - fullCells, Cell.Glyph(glyph.ToString(), style, 1));
                }
            }
        }

        /// <summary>
        /// Draws a line series as markers within a rectangle (used to overlay a second series).
        /// </summary>
        /// <param name="surface">The target surface.</param>
        /// <param name="area">The chart plot area.</param>
        /// <param name="values">The values, oldest first.</param>
        /// <param name="maximum">The value mapped to full height; values above are clamped.</param>
        /// <param name="style">The marker style.</param>
        /// <param name="marker">The marker glyph.</param>
        public static void LineSeries(ISurface surface, Rect area, IReadOnlyList<double> values, double maximum, CellStyle style, string marker)
        {
            if (area.Width <= 0 || area.Height <= 0 || values == null || values.Count == 0)
                return;

            double max = maximum <= 0 ? 1.0 : maximum;
            int width = area.Width;
            int height = area.Height;
            int start = Math.Max(0, values.Count - width);
            int count = values.Count - start;
            int pad = width - count;

            for (int column = 0; column < width; column++)
            {
                if (column < pad)
                    continue;

                double value = values[start + (column - pad)];
                double fraction = Math.Clamp(value / max, 0.0, 1.0);
                int row = (int)Math.Round(fraction * (height - 1));
                surface.Set(area.X + column, area.Y + height - 1 - row, Cell.Glyph(marker, style, 1));
            }
        }
    }
}
