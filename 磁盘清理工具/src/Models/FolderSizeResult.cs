namespace DiskCleanupTool
{
    internal sealed class FolderSizeResult
    {
        public long Bytes;
        public bool Incomplete;
        public bool HasLinks;
        public bool AccessDenied;
        public string AccessFailure;
    }
}
