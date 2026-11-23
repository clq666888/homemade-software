using System;
using System.IO;
using System.Xml.Serialization;

namespace LocalConverter
{
    public sealed class Settings
    {
        public string OutputFolder = "";
        public static string Folder { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LocalFileConverter"); } }
        public static Settings Load()
        {
            try { using (var s = File.OpenRead(Path.Combine(Folder, "settings.xml"))) return (Settings)new XmlSerializer(typeof(Settings)).Deserialize(s); }
            catch (IOException) { return new Settings(); } catch (InvalidOperationException) { return new Settings(); } catch (UnauthorizedAccessException) { return new Settings(); }
        }
        public void Save()
        {
            Directory.CreateDirectory(Folder);
            string path = Path.Combine(Folder, "settings.xml"), temp = Path.Combine(Folder, Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var s = File.Create(temp)) new XmlSerializer(typeof(Settings)).Serialize(s, this);
                if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
            }
            finally { OutputFiles.DeleteTemporary(temp); }
        }
    }
}
