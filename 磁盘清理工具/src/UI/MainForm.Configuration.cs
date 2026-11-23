using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace DiskCleanupTool
{
    internal sealed partial class MainForm
    {
        private void OnCellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (filling || e.RowIndex < 0) return;
            var row = grid.Rows[e.RowIndex];
            var entry = row.Tag as Entry;
            if (entry == null) return;
            if (e.ColumnIndex == 3)
            {
                string status = Convert.ToString(row.Cells[3].Value);
                if (status != NotCleaned && status != Inaccessible && status != Cleaned && status != NotNeeded) return;
                var paths = selectedPaths.Contains(entry.Path)
                    ? selectedPaths.ToList() : new List<string> { entry.Path };
                BeginInvoke((Action)delegate()
                {
                    if (!IsDisposed) ApplyStatusToPaths(paths, status);
                });
            }
            else if (e.ColumnIndex == 4)
            {
                string note = Convert.ToString(row.Cells[4].Value);
                if (String.IsNullOrWhiteSpace(note)) notes.Remove(entry.Path);
                else notes[entry.Path] = note;
                SaveNotes();
            }
        }

        private void ApplyStatusToPaths(List<string> paths, string status)
        {
            var distinct = new HashSet<string>(paths, StringComparer.OrdinalIgnoreCase);
            if (distinct.Count == 0) return;
            filling = true;
            grid.EndEdit();
            foreach (string path in distinct)
            {
                if (status == NotCleaned) statuses.Remove(path);
                else statuses[path] = status;
            }
            selectedPaths.Clear();
            foreach (string path in distinct) selectedPaths.Add(path);
            foreach (DataGridViewRow row in grid.Rows)
            {
                var entry = row.Tag as Entry;
                if (entry == null || !distinct.Contains(entry.Path)) continue;
                row.Cells[3].Value = status;
                row.Cells[3].ToolTipText = AccessToolTip(entry, status);
                SetRowColor(row, status);
            }
            filling = false;
            RestoreSelectedRows();
            SaveStatuses();
            infoLabel.Text = "已将 " + distinct.Count + " 项设为“" + status + "”。";
        }

        private void ResetAllStatuses()
        {
            if (isDeleting) return;
            grid.EndEdit();
            var choice = MessageBox.Show(this, "将所有路径的清理状态重置为“未清理”？备注会保留。",
                "重置所有状态", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (choice != DialogResult.Yes) return;
            statuses.Clear();
            filling = true;
            foreach (DataGridViewRow row in grid.Rows)
            {
                row.Cells[3].Value = NotCleaned;
                SetRowColor(row, NotCleaned);
            }
            filling = false;
            SaveStatuses();
            infoLabel.Text = "已重置所有路径的清理状态；备注已保留。";
        }

        private void SaveConfiguration()
        {
            if (isDeleting) return;
            grid.EndEdit();
            BeginInvoke((Action)delegate
            {
                if (IsDisposed) return;
                try
                {
                    var savedStatuses = new Dictionary<string, string>(statuses, StringComparer.OrdinalIgnoreCase);
                    var savedNotes = new Dictionary<string, string>(notes, StringComparer.OrdinalIgnoreCase);
                    foreach (DataGridViewRow row in grid.Rows)
                    {
                        var entry = row.Tag as Entry;
                        if (entry == null) continue;
                        string status = Convert.ToString(row.Cells[3].Value);
                        if (status == NotCleaned) savedStatuses.Remove(entry.Path);
                        else if (status == Inaccessible || status == Cleaned || status == NotNeeded)
                            savedStatuses[entry.Path] = status;
                        string note = Convert.ToString(row.Cells[4].Value);
                        if (String.IsNullOrWhiteSpace(note)) savedNotes.Remove(entry.Path);
                        else savedNotes[entry.Path] = note;
                    }
                    var snapshot = new ConfigSnapshot
                    {
                        Version = 1, CurrentPath = currentPath,
                        Statuses = savedStatuses, Notes = savedNotes
                    };
                    using (var dialog = new SaveFileDialog())
                    {
                        dialog.Title = "保存清理配置";
                        dialog.Filter = "JSON 配置 (*.json)|*.json";
                        dialog.DefaultExt = "json";
                        dialog.AddExtension = true;
                        dialog.OverwritePrompt = true;
                        dialog.InitialDirectory = Path.GetDirectoryName(lastConfigFile);
                        dialog.FileName = Path.GetFileName(lastConfigFile);
                        if (dialog.ShowDialog(this) != DialogResult.OK) return;
                        if (String.Equals(Path.GetPathRoot(dialog.FileName), @"C:\", StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException("请将配置保存到 E 盘或其他非 C 盘位置。");
                        ConfigurationStore.WriteSnapshot(dialog.FileName, snapshot);
                        lastConfigFile = dialog.FileName;
                        infoLabel.Text = "配置已保存到 " + lastConfigFile + "（" + savedStatuses.Count + " 个状态，" + savedNotes.Count + " 条备注）。";
                    }
                }
                catch (Exception ex) { ShowError("保存配置失败：" + ex.Message); }
            });
        }

        private void ImportConfiguration()
        {
            if (isDeleting) return;
            grid.EndEdit();
            BeginInvoke((Action)delegate
            {
                if (IsDisposed) return;
                using (var dialog = new OpenFileDialog())
                {
                    dialog.Title = "导入清理配置";
                    dialog.Filter = "JSON 配置 (*.json)|*.json";
                    dialog.InitialDirectory = Path.GetDirectoryName(lastConfigFile);
                    dialog.FileName = Path.GetFileName(lastConfigFile);
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    try
                    {
                        var snapshot = ConfigurationStore.ReadSnapshot(dialog.FileName);
                        var importedStatuses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        var importedNotes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var pair in snapshot.Statuses)
                        {
                            if (!ConfigurationStore.IsValidPath(pair.Key)) continue;
                            string status = pair.Value == "权限不够" ? Inaccessible : pair.Value;
                            if (status == Inaccessible || status == Cleaned || status == NotNeeded)
                                importedStatuses[pair.Key] = status;
                        }
                        foreach (var pair in snapshot.Notes)
                            if (ConfigurationStore.IsValidPath(pair.Key) && !String.IsNullOrWhiteSpace(pair.Value))
                                importedNotes[pair.Key] = pair.Value;
                        var answer = MessageBox.Show(this,
                            "导入 " + importedStatuses.Count + " 个清理状态和 " + importedNotes.Count + " 条备注？当前记录将被替换。\n已删除或改名的旧路径不会显示在列表中。",
                            "确认导入配置", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                            MessageBoxDefaultButton.Button2);
                        if (answer != DialogResult.Yes) return;
                        lastConfigFile = dialog.FileName;
                        statuses.Clear();
                        notes.Clear();
                        foreach (var pair in importedStatuses) statuses[pair.Key] = pair.Value;
                        foreach (var pair in importedNotes) notes[pair.Key] = pair.Value;
                        SaveStatuses();
                        SaveNotes();
                        OpenFolder(Directory.Exists(currentPath) ? currentPath : @"C:\");
                        infoLabel.Text = "配置已导入：" + importedStatuses.Count + " 个状态，" + importedNotes.Count + " 条备注。";
                    }
                    catch (Exception ex) { ShowError("导入配置失败：" + ex.Message); }
                }
            });
        }

        private void LoadStatuses()
        {
            try
            {
                if (!File.Exists(statePath)) return;
                var saved = ConfigurationStore.ReadRecords(statePath);
                if (saved == null) return;
                foreach (var pair in saved)
                {
                    if (pair.Value == Inaccessible || pair.Value == "权限不够") statuses[pair.Key] = Inaccessible;
                    else if (pair.Value == Cleaned || pair.Value == NotNeeded) statuses[pair.Key] = pair.Value;
                }
            }
            catch { /* A corrupt state file must not prevent browsing. */ }
        }

        private void SaveStatuses()
        {
            try
            {
                ConfigurationStore.WriteRecords(statePath, statuses);
            }
            catch (Exception ex) { ShowError("状态保存失败：" + ex.Message); }
        }

        private void LoadNotes()
        {
            try
            {
                if (!File.Exists(notesPath)) return;
                var saved = ConfigurationStore.ReadRecords(notesPath);
                if (saved == null) return;
                foreach (var pair in saved)
                {
                    if (!String.IsNullOrWhiteSpace(pair.Value)) notes[pair.Key] = pair.Value;
                }
            }
            catch { /* A corrupt notes file must not prevent browsing. */ }
        }

        private void SaveNotes()
        {
            try
            {
                ConfigurationStore.WriteRecords(notesPath, notes);
            }
            catch (Exception ex) { ShowError("备注保存失败：" + ex.Message); }
        }
    }
}
