namespace Docmon.App.Screens
{
    /// <summary>
    /// Identifies the top-level screens Docmon navigates between via the tab bar.
    /// </summary>
    public enum ScreenId
    {
        /// <summary>The containers master/detail dashboard.</summary>
        Containers = 0,

        /// <summary>The compose-stacks screen.</summary>
        Stacks = 1,

        /// <summary>The full-size metrics charts.</summary>
        Metrics = 2,

        /// <summary>The images and update-check screen.</summary>
        Images = 3,

        /// <summary>The live Docker events stream.</summary>
        Events = 4,

        /// <summary>The tools and housekeeping screen.</summary>
        Tools = 5
    }
}
