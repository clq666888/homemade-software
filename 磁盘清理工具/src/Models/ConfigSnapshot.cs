using System.Collections.Generic;

namespace DiskCleanupTool
{
    public sealed class ConfigSnapshot
    {
        public int Version { get; set; }
        public string CurrentPath { get; set; }
        public Dictionary<string, string> Statuses { get; set; }
        public Dictionary<string, string> Notes { get; set; }
    }
}
