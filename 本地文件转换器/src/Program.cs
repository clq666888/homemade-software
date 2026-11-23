using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

[assembly: AssemblyTitle("文件万能处理器")]
[assembly: AssemblyDescription("离线 PDF、Office、图片和 HTML 文件处理")]
[assembly: AssemblyCompany("本地工具")]
[assembly: AssemblyProduct("文件万能处理器")]
[assembly: AssemblyVersion("0.4.3.0")]
[assembly: AssemblyFileVersion("0.4.3.0")]
namespace LocalConverter
{
    static class Program
    {
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
        [STAThread] static int Main(string[] args)
        {
            if (args.Length == 4 && args[0] == "--word-worker") return WordConverter.RunWorker(args[1], args[2], args[3]);
            if (args.Length == 5 && args[0] == "--office-worker") return OfficeConverter.RunWorker(args[1],args[2],args[3],args[4]);
            try
            {
                SetProcessDPIAware();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm(args));
                return 0;
            }
            catch (Exception e) { MessageBox.Show("软件未能正常运行：\r\n" + e.Message, "文件万能处理器", MessageBoxButtons.OK, MessageBoxIcon.Error); return 1; }
        }
    }
}
