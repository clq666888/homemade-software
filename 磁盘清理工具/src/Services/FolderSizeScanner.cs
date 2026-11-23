using System;
using System.Collections.Generic;
using System.IO;
using System.Security;

namespace DiskCleanupTool
{
    internal static class FolderSizeScanner
    {
        public static string DirectoryAccessFailure(string path)
        {
            try
            {
                using (var iterator = Directory.EnumerateFileSystemEntries(path).GetEnumerator()) iterator.MoveNext();
                return null;
            }
            catch (UnauthorizedAccessException ex) { return "权限被拒绝：" + ex.Message; }
            catch (SecurityException ex) { return "安全策略拒绝访问：" + ex.Message; }
            catch (IOException ex) { return "目录读取失败：" + ex.Message; }
            catch (Exception ex) { return "无法打开目录：" + ex.Message; }
        }

        public static long CalculateFolderSize(string root, Func<bool> isCanceled, ref bool incomplete, ref bool hasLinks, ref bool accessDenied, ref string accessFailure)
        {
            long total = 0;
            try
            {
                if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
                {
                    hasLinks = true;
                    return 0;
                }
            }
            catch (Exception ex) { incomplete = true; accessFailure = "无法读取目录属性：" + ex.Message; return 0; }
            var pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                if (isCanceled()) return total;
                FileSystemInfo[] children;
                string path = pending.Pop();
                try { children = new DirectoryInfo(path).GetFileSystemInfos(); }
                catch (UnauthorizedAccessException ex) { incomplete = true; if (path == root) { accessDenied = true; accessFailure = "权限被拒绝：" + ex.Message; } continue; }
                catch (SecurityException ex) { incomplete = true; if (path == root) { accessDenied = true; accessFailure = "安全策略拒绝访问：" + ex.Message; } continue; }
                catch (Exception ex) { incomplete = true; if (path == root) accessFailure = "目录读取失败：" + ex.Message; continue; }
                foreach (FileSystemInfo child in children)
                {
                    if (isCanceled()) return total;
                    try
                    {
                        FileAttributes attributes = child.Attributes;
                        if ((attributes & FileAttributes.ReparsePoint) != 0)
                        {
                            hasLinks = true;
                            continue;
                        }
                        if ((attributes & FileAttributes.Directory) != 0)
                        {
                            pending.Push(child.FullName);
                        }
                        else
                        {
                            long length = ((FileInfo)child).Length;
                            total = length > long.MaxValue - total ? long.MaxValue : total + length;
                        }
                    }
                    catch { incomplete = true; }
                }
            }
            return total;
        }
    }
}
