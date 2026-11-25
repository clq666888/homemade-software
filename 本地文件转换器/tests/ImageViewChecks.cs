using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using LocalConverter;

static class ImageViewChecks
{
    static int checks;
    static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
    static T Field<T>(object o,string name){return (T)o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);}
    static object Call(object o,string name,params object[] args){SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());return o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,args);}
    static void Wait(ImageWorkspace images){var task=Field<Task>(images,"operation");var clock=System.Diagnostics.Stopwatch.StartNew();while(!task.IsCompleted){Application.DoEvents();Thread.Sleep(4);if(clock.Elapsed.TotalSeconds>90)throw new Exception("UI timeout");}task.GetAwaiter().GetResult();Application.DoEvents();}
    static void Run(ImageWorkspace images,string method){Call(images,"Queue",new Func<CancellationToken,Task>(t=>(Task)Call(images,method,t)));Wait(images);}
    static void Click(ImageWorkspace images,ModernButton button){SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());button.PerformClick();Wait(images);}
    static void Mouse(ImageCanvas canvas,string action,MouseButtons button,Point at){Call(canvas,action,new MouseEventArgs(button,1,at.X,at.Y,0));Application.DoEvents();}
    static void Shot(Form f,string file){using(var bitmap=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(bitmap,new Rectangle(Point.Empty,f.Size));bitmap.Save(file);}}
    static bool Near(PointF a,PointF b){return Math.Abs(a.X-b.X)<1&&Math.Abs(a.Y-b.Y)<1;}
    [STAThread]static int Main(string[] args)
    {
        try
        {
            string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);string file=Path.Combine(root,"查看与笔刷测试.png");
            using(var b=new Bitmap(1600,1000))using(var g=Graphics.FromImage(b))using(var font=new Font("Microsoft YaHei",55)){g.Clear(Color.FromArgb(235,243,252));g.DrawString("Local Image Tools 2026",font,Brushes.Black,100,120);g.DrawString("文字识别结果",font,Brushes.Black,100,340);g.FillEllipse(Brushes.CornflowerBlue,950,560,430,320);b.Save(file);}
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            using(var f=new MainForm(new string[0]))
            {
                f.Show();Application.DoEvents();Call(f,"ChangeWorkspace",2);var images=Field<ImageWorkspace>(f,"imageWorkspace");var preview=Field<ModernButton>(images,"resultPreview");var canvas=Field<ImageCanvas>(images,"canvas");
                Check(!preview.Visible,"result preview is hidden until an output exists");Call(images,"QueueAdd",(object)new[]{file});Wait(images);Check(!preview.Visible,"adding an input does not show a permanently disabled result button");
                Run(images,"Process");var store=Field<ToolResultStore>(images,"outcomes");store.Get("格式转换").Downloaded=true;Check(preview.Visible&&preview.Enabled&&preview.Text=="预览生成文件","generating output reveals clearly named preview action");
                Click(images,preview);Check(preview.Text=="返回工作预览"&&canvas.Tool=="查看"&&!Field<FlowLayoutPanel>(images,"options").Enabled,"output preview is read-only with a visible return action");Click(images,preview);Check(preview.Text=="预览生成文件"&&Field<FlowLayoutPanel>(images,"options").Enabled,"return action restores the editable work view");
                Field<ComboBox>(images,"format").SelectedItem="JPG";Check(preview.Text.Contains("上次"),"changed options identify the previous generated file");Field<ComboBox>(images,"format").SelectedItem="PNG";
                Call(images,"SetMode","图片编辑");Wait(images);Check(!preview.Visible,"another tool never exposes unrelated result preview");
                var combo=Field<ComboBox>(images,"editTool");combo.SelectedIndex=1;Wait(images);var picker=Field<BrushSizePicker>(images,"sizePicker");picker.Slider.Value=80;
                float visible=80*canvas.DeviceDpi/96F;Check(Math.Abs(canvas.BrushDiameter*canvas.Zoom-visible)<.01,"mosaic slider diameter matches the visible brush circle");
                var start=new Point(canvas.Width/2,canvas.Height/2);Mouse(canvas,"OnMouseDown",MouseButtons.Left,start);var draft=Field<ImageEdit>(canvas,"draft");using(var path=ImageBrushes.Path(draft))Check(Math.Abs(path.GetBounds().Width*canvas.Zoom-visible)<.1,"actual mosaic stamp has the same screen diameter as the cursor");Mouse(canvas,"OnMouseUp",MouseButtons.Left,start);Wait(images);
                canvas.ZoomAt(start,3);Check(Math.Abs(canvas.BrushDiameter*canvas.Zoom-visible)<.01,"zooming retains the chosen visible brush size while updating image-space diameter");
                Check(canvas.CanPan,"enlarged picture exposes right-drag navigation");var strip=Field<ImageFileStrip>(images,"strip");int edits=strip.Items[0].Edits.Count;var before=canvas.ImagePoint(start);
                Mouse(canvas,"OnMouseDown",MouseButtons.Right,start);Mouse(canvas,"OnMouseMove",MouseButtons.Right,new Point(start.X+70,start.Y+40));Mouse(canvas,"OnMouseUp",MouseButtons.Right,new Point(start.X+70,start.Y+40));var after=canvas.ImagePoint(start);
                Check(!Near(before,after),"right drag moves the enlarged viewport");Check(strip.Items[0].Edits.Count==edits,"right drag creates no mosaic stroke or edit operation");
                Mouse(canvas,"OnMouseDown",MouseButtons.Left,start);Mouse(canvas,"OnMouseUp",MouseButtons.Left,start);Wait(images);Check(Near(after,canvas.ImagePoint(start)),"committing a stroke preserves the panned viewport");
                Mouse(canvas,"OnMouseDown",MouseButtons.Right,start);Mouse(canvas,"OnMouseMove",MouseButtons.Right,new Point(start.X+10000,start.Y+10000));Mouse(canvas,"OnMouseUp",MouseButtons.Right,new Point(start.X+10000,start.Y+10000));var origin=canvas.ScreenPoint(Point.Empty);var end=canvas.ScreenPoint(new PointF(canvas.Image.Width,canvas.Image.Height));Check(origin.X<=0&&origin.Y<=0&&end.X>=canvas.Width&&end.Y>=canvas.Height,"panning bounds prevent the enlarged image from disappearing beyond the viewport");
                canvas.Fit();var pictureCenter=canvas.ScreenPoint(new PointF(800,500));Check(Math.Abs(pictureCenter.X-canvas.Width/2)<=1&&Math.Abs(pictureCenter.Y-canvas.Height/2)<=1,"fit restores the complete centered picture");
                foreach(int tool in new[]{5,11}){combo.SelectedIndex=tool;Wait(images);picker.Slider.Value=30;Check(Math.Abs(canvas.BrushDiameter*canvas.Zoom-30*canvas.DeviceDpi/96F)<.01,"slider footprint matches for brush tool "+tool);}
                Call(canvas,"OnMouseLeave",EventArgs.Empty);picker.Slider.Focus();Application.DoEvents();Shot(f,Path.Combine(root,"brush-slider.png"));
                Call(images,"SetMode","文字识别");Wait(images);var text=Field<TextBox>(images,"ocrText");Check(text.Text==""&&text.ReadOnly,"OCR readout starts empty and supports safe selection");Run(images,"Process");Check(text.Text.Contains("Local")&&text.Text.Contains("2026"),"right panel initially displays readable OCR output");
                var line=canvas.Words.First(w=>w.Text.Contains("Local"));var point=canvas.ScreenPoint(new PointF(line.Bounds.X+line.Bounds.Width/2,line.Bounds.Y+line.Bounds.Height/2));Mouse(canvas,"OnMouseDown",MouseButtons.Left,point);Mouse(canvas,"OnMouseUp",MouseButtons.Left,point);Check(text.Text==line.Text&&canvas.Words.Count(w=>w.Selected)==1,"clicking one OCR line displays that exact line in the right panel");
                int at=text.Text.IndexOf("Local",StringComparison.Ordinal);text.Select(at,5);string copy=(string)images.GetType().GetProperty("CopyText",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(images,null);Check(copy=="Local","copy action respects a partial selection in the right-hand text box");
                canvas.ZoomAt(start,3);string selectedText=text.Text;Mouse(canvas,"OnMouseDown",MouseButtons.Right,start);Mouse(canvas,"OnMouseMove",MouseButtons.Right,new Point(start.X-80,start.Y-35));Mouse(canvas,"OnMouseUp",MouseButtons.Right,start);Check(text.Text==selectedText&&canvas.Words.Count(w=>w.Selected)==1,"right panning does not change OCR selection or its readout");canvas.Fit();canvas.SelectAllWords();Check(!text.Text.Contains("\r\n")&&Field<Label>(images,"ocrSelection").Text.Contains("已选"),"multiple OCR regions join without forced newlines by default");Field<CheckBox>(images,"ocrLineBreaks").Checked=true;Check(text.Text.Contains("\r\n"),"original line breaks can be restored explicitly");Shot(f,Path.Combine(root,"ocr-text-panel.png"));
                Call(images,"SetMode","格式转换");Wait(images);Check(text.Text=="","leaving OCR clears its readout instead of retaining unrelated text");strip.Items[0].Dirty=false;Call(images,"ClearTextSelection");f.Close();while(!f.IsDisposed){Application.DoEvents();Thread.Sleep(4);}
            }
            Console.WriteLine("ALL "+checks+" IMAGE VIEW CHECKS PASSED");return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
