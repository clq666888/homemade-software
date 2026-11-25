using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using LocalConverter;

static class WatermarkChecks
{
    static int checks;
    static void Check(bool condition,string name){if(!condition)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
    static T Field<T>(MainForm f,string name){return (T)typeof(MainForm).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(f);}
    static object Call(MainForm f,string name,params object[] args){SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());var value=typeof(MainForm).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(f,args);if(name=="AddFiles")Pump(Field<Task>(f,"admissionTask"));return value;}
    static void Pump(Task task){var clock=System.Diagnostics.Stopwatch.StartNew();while(!task.IsCompleted){Application.DoEvents();Thread.Sleep(5);if(clock.Elapsed.TotalSeconds>90)throw new Exception("UI timeout");}task.GetAwaiter().GetResult();Application.DoEvents();}
    static void Shot(MainForm f,string path){Application.DoEvents();using(var bitmap=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(bitmap,new Rectangle(Point.Empty,f.Size));bitmap.Save(path);}}
    [STAThread]static int Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);string root=Path.GetFullPath(args[0]),source=Path.Combine(root,"sample.pdf");
            using(var engine=new PdfEngine())using(var renderer=new PreviewRenderer())
            {
                var settings=new WatermarkSettings();var spec=settings.Snapshot();spec.Validate();
                Check(spec.Style==2&&spec.Opacity==20,"defaults use faint diagonal repeated text");
                spec.Text=" ";bool rejected=false;try{spec.Validate();}catch(IOException){rejected=true;}Check(rejected,"empty watermark blocked before processing");
                spec=settings.Snapshot();spec.Text="机密资料 · 仅供内部使用";spec.Color=Color.FromArgb(190,45,65);
                for(int style=0;style<7;style++)
                {
                    settings.StylePicker.SelectedIndex=style;var preset=settings.Snapshot();spec.Style=style;spec.Size=preset.Size;spec.Angle=preset.Angle;spec.Position=8;spec.Outline=style==1;spec.FontStyle=style==0?FontStyle.Bold:FontStyle.Regular;
                    string folder=Path.Combine(root,"style-"+style);Directory.CreateDirectory(folder);
                    WatermarkJob.Run(engine,new[]{source},folder,"",spec,CancellationToken.None,(p,s)=>{});
                    Check(Directory.GetFiles(folder,"*.pdf").Length==1,"style "+style+" produces one complete PDF");
                    for(int i=0;i<6;i++)
                    {
                        var rendered=renderer.Render(source,i,0,1300,"",CancellationToken.None);
                        using(var image=Image.FromFile(rendered.Path))using(var preview=new Bitmap(image))
                        {
                            using(var graphics=Graphics.FromImage(preview))spec.Paint(graphics,new RectangleF(0,0,preview.Width,preview.Height),rendered.Width,rendered.Height);
                            preview.Save(Path.Combine(root,"expected-"+style+"-"+i+".png"),ImageFormat.Png);
                        }
                    }
                }
                spec=settings.Snapshot();spec.Text="范围测试";
                for(int scope=1;scope<=3;scope++){spec.Scope=scope;spec.First=2;spec.Last=3;string folder=Path.Combine(root,"scope-"+scope);Directory.CreateDirectory(folder);WatermarkJob.Run(engine,new[]{source},folder,"",spec,CancellationToken.None,(p,s)=>{});}
                spec.Scope=0;string passwordFolder=Path.Combine(root,"password");Directory.CreateDirectory(passwordFolder);WatermarkJob.Run(engine,new[]{Path.Combine(root,"encrypted.pdf")},passwordFolder,"local-test",spec,CancellationToken.None,(p,s)=>{});
                spec.Scope=3;spec.Last=99;string invalid=Path.Combine(root,"invalid");Directory.CreateDirectory(invalid);bool rangeRejected=false;try{WatermarkJob.Run(engine,new[]{source},invalid,"",spec,CancellationToken.None,(p,s)=>{});}catch(IOException){rangeRejected=true;}Check(rangeRejected&&!Directory.GetFiles(invalid).Any(),"out-of-bounds range rejected without partial PDF");
                Check(!Directory.GetDirectories(engine.Workspace,"watermark-*").Any(),"private watermark image layers cleaned after success and error");settings.Dispose();
            }
            using(var form=new MainForm(new string[0]))
            {
                form.Show();Application.DoEvents();SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());Call(form,"SetMode","水印");Call(form,"AddFiles",(object)new[]{source});Pump(Field<Task>(form,"previewTask"));
                var settings=Field<WatermarkSettings>(form,"watermarkSettings");var preview=Field<PdfPagePreview>(form,"pagePicture");Image originalImage=preview.Image;var renderer=Field<PreviewRenderer>(form,"previewRenderer");int pid=renderer.WorkerId;
                settings.Content.Text="请勿外传";settings.OpacityPicker.Value=35;settings.StylePicker.SelectedIndex=6;settings.PositionPicker.SelectedIndex=8;
                Check(preview.Watermark.Text=="请勿外传"&&preview.Watermark.Opacity==35&&preview.Watermark.Position==8&&Object.ReferenceEquals(originalImage,preview.Image)&&renderer.WorkerId==pid,"live settings update watermark without PDF rerender or worker restart");
                Shot(form,Path.Combine(root,"ui-position.png"));settings.StylePicker.SelectedIndex=2;Shot(form,Path.Combine(root,"ui-tiled.png"));
                settings.ScopePicker.SelectedIndex=1;Call(form,"TurnPreview",1);Pump(Field<Task>(form,"previewTask"));Check(!preview.Watermark.Applies(preview.PageIndex+1)&&Field<Label>(form,"previewTitle").Text.Contains("此页不加水印"),"preview explains pages excluded by selected scope");
                settings.ScopePicker.SelectedIndex=0;settings.StylePicker.SelectedIndex=2;var processing=(Task)Call(form,"ProcessFiles");Pump(processing);
                var session=(ResultSession)typeof(MainForm).GetProperty("results",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form,null);session.Downloaded=true;
                Check(session.Files.Length==1&&session.Files[0].EndsWith(".pdf")&&Field<ModernButton>(form,"download").Visible,"real UI dispatch produces downloadable watermark PDF");
                settings.Content.Text="另一个水印";Check(Field<ModernButton>(form,"download").Text.Contains("上次结果"),"changing watermark marks previous output stale");
                form.Size=form.MinimumSize;Application.DoEvents();Shot(form,Path.Combine(root,"ui-minimum.png"));
                Check(settings.Width<=settings.Parent.ClientSize.Width&&!Field<FlowLayoutPanel>(form,"options").HorizontalScroll.Visible,"watermark settings fit minimum width without horizontal clipping");
                Call(form,"SetMode","压缩");Check(!Field<ModernButton>(form,"download").Visible&&preview.Watermark==null,"tool switch hides foreign download and watermark overlay");
                form.Close();while(!form.IsDisposed){Application.DoEvents();Thread.Sleep(5);}
            }
            Console.WriteLine("ALL "+checks+" WATERMARK UI AND EXPORT CHECKS PASSED");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
