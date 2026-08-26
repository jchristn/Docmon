namespace Docmon.App.Widgets
{
    using System;
    using Docmon.App.Rendering;
    using Docmon.App.Theming;
    using TUIKit;
    using TUIKit.Widgets;

    /// <summary>
    /// The persistent header: the FIGlet "docmon" wordmark on the left, the tagline and project link
    /// beside it, and a right-aligned host summary the controller keeps current.
    /// </summary>
    public sealed class HeaderBanner : IWidget
    {
        private readonly string[] _LogoRows;
        private readonly int _LogoWidth;
        private string _HostSummary = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="HeaderBanner"/> class.
        /// </summary>
        /// <param name="logoRows">The pre-rendered wordmark rows. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="logoRows"/> is null.</exception>
        public HeaderBanner(string[] logoRows)
        {
            _LogoRows = logoRows ?? throw new ArgumentNullException(nameof(logoRows));

            int width = 0;
            foreach (string row in _LogoRows)
                width = Math.Max(width, row.Length);
            _LogoWidth = width;
        }

        /// <summary>
        /// Gets or sets the right-aligned host summary line (endpoint and container counts).
        /// </summary>
        public string HostSummary
        {
            get { return _HostSummary; }
            set { _HostSummary = value ?? string.Empty; }
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
            int height = surface.Size.Height;
            if (width < 2 || height < 1)
                return;

            surface.Fill(new Rect(0, 0, width, height), Cell.Blank(CellStyle.Default));

            CellStyle logoStyle = CellStyle.Default.WithForeground(DocmonPalette.Accent).WithAttribute(CellAttributes.Bold, true);
            for (int i = 0; i < _LogoRows.Length && i < height; i++)
                Draw.Text(surface, 0, i, _LogoRows[i], logoStyle, width);

            int textX = _LogoWidth + 3;
            int available = width - textX;
            if (available <= 4)
                return;

            int middle = Math.Max(0, (_LogoRows.Length - 1) / 2);
            Draw.Text(surface, textX, middle, DocmonBanner.Tagline, CellStyle.Default.WithForeground(DocmonPalette.Muted), available);
            if (middle + 1 < height)
                Draw.Text(surface, textX, middle + 1, DocmonBanner.ProjectUrl, CellStyle.Default.WithForeground(DocmonPalette.Accent2).WithAttribute(CellAttributes.Underline, true), available);

            if (_HostSummary.Length > 0)
            {
                int summaryWidth = Math.Min(_HostSummary.Length, available);
                int summaryX = width - summaryWidth;
                Draw.Text(surface, summaryX, 0, _HostSummary, CellStyle.Default.WithForeground(DocmonPalette.Success), summaryWidth);
            }
        }
    }
}
