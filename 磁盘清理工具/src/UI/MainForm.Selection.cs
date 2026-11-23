using System;
using System.Drawing;
using System.Windows.Forms;

namespace DiskCleanupTool
{
    internal sealed partial class MainForm
    {
        protected override bool ProcessCmdKey(ref Message message, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.F))
            { searchBox.Focus(); searchBox.SelectAll(); return true; }
            if (keyData == Keys.Escape && searchBox.Focused)
            { searchBox.Clear(); grid.Focus(); return true; }
            if (keyData == Keys.F5 && !isDeleting)
            { OpenFolder(currentPath, true); return true; }
            if (grid.ContainsFocus)
            {
                bool editingText = grid.IsCurrentCellInEditMode && grid.EditingControl is TextBox;
                if (!editingText && keyData == Keys.Delete)
                { DeleteSelectedEntries(); return true; }
                if (!editingText && keyData == (Keys.Control | Keys.A))
                {
                    selectedPaths.Clear();
                    foreach (DataGridViewRow row in grid.Rows)
                        if (row.Visible && row.Tag is Entry) selectedPaths.Add(((Entry)row.Tag).Path);
                    RestoreSelectedRows();
                    return true;
                }
                if (!editingText && keyData == (Keys.Alt | Keys.Enter))
                {
                    contextPath = grid.CurrentRow == null ? null : ((Entry)grid.CurrentRow.Tag).Path;
                    ShowFileProperties(); return true;
                }
            }
            return base.ProcessCmdKey(ref message, keyData);
        }

        private void OnGridKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.C && !grid.IsCurrentCellInEditMode)
            {
                CopySelectedPaths();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.F2 && grid.CurrentCell != null && grid.CurrentCell.ColumnIndex == 0)
            {
                allowPathEdit = true;
                grid.BeginEdit(true);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void SetRowColor(DataGridViewRow row, string status)
        {
            Color color = status == NotNeeded ? Color.FromArgb(12, 54, 42) :
                status == Cleaned ? Color.FromArgb(31, 112, 59) :
                status == Inaccessible ? Color.FromArgb(93, 72, 16) : Color.FromArgb(89, 29, 32);
            var entry = row.Tag as Entry;
            Color shown = entry != null && selectedPaths.Contains(entry.Path)
                ? Color.FromArgb(35, 109, 198) : color;
            var style = row.DefaultCellStyle;
            if (style.BackColor != shown) style.BackColor = shown;
            if (style.SelectionBackColor != shown) style.SelectionBackColor = shown;
            if (style.ForeColor != Color.White) style.ForeColor = Color.White;
            if (style.SelectionForeColor != Color.White) style.SelectionForeColor = Color.White;
        }

        private void StartDragSelection(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || isDeleting) return;
            var hit = grid.HitTest(e.X, e.Y);
            if (hit.RowIndex < 0 || (hit.ColumnIndex != 0 && hit.ColumnIndex != 5)) return;
            if ((ModifierKeys & Keys.Control) != 0) return;
            grid.EndEdit();
            if ((ModifierKeys & Keys.Shift) != 0 && dragStartRow >= 0)
            {
                SelectRowRange(dragStartRow, hit.RowIndex);
                ScheduleRestoreSelection();
                return;
            }
            dragStartRow = hit.RowIndex;
            dragSelecting = true;
            SelectRowRange(dragStartRow, dragStartRow);
            ScheduleRestoreSelection();
        }

        private void ContinueDragSelection(object sender, MouseEventArgs e)
        {
            if (!dragSelecting || e.Button != MouseButtons.Left || dragStartRow < 0) return;
            var hit = grid.HitTest(e.X, e.Y);
            if (hit.RowIndex >= 0) SelectRowRange(dragStartRow, hit.RowIndex);
        }

        private void SelectRowRange(int first, int last)
        {
            selectedPaths.Clear();
            for (int i = Math.Min(first, last); i <= Math.Max(first, last); i++)
            {
                var entry = grid.Rows[i].Tag as Entry;
                if (entry != null && grid.Rows[i].Visible) selectedPaths.Add(entry.Path);
            }
            RestoreSelectedRows();
        }

        private void CaptureActionSelection(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var clicked = grid.Rows[e.RowIndex].Tag as Entry;
            if (clicked == null) return;
            if (e.Button == MouseButtons.Right)
            {
                contextPath = clicked.Path;
                if (!selectedPaths.Contains(clicked.Path)) { selectedPaths.Clear(); selectedPaths.Add(clicked.Path); }
                ScheduleRestoreSelection();
            }
            else if (e.Button == MouseButtons.Left && (ModifierKeys & Keys.Control) != 0 && e.ColumnIndex != 3)
            {
                if (!selectedPaths.Add(clicked.Path)) selectedPaths.Remove(clicked.Path);
                ScheduleRestoreSelection();
            }
            else if (e.Button == MouseButtons.Left && e.ColumnIndex == 3)
            {
                if (!selectedPaths.Contains(clicked.Path)) { selectedPaths.Clear(); selectedPaths.Add(clicked.Path); }
                ScheduleRestoreSelection();
            }
            else if (e.Button == MouseButtons.Left && e.ColumnIndex != 0 && e.ColumnIndex != 5)
            {
                selectedPaths.Clear(); selectedPaths.Add(clicked.Path);
                ScheduleRestoreSelection();
            }
        }

        private void ScheduleRestoreSelection()
        {
            BeginInvoke((Action)delegate { if (!IsDisposed) RestoreSelectedRows(); });
        }

        private void RestoreSelectedRows()
        {
            foreach (DataGridViewRow row in grid.Rows)
            {
                var entry = row.Tag as Entry;
                if (entry != null) SetRowColor(row, Convert.ToString(row.Cells[3].Value));
            }
            if (selectedPaths.Count > 0) infoLabel.Text = "已选择 " + selectedPaths.Count + " 项";
        }
    }
}
