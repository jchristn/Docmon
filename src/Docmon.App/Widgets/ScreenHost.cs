namespace Docmon.App.Widgets
{
    using Docmon.App.Rendering;
    using Docmon.App.Screens;
    using Docmon.App.Theming;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Widgets;

    /// <summary>
    /// The single focusable widget bound to the content region. It forwards rendering, focus, and key
    /// events to whichever <see cref="DocmonScreen"/> is currently active, so switching screens is a
    /// matter of swapping <see cref="Current"/> rather than rebinding regions.
    /// </summary>
    public sealed class ScreenHost : IWidget, IFocusable, IFocusAware
    {
        private const string _LoadingMessage = "Please wait, docmon is initializing…";

        private DocmonScreen? _Current;
        private bool _Focused;
        private bool _Initializing = true;

        /// <summary>
        /// Gets or sets a value indicating whether the app is still loading its first snapshot from
        /// Docker and other sources. While true, a centered loading message is drawn in place of the
        /// active screen; the controller clears it once the first refresh has populated the screens.
        /// </summary>
        public bool Initializing
        {
            get { return _Initializing; }
            set { _Initializing = value; }
        }

        /// <summary>
        /// Gets or sets the active screen. Setting it transfers the current focus state to the new screen.
        /// </summary>
        public DocmonScreen? Current
        {
            get
            {
                return _Current;
            }
            set
            {
                _Current = value;
                _Current?.OnFocusChanged(_Focused);
            }
        }

        /// <inheritdoc/>
        public Size Measure(Size available)
        {
            return available;
        }

        /// <inheritdoc/>
        public void Render(ISurface surface)
        {
            if (_Initializing)
            {
                RenderLoading(surface);
                return;
            }

            _Current?.Render(surface);
        }

        private static void RenderLoading(ISurface surface)
        {
            int width = surface.Size.Width;
            int height = surface.Size.Height;
            if (width <= 0 || height <= 0)
                return;

            int x = System.Math.Max(0, (width - _LoadingMessage.Length) / 2);
            int y = height / 2;
            Draw.Text(surface, x, y, _LoadingMessage, CellStyle.Default.WithForeground(DocmonPalette.Muted), width);
        }

        /// <inheritdoc/>
        public bool HandleKey(KeyEvent key)
        {
            return _Current != null && _Current.HandleKey(key);
        }

        /// <inheritdoc/>
        public void OnFocusChanged(bool focused)
        {
            _Focused = focused;
            _Current?.OnFocusChanged(focused);
        }
    }
}
