using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;

namespace DiskCleanupTool
{
    internal sealed partial class MainForm
    {
        private void StartFolderScan(List<Entry> entries, int scanVersion)
        {
            var worker = new Thread(delegate()
            {
                for (int index = 0; index < entries.Count; index++)
                {
                    if (scanVersion != folderScanVersion) return;
                    var entry = entries[index];
                    if (!entry.IsDirectory) continue;
                    if (entry.IsReparse)
                    {
                        string failure = FolderSizeScanner.DirectoryAccessFailure(entry.Path);
                        if (failure != null) BeginInvoke((Action)delegate()
                        {
                            if (scanVersion == folderScanVersion && !IsDisposed) MarkAccessFailure(entry, failure);
                        });
                        continue;
                    }
                    if (entry.FolderSize.HasValue) continue;

                    bool incomplete = false;
                    bool hasLinks = false;
                    bool accessDenied = false;
                    string accessFailure = null;
                    long bytes = FolderSizeScanner.CalculateFolderSize(entry.Path,
                        delegate { return scanVersion != folderScanVersion; },
                        ref incomplete, ref hasLinks, ref accessDenied, ref accessFailure);
                    if (scanVersion != folderScanVersion) return;
                    try
                    {
                        BeginInvoke((Action)delegate()
                        {
                            if (scanVersion != folderScanVersion || IsDisposed) return;
                            entry.FolderSize = bytes;
                            entry.FolderSizeIncomplete = incomplete;
                            entry.FolderSizeHasLinks = hasLinks;
                            entry.AccessDenied = accessDenied;
                            entry.AccessFailure = accessFailure;
                            folderSizeCache[entry.Path] = new FolderSizeResult { Bytes = bytes, Incomplete = incomplete, HasLinks = hasLinks, AccessDenied = accessDenied, AccessFailure = accessFailure };
                            foreach (DataGridViewRow row in grid.Rows)
                            {
                                var displayed = row.Tag as Entry;
                                if (displayed == null || !String.Equals(displayed.Path, entry.Path, StringComparison.OrdinalIgnoreCase)) continue;
                                row.Cells[2].Value = FormatFolderSize(entry);
                                row.Cells[2].ToolTipText = FolderSizeToolTip(entry);
                                if (accessFailure != null)
                                {
                                    filling = true;
                                    row.Cells[3].Value = Inaccessible;
                                    filling = false;
                                }
                                string shownStatus = Convert.ToString(row.Cells[3].Value);
                                row.Cells[3].ToolTipText = AccessToolTip(entry, shownStatus);
                                SetRowColor(row, shownStatus);
                                break;
                            }
                            RequestSizeSort();
                        });
                    }
                    catch (InvalidOperationException) { return; }
                }
                try
                {
                    BeginInvoke((Action)delegate()
                    {
                        if (scanVersion == folderScanVersion && !IsDisposed)
                            RequestSizeSort();
                    });
                }
                catch (InvalidOperationException) { return; }
            });
            worker.IsBackground = true;
            worker.Name = "文件夹大小计算";
            worker.Start();
        }

        private void MarkAccessFailure(Entry entry, string reason)
        {
            entry.AccessDenied = true;
            entry.AccessFailure = reason;
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.Tag != entry) continue;
                filling = true;
                row.Cells[3].Value = Inaccessible;
                filling = false;
                row.Cells[3].ToolTipText = reason;
                SetRowColor(row, Inaccessible);
                break;
            }
        }

        private string FormatFolderSize(Entry entry)
        {
            if (entry.IsReparse) return "目录链接";
            if (!entry.FolderSize.HasValue) return "计算中...";
            if (entry.FolderSizeIncomplete && entry.FolderSize.Value == 0) return "大小未知";
            return (entry.FolderSizeIncomplete ? "≥ " : "") + FormatSize(entry.FolderSize.Value)
                + (entry.FolderSizeHasLinks ? " + 链接" : "");
        }

        private string FolderSizeToolTip(Entry entry)
        {
            if (entry.IsReparse) return "这是目录链接，内容可打开查看；链接目标不计入此处大小，以免把其他位置的数据重复算入。";
            if (!entry.FolderSize.HasValue) return "正在后台统计文件夹大小。";
            string links = entry.FolderSizeHasLinks ? "目录链接的目标已跳过，避免重复计数。" : "";
            if (entry.FolderSizeIncomplete)
                return entry.FolderSize.Value == 0
                    ? "部分内容无法读取，不能判断此文件夹是否为空。" + links + "刷新后会重新尝试统计。"
                    : "至少已读取 " + entry.FolderSize.Value.ToString("N0") + " 字节；部分内容无法读取，实际大小可能更大。" + links;
            return entry.FolderSize.Value.ToString("N0") + " 字节。" + links + "系统硬链接可能被重复计数。";
        }

        private string AccessToolTip(Entry entry, string status)
        {
            if (status != Inaccessible) return "";
            return entry.AccessFailure ?? "手动标记为无法访问；程序尚未检测到具体原因。";
        }

        private string FormatSize(long bytes)
        {
            if (bytes < 1024) return bytes.ToString("N0") + " B";
            double value = bytes;
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            int unit = 0;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }
            return value.ToString("0.00") + " " + units[unit];
        }
    }
}
