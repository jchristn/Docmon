namespace Docmon.App.Theming
{
    using TUIKit;
    using TUIKit.Theming;

    /// <summary>
    /// Builds the Navy and Silver theme using the dashboard's dark colors and an explicit navy backdrop.
    /// </summary>
    public static class DocmonTheme
    {
        /// <summary>
        /// Creates the Docmon theme.
        /// </summary>
        /// <returns>The theme.</returns>
        public static Theme Create()
        {
            CellStyle text = DocmonPalette.BaseStyle.WithForeground(DocmonPalette.Text);
            CellStyle accent = DocmonPalette.BaseStyle.WithForeground(DocmonPalette.Accent);
            CellStyle border = DocmonPalette.BaseStyle.WithForeground(DocmonPalette.Border);
            CellStyle muted = DocmonPalette.BaseStyle.WithForeground(DocmonPalette.Muted);

            return new Theme(
                "docmon",
                text,
                accent,
                border,
                muted,
                false,
                DocmonPalette.BaseStyle.WithForeground(DocmonPalette.Success),
                DocmonPalette.BaseStyle.WithForeground(DocmonPalette.Warning),
                DocmonPalette.BaseStyle.WithForeground(DocmonPalette.Error),
                DocmonPalette.BaseStyle.WithForeground(DocmonPalette.Accent2),
                new CellStyle(DocmonPalette.SelectionForeground, DocmonPalette.Accent),
                muted.WithAttribute(CellAttributes.Dim, true));
        }
    }
}
