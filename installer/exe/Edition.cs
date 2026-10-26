namespace ATKINSMEC.Setup
{
    // WHICH PRODUCT THIS INSTALLER CARRIES: the whole Atkins MEP Toolkit,
    // or - built with -p:Edition=AutoVent by build-installer.ps1 -AutoVent -
    // AUTOVENT on its own, while it ships apart from the suite (the user,
    // 2026-10-15). Each keeps its own list of the files it wrote, so
    // removing one never takes the other's files - EPPlus included.
    internal static class Edition
    {
#if AUTOVENT_ONLY
        internal const string Name = "AUTOVENT";

        internal const string Title = "AUTOVENT - Revit add-in";

        internal const string Manifest = "AUTOVENT.files.txt";

        // Only AUTOVENT's own saved files: the folder is the suite's too
        internal static readonly string[] Settings = { "autovent_settings.txt", "autovent_occupancy.txt" };

        internal const string SettingsSaid = "AUTOVENT's saved set-ups and occupancy rules";
#else
        internal const string Name = "Atkins MEP Toolkit";

        internal const string Title = "Atkins MEP Toolkit - Revit add-ins";

        internal const string Manifest = "ATKINSMEC.files.txt";

        // The whole folder
        internal static readonly string[] Settings = null;

        internal const string SettingsSaid = "every tool's saved settings";
#endif
    }
}
