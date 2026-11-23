using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace DiskCleanupTool
{
    internal sealed partial class MainForm
    {
        private void BuildUi()
        {
            var top = new TableLayoutPanel();
            top.Dock = DockStyle.Top;
            top.Height = 130;
            top.Padding = new Padding(12, 10, 12, 6);
            top.BackColor = Color.FromArgb(16, 16, 16);
            top.ColumnCount = 5;
            top.RowCount = 3;
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            top.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            top.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            top.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));

            var pathLabel = new Label();
            pathLabel.Text = "目录";
            pathLabel.Dock = DockStyle.Fill;
            pathLabel.TextAlign = ContentAlignment.MiddleLeft;
            pathLabel.AutoSize = true;
            pathLabel.Margin = new Padding(0, 0, 8, 0);
            top.Controls.Add(pathLabel, 0, 0);

            pathBox.Dock = DockStyle.Fill;
            pathBox.BackColor = Color.FromArgb(30, 30, 30);
            pathBox.ForeColor = Color.White;
            pathBox.BorderStyle = BorderStyle.FixedSingle;
            pathBox.Font = new Font("Consolas", 11F);
            pathBox.Margin = new Padding(0, 3, 8, 3);
            pathBox.KeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    OpenFolder(pathBox.Text);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };
            top.Controls.Add(pathBox, 1, 0);

            AddButton(top, "打开", 2, 0, delegate { OpenFolder(pathBox.Text); });
            AddButton(top, "上一级", 3, 0, GoUp);
            AddButton(top, "刷新", 4, 0, delegate { OpenFolder(currentPath, true); });

            var actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Fill;
            actions.WrapContents = false;
            actions.AutoScroll = true;
            actions.Margin = new Padding(0);
            top.Controls.Add(actions, 0, 1);
            top.SetColumnSpan(actions, 5);
            AddActionButton(actions, "复制 ls", CopyCommand);
            AddActionButton(actions, "复制给 AI", CopyListing);
            AddActionButton(actions, "保存配置", SaveConfiguration);
            AddActionButton(actions, "导入配置", ImportConfiguration);
            AddActionButton(actions, "重置状态", ResetAllStatuses);
            var selectionHint = new Label();
            selectionHint.Text = "Ctrl+点可多选；拖动图标可框选；F2 选名称文字";
            selectionHint.AutoSize = true;
            selectionHint.ForeColor = Color.Silver;
            selectionHint.Margin = new Padding(14, 8, 0, 0);
            actions.Controls.Add(selectionHint);

            var searchLabel = new Label { Text = "搜索", AutoSize = true, Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 0, 8, 0) };
            top.Controls.Add(searchLabel, 0, 2);
            searchBox.Dock = DockStyle.Fill;
            searchBox.BackColor = Color.FromArgb(30, 30, 30);
            searchBox.ForeColor = Color.White;
            searchBox.Margin = new Padding(0, 3, 8, 3);
            top.Controls.Add(searchBox, 1, 2);
            top.SetColumnSpan(searchBox, 3);
            AddButton(top, "清空搜索", 4, 2, delegate { searchBox.Clear(); searchBox.Focus(); });
            searchBox.TextChanged += delegate { if (!filling) ApplyNameFilter(); };

            grid.Dock = DockStyle.Fill;
            grid.BackgroundColor = Color.FromArgb(16, 16, 16);
            grid.GridColor = Color.FromArgb(55, 55, 55);
            grid.BorderStyle = BorderStyle.None;
            grid.RowHeadersVisible = false;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.AllowUserToResizeColumns = true;
            grid.MultiSelect = true;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            grid.ScrollBars = ScrollBars.Both;
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            grid.ShowCellToolTips = true;
            grid.RowTemplate.Height = 28;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersHeight = 36;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(35, 35, 35);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold);
            grid.DefaultCellStyle.Font = new Font("Consolas", 10F);
            grid.DefaultCellStyle.Padding = new Padding(3, 0, 3, 0);
            grid.EditMode = DataGridViewEditMode.EditOnEnter;
            grid.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText;

            var nameColumn = new DataGridViewTextBoxColumn();
            nameColumn.Name = "FullPath";
            nameColumn.HeaderText = "名称（文件夹可双击进入）";
            nameColumn.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            nameColumn.Width = 400;
            nameColumn.MinimumWidth = 180;
            nameColumn.ReadOnly = false;
            nameColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
            grid.Columns.Add(nameColumn);

            var modifiedColumn = new DataGridViewTextBoxColumn();
            modifiedColumn.Name = "Modified";
            modifiedColumn.HeaderText = "修改时间";
            modifiedColumn.Width = 155;
            modifiedColumn.ReadOnly = true;
            modifiedColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
            grid.Columns.Add(modifiedColumn);

            var sizeColumn = new DataGridViewTextBoxColumn();
            sizeColumn.Name = "Size";
            sizeColumn.HeaderText = "文件大小";
            sizeColumn.Width = 105;
            sizeColumn.ReadOnly = true;
            sizeColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
            grid.Columns.Add(sizeColumn);

            var statusColumn = new DataGridViewComboBoxColumn();
            statusColumn.Name = "Status";
            statusColumn.HeaderText = "清理状态";
            statusColumn.Width = 145;
            statusColumn.FlatStyle = FlatStyle.Flat;
            statusColumn.Items.AddRange(NotCleaned, Inaccessible, Cleaned, NotNeeded);
            statusColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
            grid.Columns.Add(statusColumn);

            var noteColumn = new DataGridViewTextBoxColumn();
            noteColumn.Name = "Note";
            noteColumn.HeaderText = "备注";
            noteColumn.Width = 150;
            noteColumn.MinimumWidth = 140;
            noteColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
            grid.Columns.Add(noteColumn);

            var iconColumn = new DataGridViewImageColumn();
            iconColumn.Name = "Icon";
            iconColumn.HeaderText = "";
            iconColumn.Width = 30;
            iconColumn.ReadOnly = true;
            iconColumn.ImageLayout = DataGridViewImageCellLayout.Normal;
            iconColumn.SortMode = DataGridViewColumnSortMode.NotSortable;
            grid.Columns.Add(iconColumn);
            iconColumn.DisplayIndex = 0;
            grid.Resize += delegate { FitColumnsToViewport(-1); };
            grid.Scroll += delegate { lastGridScrollTick = Environment.TickCount; };
            grid.ColumnWidthChanged += delegate(object sender, DataGridViewColumnEventArgs e)
            {
                if (!fittingColumns) FitColumnsToViewport(e.Column.Index);
            };
            grid.CellPainting += PaintSortHeader;
            grid.CellPainting += PaintSearchMatch;
            grid.ColumnHeaderMouseClick += ClickSortHeader;
            grid.MouseDown += StartDragSelection;
            grid.MouseMove += ContinueDragSelection;
            grid.MouseUp += delegate { dragSelecting = false; };
            grid.CellMouseDown += CaptureActionSelection;
            grid.CellClick += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex >= 0 && e.ColumnIndex == 3 && selectedPaths.Count > 1)
                    ScheduleRestoreSelection();
            };
            grid.CellBeginEdit += delegate(object sender, DataGridViewCellCancelEventArgs e)
            {
                if (e.ColumnIndex == 0 && !allowPathEdit) e.Cancel = true;
            };
            grid.EditingControlShowing += delegate(object sender, DataGridViewEditingControlShowingEventArgs e)
            {
                var combo = e.Control as ComboBox;
                if (combo != null && grid.CurrentCell != null && grid.CurrentCell.ColumnIndex == 3)
                {
                    var currentEntry = grid.Rows[grid.CurrentCell.RowIndex].Tag as Entry;
                    accessTip.SetToolTip(combo, currentEntry == null ? "" :
                        AccessToolTip(currentEntry, Convert.ToString(grid.CurrentCell.Value)));
                }
                var text = e.Control as TextBox;
                if (text != null)
                {
                    bool isPath = grid.CurrentCell != null && grid.CurrentCell.ColumnIndex == 0;
                    text.ReadOnly = isPath;
                    if (isPath) text.SelectAll();
                }
            };
            grid.KeyDown += OnGridKeyDown;

            grid.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex < 0 || e.RowIndex >= grid.Rows.Count) return;
                var entry = grid.Rows[e.RowIndex].Tag as Entry;
                if (entry != null && entry.IsDirectory) OpenFolder(entry.Path);
            };
            grid.CurrentCellDirtyStateChanged += delegate
            {
                if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            grid.CellValueChanged += OnCellValueChanged;
            grid.CellEndEdit += delegate
            {
                allowPathEdit = false;
                if (activeSortColumn == 2 && sortState != 0)
                    BeginInvoke((Action)delegate { if (!IsDisposed) SortRows(); });
            };
            BuildRowMenu();
            grid.ContextMenuStrip = rowMenu;
            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                if (isDeleting)
                {
                    e.Cancel = true;
                    MessageBox.Show(this, "文件正在删除，请等待操作完成后再关闭窗口。",
                        "磁盘清理工具", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                grid.EndEdit();
            };
            grid.DataError += delegate(object sender, DataGridViewDataErrorEventArgs e)
            {
                e.ThrowException = false;
            };

            infoLabel.Dock = DockStyle.Bottom;
            infoLabel.Height = 29;
            infoLabel.Padding = new Padding(12, 5, 0, 0);
            infoLabel.BackColor = Color.FromArgb(28, 28, 28);
            infoLabel.ForeColor = Color.Silver;

            Controls.Add(grid);
            Controls.Add(infoLabel);
            Controls.Add(top);
        }

        private void AddButton(TableLayoutPanel top, string label, int column, int row, Action action)
        {
            top.Controls.Add(CreateButton(label, action), column, row);
        }

        private void AddActionButton(FlowLayoutPanel panel, string label, Action action)
        {
            panel.Controls.Add(CreateButton(label, action));
        }

        private Button CreateButton(string label, Action action)
        {
            var button = new Button();
            button.Text = label;
            button.AutoSize = true;
            button.MinimumSize = new Size(74, 28);
            button.BackColor = Color.FromArgb(43, 43, 43);
            button.ForeColor = Color.White;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.FromArgb(80, 80, 80);
            button.Margin = new Padding(0, 2, 7, 2);
            button.Click += delegate { action(); };
            return button;
        }

        private void FitColumnsToViewport(int changedColumn = -1)
        {
            if (fittingColumns || grid.Columns.Count != 6 || grid.ClientSize.Width < 250) return;
            fittingColumns = true;
            try
            {
                bool verticalScrollbar = grid.Controls.OfType<VScrollBar>().Any(scrollbar => scrollbar.Visible);
                int available = grid.ClientSize.Width
                    - (verticalScrollbar ? SystemInformation.VerticalScrollBarWidth : 0) - 2;
                int delta = available - grid.Columns.Cast<DataGridViewColumn>().Sum(column => column.Width);
                int first = changedColumn == 0 ? 4 : 0;
                int second = first == 0 ? 4 : 0;
                if (delta > 0) grid.Columns[first].Width += delta;
                else if (delta < 0)
                {
                    foreach (int index in new[] { first, second })
                    {
                        int shrink = Math.Min(-delta, grid.Columns[index].Width - grid.Columns[index].MinimumWidth);
                        grid.Columns[index].Width -= shrink;
                        delta += shrink;
                        if (delta == 0) break;
                    }
                }
            }
            finally { fittingColumns = false; }
        }
    }
}
