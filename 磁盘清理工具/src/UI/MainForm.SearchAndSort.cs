using System;
using System.Collections;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace DiskCleanupTool
{
    internal sealed partial class MainForm
    {
        private void ApplyNameFilter()
        {
            grid.EndEdit();
            grid.CurrentCell = null;
            string query = searchBox.Text;
            int matches = 0;
            foreach (DataGridViewRow row in grid.Rows)
            {
                bool visible = String.IsNullOrEmpty(query) || Convert.ToString(row.Cells[0].Value)
                    .IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0;
                row.Visible = visible;
                if (visible) matches++;
                else
                {
                    var entry = row.Tag as Entry;
                    if (entry != null) selectedPaths.Remove(entry.Path);
                }
            }
            contextPath = null;
            RestoreSelectedRows();
            FitColumnsToViewport();
            grid.InvalidateColumn(0);
            infoLabel.Text = "目录 " + currentPath + "    |    显示 " + matches + " / " + grid.Rows.Count + " 项";
        }

        private void PaintSearchMatch(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != 0 || String.IsNullOrEmpty(searchBox.Text)) return;
            string name = Convert.ToString(e.FormattedValue);
            string query = searchBox.Text;
            int match = name.IndexOf(query, StringComparison.CurrentCultureIgnoreCase);
            if (match < 0) return;
            e.Paint(e.ClipBounds, e.PaintParts & ~DataGridViewPaintParts.ContentForeground);
            var bounds = new Rectangle(e.CellBounds.X + e.CellStyle.Padding.Left + 2,
                e.CellBounds.Y, Math.Max(0, e.CellBounds.Width - e.CellStyle.Padding.Horizontal - 4), e.CellBounds.Height);
            var flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.PreserveGraphicsClipping;
            var state = e.Graphics.Save();
            e.Graphics.SetClip(Rectangle.Intersect(bounds, e.ClipBounds));
            TextRenderer.DrawText(e.Graphics, name, e.CellStyle.Font, bounds, Color.White, flags);
            while (match >= 0)
            {
                int prefix = TextRenderer.MeasureText(e.Graphics, name.Substring(0, match), e.CellStyle.Font,
                    new Size(Int32.MaxValue, bounds.Height), flags).Width;
                int width = TextRenderer.MeasureText(e.Graphics, name.Substring(match, query.Length), e.CellStyle.Font,
                    new Size(Int32.MaxValue, bounds.Height), flags).Width;
                var highlight = new Rectangle(bounds.X + prefix, bounds.Y + 3, width, Math.Max(0, bounds.Height - 6));
                using (var brush = new SolidBrush(Color.FromArgb(255, 216, 90))) e.Graphics.FillRectangle(brush, highlight);
                var textBounds = new Rectangle(highlight.X, bounds.Y, width, bounds.Height);
                TextRenderer.DrawText(e.Graphics, name.Substring(match, query.Length), e.CellStyle.Font,
                    textBounds, Color.FromArgb(25, 25, 25), flags);
                match = name.IndexOf(query, match + query.Length, StringComparison.CurrentCultureIgnoreCase);
            }
            e.Graphics.Restore(state);
            e.Handled = true;
        }

        private void PaintSortHeader(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex != -1 || e.ColumnIndex < 0 || e.ColumnIndex > 2) return;
            e.Paint(e.CellBounds, DataGridViewPaintParts.Background | DataGridViewPaintParts.Border);
            var title = new Rectangle(e.CellBounds.X + 7, e.CellBounds.Y,
                Math.Max(0, e.CellBounds.Width - 34), e.CellBounds.Height);
            TextRenderer.DrawText(e.Graphics, Convert.ToString(e.FormattedValue),
                grid.ColumnHeadersDefaultCellStyle.Font, title, Color.White,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            bool active = activeSortColumn == e.ColumnIndex && sortState != 0;
            string arrow = active ? (sortState == 1 ? "▼" : "▲") : "▽";
            using (var arrowFont = new Font("Microsoft YaHei UI", 11F,
                active ? FontStyle.Bold : FontStyle.Regular))
            {
                var arrowRect = new Rectangle(e.CellBounds.Right - 29, e.CellBounds.Y, 24, e.CellBounds.Height);
                TextRenderer.DrawText(e.Graphics, arrow, arrowFont, arrowRect,
                    active ? Color.White : Color.FromArgb(115, 115, 115),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            e.Handled = true;
        }

        private void ClickSortHeader(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.ColumnIndex < 0 || e.ColumnIndex > 2 || e.X < grid.Columns[e.ColumnIndex].Width - 32) return;
            grid.EndEdit();
            if (activeSortColumn == e.ColumnIndex) sortState = (sortState + 1) % 3;
            else { activeSortColumn = e.ColumnIndex; sortState = 1; }
            if (sortState == 0) activeSortColumn = -1;
            BeginInvoke((Action)delegate { if (!IsDisposed) SortRows(); });
            for (int i = 0; i < 3; i++) grid.InvalidateCell(grid.Columns[i].HeaderCell);
        }

        private void SortRows(bool preserveViewport = false)
        {
            if (grid.Rows.Count < 2 || grid.IsCurrentCellInEditMode) return;
            var anchor = preserveViewport ? CaptureViewport() : null;
            if (anchor != null && anchor.RowIndex == 0) anchor.TopPath = null;
            grid.Sort(new EntryRowComparer(activeSortColumn, sortState));
            if (anchor != null) RestoreViewport(anchor);
            RestoreSelectedRows();
        }

        private void RequestSizeSort()
        {
            if (activeSortColumn != 2 || sortState == 0) return;
            sizeSortPending = true;
            sizeSortTimer.Start();
        }

        private ViewportAnchor CaptureViewport()
        {
            int index = grid.FirstDisplayedScrollingRowIndex;
            string path = null;
            if (index >= 0 && index < grid.Rows.Count)
            {
                var entry = grid.Rows[index].Tag as Entry;
                if (entry != null) path = entry.Path;
            }
            return new ViewportAnchor
            {
                TopPath = path, RowIndex = Math.Max(0, index),
                HorizontalOffset = grid.HorizontalScrollingOffset
            };
        }

        private void RestoreViewport(ViewportAnchor anchor)
        {
            if (anchor == null || grid.Rows.Count == 0) return;
            int index = Math.Min(anchor.RowIndex, grid.Rows.Count - 1);
            if (anchor.TopPath != null)
            {
                for (int i = 0; i < grid.Rows.Count; i++)
                {
                    var entry = grid.Rows[i].Tag as Entry;
                    if (entry != null && String.Equals(entry.Path, anchor.TopPath, StringComparison.OrdinalIgnoreCase))
                    { index = i; break; }
                }
            }
            try { grid.FirstDisplayedScrollingRowIndex = index; }
            catch (InvalidOperationException) { /* A short list may not scroll. */ }
            catch (ArgumentOutOfRangeException) { /* The anchor row may have disappeared. */ }
            try { grid.HorizontalScrollingOffset = anchor.HorizontalOffset; }
            catch (ArgumentOutOfRangeException) { /* Column widths may have changed. */ }
        }

        private sealed class EntryRowComparer : IComparer
        {
            private readonly int column;
            private readonly int state;
            public EntryRowComparer(int column, int state) { this.column = column; this.state = state; }
            public int Compare(object left, object right)
            {
                var a = ((DataGridViewRow)left).Tag as Entry;
                var b = ((DataGridViewRow)right).Tag as Entry;
                if (a == null || b == null) return 0;
                if (state == 0 || column < 0) return a.DefaultIndex.CompareTo(b.DefaultIndex);
                int result = 0;
                if (column == 0)
                    result = StringComparer.CurrentCultureIgnoreCase.Compare(Path.GetFileName(a.Path), Path.GetFileName(b.Path));
                else if (column == 1)
                    result = a.Modified.CompareTo(b.Modified);
                else if (column == 2)
                {
                    bool aUnknown = a.IsDirectory && !a.FolderSize.HasValue;
                    bool bUnknown = b.IsDirectory && !b.FolderSize.HasValue;
                    if (aUnknown != bUnknown) return aUnknown ? 1 : -1;
                    if (!aUnknown)
                        result = (a.IsDirectory ? a.FolderSize.Value : a.Length)
                            .CompareTo(b.IsDirectory ? b.FolderSize.Value : b.Length);
                }
                if (state == 1) result = -result;
                return result != 0 ? result : a.DefaultIndex.CompareTo(b.DefaultIndex);
            }
        }
    }
}
