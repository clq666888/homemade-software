using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using LocalConverter;

static class ImageChecks
{
    static int checks;static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
    static T Field<T>(object o,string name){return (T)o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);}
    static object Call(object o,string name,params object[] args){SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());return o.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,args);}
    static void Pump(Task task){var w=System.Diagnostics.Stopwatch.StartNew();while(!task.IsCompleted){Application.DoEvents();Thread.Sleep(5);if(w.Elapsed.TotalSeconds>120)throw new Exception("UI timed out");}task.GetAwaiter().GetResult();Application.DoEvents();}
    static void Wait(ImageWorkspace images){Pump(Field<Task>(images,"operation"));}
    static void Run(ImageWorkspace images,string method){Call(images,"Queue",new Func<CancellationToken,Task>(t=>(Task)Call(images,method,t)));Wait(images);}
    static void Click(ModernButton button){SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());button.PerformClick();}
    static void Shot(Form form,string file){Application.DoEvents();using(var b=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(b,new Rectangle(Point.Empty,form.Size));b.Save(file);}}
    [STAThread]static int Main(string[] args)
    {
        try
        {
            string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);string source=Path.Combine(root,"编辑 测试.png");using(var b=new Bitmap(1200,800)){using(var g=Graphics.FromImage(b)){g.Clear(Color.FromArgb(228,239,251));using(var font=new Font("Microsoft YaHei",42)){g.DrawString("文件工具 图片处理",font,Brushes.Black,80,90);g.DrawString("Local Image Tools 2026",font,Brushes.Black,80,240);}g.FillEllipse(Brushes.CornflowerBlue,720,400,350,300);}b.Save(source);}
            byte[] original=File.ReadAllBytes(source);
            using(var initial=ImageEditing.Load(source))using(var gray=ImageEditing.Apply(initial,new ImageEdit{Kind="gray"})){Color c=gray.GetPixel(1,1);Check(c.R==c.G&&c.G==c.B,"grayscale preserves equal luminance channels");}
            using(var initial=ImageEditing.Load(source))using(var cropped=ImageEditing.Apply(initial,new ImageEdit{Kind="crop",Rect=new Rectangle(30,20,300,250)})){Check(cropped.Size==new Size(300,250),"crop applies exact image pixel region");}
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            using(var form=new MainForm(new string[0]))
            {
                form.Show();Application.DoEvents();Call(form,"ChangeWorkspace",2);var images=Field<ImageWorkspace>(form,"imageWorkspace");Call(images,"QueueAdd",(object)new[]{source});Wait(images);var strip=Field<ImageFileStrip>(images,"strip");var canvas=Field<ImageCanvas>(images,"canvas");Check(strip.Items.Count==1&&canvas.Image.Size==new Size(1200,800),"image workspace accepts file and displays full pixel image");
                Point center=new Point(canvas.Width/2,canvas.Height/2);canvas.ZoomAt(new Point(canvas.Width/3,canvas.Height/3),1.6F);var atCenter=canvas.ImagePoint(center);Check(Math.Abs(atCenter.X-canvas.Image.Width/2F)<2&&Math.Abs(atCenter.Y-canvas.Image.Height/2F)<2,"wheel zoom keeps the image fixed at canvas center");canvas.Fit();
                Run(images,"Process");var store=Field<ToolResultStore>(images,"outcomes");Check(store.Get("格式转换").Files.Length==1&&Field<ModernButton>(images,"download").Visible,"conversion produces per-tool downloadable result");store.Get("格式转换").Downloaded=true;
                Call(images,"SetMode","黑白化");Wait(images);Check(!Field<ModernButton>(images,"download").Visible&&Field<SmoothProgress>(images,"progress").Value==0,"switching image tool hides prior download and progress");Run(images,"Process");store.Get("黑白化").Downloaded=true;using(var gray=ImageEditing.Load(store.Get("黑白化").Files[0])){var c=gray.GetPixel(5,5);Check(c.R==c.G&&c.G==c.B,"black-white exported pixels match actual grayscale");}
                Call(images,"SetMode","水印");Wait(images);var wm=Field<ImageWatermarkSettings>(images,"watermark");wm.Content.Text="© 仅供测试";wm.OpacityValue.Value=80;wm.ColorValue.SelectedColor=Color.DarkBlue;Run(images,"Process");store.Get("水印").Downloaded=true;using(var stamped=ImageEditing.Load(store.Get("水印").Files[0]))using(var sourceImage=ImageEditing.Load(source)){int changed=0;for(int y=650;y<800;y++)for(int x=600;x<1200;x++)if(stamped.GetPixel(x,y)!=sourceImage.GetPixel(x,y))changed++;Check(changed>100,"watermark writes visible text in selected bottom-right area");}Shot(form,Path.Combine(root,"image-watermark.png"));
                string logo=Path.Combine(root,"透明Logo.png");using(var b=new Bitmap(60,40)){using(var g=Graphics.FromImage(b))g.Clear(Color.DarkRed);b.Save(logo);}wm.LogoPath=logo;wm.Type.SelectedIndex=1;wm.Style.SelectedIndex=0;wm.OpacityValue.Value=50;Run(images,"Process");store.Get("水印").Downloaded=true;using(var stamped=ImageEditing.Load(store.Get("水印").Files[0])){var centerPixel=stamped.GetPixel(600,400);Check(centerPixel.G<180&&centerPixel.R>160,"image watermark honors position and half opacity in exported pixels");}
                Call(images,"SetMode","图片编辑");Wait(images);Call(images,"CommitEdit",new ImageEdit{Kind="crop",Rect=new Rectangle(40,30,900,650)});Wait(images);Check(canvas.Image.Size==new Size(900,650),"editing crop updates live canvas");Click(Field<ModernButton>(images,"undo"));Wait(images);Check(canvas.Image.Size==new Size(1200,800),"undo restores original geometry");Click(Field<ModernButton>(images,"redo"));Wait(images);Check(canvas.Image.Size==new Size(900,650),"redo reapplies crop");
                foreach(var edit in new[]{new ImageEdit{Kind="mosaic",Rect=new Rectangle(15,15,100,100),Value=15},new ImageEdit{Kind="text",Rect=new Rectangle(50,60,350,90),Text="编辑文字",Value=44,Color=Color.DarkRed},new ImageEdit{Kind="icon",Rect=new Rectangle(600,300,160,160),Text="星",Color=Color.Goldenrod},new ImageEdit{Kind="overlay",Rect=new Rectangle(550,30,200,150),Path=source,Opacity=60},new ImageEdit{Kind="rotate"}}){Call(images,"CommitEdit",edit);Wait(images);}Check(strip.Items[0].Edits.Count==6&&canvas.Image.Size==new Size(650,900),"mosaic text icon overlay and rotation compose correctly");Run(images,"Process");store.Get("图片编辑").Downloaded=true;using(var edited=ImageEditing.Load(store.Get("图片编辑").Files[0]))Check(edited.Size==canvas.Image.Size,"editor export shares geometry with preview");Shot(form,Path.Combine(root,"image-editor.png"));
                Call(images,"SetMode","文字识别");Wait(images);Run(images,"Process");Check(canvas.Words.Count>0&&canvas.Words.Any(w=>w.Text.Contains("2026")),"real OCR dispatch populates selectable canvas boxes");canvas.SelectAllWords();Check(canvas.SelectedText.Contains("Local"),"selecting original image words builds copyable text");Shot(form,Path.Combine(root,"image-ocr.png"));Check(!Field<ModernButton>(images,"download").Visible,"OCR creates no new image or download result");
                form.Size=form.MinimumSize;Application.DoEvents();Shot(form,Path.Combine(root,"image-minimum.png"));Check(canvas.Width>300&&canvas.Height>150,"minimum window preserves usable centered canvas");Check(original.SequenceEqual(File.ReadAllBytes(source)),"edits conversion OCR never overwrite original");
                using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();bool stopped=false;using(var worker=new PdfEngine()){try{worker.Run("image_ocr",new System.Collections.Generic.Dictionary<string,object>{{"files",new[]{source}},{"folder",root}},cancelled.Token);}catch(OperationCanceledException){stopped=true;}Check(stopped&&Directory.GetDirectories(worker.Workspace).Length==0,"cancelled image worker cleans private job without starting native work");}}
                strip.Items[0].Edits.Clear();strip.Items[0].Dirty=false;Call(form,"SwitchWorkspace",false);form.Close();while(!form.IsDisposed){Application.DoEvents();Thread.Sleep(5);}
            }
            Console.WriteLine("ALL "+checks+" IMAGE UI CHECKS PASSED");return 0;
        }catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
