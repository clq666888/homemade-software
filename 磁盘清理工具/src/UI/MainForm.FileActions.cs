using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using Microsoft.VisualBasic.FileIO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace DiskCleanupTool
{
    internal sealed partial class MainForm
    {
        private void BuildRowMenu()
        {
            rowMenu.Items.Add("刷新选中项", null, delegate { RefreshSelectedEntries(); });
            rowMenu.Items.Add("复制文件路径", null, delegate { CopySelectedPaths(); });
            rowMenu.Items.Add("打开对应文件路径", null, delegate
            {
                if (String.IsNullOrEmpty(contextPath)) return;
                try { Process.Start("explorer.exe", "/select,\"" + contextPath + "\""); }
                catch (Exception ex) { ShowError("无法打开路径：" + ex.Message); }
            });
            rowMenu.Items.Add("删除文件", null, delegate { DeleteSelectedEntries(); });
            rowMenu.Items.Add("属性", null, delegate { ShowFileProperties(); });
            rowMenu.Opening += delegate(object sender, System.ComponentModel.CancelEventArgs e)
            {
                var point = grid.PointToClient(Cursor.Position);
                if (grid.HitTest(point.X, point.Y).RowIndex < 0 || String.IsNullOrEmpty(contextPath)) e.Cancel = true;
            };
        }

        private void CopySelectedPaths()
        {
            var paths = grid.Rows.Cast<DataGridViewRow>()
                .Select(row => row.Tag as Entry)
                .Where(entry => entry != null && selectedPaths.Contains(entry.Path))
                .Select(entry => entry.Path).ToArray();
            if (paths.Length == 0 && !String.IsNullOrEmpty(contextPath)) paths = new[] { contextPath };
            if (paths.Length == 0) return;
            try { Clipboard.SetText(String.Join(Environment.NewLine, paths)); infoLabel.Text = "已复制 " + paths.Length + " 条路径。"; }
            catch (Exception ex) { ShowError("复制失败：" + ex.Message); }
        }

        private void DeleteSelectedEntries()
        {
            if (isDeleting) return;
            grid.EndEdit();
            var requested = grid.Rows.Cast<DataGridViewRow>().Where(row => row.Visible)
                .Select(row => row.Tag as Entry)
                .Where(entry => entry != null && selectedPaths.Contains(entry.Path)).ToList();
            if (requested.Count > 0) RecycleEntries(requested);
        }

        private void ShowFileProperties()
        {
            string path = contextPath;
            var entry = grid.CurrentRow == null ? null : grid.CurrentRow.Tag as Entry;
            if (String.IsNullOrEmpty(path) && entry != null) path = entry.Path;
            if (String.IsNullOrEmpty(path)) return;
            try { WindowsShell.ShowProperties(Handle, path); }
            catch (Exception ex) { ShowError("无法打开属性：" + ex.Message); }
        }

        private Image GetFileIcon(Entry entry)
        {
            string extension = entry.IsDirectory ? "<folder>" : Path.GetExtension(entry.Path).ToLowerInvariant();
            if (extension.Length == 0) extension = "<file>";
            Image cached;
            if (iconCache.TryGetValue(extension, out cached)) return cached;
            Image result = WindowsShell.ReadFileIcon(entry.IsDirectory, extension);
            iconCache[extension] = result;
            return result;
        }

        private void RecycleEntries(List<Entry> requested)
        {
            var eligible = requested.Where(entry => !IsProtectedDeletionTarget(entry.Path)).ToList();
            int protectedCount = requested.Count - eligible.Count;
            if (eligible.Count == 0)
            {
                ShowError("所选路径属于系统或工具自身目录，不能在这里删除。");
                return;
            }
            string sample = String.Join("\n", eligible.Take(5).Select(entry => entry.Path).ToArray());
            if (eligible.Count > 5) sample += "\n……";
            var answer = MessageBox.Show(this,
                "确定将选中的 " + eligible.Count + " 个文件/文件夹移入回收站吗？\n\n" + sample +
                (protectedCount > 0 ? "\n\n另有 " + protectedCount + " 项受保护，将跳过。" : "") +
                "\n\n文件夹会连同全部内容移入回收站；清空回收站后才释放空间。",
                "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes) return;

            var viewport = CaptureViewport();
            isDeleting = true;
            grid.Enabled = false;
            infoLabel.Text = "正在将 " + eligible.Count + " 项移入回收站……";
            var worker = new Thread(delegate()
            {
                int moved = 0;
                var movedPaths = new List<string>();
                var failures = new List<string>();
                foreach (Entry entry in eligible)
                {
                    try { MovePathToRecycleBin(entry); moved++; movedPaths.Add(entry.Path); }
                    catch (Exception ex) { failures.Add(entry.Path + "：" + ex.Message); }
                }
                try
                {
                    BeginInvoke((Action)delegate()
                    {
                        isDeleting = false;
                        grid.Enabled = true;
                        foreach (string path in movedPaths) InvalidateFolderSizes(path);
                        OpenFolder(currentPath);
                        RestoreViewport(viewport);
                        infoLabel.Text = "已移入回收站 " + moved + " 项；失败 " + failures.Count + " 项" +
                            (protectedCount > 0 ? "；跳过受保护项 " + protectedCount + " 项" : "") + "。";
                        if (failures.Count > 0)
                            MessageBox.Show(this, "以下项目未能移入回收站：\n" + String.Join("\n", failures.Take(8).ToArray()),
                                "部分操作失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    });
                }
                catch (InvalidOperationException) { /* The window has closed. */ }
            });
            worker.IsBackground = true;
            worker.Name = "文件删除";
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();
        }

        private bool IsProtectedDeletionTarget(string path)
        {
            string full = Path.GetFullPath(path).TrimEnd('\\');
            if (String.Equals(full, Path.GetPathRoot(path).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return true;
            if (IsSameOrBelow(full, Application.StartupPath)) return true;
            string[] protectedExactFolders = {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.Windows)
            };
            foreach (string root in protectedExactFolders)
            {
                if (!String.IsNullOrEmpty(root) &&
                    String.Equals(full, Path.GetFullPath(root).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return true;
            }
            string[] protectedTrees = {
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                @"C:\Recovery", @"C:\System Volume Information", @"C:\$Recycle.Bin"
            };
            foreach (string root in protectedTrees)
                if (!String.IsNullOrEmpty(root) && IsSameOrBelow(full, root)) return true;
            string usersRoot = Path.Combine(Path.GetPathRoot(path), "Users");
            if (String.Equals(full, usersRoot, StringComparison.OrdinalIgnoreCase) ||
                String.Equals(full, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private bool IsSameOrBelow(string path, string root)
        {
            string normalized = Path.GetFullPath(root).TrimEnd('\\');
            return String.Equals(path, normalized, StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(normalized + "\\", StringComparison.OrdinalIgnoreCase);
        }

        private void MovePathToRecycleBin(Entry entry)
        {
            if (entry.IsDirectory)
                FileSystem.DeleteDirectory(entry.Path, UIOption.OnlyErrorDialogs,
                    RecycleOption.SendToRecycleBin, UICancelOption.ThrowException);
            else
                FileSystem.DeleteFile(entry.Path, UIOption.OnlyErrorDialogs,
                    RecycleOption.SendToRecycleBin, UICancelOption.ThrowException);
        }

        private string LsCommand()
        {
            return "ls -LiteralPath '" + currentPath.Replace("'", "''") +
                "' -Force | Select-Object Mode,LastWriteTime,Length,FullName";
        }

        private void CopyCommand()
        {
            try { Clipboard.SetText(LsCommand()); infoLabel.Text = "已复制完整 ls 指令。"; }
            catch (Exception ex) { ShowError("复制失败：" + ex.Message); }
        }

        private void CopyListing()
        {
            try
            {
                grid.EndEdit();
                var output = new StringBuilder();
                output.AppendLine("目录: " + currentPath);
                output.AppendLine("PowerShell 指令: " + LsCommand());
                output.AppendLine("类型\t修改时间\t文件大小\t清理状态\t备注\t完整路径");
                foreach (DataGridViewRow row in grid.Rows)
                {
                    if (!row.Visible) continue;
                    var entry = row.Tag as Entry;
                    if (entry == null) continue;
                    output.Append(entry.IsDirectory ? "目录" : "文件").Append('\t');
                    output.Append(entry.Modified == DateTime.MinValue ? "" : entry.Modified.ToString("yyyy-MM-dd HH:mm:ss")).Append('\t');
                    output.Append(Convert.ToString(row.Cells[2].Value)).Append('\t');
                    output.Append(Convert.ToString(row.Cells[3].Value)).Append('\t');
                    output.Append(Convert.ToString(row.Cells[4].Value)).Append('\t');
                    output.AppendLine(entry.Path);
                }
                Clipboard.SetText(output.ToString());
                infoLabel.Text = "已复制 " + grid.Rows.Count + " 项，可粘贴给 AI。";
            }
            catch (Exception ex) { ShowError("复制失败：" + ex.Message); }
        }
    }
}
