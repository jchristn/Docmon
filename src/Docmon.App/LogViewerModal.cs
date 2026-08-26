namespace Docmon.App
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Docmon.App.Rendering;
    using Docmon.App.Theming;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Input;
    using TUIKit.Modals;

    /// <summary>
    /// A scrollable modal for viewing container logs (or any long text). Supports line and page
    /// scrolling, Home/End, a copy-to-clipboard shortcut, and closes only on Escape so the content
    /// stays put while the user reads and scrolls.
    /// </summary>
    public sealed class LogViewerModal : Modal
    {
        private const int _MarginX = 2;
        private const int _MarginY = 1;

        private readonly string _Title;
        private readonly IReadOnlyList<string> _Lines;
        private int _ScrollTop;
        private int _ViewportHeight = 10;
        private bool _Copied;

        /// <summary>
        /// Initializes a new instance of the <see cref="LogViewerModal"/> class.
        /// </summary>
        /// <param name="title">The box title. May be empty.</param>
        /// <param name="lines">The content lines. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="lines"/> is null.</exception>
        public LogViewerModal(string title, IReadOnlyList<string> lines)
        {
            _Title = title ?? string.Empty;
            _Lines = lines ?? throw new ArgumentNullException(nameof(lines));
            _ScrollTop = Math.Max(0, _Lines.Count - 1);
        }

        /// <summary>
        /// Handles scrolling, copy, and close keys. Only Escape closes the modal; every other key is
        /// consumed so it does not leak to the interface behind the modal.
        /// </summary>
        /// <param name="key">The key event.</param>
        /// <returns>Always true; the modal consumes all keys while open.</returns>
        public override bool HandleKey(KeyEvent key)
        {
            switch (key.Code)
            {
                case KeyCode.Escape:
                    Close(0);
                    return true;
                case KeyCode.Up:
                    Scroll(-1);
                    return true;
                case KeyCode.Down:
                    Scroll(1);
                    return true;
                case KeyCode.PageUp:
                    Scroll(-Math.Max(1, _ViewportHeight - 1));
                    return true;
                case KeyCode.PageDown:
                    Scroll(Math.Max(1, _ViewportHeight - 1));
                    return true;
                case KeyCode.Home:
                    _ScrollTop = 0;
                    return true;
                case KeyCode.End:
                    _ScrollTop = MaxScroll();
                    return true;
                case KeyCode.Character:
                    if (key.Rune == 'c' || key.Rune == 'C')
                        CopyToClipboard();
                    return true;
                default:
                    return true;
            }
        }

        /// <summary>
        /// Renders the scrollable box.
        /// </summary>
        /// <param name="surface">The surface to draw on. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="surface"/> is null.</exception>
        public override void Render(ISurface surface)
        {
            if (surface == null) throw new ArgumentNullException(nameof(surface));

            int screenWidth = surface.Size.Width;
            int screenHeight = surface.Size.Height;
            if (screenWidth < 8 || screenHeight < 6)
                return;

            int boxX = _MarginX;
            int boxY = _MarginY;
            int boxWidth = screenWidth - (2 * _MarginX);
            int boxHeight = screenHeight - (2 * _MarginY);
            Rect box = new Rect(boxX, boxY, boxWidth, boxHeight);

            surface.Fill(box, Cell.Blank(CellStyle.Default));
            surface.DrawBox(box, CellStyle.Default.WithForeground(DocmonPalette.Accent), _Title);

            int contentX = boxX + 2;
            int contentWidth = boxWidth - 4;
            int firstRow = boxY + 1;
            int footerRow = boxY + boxHeight - 2;
            _ViewportHeight = Math.Max(1, footerRow - firstRow);

            ClampScroll();

            CellStyle textStyle = CellStyle.Default.WithForeground(DocmonPalette.Text);
            for (int i = 0; i < _ViewportHeight; i++)
            {
                int lineIndex = _ScrollTop + i;
                if (lineIndex >= _Lines.Count)
                    break;

                Draw.Text(surface, contentX, firstRow + i, _Lines[lineIndex], textStyle, contentWidth);
            }

            int lastVisible = Math.Min(_Lines.Count, _ScrollTop + _ViewportHeight);
            string position = _Lines.Count == 0 ? "empty" : (_ScrollTop + 1) + "-" + lastVisible + " / " + _Lines.Count;
            string hint = "↑↓ scroll · PgUp/PgDn page · Home/End · c copy · Esc close";
            if (_Copied)
                hint = "copied to clipboard   ·   " + hint;

            Draw.Text(surface, contentX, footerRow, hint, CellStyle.Default.WithForeground(DocmonPalette.Dim), Math.Max(0, contentWidth - position.Length - 1));
            Draw.Text(surface, contentX + contentWidth - position.Length, footerRow, position, CellStyle.Default.WithForeground(DocmonPalette.Muted), position.Length);
        }

        private void Scroll(int delta)
        {
            _Copied = false;
            _ScrollTop += delta;
            ClampScroll();
        }

        private void ClampScroll()
        {
            _ScrollTop = Math.Clamp(_ScrollTop, 0, MaxScroll());
        }

        private int MaxScroll()
        {
            return Math.Max(0, _Lines.Count - _ViewportHeight);
        }

        private void CopyToClipboard()
        {
            StringBuilder builder = new StringBuilder();
            foreach (string line in _Lines)
                builder.Append(line).Append('\n');

            try
            {
                Console.Out.Write(SystemClipboard.BuildWriteSequence(builder.ToString()));
                Console.Out.Flush();
                _Copied = true;
            }
            catch (Exception)
            {
                // Clipboard writes rely on terminal OSC 52 support; ignore failures.
            }
        }
    }
}
