using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DiskCleanupTool
{
    // Accessed on the UI thread; workers publish results through BeginInvoke.
    internal sealed class FolderSizeCache
    {
        private readonly Dictionary<string, FolderSizeResult> values =
            new Dictionary<string, FolderSizeResult>(StringComparer.OrdinalIgnoreCase);

        public FolderSizeResult this[string path] { set { values[path] = value; } }
        public bool TryGetValue(string path, out FolderSizeResult result) { return values.TryGetValue(path, out result); }
        public void Remove(string path) { values.Remove(path); }

        public void InvalidateAffectedPath(string path)
        {
            foreach (string key in values.Keys.Where(key => IsSameOrBelow(key, path) || IsSameOrBelow(path, key)).ToList())
                values.Remove(key);
        }

        public void InvalidateCurrentPage(string path)
        {
            foreach (string key in values.Keys.Where(key => IsDirectChild(key, path)).ToList()) values.Remove(key);
        }

        private static bool IsDirectChild(string path, string parentPath)
        {
            try
            {
                var parent = Directory.GetParent(path);
                return parent != null && String.Equals(parent.FullName, parentPath, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static bool IsSameOrBelow(string path, string root)
        {
            string normalized = Path.GetFullPath(root).TrimEnd('\\');
            return String.Equals(path, normalized, StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(normalized + "\\", StringComparison.OrdinalIgnoreCase);
        }
    }
}
