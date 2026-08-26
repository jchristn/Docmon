namespace Docmon.App.Rendering
{
    using System;
    using TUIKit;

    /// <summary>
    /// A single row in a <see cref="TableState"/>: its column cell texts, an optional foreground color
    /// override, and an opaque tag that lets callers recover the domain object the row represents.
    /// </summary>
    public sealed class RowItem
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RowItem"/> class.
        /// </summary>
        /// <param name="cells">The per-column cell texts. Must not be null.</param>
        /// <param name="tag">The domain object this row represents, or null.</param>
        /// <param name="color">An optional foreground color override for the row, or null for the default.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="cells"/> is null.</exception>
        public RowItem(string[] cells, object? tag, Color? color = null)
        {
            Cells = cells ?? throw new ArgumentNullException(nameof(cells));
            Tag = tag;
            Color = color;
        }

        /// <summary>
        /// Gets the per-column cell texts.
        /// </summary>
        public string[] Cells { get; }

        /// <summary>
        /// Gets the domain object this row represents, or null.
        /// </summary>
        public object? Tag { get; }

        /// <summary>
        /// Gets the optional foreground color override for the row.
        /// </summary>
        public Color? Color { get; }
    }
}
