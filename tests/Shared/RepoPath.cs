using System;
using System.IO;

// Finds a file by its path from the REPOSITORY ROOT, walking up from
// wherever the harness was built - so no harness names C:\Users\... and
// the tests run from any clone. Before the move into the repository they
// lived in %TEMP% and pointed at Downloads and the Desktop, and Windows
// cleaned several of them away.
internal static class RepoPath
{
    public static string Find(string fromRoot)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, fromRoot);

            if (File.Exists(candidate) || Directory.Exists(candidate))
                return candidate;
        }

        // Not found: hand back the relative path, so the harness's own
        // "file not found" message names what it was looking for
        return fromRoot;
    }
}
