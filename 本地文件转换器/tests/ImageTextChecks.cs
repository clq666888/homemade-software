using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using LocalConverter;

static class ImageTextChecks
{
    static int checks;
    static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
    static T Field<T>(object value,string name){return (T)value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(value);}
    static void Context(){SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());}
    static object Call(object value,string name,params object[] args){Context();return value.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(value,args);}
    static void Pump(Task task){var clock=System.Diagnostics.Stopwatch.StartNew();while(!task.IsCompleted){Application.DoEvents();Thread.Sleep(4);if(clock.Elapsed.TotalSeconds>45)throw new Exception("UI timeout");}task.GetAwaiter().GetResult();Application.DoEvents();}
    static void Wait(ImageWorkspace w){Pump(Field<Task>(w,"operation"));Pump(Field<Task>(w,"textPreview"));}
    static void Click(ImageWorkspace w,string field){Context();Field<ModernButton>(w,field).PerformClick();Wait(w);}
    static string Hash(Bitmap b){using(var stream=new MemoryStream())using(var sha=SHA256.Create()){b.Save(stream,System.Drawing.Imaging.ImageFormat.Png);return Convert.ToBase64String(sha.ComputeHash(stream.ToArray()));}}
    static void Shot(Control form,string path){using(var b=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(b,new Rectangle(Point.Empty,form.Size));b.Save(path);}}
    [STAThread]static int Main(string[] args)
    {
        try
        {
            var root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);string file=Path.Combine(root,"实时文字.png");
            using(var b=new Bitmap(1200,800))using(var g=Graphics.FromImage(b)){g.Clear(Color.White);g.FillRectangle(Brushes.LightSteelBlue,0,0,1200,35);b.Save(file);}
            byte[] source=File.ReadAllBytes(file);var info=new FileInfo(file);
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            using(var form=new Form{Size=new Size(1220,930),Text="0.8.2 实时文字交互检查"})using(var w=new ImageWorkspace())
            {
                form.Controls.Add(w);form.Show();Application.DoEvents();var strip=Field<ImageFileStrip>(w,"strip");var canvas=Field<ImageCanvas>(w,"canvas");
                var asset=new ImageAsset{Source=file,Prepared=file,Width=1200,Height=800,Frames=1,Format="PNG",Length=info.Length,Ticks=info.LastWriteTimeUtc.Ticks};strip.Items.Add(asset);strip.SelectedIndex=0;strip.RefreshItems();
                Call(w,"SetMode","图片编辑");Wait(w);Context();Field<ComboBox>(w,"editTool").SelectedIndex=2;Wait(w);
                var original=new ImageEdit{Kind="text",Text="原文字",Rect=new Rectangle(150,160,800,140),Value=40,Color=Color.RoyalBlue};Call(w,"CommitEdit",original);Wait(w);
                var text=Field<TextBox>(w,"content");var options=Field<FlowLayoutPanel>(w,"options");string id=original.Id;
                Check(!options.Controls.Cast<Control>().Any(c=>c.Text=="更新选中文字"),"manual update button is removed");
                Context();text.Focus();text.SelectAll();text.SelectedText="文字实时更新";int caret=text.SelectionStart;
                Check(!w.Busy&&text.Enabled&&text.Focused,"typing never disables or removes focus from the input");
                Check(ImageEditing.Effective(asset.Edits).Single(e=>e.Id==id).Text=="文字实时更新","input immediately updates the selected text object");
                Wait(w);Check(text.Focused&&text.SelectionStart==caret,"background preview preserves focus and caret");
                using(var expected=ImageEditing.Render(file,asset.Edits))Check(Hash(expected)==Hash(canvas.Image),"automatic preview matches output pixels without a manual update");
                Context();for(int i=0;i<120;i++){text.SelectionStart=text.TextLength;text.SelectedText="字";}
                Check(asset.Edits.Count==2,"120 typed characters coalesce into one undo command");Wait(w);
                Check(ImageEditing.Effective(asset.Edits).Single(e=>e.Id==id).Text==text.Text,"rapid input retains the latest complete text");
                Check(canvas.TextTargets.Single(t=>t.Edit.Id==id).Edit.Text==text.Text,"canvas hit targets also use the latest text");
                using(var expected=ImageEditing.Render(file,asset.Edits))Check(Hash(expected)==Hash(canvas.Image),"late background results cannot overwrite newer typing");
                string latest=text.Text;Click(w,"undo");Check(ImageEditing.Effective(asset.Edits).Single(e=>e.Id==id).Text=="原文字","one undo restores content before the typing session");
                Click(w,"redo");Check(text.Text==latest,"redo restores the complete typing session in the editor");
                Context();text.Focus();text.Select(0,2);text.SelectedText="";Wait(w);
                Check(ImageEditing.Effective(asset.Edits).Count(e=>e.Kind=="text")==1,"deleting characters in the input preserves the text object");
                var key=new KeyEventArgs(Keys.Delete);Call(text,"OnKeyDown",key);Check(!key.SuppressKeyPress,"Delete in the input keeps normal text editing behavior");
                Context();Field<NumericUpDown>(w,"textSize").Value=48;Wait(w);
                Check(canvas.TextTargets.Single(t=>t.Edit.Id==id).Edit.Value==48,"font size changes appear automatically");
                Context();Field<NumericUpDown>(w,"opacity").Value=65;Wait(w);
                Check(canvas.TextTargets.Single(t=>t.Edit.Id==id).Edit.Opacity==65,"opacity changes appear automatically");
                canvas.ActualSize();canvas.ZoomAt(new Point(canvas.Width/2,canvas.Height/2),1.5F);PointF before=canvas.ImagePoint(new Point(canvas.Width/2,canvas.Height/2));float zoom=canvas.Zoom;
                Context();text.Text="保持缩放与视口";Wait(w);PointF after=canvas.ImagePoint(new Point(canvas.Width/2,canvas.Height/2));
                Check(canvas.Zoom==zoom&&Math.Abs(before.X-after.X)<.1&&Math.Abs(before.Y-after.Y)<.1,"live updates preserve zoom and viewport");
                var stale=canvas.TextTargets.Single(t=>t.Edit.Id==id);Context();text.Text="最后输入的内容";Call(w,"ChooseText",stale);Wait(w);
                Check(text.Text=="最后输入的内容","clicking a stale hit target cannot restore old text");
                canvas.Fit();Shot(form,Path.Combine(root,"live-text.png"));
                canvas.Focus();key=new KeyEventArgs(Keys.Delete);Call(canvas,"OnKeyDown",key);Wait(w);
                Check(key.SuppressKeyPress&&!ImageEditing.Effective(asset.Edits).Any(e=>e.Kind=="text"),"Delete on a selected canvas text object removes it");
                Check(Field<ImageEdit>(w,"selectedTextOriginal")==null,"deletion clears the selected text fields");
                Click(w,"undo");Check(ImageEditing.Effective(asset.Edits).Single(e=>e.Id==id).Text=="最后输入的内容","undo restores deleted text with the latest content");
                Click(w,"redo");Check(!ImageEditing.Effective(asset.Edits).Any(e=>e.Kind=="text"),"redo removes the same logical text object");
                Click(w,"undo");Call(w,"ChooseText",canvas.TextTargets.Single());Context();text.Text="切换前最新内容";Call(w,"SetMode","滤镜");Wait(w);
                using(var basis=ImageEditing.Load(file))using(var expected=ImageFilters.Apply(basis,0,100))Check(Hash(expected)==Hash(canvas.Image),"switching tools rejects late text preview frames");
                Call(w,"SetMode","图片编辑");Wait(w);Check(canvas.TextTargets.Single().Edit.Text=="切换前最新内容","switching tools preserves the live text edit");
                Call(w,"ChooseText",canvas.TextTargets.Single());Context();text.Text="导出最新内容";
                Call(w,"Queue",new Func<CancellationToken,Task>(t=>(Task)Call(w,"Process",t)));Wait(w);
                var output=Field<ToolResultStore>(w,"outcomes").Get("图片编辑");using(var exported=ImageEditing.Load(output.Files[0]))using(var expected=ImageEditing.Render(file,asset.Edits))Check(Hash(exported)==Hash(expected),"export started during preview uses the latest text snapshot");output.Downloaded=true;
                Check(source.SequenceEqual(File.ReadAllBytes(file)),"editing and export never change source bytes");
                Context();text.Text="关闭时仍在输入";Pump(w.StopAsync());Check(Field<Task>(w,"textPreview").IsCompleted,"closing waits for the pending live renderer");
                Context();text.Text="取消关闭后仍实时更新";Wait(w);Check(canvas.TextTargets.Single().Edit.Text==text.Text,"text preview can resume after cancelling a close operation");asset.Dirty=false;
                form.Close();
            }
            using(var overview=new PageCanvas{Size=new Size(650,200)})Shot(overview,Path.Combine(root,"page-overview.png"));
            Console.WriteLine("ALL "+checks+" IMAGE TEXT CHECKS PASSED");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
