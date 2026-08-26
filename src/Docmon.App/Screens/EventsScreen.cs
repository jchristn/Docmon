namespace Docmon.App.Screens
{
    using System;
    using System.Collections.Generic;
    using Docmon.App.Rendering;
    using Docmon.App.Theming;
    using Docmon.Core.Helpers;
    using Docmon.Core.Models;
    using TUIKit;

    /// <summary>
    /// A live, scrolling view of Docker daemon events, newest at the bottom.
    /// </summary>
    public sealed class EventsScreen : DocmonScreen
    {
        private readonly int _Capacity;
        private readonly LinkedList<DockerEventInfo> _Events = new LinkedList<DockerEventInfo>();

        /// <summary>
        /// Initializes a new instance of the <see cref="EventsScreen"/> class.
        /// </summary>
        /// <param name="capacity">The maximum number of events to retain. Values below one are treated as one.</param>
        public EventsScreen(int capacity)
        {
            _Capacity = Math.Max(1, capacity);
        }

        /// <inheritdoc/>
        public override string KeyHints
        {
            get { return "live daemon events (newest at bottom)"; }
        }

        /// <summary>
        /// Appends an event, discarding the oldest when at capacity.
        /// </summary>
        /// <param name="info">The event. Must not be null.</param>
        public void AddEvent(DockerEventInfo info)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));

            _Events.AddLast(info);
            while (_Events.Count > _Capacity)
                _Events.RemoveFirst();
        }

        /// <inheritdoc/>
        public override void Render(ISurface surface)
        {
            if (surface == null) throw new ArgumentNullException(nameof(surface));

            int width = surface.Size.Width;
            int height = surface.Size.Height;
            if (width < 8 || height < 3)
                return;

            Rect inner = Draw.Panel(surface, new Rect(0, 0, width, height), "Events (" + _Events.Count + ")", CellStyle.Default.WithForeground(DocmonPalette.Dim));
            if (inner.Height <= 0)
                return;

            int rows = inner.Height;
            int skip = Math.Max(0, _Events.Count - rows);

            int index = 0;
            int y = inner.Y;
            foreach (DockerEventInfo info in _Events)
            {
                if (index < skip)
                {
                    index++;
                    continue;
                }
                if (y >= inner.Y + inner.Height)
                    break;

                string line = TimeFormatter.FormatClock(info.TimeUtc) + "  " + info.Type.PadRight(9) + " " + info.Action.PadRight(14) + " " + info.Actor;
                Draw.Text(surface, inner.X, y, line, CellStyle.Default.WithForeground(ColorFor(info.Action)), inner.Width);
                y += 1;
                index++;
            }

            if (_Events.Count == 0)
                Draw.Text(surface, inner.X, inner.Y, "Waiting for events…", CellStyle.Default.WithForeground(DocmonPalette.Dim), inner.Width);
        }

        private static Color ColorFor(string action)
        {
            if (action.IndexOf("die", StringComparison.OrdinalIgnoreCase) >= 0 || action.IndexOf("kill", StringComparison.OrdinalIgnoreCase) >= 0 || action.IndexOf("oom", StringComparison.OrdinalIgnoreCase) >= 0)
                return DocmonPalette.Error;
            if (action.IndexOf("start", StringComparison.OrdinalIgnoreCase) >= 0 || action.IndexOf("create", StringComparison.OrdinalIgnoreCase) >= 0 || action.IndexOf("pull", StringComparison.OrdinalIgnoreCase) >= 0)
                return DocmonPalette.Success;
            if (action.IndexOf("stop", StringComparison.OrdinalIgnoreCase) >= 0 || action.IndexOf("pause", StringComparison.OrdinalIgnoreCase) >= 0)
                return DocmonPalette.Warning;

            return DocmonPalette.Muted;
        }
    }
}
