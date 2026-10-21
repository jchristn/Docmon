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

            surface.Fill(new Rect(0, 0, width, surface.Size.Height), Cell.Blank(DocmonPalette.SurfaceStyle));
            int messageWidth = _Message.Length == 0 ? 0 : Math.Min(_Message.Length, Math.Max(1, width * 2 / 3));
            int hintsWidth = messageWidth == 0 ? width : Math.Max(0, width - messageWidth - 2);
            Draw.Text(surface, 0, 0, _Hints, DocmonPalette.SurfaceStyle.WithForeground(DocmonPalette.Dim), hintsWidth);
            if (messageWidth > 0)
                Draw.Text(surface, width - messageWidth, 0, _Message, DocmonPalette.SurfaceStyle.WithForeground(DocmonPalette.Accent2), messageWidth);
        }
    }
}
