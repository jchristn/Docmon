namespace Docmon.App.Widgets
{
    using System;
    using Docmon.App.Rendering;
    using Docmon.App.Theming;
    using TUIKit;
    using TUIKit.Widgets;

    /// <summary>
    /// The bottom status bar: context key hints on the left and the latest status message on the right.
    /// </summary>
    public sealed class StatusBar : IWidget
    {
        private string _Hints = string.Empty;
        private string _Message = string.Empty;

        /// <summary>
        /// Gets or sets the context-sensitive key hints shown on the left.
        /// </summary>
        public string Hints
        {
            get { return _Hints; }
            set { _Hints = value ?? string.Empty; }
        }

        /// <summary>
        /// Gets or sets the latest status message shown on the right.
        /// </summary>
        public string Message
        {
            get { return _Message; }
            set { _Message = value ?? string.Empty; }
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            return available;
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (surface == null) throw new ArgumentNullException(nameof(surface));

            int width = surface.Size.Width;
            if (width < 2)
                return;

            surface.Fill(new Rect(0, 0, width, surface.Size.Height), Cell.Blank(CellStyle.Default));
            Draw.Text(surface, 0, 0, _Hints, CellStyle.Default.WithForeground(DocmonPalette.Dim), width);

            if (_Message.Length > 0)
            {
                int messageWidth = Math.Min(_Message.Length, Math.Max(0, width - _Hints.Length - 2));
                if (messageWidth > 0)
                {
                    int x = width - messageWidth;
                    Draw.Text(surface, x, 0, _Message, CellStyle.Default.WithForeground(DocmonPalette.Accent2), messageWidth);
                }
            }
        }
    }
}
