using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using LocalConverter;

static class OfficeIntegration
{
    [DllImport("user32.dll")]static extern bool SetProcessDPIAware();
    static int count;static void Check(bool value,string message){if(!value)throw new Exception(message);count++;Console.WriteLine("PASS "+message);}
    static T Field<T>(MainForm f,string name){return (T)typeof(MainForm).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(f);}
    static object Call(MainForm f,string name,params object[] args){SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());var value=typeof(MainForm).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f,args);if(name=="AddFiles"){var task=Field<Task>(f,"admissionTask");while(!task.IsCompleted){Application.DoEvents();Thread.Sleep(5);}task.GetAwaiter().GetResult();}return value;}
    static ResultSession Run(MainForm f){var task=(Task)Call(f,"ProcessFiles");while(!task.IsCompleted){Application.DoEvents();Thread.Sleep(15);}task.GetAwaiter().GetResult();var result=(ResultSession)typeof(MainForm).GetProperty("results",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(f,null);Check(result!=null&&result.Files.Length>0,"UI conversion produces downloadable result");result.Downloaded=true;return result;}
    [STAThread]static int Main(string[] args)
    {
        if(args.Length==5&&args[0]=="--office-worker")return OfficeConverter.RunWorker(args[1],args[2],args[3],args[4]);
        try
        {
            string folder=Path.GetFullPath(args[0]);using(var engine=new PdfEngine())
            {
                if(args.Length>1&&args[1]=="--html-only"){HtmlConverter.ExportFile(Path.Combine(folder,"input.html"),Path.Combine(folder,"from-html.pdf"),CancellationToken.None);Check(File.Exists(Path.Combine(folder,"from-html.pdf")),"HTML print succeeds");return 0;}
                foreach(var ext in new[]{"xlsx","pptx","html","xls","ppt"}.Where(ext=>File.Exists(Path.Combine(folder,"input."+ext))))
                {
                    string input=Path.Combine(folder,"input."+ext),output=Path.Combine(folder,"from-"+ext+".pdf");byte[] before=File.ReadAllBytes(input);
                    if(ext=="html")
                    {
                        var listener=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,18764);listener.Start();
                        try{HtmlConverter.ExportFile(input,output,CancellationToken.None);Check(!listener.Pending(),"HTML printing makes no request for remote loopback image");}finally{listener.Stop();}
                    }
                    else OfficeConverter.ExportFile(input,output,CancellationToken.None);
                    var inspected=engine.Run("inspect",new Dictionary<string,object>{{"files",new[]{output}}},CancellationToken.None);
                    Check(System.Convert.ToInt32(inspected["count"])>0,ext+" converts through desktop component to valid PDF");
                    var text=engine.Run("convert",new Dictionary<string,object>{{"files",new[]{output}},{"format","txt"},{"folder",Path.Combine(folder,"text-"+ext)}},CancellationToken.None);string content=File.ReadAllText(PdfEngine.Outputs(text)[0]);
                    Check(content.Contains("Alpha"),ext+" PDF preserves source text");if(ext=="html")Check(!content.Contains("SCRIPT_EXECUTED"),"HTML printing disables page scripts");
                    Check(before.SequenceEqual(File.ReadAllBytes(input)),ext+" input remains unchanged");
                }
                foreach(string ext in new[]{"xlsx","pptx"})
                {
                    string converted=Directory.GetFiles(folder,"*."+ext,SearchOption.AllDirectories).First(p=>Path.GetFileName(p)!="input."+ext);string roundtrip=Path.Combine(folder,"roundtrip-"+ext+".pdf");OfficeConverter.ExportFile(converted,roundtrip,CancellationToken.None);
                    Check(new FileInfo(roundtrip).Length>100, "generated "+ext+" opens in desktop Office and exports to PDF");
                }
            }
            SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            using(var form=new MainForm(new string[0]))
            {
                form.Show();Application.DoEvents();var format=Field<ComboBox>(form,"format");Check(new[]{"XLSX","PPTX","HTML"}.All(x=>format.Items.Contains(x)),"UI exposes all new outputs");
                Call(form,"AddFiles",(object)new[]{Path.Combine(folder,"input.xlsx"),Path.Combine(folder,"input.pptx"),Path.Combine(folder,"input.html")});Check(Field<FileCards>(form,"cards").Items.Count==3,"UI accepts spreadsheet presentation and HTML inputs");
                var merged=Run(form);Check(merged.Files.Length==1&&Path.GetExtension(merged.Files[0])==".pdf","mixed Excel PPT HTML inputs combine as PDF");
                var cards=Field<FileCards>(form,"cards");cards.Items.Clear();Call(form,"AddFiles",(object)new[]{Path.Combine(folder,"table.pdf")});
                foreach(var target in new[]{"XLSX","PPTX","HTML"}){format.SelectedItem=target;Application.DoEvents();if(target=="HTML"){var style=Field<ComboBox>(form,"htmlMode");Check(style.Visible,"HTML output reveals appearance and text options");style.SelectedIndex=1;using(var bmp=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bmp,new Rectangle(Point.Empty,form.Size));bmp.Save(Path.Combine(folder,"ui-html.png"));}}var result=Run(form);Check(result.Files.Length==1&&Path.GetExtension(result.Files[0]).Equals("."+target,StringComparison.OrdinalIgnoreCase),"UI PDF to "+target+" uses correct extension");}
                format.SelectedItem="PDF";Check(Field<ComboBox>(form,"htmlMode").Parent==null||!Field<ComboBox>(form,"htmlMode").Visible,"HTML-only options hide for PDF output");form.Close();while(!form.IsDisposed){Application.DoEvents();Thread.Sleep(10);}
            }
            Console.WriteLine("ALL "+count+" OFFICE/UI INTEGRATION CHECKS PASSED");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
