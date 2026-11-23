using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace DiskCleanupTool
{
    internal static class WindowsShell
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ShellExecuteInfo
        {
            public int Size;
            public uint Mask;
            public IntPtr Window;
            public string Verb;
            public string File;
            public string Parameters;
            public string Directory;
            public int Show;
            public IntPtr Instance;
            public IntPtr IdList;
            public string Class;
            public IntPtr ClassKey;
            public uint HotKey;
            public IntPtr Icon;
            public IntPtr Process;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShellExecuteEx(ref ShellExecuteInfo info);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct ShellFileInfo
        {
            public IntPtr IconHandle;
            public int IconIndex;
            public uint Attributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string TypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string path, uint attributes, ref ShellFileInfo info,
            uint infoSize, uint flags);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr handle);

        public static void ShowProperties(IntPtr owner, string path)
        {
            var info = new ShellExecuteInfo
            {
                Size = Marshal.SizeOf(typeof(ShellExecuteInfo)), Mask = 0x10C,
                Window = owner, Verb = "properties", File = path, Show = 5
            };
            if (!ShellExecuteEx(ref info))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }

        public static Image ReadFileIcon(bool directory, string extension)
        {
            var info = new ShellFileInfo();
            string lookup = directory ? "folder" : "file" + (extension == "<file>" ? ".dat" : extension);
            uint attributes = directory ? 0x10u : 0x80u;
            Image result = null;
            try
            {
                SHGetFileInfo(lookup, attributes, ref info, (uint)Marshal.SizeOf(typeof(ShellFileInfo)), 0x111u);
                if (info.IconHandle != IntPtr.Zero) result = Icon.FromHandle(info.IconHandle).ToBitmap();
            }
            catch { result = null; }
            finally { if (info.IconHandle != IntPtr.Zero) DestroyIcon(info.IconHandle); }
            return result ?? SystemIcons.Application.ToBitmap();
        }
    }
}
