namespace Docmon.App.Screens
{
    using System;
    using Docmon.App.Rendering;
    using Docmon.App.Theming;
    using Docmon.Core.Helpers;
    using Docmon.Core.Models;
    using TUIKit;

    /// <summary>
    /// Shows a roll-up of local Docker resource usage and offers housekeeping actions.
    /// </summary>
    public sealed class ToolsScreen : DocmonScreen
    {
        private SystemUsage _Usage = new SystemUsage();

        /// <inheritdoc/>
        public override string KeyHints
        {
            get { return "x prune dangling images"; }
        }

        /// <summary>
        /// Updates the usage roll-up shown.
        /// </summary>
        /// <param name="usage">The usage roll-up. Must not be null.</param>
        public void SetUsage(SystemUsage usage)
        {
            _Usage = usage ?? throw new ArgumentNullException(nameof(usage));
        }

        /// <inheritdoc/>
        public override void Render(ISurface surface)
        {
            if (surface == null) throw new ArgumentNullException(nameof(surface));

            int width = surface.Size.Width;
            int height = surface.Size.Height;
            if (width < 8 || height < 6)
                return;

            Rect inner = Draw.Panel(surface, new Rect(0, 0, width, height), "Tools · System usage", CellStyle.Default.WithForeground(DocmonPalette.Dim));
            if (inner.Width <= 2 || inner.Height <= 1)
                return;

            CellStyle label = CellStyle.Default.WithForeground(DocmonPalette.Muted);
            CellStyle value = CellStyle.Default.WithForeground(DocmonPalette.Text);
            int x = inner.X;
            int y = inner.Y;

            Field(surface, x, ref y, inner.Width, "Engine", _Usage.EngineVersion + " (" + _Usage.OperatingSystem + ")", label, value);
            Field(surface, x, ref y, inner.Width, "Containers", _Usage.RunningCount + " running / " + _Usage.ContainerCount + " total", label, value);
            Field(surface, x, ref y, inner.Width, "Images", _Usage.ImageCount + " (" + _Usage.DanglingImageCount + " dangling)", label, value);
            Field(surface, x, ref y, inner.Width, "Image size", ByteFormatter.Format(_Usage.ImageSizeBytes), label, value);
            Field(surface, x, ref y, inner.Width, "Volumes", _Usage.VolumeCount.ToString(System.Globalization.CultureInfo.InvariantCulture), label, value);

            y += 1;
            Draw.Text(surface, x, y, "Press 'x' to prune dangling images and reclaim space.", CellStyle.Default.WithForeground(DocmonPalette.Dim), inner.Width);
        }

        private static void Field(ISurface surface, int x, ref int y, int width, string name, string text, CellStyle labelStyle, CellStyle valueStyle)
        {
            Draw.Text(surface, x, y, name.PadRight(12), labelStyle, 12);
            Draw.Text(surface, x + 13, y, text, valueStyle, Math.Max(0, width - 13));
            y += 1;
        }
    }
}
