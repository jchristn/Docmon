namespace Docmon.App.Screens
{
    using System;
    using System.Collections.Generic;
    using Docmon.App.Monitoring;
    using Docmon.App.Rendering;
    using Docmon.App.Theming;
    using Docmon.Core.Helpers;
    using TUIKit;
    using TUIKit.Input;

    /// <summary>
    /// Full-size performance charts for the whole deployment or a single container: CPU, memory,
    /// network, and block I/O plotted over time in a two-by-two grid.
    /// </summary>
    public sealed class MetricsScreen : DocmonScreen
    {
        private StatsHistory? _History;
        private string _TargetId = string.Empty;
        private string _TargetName = string.Empty;
        private bool _ShowOverall = true;

        /// <inheritdoc/>
        public override string KeyHints
        {
            get { return "o toggle overall / selected container   ·   charts update live"; }
        }

        /// <summary>
        /// Updates the data the charts render from.
        /// </summary>
        /// <param name="history">The stats history. Must not be null.</param>
        /// <param name="targetId">The selected container ID, or an empty string.</param>
        /// <param name="targetName">The selected container name, or an empty string.</param>
        public void SetData(StatsHistory history, string targetId, string targetName)
        {
            _History = history ?? throw new ArgumentNullException(nameof(history));
            _TargetId = targetId ?? string.Empty;
            _TargetName = targetName ?? string.Empty;
        }

        /// <inheritdoc/>
        public override bool HandleKey(KeyEvent key)
        {
            if (key.Code == KeyCode.Character && (key.Rune == 'o' || key.Rune == 'O'))
            {
                _ShowOverall = !_ShowOverall;
                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        public override void Render(ISurface surface)
        {
            if (surface == null) throw new ArgumentNullException(nameof(surface));
            if (_History == null)
                return;

            int width = surface.Size.Width;
            int height = surface.Size.Height;
            if (width < 8 || height < 6)
                return;

            bool overall = _ShowOverall || string.IsNullOrEmpty(_TargetId);
            ContainerHistory? container = overall ? null : _History.Get(_TargetId);

            string scope = overall ? "overall deployment" : (_TargetName.Length > 0 ? _TargetName : "container");
            string modeLine = "Scope: " + scope + "   (press 'o' to toggle)";
            Draw.Text(surface, 0, 0, modeLine, CellStyle.Default.WithForeground(DocmonPalette.Muted), width);

            int gridY = 1;
            int gridH = height - 1;
            int halfW = width / 2;
            int halfH = gridH / 2;

            Rect cpuRect = new Rect(0, gridY, halfW, halfH);
            Rect memRect = new Rect(halfW, gridY, width - halfW, halfH);
            Rect netRect = new Rect(0, gridY + halfH, halfW, gridH - halfH);
            Rect diskRect = new Rect(halfW, gridY + halfH, width - halfW, gridH - halfH);

            if (overall)
            {
                DrawArea(surface, cpuRect, "CPU %", _History.OverallCpu, Math.Max(100.0, _History.OverallCpu.Max), _History.OverallCpu.Latest.ToString("0.0", Culture()) + "%", DocmonPalette.Accent);
                DrawArea(surface, memRect, "Memory", _History.OverallMemory, _History.OverallMemory.Max, ByteFormatter.Format((long)_History.OverallMemory.Latest), DocmonPalette.Accent2);
                DrawArea(surface, netRect, "Network", _History.OverallNetRate, _History.OverallNetRate.Max, ByteFormatter.FormatRate(_History.OverallNetRate.Latest), DocmonPalette.Success);
                DrawArea(surface, diskRect, "Block I/O", _History.OverallDiskRate, _History.OverallDiskRate.Max, ByteFormatter.FormatRate(_History.OverallDiskRate.Latest), DocmonPalette.Warning);
            }
            else if (container != null)
            {
                double memMax = container.MemoryLimit > 0 ? container.MemoryLimit : container.MemoryUsage.Max;
                DrawArea(surface, cpuRect, "CPU %", container.Cpu, Math.Max(100.0, container.Cpu.Max), container.Cpu.Latest.ToString("0.0", Culture()) + "%", DocmonPalette.Accent);
                DrawArea(surface, memRect, "Memory", container.MemoryUsage, memMax, ByteFormatter.Format((long)container.MemoryUsage.Latest) + " / " + ByteFormatter.Format(container.MemoryLimit), DocmonPalette.Accent2);
                DrawDualNet(surface, netRect, container);
                DrawArea(surface, diskRect, "Block I/O", container.DiskRate, container.DiskRate.Max, ByteFormatter.FormatRate(container.DiskRate.Latest), DocmonPalette.Warning);
            }
            else
            {
                Draw.Text(surface, 0, gridY + 1, "No live data for the selected container.", CellStyle.Default.WithForeground(DocmonPalette.Dim), width);
            }
        }

        private static void DrawArea(ISurface surface, Rect rect, string title, MetricSeries series, double max, string valueLabel, Color color)
        {
            Rect inner = Draw.Panel(surface, rect, title, CellStyle.Default.WithForeground(DocmonPalette.Dim));
            if (inner.Width <= 0 || inner.Height <= 0)
                return;

            string label = valueLabel ?? string.Empty;
            int labelX = inner.X + Math.Max(0, inner.Width - label.Length);
            Draw.Text(surface, labelX, inner.Y, label, CellStyle.Default.WithForeground(color).WithAttribute(CellAttributes.Bold, true), label.Length);

            Rect plot = new Rect(inner.X, inner.Y + 1, inner.Width, Math.Max(1, inner.Height - 1));
            Draw.AreaChart(surface, plot, series.Snapshot(), max, CellStyle.Default.WithForeground(color));
        }

        private static void DrawDualNet(ISurface surface, Rect rect, ContainerHistory container)
        {
            Rect inner = Draw.Panel(surface, rect, "Network ↓rx ↑tx", CellStyle.Default.WithForeground(DocmonPalette.Dim));
            if (inner.Width <= 0 || inner.Height <= 0)
                return;

            string label = "↓" + ByteFormatter.FormatRate(container.NetRxRate.Latest) + " ↑" + ByteFormatter.FormatRate(container.NetTxRate.Latest);
            int labelX = inner.X + Math.Max(0, inner.Width - label.Length);
            Draw.Text(surface, labelX, inner.Y, label, CellStyle.Default.WithForeground(DocmonPalette.Success).WithAttribute(CellAttributes.Bold, true), label.Length);

            Rect plot = new Rect(inner.X, inner.Y + 1, inner.Width, Math.Max(1, inner.Height - 1));
            double max = Math.Max(container.NetRxRate.Max, container.NetTxRate.Max);
            Draw.AreaChart(surface, plot, container.NetRxRate.Snapshot(), max, CellStyle.Default.WithForeground(DocmonPalette.Success));
            Draw.LineSeries(surface, plot, container.NetTxRate.Snapshot(), max, CellStyle.Default.WithForeground(DocmonPalette.Warning), "•");
        }

        private static System.Globalization.CultureInfo Culture()
        {
            return System.Globalization.CultureInfo.InvariantCulture;
        }
    }
}
