using System;

namespace DiskCleanupTool
{
    internal sealed class Entry
    {
        public string Path;
        public DateTime Modified;
        public bool IsDirectory;
        public bool IsReparse;
        public long Length;
        public long? FolderSize;
        public bool FolderSizeIncomplete;
        public bool FolderSizeHasLinks;
        public bool AccessDenied;
        public string AccessFailure;
        public int DefaultIndex;
    }
}
