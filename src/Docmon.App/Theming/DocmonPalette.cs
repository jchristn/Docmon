namespace Docmon.App.Theming
{
    using TUIKit;

    /// <summary>
    /// Navy and Silver colors shared with the dashboard's dark theme. Truecolor terminals display
    /// the exact RGB values; TUIKit approximates them on terminals with a smaller color palette.
    /// </summary>
    public static class DocmonPalette
    {
        /// <summary>Page background, matching dashboard --bg.</summary>
        public static readonly Color Background = Color.FromRgb(0x15, 0x1E, 0x28);

        /// <summary>Header, footer, and dialog surface, matching dashboard --surface.</summary>
        public static readonly Color Surface = Color.FromRgb(0x20, 0x2D, 0x3B);

        /// <summary>Secondary surface, matching dashboard --surface2.</summary>
        public static readonly Color Surface2 = Color.FromRgb(0x29, 0x39, 0x4A);

        /// <summary>Border color, matching dashboard --border.</summary>
        public static readonly Color Border = Color.FromRgb(0x3C, 0x4D, 0x5D);

        /// <summary>Blue accent for titles, links, charts, and active controls, matching --accent.</summary>
        public static readonly Color Accent = Color.FromRgb(0x83, 0xBD, 0xE4);

        /// <summary>Secondary charts and links use the same blue accent as the dashboard.</summary>
        public static readonly Color Accent2 = Accent;

        /// <summary>Body text, matching dashboard --text.</summary>
        public static readonly Color Text = Color.FromRgb(0xED, 0xF2, 0xF6);

        /// <summary>Secondary text, matching dashboard --muted.</summary>
        public static readonly Color Muted = Color.FromRgb(0xB1, 0xBF, 0xCC);

        /// <summary>Readable silver text for hints and inactive elements.</summary>
        public static readonly Color Dim = Muted;

        /// <summary>Running and successful states use the dashboard's blue accent.</summary>
        public static readonly Color Success = Accent;

        /// <summary>Paused and attention states use the dashboard's neutral silver.</summary>
        public static readonly Color Warning = Muted;

        /// <summary>Error and unhealthy states, matching dashboard --danger.</summary>
        public static readonly Color Error = Color.FromRgb(0xFF, 0x81, 0x84);

        /// <summary>Text on blue selections, matching dashboard --accent-text.</summary>
        public static readonly Color SelectionForeground = Color.FromRgb(0x17, 0x2C, 0x40);

        /// <summary>Default drawing style with an explicit navy background.</summary>
        public static readonly CellStyle BaseStyle = new CellStyle(Text, Background);

        /// <summary>Drawing style for headers, footers, tabs, and dialogs.</summary>
        public static readonly CellStyle SurfaceStyle = new CellStyle(Text, Surface);
    }
}
