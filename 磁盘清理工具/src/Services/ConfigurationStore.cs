using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace DiskCleanupTool
{
    internal static class ConfigurationStore
    {
        private static JavaScriptSerializer Serializer()
        { return new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue }; }

        public static Dictionary<string, string> ReadRecords(string path)
        {
            if (!File.Exists(path)) return null;
            return Serializer().Deserialize<Dictionary<string, string>>(File.ReadAllText(path, Encoding.UTF8));
        }

        public static void WriteRecords(string path, Dictionary<string, string> records)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, Serializer().Serialize(records), Encoding.UTF8);
        }

        public static void WriteSnapshot(string path, ConfigSnapshot snapshot)
        { File.WriteAllText(path, Serializer().Serialize(snapshot), Encoding.UTF8); }

        public static ConfigSnapshot ReadSnapshot(string path)
        {
            if (new FileInfo(path).Length > 32L * 1024 * 1024) throw new InvalidDataException("配置文件过大。");
            var snapshot = Serializer().Deserialize<ConfigSnapshot>(File.ReadAllText(path, Encoding.UTF8));
            if (snapshot == null || snapshot.Version != 1 || snapshot.Statuses == null || snapshot.Notes == null)
                throw new InvalidDataException("这不是有效的清理工具配置文件。");
            return snapshot;
        }

        public static bool IsValidPath(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) return false;
            try { return Path.IsPathRooted(path) && Path.GetFullPath(path) == path; }
            catch { return false; }
        }
    }
}
