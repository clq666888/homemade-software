using System;
using System.IO;
using System.Runtime.InteropServices;

// Recovers every embedded icon resolution if the editable ICO was removed.
internal static class ExportIcon
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibraryEx(string file, IntPtr reserved, uint flags);
    [DllImport("kernel32.dll")] private static extern IntPtr FindResource(IntPtr module, IntPtr name, IntPtr type);
    [DllImport("kernel32.dll")] private static extern IntPtr LoadResource(IntPtr module, IntPtr resource);
    [DllImport("kernel32.dll")] private static extern IntPtr LockResource(IntPtr resource);
    [DllImport("kernel32.dll")] private static extern uint SizeofResource(IntPtr module, IntPtr resource);
    [DllImport("kernel32.dll")] private static extern bool FreeLibrary(IntPtr module);

    private static byte[] Read(IntPtr module, int type, int id)
    {
        var resource = FindResource(module, (IntPtr)id, (IntPtr)type);
        if (resource == IntPtr.Zero) throw new InvalidDataException("Missing icon resource.");
        var bytes = new byte[SizeofResource(module, resource)];
        Marshal.Copy(LockResource(LoadResource(module, resource)), bytes, 0, bytes.Length);
        return bytes;
    }

    private static void Main(string[] args)
    {
        var module = LoadLibraryEx(args[0], IntPtr.Zero, 2);
        try
        {
            var group = Read(module, 14, 32512);
            int count = BitConverter.ToUInt16(group, 4);
            var images = new byte[count][];
            using (var writer = new BinaryWriter(File.Create(args[1])))
            {
                writer.Write(group, 0, 6);
                int offset = 6 + 16 * count;
                for (int i = 0; i < count; i++)
                {
                    int position = 6 + 14 * i;
                    images[i] = Read(module, 3, BitConverter.ToUInt16(group, position + 12));
                    writer.Write(group, position, 8);
                    writer.Write(images[i].Length);
                    writer.Write(offset);
                    offset += images[i].Length;
                }
                foreach (var bytes in images) writer.Write(bytes);
            }
        }
        finally { FreeLibrary(module); }
    }
}
