namespace Docmon.App.Screens
{
    using System;
    using System.Collections.Generic;
    using Docmon.App.Rendering;
    using Docmon.App.Theming;
    using Docmon.Core.Enums;
    using Docmon.Core.Models;
    using TUIKit;
    using TUIKit.Input;

    /// <summary>
    /// Shows compose projects (stacks) in a table, with the selected stack's services listed beneath.
    /// </summary>
    public sealed class StacksScreen : DocmonScreen
    {
        private static readonly string[] _Headers = { "PROJECT", "SERVICES", "STATE", "FILE" };
        private static readonly int[] _Weights = { 6, 3, 3, 8 };

        private readonly TableState _Table = new TableState();
        private int _Count;

        /// <inheritdoc/>
        public override string KeyHints
        {
            get { return "↑↓ select · u up -d · d down · r restart · P pull"; }
        }

        /// <inheritdoc/>
        public override object? SelectedTag
        {
            get { return _Table.SelectedTag; }
        }

        /// <summary>
        /// Updates the stack list.
        /// </summary>
        /// <param name="stacks">The stacks. Must not be null.</param>
        public void SetStacks(IReadOnlyList<ComposeStack> stacks)
        {
            if (stacks == null) throw new ArgumentNullException(nameof(stacks));

            _Count = stacks.Count;
            List<RowItem> rows = new List<RowItem>(stacks.Count);
            foreach (ComposeStack stack in stacks)
            {
                string state = stack.RunningCount == stack.ServiceCount && stack.ServiceCount > 0 ? "up" : (stack.RunningCount == 0 ? "stopped" : "partial");
                Color color = stack.RunningCount == stack.ServiceCount && stack.ServiceCount > 0 ? DocmonPalette.Success : (stack.RunningCount == 0 ? DocmonPalette.Muted : DocmonPalette.Warning);
                string[] cells =
                {
                    stack.Project,
                    stack.RunningCount + "/" + stack.ServiceCount,
                    state,
                    stack.ConfigFilePath
                };
                rows.Add(new RowItem(cells, stack, color));
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

            int topHeight = Math.Max(4, height * 55 / 100);
            CellStyle border = CellStyle.Default.WithForeground(Focused ? DocmonPalette.Accent : DocmonPalette.Dim);

            Rect tableInner = Draw.Panel(surface, new Rect(0, 0, width, topHeight), "Stacks (" + _Count + ")", border);
            _Table.Render(surface, tableInner, _Headers, _Weights, Focused, "No compose stacks found.");

            Rect servicesInner = Draw.Panel(surface, new Rect(0, topHeight, width, height - topHeight), "Services", CellStyle.Default.WithForeground(DocmonPalette.Dim));
            RenderServices(surface, servicesInner);
        }

        private void RenderServices(ISurface surface, Rect area)
        {
            if (area.Width <= 2 || area.Height < 1)
                return;

            ComposeStack? stack = _Table.SelectedTag as ComposeStack;
            if (stack == null)
            {
                Draw.Text(surface, area.X, area.Y, "Select a stack.", CellStyle.Default.WithForeground(DocmonPalette.Dim), area.Width);
                return;
            }

            CellStyle header = CellStyle.Default.WithForeground(DocmonPalette.Muted).WithAttribute(CellAttributes.Bold, true);
            Draw.Text(surface, area.X, area.Y, "SERVICE".PadRight(18) + "IMAGE".PadRight(30) + "STATE", header, area.Width);

            int y = area.Y + 1;
            foreach (ComposeServiceInfo service in stack.Services)
            {
                if (y >= area.Y + area.Height)
                    break;

                string line = service.Name.PadRight(18) + Draw.Clip(service.RunningImage, 28).PadRight(30) + service.State.ToString().ToLowerInvariant();
                Draw.Text(surface, area.X, y, line, CellStyle.Default.WithForeground(StatusColors.ForState(service.State)), area.Width);
                y += 1;
            }
        }
    }
}
