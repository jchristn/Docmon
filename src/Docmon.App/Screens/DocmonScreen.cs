namespace Docmon.App.Screens
{
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Widgets;

    /// <summary>
    /// Base class for a Docmon screen: a focusable widget that renders one section's content, exposes a
    /// status-bar key-hint line, and can report the domain object it currently has selected.
    /// </summary>
    public abstract class DocmonScreen : IWidget, IFocusable, IFocusAware
    {
        /// <summary>
        /// Gets a value indicating whether this screen currently holds focus.
        /// </summary>
        protected bool Focused { get; private set; }

        /// <summary>
        /// Gets the context-sensitive key hints shown in the status bar for this screen.
        /// </summary>
        public abstract string KeyHints { get; }

        /// <summary>
        /// Gets the domain object the screen currently has selected, or null.
        /// </summary>
        public virtual object? SelectedTag
        {
            get { return null; }
        }

        /// <inheritdoc/>
        public virtual Size Measure(Size available)
        {
            return available;
        }

        /// <inheritdoc/>
        public abstract void Render(ISurface surface);

        /// <inheritdoc/>
        public virtual bool HandleKey(KeyEvent key)
        {
            return false;
        }

        /// <inheritdoc/>
        public virtual void OnFocusChanged(bool focused)
        {
            Focused = focused;
        }
    }
}
