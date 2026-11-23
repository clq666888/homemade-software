using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using LocalConverter;

static class LayoutChecks
{
    [DllImport("user32.dll")]static extern bool SetProcessDPIAware();
    static int checks;
    static T Field<T>(MainForm form,string name){return (T)typeof(MainForm).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);}
    static object Call(MainForm form,string name,params object[] args){return typeof(MainForm).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,args);}
    static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
    static void Snapshot(MainForm form,string path){form.PerformLayout();Application.DoEvents();using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(path);}}
    [STAThread]static int Main(string[] args)
    {
        try
        {
            SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            string folder=Path.GetFullPath(args[0]);
            using(var form=new MainForm(new string[0]))
            {
                form.Show();Application.DoEvents();
                var tabs=Field<System.Collections.Generic.Dictionary<string,FunctionTab>>(form,"tabs");
                Check(tabs.Values.OrderBy(t=>t.Left).Select(t=>t.Text).SequenceEqual(new[]{"转换","页面编辑","合并","拆分","加密","移除加密","压缩"}),"navigation order matches requested workflow");
                Check(tabs.First().Value.Parent.Controls.OfType<Panel>().Count()==6,"navigation items have six visible separators");
                Call(form,"SetMode","压缩");Application.DoEvents();var quality=Field<ComboBox>(form,"quality");
                int text=quality.Items.Cast<object>().Max(item=>TextRenderer.MeasureText(item.ToString(),quality.Font).Width);
                Check(quality.Width>=text+SystemInformation.VerticalScrollBarWidth+8&&quality.DropDownWidth>=quality.Width,"compression combo fits longest Chinese label");
                quality.SelectedIndex=1;
                Snapshot(form,Path.Combine(folder,"layout-compress.png"));
                Call(form,"SetMode","页面编辑");Application.DoEvents();Snapshot(form,Path.Combine(folder,"layout-page-edit.png"));
                Call(form,"SetMode","拆分");Application.DoEvents();var split=Field<SplitSettings>(form,"splitSettings");
                Check(split.Method.SelectedIndex==0&&!split.CountPicker.Visible&&!split.TopBottom.Visible,"per-page mode hides custom and ratio settings");
                Snapshot(form,Path.Combine(folder,"layout-split-each.png"));
                split.Method.SelectedIndex=1;Application.DoEvents();
                Check(split.CountPicker.Visible&&!split.TopBottom.Visible,"custom mode reveals only count and range inputs");
                bool emptyRejected=false;try{split.JobValues();}catch(IOException){emptyRejected=true;}Check(emptyRejected,"blank custom page groups blocked before processing");
                var inputs=new System.Collections.Generic.List<TextBox>();Action<Control> collect=null;collect=c=>{foreach(Control child in c.Controls){var input=child as TextBox;if(input!=null&&input.AccessibleName!=null&&(input.AccessibleName.EndsWith("起始页")||input.AccessibleName.EndsWith("结束页")))inputs.Add(input);collect(child);}};collect(split);
                Check(inputs.Count==4&&inputs.All(t=>t.Text==""),"each custom part starts with two empty page boxes");
                var tilde=inputs[0].Parent.Controls.OfType<Label>().Single(l=>l.Text=="~");Check(Math.Abs(inputs[0].PointToScreen(new Point(0,inputs[0].Height/2)).Y-tilde.PointToScreen(new Point(0,tilde.Height/2)).Y)<=2,"range boxes and tilde align on the same row");
                inputs[0].Text="1";inputs[1].Text="2";inputs[2].Text=inputs[3].Text="3";
                var values=split.JobValues();Check(((string[])values["ranges"]).SequenceEqual(new[]{"1-2","3-3"}),"numeric bounds generate ranges without user-entered separator");
                foreach(string invalid in new[]{"0","1-3","1–3","abc","2147483648"}){inputs[0].Text=invalid;bool rejected=false;try{split.JobValues();}catch(IOException){rejected=true;}Check(rejected,"invalid numeric start rejected: "+invalid);}
                inputs[0].Text="3";bool reversed=false;try{split.JobValues();}catch(IOException){reversed=true;}Check(reversed,"start page beyond end page rejected");inputs[0].Text="1";
                split.CountPicker.Value=3;split.CountPicker.Value=2;values=split.JobValues();Check(((string[])values["ranges"]).SequenceEqual(new[]{"1-2","3-3"}),"changing part count preserves retained start and end values");
                Snapshot(form,Path.Combine(folder,"layout-split-ranges.png"));
                split.Method.SelectedIndex=2;Application.DoEvents();
                Check(!split.CountPicker.Visible&&split.TopBottom.Visible&&split.RatioPicker.Value==50,"ratio mode reveals checkboxes and default 50 percent");
                split.LeftRight.Checked=true;Check(!split.TopBottom.Checked&&split.LeftRight.Checked,"cut directions are mutually exclusive");
                split.LeftRight.Checked=false;Check(split.LeftRight.Checked,"one cut direction remains selected");
                Snapshot(form,Path.Combine(folder,"layout-split-ratio.png"));
                form.Size=form.MinimumSize;Application.DoEvents();Snapshot(form,Path.Combine(folder,"layout-minimum.png"));
                var main=form.Controls[0].Controls[1];var start=Field<ModernButton>(form,"start");Point corner=form.PointToClient(start.PointToScreen(new Point(start.Width,start.Height)));
                Check(main.Bottom<=form.ClientSize.Height&&corner.X<=form.ClientSize.Width&&corner.Y<=form.ClientSize.Height,"minimum window keeps execution button and content within frame");
                Check(split.Width<=split.Parent.ClientSize.Width&&split.Method.Width<=split.Width,"split controls fit minimum window width");
                var options=Field<FlowLayoutPanel>(form,"options");
                Check(!options.HorizontalScroll.Visible,"minimum window wraps options without horizontal clipping");
                split.CountPicker.Value=12;split.Method.SelectedIndex=1;Application.DoEvents();
                Check(split.CountPicker.Visible&&split.Height<700,"many custom groups use a bounded scrollable area");
                split.CountPicker.Value=2;
                split.Method.SelectedIndex=1;Call(form,"AddFiles",(object)new[]{Path.Combine(folder,"a.pdf")});
                var task=(Task)Call(form,"ProcessFiles");while(!task.IsCompleted){Application.DoEvents();System.Threading.Thread.Sleep(20);}task.GetAwaiter().GetResult();
                var results=(ResultSession)typeof(MainForm).GetProperty("results",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form,null);
                Check(results!=null&&results.Files.Length==1&&Path.GetExtension(results.Files[0])==".zip","UI custom split dispatches to packaged engine and returns one ZIP");
                Check(Field<ModernButton>(form,"download").Visible,"completed split exposes download below progress");
                results.Downloaded=true;Call(form,"SetMode","压缩");Check(!Field<ModernButton>(form,"download").Visible&&Field<SmoothProgress>(form,"progress").Value==0,"switching tools hides foreign ZIP download and resets progress");
                form.Close();while(!form.IsDisposed){Application.DoEvents();System.Threading.Thread.Sleep(10);}
            }
            Console.WriteLine("ALL "+checks+" LAYOUT AND UI FLOW CHECKS PASSED");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
