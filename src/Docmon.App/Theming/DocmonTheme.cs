namespace Docmon.App.Theming
{
    using TUIKit;
    using TUIKit.Theming;

    /// <summary>
    /// Builds the Docmon theme. The background is left at the terminal default so Docmon blends into the
    /// user's terminal rather than painting its own backdrop.
    /// </summary>
    public static class DocmonTheme
    {
        /// <summary>
        /// Creates the Docmon theme.
        /// </summary>
        /// <returns>The theme.</returns>
        public static Theme Create()
        {
            CellStyle text = CellStyle.Default.WithForeground(DocmonPalette.Text);
            CellStyle accent = CellStyle.Default.WithForeground(DocmonPalette.Accent);
            CellStyle border = CellStyle.Default.WithForeground(DocmonPalette.Dim);
            CellStyle muted = CellStyle.Default.WithForeground(DocmonPalette.Muted);

            return new Theme(
                "docmon",
                text,
                accent,
                border,
                muted,
                false,
                CellStyle.Default.WithForeground(DocmonPalette.Success),
                CellStyle.Default.WithForeground(DocmonPalette.Warning),
                CellStyle.Default.WithForeground(DocmonPalette.Error),
                CellStyle.Default.WithForeground(DocmonPalette.Accent2),
                new CellStyle(DocmonPalette.SelectionForeground, DocmonPalette.Accent),
                muted.WithAttribute(CellAttributes.Dim, true));
        }
    }
}
