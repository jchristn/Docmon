namespace Docmon.App
{
    using System;
    using System.Collections.Generic;
    using Docmon.App.Rendering;
    using Docmon.App.Theming;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Modals;

    /// <summary>
    /// A live modal that streams progress lines while an image pull runs, then waits for a keypress to
    /// close. While the pull is in flight, Escape requests cancellation; once complete, any key closes.
    /// </summary>
    public sealed class PullProgressModal : Modal
    {
        private const int _MarginX = 3;
        private const int _MarginY = 2;
        private const int _MaxLines = 400;

        private readonly string _Title;
        private readonly string _Activity;
        private bool _Succeeded = true;
        private readonly Action? _OnCancel;
        private readonly List<string> _Lines = new List<string>();
        private bool _Done;
        private bool _CancelRequested;
        private string _Summary = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="PullProgressModal"/> class.
        /// </summary>
        /// <param name="title">The box title.</param>
        /// <param name="onCancel">An action invoked when the user presses Escape while the pull runs, or null.</param>
        /// <param name="activity">Busy footer text. Defaults to pulling.</param>
        public PullProgressModal(string title, Action? onCancel, string activity = "pulling...")
        {
            _Title = title ?? string.Empty;
            _Activity = activity ?? string.Empty;
            _OnCancel = onCancel;
        }

        /// <summary>
        /// Appends a progress line (call on the UI thread).
        /// </summary>
        /// <param name="line">The line to append.</param>
        public void Append(string line)
        {
            _Lines.Add(line ?? string.Empty);
            if (_Lines.Count > _MaxLines)
                _Lines.RemoveRange(0, _Lines.Count - _MaxLines);
        }

        /// <summary>
        /// Marks the operation complete with a summary (call on the UI thread). The modal then closes on
        /// the next keypress.
        /// </summary>
        /// <param name="summary">The completion summary shown in the footer.</param>
        /// <param name="succeeded">True for success; false uses the error color.</param>
        public void MarkDone(string summary, bool succeeded = true)
        {
            _Done = true;
            _Succeeded = succeeded;
            _Summary = summary ?? string.Empty;
            if (_Lines.Count == 0 || _Lines[_Lines.Count - 1] != _Summary) Append(_Summary);
        }

        /// <inheritdoc/>
        public override bool HandleKey(KeyEvent key)
        {
            if (_Done)
            {
                Close(0);
                return true;
            }

            if (key.Code == KeyCode.Escape && _OnCancel != null && !_CancelRequested)
            {
                _CancelRequested = true;
                _OnCancel();
                Append("cancelling…");
            }

            return true;
        }

        /// <inheritdoc/>
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

            surface.Fill(box, Cell.Blank(DocmonPalette.BaseStyle));
            surface.DrawBox(box, DocmonPalette.BaseStyle.WithForeground(DocmonPalette.Accent), _Title);

            int contentX = boxX + 2;
            int contentWidth = boxWidth - 4;
            int firstRow = boxY + 1;
            int footerRow = boxY + boxHeight - 2;
            int viewport = Math.Max(1, footerRow - firstRow);

            if (contentWidth <= 0) return;
            List<string> wrapped = new List<string>();
            foreach (string line in _Lines)
            {
                if (line.Length == 0) wrapped.Add(string.Empty);
                for (int offset = 0; offset < line.Length; offset += contentWidth)
                    wrapped.Add(line.Substring(offset, Math.Min(contentWidth, line.Length - offset)));
            }
            int start = Math.Max(0, wrapped.Count - viewport);
            CellStyle textStyle = DocmonPalette.BaseStyle.WithForeground(DocmonPalette.Text);
            for (int i = 0; i < viewport && start + i < wrapped.Count; i++)
                Draw.Text(surface, contentX, firstRow + i, wrapped[start + i], textStyle, contentWidth);

            string hint;
            CellStyle hintStyle;
            if (_Done)
            {
                hint = _Summary + "   ·   press any key to close";
                hintStyle = DocmonPalette.BaseStyle.WithForeground(_Succeeded ? DocmonPalette.Success : DocmonPalette.Error);
            }
            else
            {
                hint = _Activity + "   ·   Esc to cancel";
                hintStyle = DocmonPalette.BaseStyle.WithForeground(DocmonPalette.Muted);
            }

            Draw.Text(surface, contentX, footerRow, hint, hintStyle, contentWidth);
        }
    }
}
