namespace Docmon.App.Widgets
{
    using System;
    using Docmon.App.Rendering;
    using Docmon.App.Theming;
    using TUIKit;
    using TUIKit.Widgets;

    /// <summary>
    /// A single-row tab bar showing the top-level screens, with the active tab highlighted. Navigation
    /// is driven externally (number keys and Tab/Shift+Tab); this widget only reflects the active index.
    /// </summary>
    public sealed class TabBar : IWidget
    {
        private readonly string[] _Tabs;
        private int _ActiveIndex;

        /// <summary>
        /// Initializes a new instance of the <see cref="TabBar"/> class.
        /// </summary>
        /// <param name="tabs">The tab labels in order. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tabs"/> is null.</exception>
        public TabBar(string[] tabs)
        {
            _Tabs = tabs ?? throw new ArgumentNullException(nameof(tabs));
        }

        /// <summary>
        /// Gets or sets the active tab index. Values are clamped to the available tabs.
        /// </summary>
        public int ActiveIndex
        {
            get { return _ActiveIndex; }
            set { _ActiveIndex = Math.Clamp(value, 0, Math.Max(0, _Tabs.Length - 1)); }
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

            CellStyle active = new CellStyle(DocmonPalette.SelectionForeground, DocmonPalette.Accent).WithAttribute(CellAttributes.Bold, true);
            CellStyle inactive = CellStyle.Default.WithForeground(DocmonPalette.Muted);
            CellStyle separator = CellStyle.Default.WithForeground(DocmonPalette.Dim);

            int x = 0;
            for (int i = 0; i < _Tabs.Length; i++)
            {
                string label = " " + (i + 1) + " " + _Tabs[i] + " ";
                CellStyle style = i == _ActiveIndex ? active : inactive;
                if (x + label.Length > width)
                    break;

                surface.DrawText(x, 0, label, style);
                x += label.Length;

                if (i < _Tabs.Length - 1 && x < width)
                {
                    surface.Set(x, 0, Cell.Glyph("│", separator, 1));
                    x += 1;
                }
            }
        }
    }
}
