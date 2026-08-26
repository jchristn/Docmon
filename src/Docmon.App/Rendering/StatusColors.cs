namespace Docmon.App.Rendering
{
    using Docmon.App.Theming;
    using Docmon.Core.Enums;
    using TUIKit;

    /// <summary>
    /// Maps domain states to the palette colors Docmon draws them with.
    /// </summary>
    public static class StatusColors
    {
        /// <summary>
        /// Returns the color for a container lifecycle state.
        /// </summary>
        /// <param name="state">The container state.</param>
        /// <returns>The color.</returns>
        public static Color ForState(ContainerStateEnum state)
        {
            switch (state)
            {
                case ContainerStateEnum.Running: return DocmonPalette.Success;
                case ContainerStateEnum.Paused: return DocmonPalette.Warning;
                case ContainerStateEnum.Restarting: return DocmonPalette.Warning;
                case ContainerStateEnum.Exited: return DocmonPalette.Error;
                case ContainerStateEnum.Dead: return DocmonPalette.Error;
                case ContainerStateEnum.Created: return DocmonPalette.Muted;
                default: return DocmonPalette.Muted;
            }
        }

        /// <summary>
        /// Returns the color for an image update status.
        /// </summary>
        /// <param name="status">The update status.</param>
        /// <returns>The color.</returns>
        public static Color ForUpdate(UpdateStatusEnum status)
        {
            switch (status)
            {
                case UpdateStatusEnum.Current: return DocmonPalette.Success;
                case UpdateStatusEnum.UpdateAvailable: return DocmonPalette.Warning;
                case UpdateStatusEnum.Error: return DocmonPalette.Error;
                case UpdateStatusEnum.Checking: return DocmonPalette.Accent2;
                default: return DocmonPalette.Dim;
            }
        }
    }
}
