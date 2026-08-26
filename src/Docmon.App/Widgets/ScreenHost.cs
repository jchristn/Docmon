namespace Docmon.App.Widgets
{
    using Docmon.App.Screens;
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
        private DocmonScreen? _Current;
        private bool _Focused;

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
            _Current?.Render(surface);
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
