using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace DiskCleanupTool
{
    internal sealed partial class MainForm
    {
        private void RefreshSelectedEntries()
        {
            if (isDeleting || selectedPaths.Count == 0) return;
            grid.EndEdit();
            int scanVersion = ++folderScanVersion;
            int refreshed = 0;
            int missing = 0;
            filling = true;
            try
            {
                foreach (DataGridViewRow row in grid.Rows.Cast<DataGridViewRow>().ToList())
                {
                    var entry = row.Tag as Entry;
                    if (entry == null || !selectedPaths.Contains(entry.Path)) continue;
                    try
                    {
                        FileAttributes attributes = File.GetAttributes(entry.Path);
                        entry.IsDirectory = (attributes & FileAttributes.Directory) != 0;
                        entry.IsReparse = entry.IsDirectory && (attributes & FileAttributes.ReparsePoint) != 0;
                        entry.Modified = File.GetLastWriteTime(entry.Path);
                        entry.AccessFailure = null;
                        entry.AccessDenied = false;
                        entry.FolderSizeIncomplete = false;
                        entry.FolderSizeHasLinks = entry.IsReparse;
                        folderSizeCache.Remove(entry.Path);
                        if (entry.IsDirectory) entry.FolderSize = entry.IsReparse ? (long?)0 : null;
                        else
                        {
                            entry.FolderSize = null;
                            entry.Length = new FileInfo(entry.Path).Length;
                        }
                        row.Cells[1].Value = entry.Modified.ToString("yyyy-MM-dd HH:mm:ss");
                        row.Cells[2].Value = entry.IsDirectory ? FormatFolderSize(entry) : FormatSize(entry.Length);
                        row.Cells[2].ToolTipText = entry.IsDirectory ? FolderSizeToolTip(entry) : entry.Length.ToString("N0") + " 字节";
                        row.Cells[5].Value = GetFileIcon(entry);
                        string status;
                        if (!statuses.TryGetValue(entry.Path, out status)) status = NotCleaned;
                        row.Cells[3].Value = status;
                        row.Cells[3].ToolTipText = AccessToolTip(entry, status);
                        SetRowColor(row, status);
                        refreshed++;
                    }
                    catch (FileNotFoundException)
                    {
                        selectedPaths.Remove(entry.Path);
                        grid.Rows.Remove(row);
                        missing++;
                    }
                    catch (DirectoryNotFoundException)
                    {
                        selectedPaths.Remove(entry.Path);
                        grid.Rows.Remove(row);
                        missing++;
                    }
                    catch (Exception ex)
                    {
                        entry.AccessFailure = "刷新失败：" + ex.Message;
                        row.Cells[3].Value = Inaccessible;
                        row.Cells[3].ToolTipText = entry.AccessFailure;
                        SetRowColor(row, Inaccessible);
                        refreshed++;
                    }
                }
            }
            finally { filling = false; }
            if (sortState != 0) SortRows();
            var pending = grid.Rows.Cast<DataGridViewRow>().Select(row => row.Tag as Entry)
                .Where(entry => entry != null && entry.IsDirectory && (!entry.FolderSize.HasValue || entry.IsReparse)).ToList();
            if (pending.Count > 0) StartFolderScan(pending, scanVersion);
            infoLabel.Text = "已刷新选中项 " + refreshed + " 个；已不存在 " + missing + " 个。";
        }

        private void GoUp()
        {
            try
            {
                var parent = Directory.GetParent(currentPath);
                if (parent != null) OpenFolder(parent.FullName);
            }
            catch (Exception ex) { ShowError(ex.Message); }
        }

        private void InvalidateFolderSizes(string path)
        {
            folderSizeCache.InvalidateAffectedPath(path);
        }

        private void InvalidateCurrentPageSizes(string path)
        {
            folderSizeCache.InvalidateCurrentPage(path);
        }

        private void OpenFolder(string requestedPath, bool forceRescan = false)
        {
            try
            {
                if (isDeleting) return;
                grid.EndEdit();
                string fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(requestedPath.Trim().Trim('"')));
                var directory = new DirectoryInfo(fullPath);
                if (!directory.Exists) throw new DirectoryNotFoundException("找不到目录：" + fullPath);
                if (forceRescan) InvalidateCurrentPageSizes(directory.FullName);
                int scanVersion = ++folderScanVersion;
                sizeSortPending = false;
                sizeSortTimer.Stop();

                var entries = new List<Entry>();
                foreach (FileSystemInfo item in directory.EnumerateFileSystemInfos("*", System.IO.SearchOption.TopDirectoryOnly))
                {
                    bool isDirectory = (item.Attributes & FileAttributes.Directory) != 0;
                    long length = 0;
                    string fileFailure = null;
                    if (!isDirectory)
                    {
                        try { length = ((FileInfo)item).Length; }
                        catch (Exception ex) { fileFailure = "无法读取文件大小：" + ex.Message; }
                    }
                    DateTime modified;
                    try { modified = item.LastWriteTime; }
                    catch { modified = DateTime.MinValue; }
                    var entry = new Entry
                    {
                        Path = item.FullName, Modified = modified, IsDirectory = isDirectory, Length = length,
                        IsReparse = isDirectory && (item.Attributes & FileAttributes.ReparsePoint) != 0,
                        AccessFailure = fileFailure
                    };
                    FolderSizeResult cached;
                    if (entry.IsReparse)
                    {
                        entry.FolderSize = 0;
                        entry.FolderSizeHasLinks = true;
                    }
                    else if (isDirectory && folderSizeCache.TryGetValue(entry.Path, out cached))
                    {
                        entry.FolderSize = cached.Bytes;
                        entry.FolderSizeIncomplete = cached.Incomplete;
                        entry.FolderSizeHasLinks = cached.HasLinks;
                        entry.AccessDenied = cached.AccessDenied;
                        entry.AccessFailure = cached.AccessFailure;
                    }
                    entries.Add(entry);
                }
                entries = entries.OrderByDescending(x => x.IsDirectory)
                    .ThenBy(x => x.Path, StringComparer.CurrentCultureIgnoreCase).ToList();
                for (int i = 0; i < entries.Count; i++) entries[i].DefaultIndex = i;

                filling = true;
                if (!String.Equals(currentPath.TrimEnd('\\'), directory.FullName.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                    searchBox.Clear();
                grid.Rows.Clear();
                selectedPaths.Clear();
                contextPath = null;
                foreach (var entry in entries)
                {
                    string status;
                    if (!statuses.TryGetValue(entry.Path, out status)) status = NotCleaned;
                    if (entry.AccessFailure != null) status = Inaccessible;
                    string note;
                    if (!notes.TryGetValue(entry.Path, out note)) note = "";
                    int rowIndex = grid.Rows.Add(Path.GetFileName(entry.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                        entry.Modified == DateTime.MinValue ? "" : entry.Modified.ToString("yyyy-MM-dd HH:mm:ss"),
                        entry.IsDirectory ? FormatFolderSize(entry) : FormatSize(entry.Length),
                        status, note, GetFileIcon(entry));
                    var row = grid.Rows[rowIndex];
                    row.Tag = entry;
                    row.Cells[0].ToolTipText = entry.Path + (entry.IsDirectory ? "\n双击进入文件夹" : "\n大小 " + entry.Length.ToString("N0") + " 字节");
                    row.Cells[2].ToolTipText = entry.IsDirectory
                        ? FolderSizeToolTip(entry)
                        : entry.Length.ToString("N0") + " 字节";
                    row.Cells[3].ToolTipText = AccessToolTip(entry, status);
                    SetRowColor(row, status);
                }
                filling = false;
                FitColumnsToViewport();
                if (sortState != 0) SortRows();
                currentPath = directory.FullName;
                pathBox.Text = currentPath;
                if (searchBox.Text.Length > 0) ApplyNameFilter();
                try { File.WriteAllText(lastPathPath, currentPath, Encoding.UTF8); }
                catch { /* Browsing still works if the last path cannot be saved. */ }
                int remaining = entries.Count(entry => entry.IsDirectory && !entry.FolderSize.HasValue);
                infoLabel.Text = "目录 " + currentPath + "    |    " + entries.Count + " 项    |    " +
                    (remaining == 0 ? "文件夹大小已显示" : remaining + " 个文件夹大小正在后台计算");
                if (remaining > 0 || entries.Any(entry => entry.IsReparse)) StartFolderScan(entries, scanVersion);
            }
            catch (Exception ex)
            {
                filling = false;
                ShowError(ex.Message);
            }
        }
    }
}
