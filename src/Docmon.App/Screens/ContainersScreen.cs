namespace Docmon.App.Screens
{
    using System;
    using System.Collections.Generic;
    using Docmon.App.Monitoring;
    using Docmon.App.Rendering;
    using Docmon.App.Theming;
    using Docmon.Core.Enums;
    using Docmon.Core.Helpers;
    using Docmon.Core.Models;
    using TUIKit;
    using TUIKit.Input;

    /// <summary>
    /// The default screen: a sortable container table on the left, a live detail panel with inline CPU
    /// and memory charts on the right, and a deployment-wide host strip along the bottom.
    /// </summary>
    public sealed class ContainersScreen : DocmonScreen
    {
        private static readonly string[] _Headers = { "NAME", "IMAGE", "ID", "STATE", "CPU", "MEM" };
        private static readonly int[] _Weights = { 6, 7, 4, 4, 3, 4 };

        private readonly TableState _Table = new TableState();
        private IReadOnlyList<ContainerInfo> _Containers = new List<ContainerInfo>();
        private StatsHistory? _History;

        /// <inheritdoc/>
        public override string KeyHints
        {
            get
            {
                return "↑↓ select · Enter inspect · s shell · x exec · l logs · r restart · S stop · K kill · p pause · t transfer · u check · U pull+apply · d remove";
            }
        }

        /// <inheritdoc/>
        public override object? SelectedTag
        {
            get { return _Table.SelectedTag; }
        }

        /// <summary>
        /// Updates the container list and the stats history used for the detail charts.
        /// </summary>
        /// <param name="containers">The containers. Must not be null.</param>
        /// <param name="history">The stats history. Must not be null.</param>
        public void SetContainers(IReadOnlyList<ContainerInfo> containers, StatsHistory history)
        {
            _Containers = containers ?? throw new ArgumentNullException(nameof(containers));
            _History = history ?? throw new ArgumentNullException(nameof(history));

            List<RowItem> rows = new List<RowItem>(containers.Count);
            foreach (ContainerInfo container in containers)
            {
                string cpu = "-";
                string mem = "-";
                ContainerHistory? containerHistory = container.IsRunning ? history.Get(container.Id) : null;
                if (containerHistory != null)
                {
                    cpu = containerHistory.Cpu.Latest.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%";
                    mem = ByteFormatter.Format(container.IsRunning ? (long)containerHistory.MemoryUsage.Latest : 0);
                }

                string[] cells =
                {
                    container.Name,
                    container.Image,
                    container.ShortId,
                    StateLabel(container),
                    cpu,
                    mem
                };
                rows.Add(new RowItem(cells, container, StatusColors.ForState(container.State)));
            }

            _Table.SetRows(rows);
        }

        /// <inheritdoc/>
        public override bool HandleKey(KeyEvent key)
        {
            return _Table.HandleKey(key);
        }

        /// <inheritdoc/>
        public override void Render(ISurface surface)
        {
            if (surface == null) throw new ArgumentNullException(nameof(surface));

            int width = surface.Size.Width;
            int height = surface.Size.Height;
            if (width < 8 || height < 6)
                return;

            int hostHeight = 3;
            int topHeight = height - hostHeight;
            int leftWidth = Math.Max(34, width * 55 / 100);

            CellStyle tableBorder = CellStyle.Default.WithForeground(Focused ? DocmonPalette.Accent : DocmonPalette.Dim);
            CellStyle detailBorder = CellStyle.Default.WithForeground(DocmonPalette.Dim);

            Rect tableInner = Draw.Panel(surface, new Rect(0, 0, leftWidth, topHeight), "Containers (" + _Containers.Count + ")", tableBorder);
            _Table.Render(surface, tableInner, _Headers, _Weights, Focused, "No containers found.");

            Rect detailInner = Draw.Panel(surface, new Rect(leftWidth, 0, width - leftWidth, topHeight), "Detail", detailBorder);
            RenderDetail(surface, detailInner);

            Rect hostInner = Draw.Panel(surface, new Rect(0, topHeight, width, hostHeight), "Host", detailBorder);
            RenderHostStrip(surface, hostInner);
        }

        private void RenderDetail(ISurface surface, Rect area)
        {
            if (area.Width <= 2 || area.Height <= 1)
                return;

            ContainerInfo? container = _Table.SelectedTag as ContainerInfo;
            if (container == null)
            {
                Draw.Text(surface, area.X, area.Y, "Select a container.", CellStyle.Default.WithForeground(DocmonPalette.Dim), area.Width);
                return;
            }

            CellStyle label = CellStyle.Default.WithForeground(DocmonPalette.Muted);
            CellStyle value = CellStyle.Default.WithForeground(DocmonPalette.Text);
            int x = area.X;
            int y = area.Y;
            int right = area.Width;

            DrawField(surface, x, ref y, right, "Name", container.Name, label, value);
            DrawField(surface, x, ref y, right, "Image", container.Image, label, value);
            DrawField(surface, x, ref y, right, "ID", container.ShortId, label, value);
            DrawField(surface, x, ref y, right, "State", container.State.ToString(), label, CellStyle.Default.WithForeground(StatusColors.ForState(container.State)));
            if (container.Health.Length > 0)
                DrawField(surface, x, ref y, right, "Health", container.Health, label, value);
            DrawField(surface, x, ref y, right, "Status", container.Status, label, value);

            if (container.Ports.Count > 0)
            {
                System.Text.StringBuilder ports = new System.Text.StringBuilder();
                foreach (PortMap port in container.Ports)
                {
                    if (ports.Length > 0)
                        ports.Append(", ");
                    ports.Append(port.ToString());
                }
                DrawField(surface, x, ref y, right, "Ports", ports.ToString(), label, value);
            }

            if (container.ComposeProject.Length > 0)
                DrawField(surface, x, ref y, right, "Stack", container.ComposeProject + " / " + container.ComposeService, label, value);

            ContainerHistory? history = _History?.Get(container.Id);
            if (history != null && container.IsRunning && y + 4 < area.Y + area.Height)
            {
                y += 1;
                double cpu = history.Cpu.Latest;
                Draw.Text(surface, x, y, "CPU  " + cpu.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%", label, right);
                y += 1;
                string spark = Draw.Sparkline(history.Cpu.Snapshot(), Math.Min(right, 40), Math.Max(100.0, history.Cpu.Max));
                Draw.Text(surface, x, y, spark, CellStyle.Default.WithForeground(DocmonPalette.Accent), right);
                y += 2;

                long memUsage = (long)history.MemoryUsage.Latest;
                long memLimit = history.MemoryLimit;
                double memFraction = memLimit > 0 ? (double)memUsage / memLimit : 0.0;
                Draw.Text(surface, x, y, "MEM  " + ByteFormatter.Format(memUsage) + " / " + ByteFormatter.Format(memLimit), label, right);
                y += 1;
                Draw.Bar(surface, x, y, Math.Min(right, 40), memFraction, CellStyle.Default.WithForeground(DocmonPalette.Accent2), CellStyle.Default.WithForeground(DocmonPalette.Dim));
            }
        }

        private void RenderHostStrip(ISurface surface, Rect area)
        {
            if (area.Width <= 2 || area.Height < 1 || _History == null)
                return;

            int running = 0;
            foreach (ContainerInfo container in _Containers)
            {
                if (container.IsRunning)
                    running++;
            }

            double cpu = _History.OverallCpu.Latest;
            double mem = _History.OverallMemory.Latest;
            double net = _History.OverallNetRate.Latest;
            double disk = _History.OverallDiskRate.Latest;

            string summary = "CPU " + cpu.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%"
                + "   MEM " + ByteFormatter.Format((long)mem)
                + "   NET " + ByteFormatter.FormatRate(net)
                + "   DISK " + ByteFormatter.FormatRate(disk)
                + "   " + running + "/" + _Containers.Count + " running";

            Draw.Text(surface, area.X, area.Y, summary, CellStyle.Default.WithForeground(DocmonPalette.Text), area.Width);
        }

        private static void DrawField(ISurface surface, int x, ref int y, int width, string name, string text, CellStyle labelStyle, CellStyle valueStyle)
        {
            Draw.Text(surface, x, y, name.PadRight(7), labelStyle, 7);
            Draw.Text(surface, x + 8, y, text, valueStyle, Math.Max(0, width - 8));
            y += 1;
        }

        private static string StateLabel(ContainerInfo container)
        {
            switch (container.State)
            {
                case ContainerStateEnum.Running: return container.Health.Length > 0 ? "up (" + container.Health + ")" : "up";
                case ContainerStateEnum.Exited: return "exited";
                case ContainerStateEnum.Paused: return "paused";
                case ContainerStateEnum.Restarting: return "restarting";
                case ContainerStateEnum.Created: return "created";
                case ContainerStateEnum.Dead: return "dead";
                default: return container.State.ToString().ToLowerInvariant();
            }
        }
    }
}
