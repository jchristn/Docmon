namespace Docmon.App.Rendering
{
    using System;
    using System.Collections.Generic;
    using Docmon.App.Theming;
    using TUIKit;
    using TUIKit.Input;

    /// <summary>
    /// A reusable, scrollable, single-select table: it holds rows and selection state, handles
    /// navigation keys, and renders a header row with proportional columns and a selection highlight.
    /// Screens use one instance per table they show.
    /// </summary>
    public sealed class TableState
    {
        private readonly List<RowItem> _Rows = new List<RowItem>();
        private int _Selected;
        private int _ScrollTop;
        private int _PageSize = 10;

        /// <summary>
        /// Gets the tag of the selected row, or null when there is no selection.
        /// </summary>
        public object? SelectedTag
        {
            get { return _Selected >= 0 && _Selected < _Rows.Count ? _Rows[_Selected].Tag : null; }
        }

        /// <summary>
        /// Gets the selected row index, or -1 when empty.
        /// </summary>
        public int SelectedIndex
        {
            get { return _Rows.Count == 0 ? -1 : _Selected; }
        }

        /// <summary>
        /// Gets the number of rows.
        /// </summary>
        public int Count
        {
            get { return _Rows.Count; }
        }

        /// <summary>
        /// Replaces the rows, preserving the selected index where possible.
        /// </summary>
        /// <param name="rows">The new rows. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="rows"/> is null.</exception>
        public void SetRows(IEnumerable<RowItem> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));

            _Rows.Clear();
            _Rows.AddRange(rows);
            if (_Selected >= _Rows.Count)
                _Selected = Math.Max(0, _Rows.Count - 1);
        }

        /// <summary>
        /// Handles a navigation key, updating the selection.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns>True when the key was consumed; otherwise false.</returns>
        public bool HandleKey(KeyEvent key)
        {
            switch (key.Code)
            {
                case KeyCode.Up: return Move(-1);
                case KeyCode.Down: return Move(1);
                case KeyCode.PageUp: return Move(-_PageSize);
                case KeyCode.PageDown: return Move(_PageSize);
                case KeyCode.Home: return MoveTo(0);
                case KeyCode.End: return MoveTo(_Rows.Count - 1);
                default: return false;
            }
        }

        /// <summary>
        /// Renders the table into a rectangle.
        /// </summary>
        /// <param name="surface">The target surface.</param>
        /// <param name="rect">The rectangle to render within.</param>
        /// <param name="headers">The column headers.</param>
        /// <param name="weights">The relative column weights (same length as headers).</param>
        /// <param name="focused">Whether the owning region is focused (affects the selection highlight).</param>
        /// <param name="emptyMessage">A message shown when there are no rows.</param>
        public void Render(ISurface surface, Rect rect, string[] headers, int[] weights, bool focused, string emptyMessage)
        {
            if (rect.Width <= 0 || rect.Height <= 0)
                return;

            CellStyle headerStyle = CellStyle.Default.WithForeground(DocmonPalette.Muted).WithAttribute(CellAttributes.Bold, true);
            CellStyle textStyle = CellStyle.Default.WithForeground(DocmonPalette.Text);

            int[] widths = ComputeWidths(weights, rect.Width);

            int headerRow = rect.Y;
            DrawRow(surface, rect.X, headerRow, widths, headers, headerStyle);

            int firstDataRow = rect.Y + 1;
            int visibleRows = rect.Height - 1;
            if (visibleRows <= 0)
                return;

            _PageSize = Math.Max(1, visibleRows);

            if (_Rows.Count == 0)
            {
                Draw.Text(surface, rect.X, firstDataRow, emptyMessage, CellStyle.Default.WithForeground(DocmonPalette.Dim), rect.Width);
                return;
            }

            EnsureVisible(visibleRows);

            for (int i = 0; i < visibleRows; i++)
            {
                int rowIndex = _ScrollTop + i;
                if (rowIndex >= _Rows.Count)
                    break;

                RowItem row = _Rows[rowIndex];
                int y = firstDataRow + i;
                bool selected = rowIndex == _Selected;

                if (selected)
                {
                    CellStyle selectionStyle = focused
                        ? new CellStyle(DocmonPalette.SelectionForeground, DocmonPalette.Accent)
                        : CellStyle.Default.WithForeground(DocmonPalette.Accent).WithAttribute(CellAttributes.Bold, true);

                    if (focused)
                        surface.Fill(new Rect(rect.X, y, rect.Width, 1), Cell.Blank(selectionStyle));

                    DrawRow(surface, rect.X, y, widths, row.Cells, selectionStyle);
                }
                else
                {
                    CellStyle style = row.Color.HasValue ? CellStyle.Default.WithForeground(row.Color.Value) : textStyle;
                    DrawRow(surface, rect.X, y, widths, row.Cells, style);
                }
            }
        }

        private bool Move(int delta)
        {
            if (_Rows.Count == 0)
                return true;

            return MoveTo(_Selected + delta);
        }

        private bool MoveTo(int index)
        {
            if (_Rows.Count == 0)
                return true;

            _Selected = Math.Clamp(index, 0, _Rows.Count - 1);
            return true;
        }

        private void EnsureVisible(int visibleRows)
        {
            if (_Selected < _ScrollTop)
                _ScrollTop = _Selected;
            else if (_Selected >= _ScrollTop + visibleRows)
                _ScrollTop = _Selected - visibleRows + 1;

            if (_ScrollTop < 0)
                _ScrollTop = 0;
        }

        private static void DrawRow(ISurface surface, int x, int y, int[] widths, string[] cells, CellStyle style)
        {
            int cursor = x;
            for (int i = 0; i < widths.Length; i++)
            {
                int width = widths[i];
                string value = i < cells.Length ? cells[i] : string.Empty;
                Draw.Text(surface, cursor, y, value, style, Math.Max(0, width - 1));
                cursor += width;
            }
        }

        private static int[] ComputeWidths(int[] weights, int totalWidth)
        {
            int[] widths = new int[weights.Length];
            int totalWeight = 0;
            for (int i = 0; i < weights.Length; i++)
                totalWeight += Math.Max(1, weights[i]);

            if (totalWeight == 0)
                totalWeight = 1;

            int assigned = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                int weight = Math.Max(1, weights[i]);
                int width = totalWidth * weight / totalWeight;
                if (width < 3)
                    width = 3;
                widths[i] = width;
                assigned += width;
            }

            // Give any rounding remainder to the last column.
            int remainder = totalWidth - assigned;
            if (remainder != 0 && widths.Length > 0)
                widths[widths.Length - 1] = Math.Max(3, widths[widths.Length - 1] + remainder);

            return widths;
        }
    }
}
