namespace Docmon.App.Theming
{
    using TUIKit;

    /// <summary>
    /// The named colors Docmon draws with. Truecolor values are used so the interface looks consistent
    /// on terminals that support 24-bit color; TUIKit degrades them to the 256-color palette elsewhere.
    /// </summary>
    public static class DocmonPalette
    {
        /// <summary>Primary accent (cyan) used for the logo, titles, and the active tab.</summary>
        public static readonly Color Accent = Color.FromRgb(0x22, 0xD3, 0xEE);

        /// <summary>Secondary accent (sky blue) used for links and secondary chart series.</summary>
        public static readonly Color Accent2 = Color.FromRgb(0x38, 0xBD, 0xF8);

        /// <summary>Default body text color.</summary>
        public static readonly Color Text = Color.FromRgb(0xD0, 0xD4, 0xDC);

        /// <summary>Muted text color for secondary detail.</summary>
        public static readonly Color Muted = Color.FromRgb(0x94, 0xA3, 0xB8);

        /// <summary>Dim color for borders, hints, and inactive elements.</summary>
        public static readonly Color Dim = Color.FromRgb(0x5B, 0x63, 0x74);

        /// <summary>Success/running color (green).</summary>
        public static readonly Color Success = Color.FromRgb(0x22, 0xC5, 0x5E);

        /// <summary>Warning color (amber) for paused/updates.</summary>
        public static readonly Color Warning = Color.FromRgb(0xEA, 0xB3, 0x08);

        /// <summary>Error color (red) for exited/failed/unhealthy.</summary>
        public static readonly Color Error = Color.FromRgb(0xEF, 0x44, 0x44);

        /// <summary>Foreground used on the selection highlight bar.</summary>
        public static readonly Color SelectionForeground = Color.FromRgb(0x0B, 0x10, 0x20);
    }
}
