using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

// Runs entirely beside this checker under .build/checks, without changing user records.
internal static class RegressionChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    private static object Field(object target, string name)
    { return target.GetType().GetField(name, Private).GetValue(target); }
    private static void Set(object target, string name, object value)
    { target.GetType().GetField(name, Private).SetValue(target, value); }
    private static object Invoke(object target, string name, params object[] args)
    { return target.GetType().GetMethod(name, Private).Invoke(target, args); }
    private static void Assert(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        checks++;
        Console.WriteLine("PASS " + description);
    }
    private static object Static(Assembly assembly, string type, string method, params object[] args)
    { return assembly.GetType("DiskCleanupTool." + type).GetMethod(method).Invoke(null, args); }
    private static void Pump(int milliseconds)
    {
        var watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < milliseconds) { Application.DoEvents(); Thread.Sleep(10); }
    }
    private static string RowName(DataGridViewRow row) { return Convert.ToString(row.Cells[0].Value); }

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();
            string sandbox = Application.StartupPath;
            string fixture = Path.Combine(sandbox, "fixture");
            Directory.CreateDirectory(fixture);
            Directory.CreateDirectory(Path.Combine(fixture, "ALPHABET"));
            File.WriteAllText(Path.Combine(fixture, "Alpha.txt"), "abc");
            File.WriteAllText(Path.Combine(fixture, "beta.txt"), "1234567");
            for (int i = 0; i < 60; i++) File.WriteAllText(Path.Combine(fixture, "row" + i.ToString("00") + ".txt"), "x");
            File.WriteAllText(Path.Combine(sandbox, "lastpath.txt"), fixture);
            File.WriteAllText(Path.Combine(sandbox, "status.json"), "{}");
            File.WriteAllText(Path.Combine(sandbox, "notes.json"), "{}");
            var assembly = Assembly.LoadFile(Path.GetFullPath(args[0]));
            var signature = CheckWindow(assembly, fixture);
            if (args.Length > 1)
            {
                var baseline = Assembly.LoadFile(Path.GetFullPath(args[1]));
                Assert(signature == CheckWindow(baseline, fixture), "baseline and refactored list behavior agree");
            }
            CheckServices(assembly, fixture, sandbox);
            Console.WriteLine("Checks passed: " + checks);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static string CheckWindow(Assembly assembly, string fixture)
    {
        using (var form = (Form)Activator.CreateInstance(assembly.GetType("DiskCleanupTool.MainForm"), true))
        {
            form.ShowInTaskbar = false;
            form.Opacity = 0;
            form.Size = new Size(980, 520);
            form.Show();
            var grid = (DataGridView)Field(form, "grid");
            if (assembly.GetType("DiskCleanupTool.BufferedDataGridView") != null)
                Assert((bool)typeof(Control).GetProperty("DoubleBuffered", Private).GetValue(grid, null), "offscreen buffering enabled");
            var search = (TextBox)Field(form, "searchBox");
            Pump(300);
            search.Text = "alpha";
            Assert(grid.Rows.Cast<DataGridViewRow>().Count(row => row.Visible) == 2, "case-insensitive name search");
            using (var image = new Bitmap(grid.Width, grid.Height))
                grid.DrawToBitmap(image, new Rectangle(0, 0, grid.Width, grid.Height));
            var selected = (HashSet<string>)Field(form, "selectedPaths");
            selected.Add(Path.Combine(fixture, "beta.txt"));
            Invoke(form, "ApplyNameFilter");
            Assert(selected.Count == 0, "hidden rows excluded from selection");
            search.Text = "no-result";
            Assert(grid.Rows.Cast<DataGridViewRow>().All(row => !row.Visible), "zero-match search");
            search.Clear();
            Assert(grid.Rows.Cast<DataGridViewRow>().All(row => row.Visible), "clearing search restores list");
            var menu = (ContextMenuStrip)Field(form, "rowMenu");
            Assert(menu.Items[0].Text == "刷新选中项" && menu.Items[menu.Items.Count - 1].Text == "属性", "context menu actions retained");
            var paths = new[] { Path.Combine(fixture, "Alpha.txt"), Path.Combine(fixture, "beta.txt") };
            Invoke(form, "ApplyStatusToPaths", new List<string>(paths), "已清理");
            Assert(selected.Count == 2 && grid.Rows.Cast<DataGridViewRow>().Where(row => paths.Contains(((object)row.Tag).GetType().GetField("Path").GetValue(row.Tag).ToString())).All(row => Convert.ToString(row.Cells[3].Value) == "已清理"), "batch status preserves multiselection");
            string signature = String.Join("|", grid.Rows.Cast<DataGridViewRow>().Select(row => RowName(row) + ":" + row.Cells[3].Value));
            Set(form, "activeSortColumn", 2);
            Set(form, "sortState", 1);
            grid.EndEdit();
            var folderRow = grid.Rows.Cast<DataGridViewRow>().First(row => RowName(row) == "ALPHABET");
            folderRow.Tag.GetType().GetField("FolderSize").SetValue(folderRow.Tag, null);
            Invoke(form, "RequestSizeSort");
            Pump(450);
            Assert(RowName(grid.Rows[0]) == "beta.txt" && RowName(grid.Rows[grid.Rows.Count - 1]) == "ALPHABET", "known sizes sort before unfinished folder");
            folderRow.Tag.GetType().GetField("FolderSize").SetValue(folderRow.Tag, (long?)999999);
            bool buffered = assembly.GetType("DiskCleanupTool.BufferedDataGridView") != null;
            if (buffered) Set(form, "lastGridScrollTick", Environment.TickCount);
            Invoke(form, "RequestSizeSort");
            if (buffered)
            {
                Pump(100);
                Assert(RowName(grid.Rows[0]) == "beta.txt", "automatic sort deferred while scrolling");
            }
            Pump(450);
            Assert(RowName(grid.Rows[0]) == "ALPHABET", "completed size moves immediately after scrolling stops");
            grid.FirstDisplayedScrollingRowIndex = 20;
            var anchor = Invoke(form, "CaptureViewport");
            grid.FirstDisplayedScrollingRowIndex = 0;
            Invoke(form, "RestoreViewport", anchor);
            Assert(grid.FirstDisplayedScrollingRowIndex == 20, "viewport restoration");
            form.Close();
            return signature;
        }
    }

    private static void CheckServices(Assembly assembly, string fixture, string sandbox)
    {
        object[] scanArgs = { fixture, new Func<bool>(() => false), false, false, false, null };
        long bytes = (long)Static(assembly, "FolderSizeScanner", "CalculateFolderSize", scanArgs);
        Assert(bytes == 70 && !(bool)scanArgs[2], "independent recursive size scan");
        scanArgs = new object[] { fixture, new Func<bool>(() => true), false, false, false, null };
        Assert((long)Static(assembly, "FolderSizeScanner", "CalculateFolderSize", scanArgs) == 0, "scan cancellation");
        var cacheType = assembly.GetType("DiskCleanupTool.FolderSizeCache");
        var cache = Activator.CreateInstance(cacheType, true);
        var result = Activator.CreateInstance(assembly.GetType("DiskCleanupTool.FolderSizeResult"), true);
        string child = Path.Combine(fixture, "ALPHABET");
        string descendant = Path.Combine(child, "nested");
        string other = Path.Combine(sandbox, "other");
        foreach (string key in new[] { fixture, child, descendant, other }) cacheType.GetProperty("Item").SetValue(cache, result, new object[] { key });
        cacheType.GetMethod("InvalidateCurrentPage").Invoke(cache, new object[] { fixture });
        Assert(!Cached(cache, child) && Cached(cache, descendant) && Cached(cache, fixture) && Cached(cache, other), "refresh invalidates current page only");
        cacheType.GetMethod("InvalidateAffectedPath").Invoke(cache, new object[] { descendant });
        Assert(!Cached(cache, fixture) && !Cached(cache, descendant) && Cached(cache, other), "deletion keeps unrelated cache");
        var snapshotType = assembly.GetType("DiskCleanupTool.ConfigSnapshot");
        var snapshot = Activator.CreateInstance(snapshotType);
        snapshotType.GetProperty("Version").SetValue(snapshot, 1, null);
        snapshotType.GetProperty("CurrentPath").SetValue(snapshot, fixture, null);
        var statuses = new Dictionary<string, string> { { child, "已清理" }, { Path.Combine(fixture, "deleted.txt"), "不需要清理" } };
        var notes = new Dictionary<string, string> { { child, "备注：中文与 \"引号\"\n换行" } };
        snapshotType.GetProperty("Statuses").SetValue(snapshot, statuses, null);
        snapshotType.GetProperty("Notes").SetValue(snapshot, notes, null);
        string config = Path.Combine(sandbox, "roundtrip.json");
        Static(assembly, "ConfigurationStore", "WriteSnapshot", config, snapshot);
        var loaded = Static(assembly, "ConfigurationStore", "ReadSnapshot", config);
        var restoredNotes = (Dictionary<string, string>)snapshotType.GetProperty("Notes").GetValue(loaded, null);
        var restoredStatuses = (Dictionary<string, string>)snapshotType.GetProperty("Statuses").GetValue(loaded, null);
        Assert(restoredNotes[child] == notes[child] && restoredStatuses.Count == 2, "status/note roundtrip with deleted path");
        string legacy = Path.Combine(sandbox, "legacy.json");
        Static(assembly, "ConfigurationStore", "WriteRecords", legacy, notes);
        var records = (Dictionary<string, string>)Static(assembly, "ConfigurationStore", "ReadRecords", legacy);
        Assert(records[child] == notes[child], "legacy automatic records compatible");
        File.WriteAllText(config, "{\"Version\":999}");
        bool rejected = false;
        try { Static(assembly, "ConfigurationStore", "ReadSnapshot", config); }
        catch (TargetInvocationException ex) { rejected = ex.InnerException is InvalidDataException; }
        Assert(rejected, "invalid config rejected before import");
        using (var icon = (Image)Static(assembly, "WindowsShell", "ReadFileIcon", true, "<folder>"))
            Assert(icon.Width > 0, "native shell folder icon");
    }

    private static bool Cached(object cache, string path)
    {
        object[] args = { path, null };
        return (bool)cache.GetType().GetMethod("TryGetValue").Invoke(cache, args);
    }
}
