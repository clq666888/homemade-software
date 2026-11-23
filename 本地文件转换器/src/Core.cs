using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace LocalConverter
{
    public sealed class InputFile
    {
        public string Path { get; set; }
        public int Rotation { get; set; }
        public InputFile(string path) { Path = System.IO.Path.GetFullPath(path); }
    }
    public sealed class ConversionOptions
    {
        public string OutputFolder;
        public bool Merge = true;
        public string MergeName = "合并图片";
        public int Orientation; // 0 自动，1 纵向，2 横向
    }
    public interface IConverter
    {
        string Id { get; }
        string Title { get; }
        string Category { get; }
        string Description { get; }
        string[] Extensions { get; }
        void Convert(IList<InputFile> files, ConversionOptions options, CancellationToken token, Action<int, string> progress);
    }
    public static class ConverterRegistry
    {
        // 新的转换功能在此注册，窗口不依赖具体转换引擎。
        public static readonly IConverter[] All = { new WordConverter(), new ImageConverter() };
    }
    public static class OutputFiles
    {
        public static void ValidateName(string name)
        {
            if (String.IsNullOrWhiteSpace(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.EndsWith(".") || name.EndsWith(" "))
                throw new IOException("输出名称不能为空，也不能含有路径或非法字符。");
            string stem = name.Split('.')[0].ToUpperInvariant();
            if (new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" }.Contains(stem))
                throw new IOException("请使用其他输出名称，该名称是 Windows 保留名称。");
        }
        // 临时文件与目标在同一目录；最终 Move 不覆盖已有文件，消除检查后的竞争窗口。
        public static string Commit(string temporary, string folder, string name, string extension = ".pdf")
        {
            ValidateName(name);
            for (int i = 0; i < 10000; i++)
            {
                string dest = Path.Combine(folder, name + (i == 0 ? "" : " (" + i + ")") + extension);
                try { File.Move(temporary, dest); return dest; }
                catch (IOException) { if (!File.Exists(dest)) throw; }
            }
            throw new IOException("同名文件过多，请更换输出名称。");
        }
        public static string Temporary(string folder) { return Path.Combine(folder, ".localconverter-" + Guid.NewGuid().ToString("N") + ".tmp"); }
        public static void DeleteTemporary(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { /* 仅清理本次生成的临时文件。 */ } }
    }
}
