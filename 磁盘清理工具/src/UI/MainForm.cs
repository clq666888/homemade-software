using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace DiskCleanupTool
{
    internal sealed partial class MainForm : Form
    {
        private const string NotCleaned = "未清理";
        private const string Inaccessible = "无法访问";
        private const string Cleaned = "已清理";
        private const string NotNeeded = "不需要清理";

        private readonly TextBox pathBox = new TextBox();
        private readonly TextBox searchBox = new TextBox();
        private readonly DataGridView grid = new BufferedDataGridView();
        private readonly ContextMenuStrip rowMenu = new ContextMenuStrip();
        private readonly ToolTip accessTip = new ToolTip();
        private readonly Dictionary<string, Image> iconCache = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        private readonly FolderSizeCache folderSizeCache = new FolderSizeCache();
        private readonly Label infoLabel = new Label();
        private readonly Dictionary<string, string> statuses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> notes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly string statePath;
        private readonly string notesPath;
        private readonly string lastPathPath;
        private readonly string configPath;
        private string lastConfigFile;
        private string currentPath = @"C:\";
        private bool filling;
        private bool isDeleting;
        private bool dragSelecting;
        private int dragStartRow = -1;
        private readonly HashSet<string> selectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private string contextPath;
        private bool allowPathEdit;
        private int activeSortColumn = -1;
        private int sortState;
        private volatile int folderScanVersion;
        private bool fittingColumns;
        private readonly System.Windows.Forms.Timer sizeSortTimer = new System.Windows.Forms.Timer();
        private bool sizeSortPending;
        private int lastGridScrollTick = unchecked(Environment.TickCount - 1000);

        private sealed class ViewportAnchor
        {
            public string TopPath;
            public int RowIndex;
            public int HorizontalOffset;
        }

        public MainForm()
        {
            Text = "磁盘清理工具";
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { /* The tool remains usable if Windows cannot load its icon. */ }
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(450, 300);
            Rectangle screen = Screen.PrimaryScreen.WorkingArea;
            Size = new Size((int)(screen.Width * 0.88), (int)(screen.Height * 0.82));
            BackColor = Color.FromArgb(16, 16, 16);
            ForeColor = Color.Gainsboro;
            Font = new Font("Microsoft YaHei UI", 9F);

            statePath = Path.Combine(Application.StartupPath, "status.json");
            notesPath = Path.Combine(Application.StartupPath, "notes.json");
            lastPathPath = Path.Combine(Application.StartupPath, "lastpath.txt");
            configPath = Path.Combine(Application.StartupPath, "当前配置.json");
            lastConfigFile = configPath;
            LoadStatuses();
            LoadNotes();
            BuildUi();
            sizeSortTimer.Interval = 200;
            sizeSortTimer.Tick += delegate
            {
                if (!sizeSortPending || activeSortColumn != 2 || sortState == 0)
                {
                    sizeSortPending = false;
                    sizeSortTimer.Stop();
                    return;
                }
                if (filling || isDeleting || dragSelecting || grid.IsCurrentCellInEditMode ||
                    rowMenu.Visible || Control.MouseButtons != MouseButtons.None ||
                    unchecked((uint)(Environment.TickCount - lastGridScrollTick)) < 250) return;
                sizeSortPending = false;
                sizeSortTimer.Stop();
                SortRows(true);
            };
            FormClosed += delegate { sizeSortTimer.Dispose(); };
            Shown += delegate { FitColumnsToViewport(); };
            string initialPath = @"C:\";
            try
            {
                if (File.Exists(lastPathPath))
                {
                    string savedPath = File.ReadAllText(lastPathPath, Encoding.UTF8).Trim();
                    if (Directory.Exists(savedPath)) initialPath = savedPath;
                }
            }
            catch { /* Start at C: if the last path cannot be read. */ }
            OpenFolder(initialPath);
        }

        private void ShowError(string message)
        {
            infoLabel.Text = message;
            MessageBox.Show(this, message, "磁盘清理工具", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
