namespace Docmon.App.Screens
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using Docmon.App.Monitoring;
    using Docmon.App.Rendering;
    using Docmon.App.Theming;
    using Docmon.Core.Helpers;
    using Docmon.Core.Models;
    using TUIKit;
    using TUIKit.Input;

    /// <summary>
    /// Full-width dashboard-equivalent metric tabs for all Docker workloads or any container.
    /// Accessed only on the UI thread. Arrow keys select charts and brackets select the scope.
    /// </summary>
    public sealed class MetricsScreen : DocmonScreen
    {
        private static readonly string[] _Titles =
        {
            "CPU %", "Memory", "Network received", "Network transmitted", "Block read", "Block written"
        };
        private static readonly string[] _Timeframes = { "live", "hour", "day", "week", "month" };
        private static readonly string[] _TimeframeLabels = { "Near real time", "Last hour", "Last day", "Last week", "Last month" };
        private int _Timeframe;
        private IReadOnlyList<MetricHistoryBucket>? _Historical;
        private string? _HistoryError;
        private StatsHistory? _History;
        private IReadOnlyList<ContainerInfo> _Containers = Array.Empty<ContainerInfo>();
        private string _TargetId = string.Empty;
        private string _SelectedId = string.Empty;
        private int _Chart;
        private int _SampleOffset;

        /// <inheritdoc/>
        public override string KeyHints
        {
            get { return "←/→ chart · [ previous · ] next scope · t/T timeframe · ↑/↓ sample · End latest · o host/selected"; }
        }

        /// <summary>Gets the selected history window, or live for near real time.</summary>
        public string Timeframe => _Timeframes[_Timeframe];

        /// <summary>Gets the selected container ID, or empty for host Docker workloads.</summary>
        public string TargetId => _TargetId;

        /// <summary>Applies a history response only if its scope still matches the current selection.</summary>
        /// <param name="timeframe">Requested timeframe.</param>
        /// <param name="containerId">Requested scope.</param>
        /// <param name="buckets">Chronological buckets, or null on error.</param>
        /// <param name="error">Optional load error.</param>
        public void SetHistoricalData(string timeframe, string containerId, IReadOnlyList<MetricHistoryBucket>? buckets, string? error = null)
        {
            if (timeframe != Timeframe || containerId != _TargetId) return;
            _Historical = buckets;
            _HistoryError = error;
        }

        /// <summary>Updates history and the container selected in the Containers screen.</summary>
        /// <param name="history">The history, required.</param>
        /// <param name="targetId">Selected container ID, or empty.</param>
        /// <param name="targetName">Selected container name, or empty.</param>
        public void SetData(StatsHistory history, string targetId, string targetName)
        {
            _History = history ?? throw new ArgumentNullException(nameof(history));
            _SelectedId = targetId ?? string.Empty;
            if (_Containers.Count == 0 && _SelectedId.Length > 0)
                _Containers = new[] { new ContainerInfo { Id = _SelectedId, Name = targetName ?? string.Empty } };
        }

        /// <summary>Updates selectable containers without changing the chosen scope on refresh.</summary>
        /// <param name="containers">All containers, including stopped containers. Required.</param>
        public void SetContainers(IReadOnlyList<ContainerInfo> containers)
        {
            _Containers = containers ?? throw new ArgumentNullException(nameof(containers));
            if (_TargetId.Length > 0 && TargetIndex() < 0)
            {
                _TargetId = string.Empty;
                ResetHistory();
            }
        }

        /// <inheritdoc/>
        public override bool HandleKey(KeyEvent key)
        {
            if (key.Code == KeyCode.Left || key.Code == KeyCode.Right)
            {
                _Chart = (_Chart + (key.Code == KeyCode.Right ? 1 : _Titles.Length - 1)) % _Titles.Length;
                return true;
            }
            if (key.Code == KeyCode.Up || key.Code == KeyCode.Down)
            {
                int count = Timeframe == "live" ? CurrentHistory()?.Cpu.Snapshot().Count ?? 0 : _Historical?.Count ?? 0;
                _SampleOffset = Math.Clamp(_SampleOffset + (key.Code == KeyCode.Up ? 1 : -1), 0, Math.Max(0, count - 1));
                return true;
            }
            if (key.Code == KeyCode.End)
            {
                _SampleOffset = 0;
                return true;
            }
            if (key.Code != KeyCode.Character) return false;
            if (key.Rune == 't' || key.Rune == 'T')
            {
                _Timeframe = (_Timeframe + (key.Rune == 't' ? 1 : _Timeframes.Length - 1)) % _Timeframes.Length;
                ResetHistory();
                return true;
            }
            if (key.Rune == '[' || key.Rune == ']')
            {
                int index = TargetIndex() + 1;
                int next = (index + (key.Rune != '[' ? 1 : _Containers.Count)) % (_Containers.Count + 1);
                _TargetId = next == 0 ? string.Empty : _Containers[next - 1].Id;
                ResetHistory();
                return true;
            }
            if (key.Rune == 'o' || key.Rune == 'O')
            {
                _TargetId = _TargetId.Length == 0 ? _SelectedId : string.Empty;
                ResetHistory();
                return true;
            }
            return false;
        }

        /// <inheritdoc/>
        public override void Render(ISurface surface)
        {
            ArgumentNullException.ThrowIfNull(surface);
            int width = surface.Size.Width;
            int height = surface.Size.Height;
            if (width < 8 || height < 6 || _History == null) return;

            int targetIndex = TargetIndex();
            string scope = _TargetId.Length == 0 ? "Host: all Docker workloads" :
                targetIndex >= 0 ? _Containers[targetIndex].Name : _TargetId;
            CellStyle muted = DocmonPalette.BaseStyle.WithForeground(DocmonPalette.Muted);
            CellStyle accent = DocmonPalette.BaseStyle.WithForeground(DocmonPalette.Accent);
            Draw.Text(surface, 0, 0, "Scope: " + scope + "  ([ previous, ] next)", muted, width);
            Draw.Text(surface, 0, 1, "Window: " + _TimeframeLabels[_Timeframe] + "  (t/T change) · Host = Docker workloads", muted, width);
            Draw.Text(surface, 0, 2, (_Chart + 1) + "/6  < " + _Titles[_Chart] + " >", accent, width);

            ContainerHistory? history = CurrentHistory();
            bool historical = Timeframe != "live";
            if (historical && _Historical == null)
            {
                Draw.Text(surface, 0, 4, _HistoryError ?? "Loading historical metrics...", muted, width);
                return;
            }
            if (!historical && (history == null || history.Cpu.Snapshot().Count == 0))
            {
                Draw.Text(surface, 0, 4, "No live data for this scope. Use [ for previous scope or ] for next.", muted, width);
                return;
            }
            List<double> values = new List<double>();
            List<double> times = new List<double>();
            List<bool> available = new List<bool>();
            if (historical)
            {
                foreach (MetricHistoryBucket bucket in _Historical!)
                {
                    values.Add(Value(bucket));
                    times.Add(bucket.AtUtc.Ticks);
                    available.Add(bucket.HasData);
                }
            }
            else
            {
                values.AddRange(Series(history!).Snapshot());
                times.AddRange(history!.SampleTimes.Snapshot());
                foreach (double value in values) available.Add(true);
            }
            if (values.Count == 0) return;
            int selected = Math.Max(0, values.Count - 1 - _SampleOffset);
            string unit = _Chart == 0 ? "%" : "bytes";
            Rect inner = Draw.Panel(surface, new Rect(0, 3, width, height - 3), _Titles[_Chart] + " (" + unit + ")", muted);
            if (inner.Width < 18 || inner.Height < 5) return;
            Draw.Text(surface, inner.X, inner.Y, Time(times[selected]) + (historical ? " to " + Time(_Historical![selected].EndUtc.Ticks) : "") + " UTC  " + (available[selected] ? Format(values[selected]) : "No observations") +
                (_SampleOffset == 0 ? historical ? "  latest bucket" : "  live" : "  sample detail"), accent, inner.Width);
            int axisWidth = Math.Min(13, inner.Width / 3);
            Rect plot = new Rect(inner.X + axisWidth, inner.Y + 1, inner.Width - axisWidth, inner.Height - 3);
            double maximum = _Chart == 0 ? 100 : 1;
            foreach (double value in values) maximum = Math.Max(maximum, value);
            Draw.Text(surface, inner.X, plot.Y, Format(maximum), muted, axisWidth);
            Draw.Text(surface, inner.X, plot.Y + plot.Height - 1, "0 " + unit, muted, axisWidth);
            // Resample the entire retained window, so narrow terminals do not discard history.
            for (int column = 0; column < plot.Width; column++)
            {
                int index = (int)Math.Round(column * (values.Count - 1.0) / Math.Max(1, plot.Width - 1));
                if (available[index])
                    Draw.AreaChart(surface, new Rect(plot.X + column, plot.Y, 1, plot.Height), new[] { values[index] }, maximum, accent);
            }
            int cursorX = plot.X + (int)Math.Round(selected * (plot.Width - 1.0) / Math.Max(1, values.Count - 1));
            surface.Set(cursorX, plot.Y + plot.Height - 1, Cell.Glyph("▲", muted, 1));
            Draw.Text(surface, plot.X, plot.Y + plot.Height, Time(times[0]), muted, plot.Width);
            string latest = Time(historical ? _Historical![_Historical.Count - 1].EndUtc.Ticks : times[times.Count - 1]);
            if (plot.Width >= latest.Length * 2 + 1)
                Draw.Text(surface, plot.X + plot.Width - latest.Length, plot.Y + plot.Height, latest, muted, latest.Length);
            Draw.Text(surface, plot.X, plot.Y + plot.Height + 1, "Time (UTC) · ↑ older / ↓ newer", muted, plot.Width);
        }

        private void ResetHistory()
        {
            _Historical = null;
            _HistoryError = null;
            _SampleOffset = 0;
        }

        private double Value(ContainerStatsSample sample)
        {
            return _Chart switch
            {
                1 => sample.MemoryUsage,
                2 => sample.NetworkRxBytes,
                3 => sample.NetworkTxBytes,
                4 => sample.BlockReadBytes,
                5 => sample.BlockWriteBytes,
                _ => sample.CpuPercent
            };
        }

        private int TargetIndex()
        {
            for (int i = 0; i < _Containers.Count; i++)
                if (_Containers[i].Id == _TargetId) return i;
            return -1;
        }

        private ContainerHistory? CurrentHistory()
        {
            return _TargetId.Length == 0 ? _History?.Overall : _History?.Get(_TargetId);
        }

        private MetricSeries Series(ContainerHistory history)
        {
            switch (_Chart)
            {
                case 1: return history.MemoryUsage;
                case 2: return history.NetworkRxBytes;
                case 3: return history.NetworkTxBytes;
                case 4: return history.BlockReadBytes;
                case 5: return history.BlockWriteBytes;
                default: return history.Cpu;
            }
        }

        private string Format(double value)
        {
            return _Chart == 0 ? value.ToString("0.0", CultureInfo.InvariantCulture) + "%" : ByteFormatter.Format((long)value);
        }

        private string Time(double ticks)
        {
            return new DateTime((long)ticks, DateTimeKind.Utc).ToString(Timeframe == "live" || Timeframe == "hour" ? "HH:mm:ss" : "MM-dd HH:mm", CultureInfo.InvariantCulture);
        }
    }
}
