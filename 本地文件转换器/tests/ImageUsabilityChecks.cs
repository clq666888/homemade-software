using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using LocalConverter;

static class ImageUsabilityChecks
{
    static int count;
    static void Check(bool value,string name){if(!value)throw new Exception(name);count++;Console.WriteLine("PASS "+name);}
    static T Field<T>(object value,string name){return (T)value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(value);}
    static void Context(){SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());}
    static object Call(object value,string name,params object[] args){Context();return value.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(value,args);}
    static void Pump(Task task){var watch=System.Diagnostics.Stopwatch.StartNew();while(!task.IsCompleted){Application.DoEvents();Thread.Sleep(4);if(watch.Elapsed.TotalSeconds>90)throw new Exception("UI timeout");}task.GetAwaiter().GetResult();Application.DoEvents();}
    static void Wait(ImageWorkspace w){Pump(Field<Task>(w,"operation"));}
    static void Run(ImageWorkspace w,string method){Call(w,"Queue",new Func<CancellationToken,Task>(t=>(Task)Call(w,method,t)));Wait(w);}
    static string Output(ImageWorkspace w){var r=Field<ToolResultStore>(w,"outcomes").Get("压缩");r.Downloaded=true;return r.Files.Single();}
    static void Shot(Form form,string path){using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(path);}}
    [STAThread]static int Main(string[] args)
    {
        try
        {
            var words=new[]{new ImageWord{Text="power",NewLine=true},new ImageWord{Text="generation",NewLine=true},new ImageWord{Text="exceeded",NewLine=true},new ImageWord{Text="5.2",NewLine=true}};
            Check(OcrTextFlow.Join(words,false)=="power generation exceeded 5.2","English OCR fragments join with spaces instead of line breaks");
            Check(OcrTextFlow.Join(new[]{new ImageWord{Text="中华"},new ImageWord{Text="人民共和国",NewLine=true}},false)=="中华人民共和国","Chinese fragments join without artificial spaces");
            Check(OcrTextFlow.Join(new[]{new ImageWord{Text="hello"},new ImageWord{Text=","},new ImageWord{Text="world"},new ImageWord{Text="!"}},false)=="hello, world!","joining preserves readable punctuation");
            Check(OcrTextFlow.Join(words,true).Split(new[]{"\r\n"},StringSplitOptions.None).Length==4,"explicit layout mode preserves original region line breaks");
            string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);string source=Path.Combine(root,"压缩样本.png");
            using(var b=new Bitmap(1000,600)){var random=new Random(42);for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++)b.SetPixel(x,y,Color.FromArgb((x/4+random.Next(60))%256,(y/3+random.Next(60))%256,(x/8+y/4+random.Next(60))%256));b.Save(source);}
            var bytes=File.ReadAllBytes(source);var f=new FileInfo(source);var asset=new ImageAsset{Source=source,Prepared=source,Format="PNG",Width=1000,Height=600,Frames=1,Length=f.Length,Ticks=f.LastWriteTimeUtc.Ticks};
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            using(var form=new Form{Size=new Size(1250,1000),Text="0.8.3 图片操作检查"})using(var w=new ImageWorkspace())
            {
                form.Controls.Add(w);form.Show();Application.DoEvents();var strip=Field<ImageFileStrip>(w,"strip");strip.Items.Add(asset);strip.SelectedIndex=0;strip.RefreshItems();Call(w,"SetMode","压缩");Wait(w);
                var panel=Field<ImageCompressionSettings>(w,"compression");var options=Field<FlowLayoutPanel>(w,"options");var canvas=Field<ImageCanvas>(w,"canvas");
                Check(panel.Visible&&panel.Output.Text.Contains("保持原格式"),"compression opens with a plain-language keep-format choice");
                Check(!panel.Quality.Visible&&!panel.Preset.Visible&&!panel.Lossless.Visible,"PNG hides inapplicable quality and WebP controls");
                Check(Field<NumericUpDown>(w,"edge").Parent==null&&Field<NumericUpDown>(w,"quality").Parent==null,"compression removes unexplained raw edge and quality fields");
                Check(!panel.FirstFrame.Visible,"single image hides animation-only options");
                Check(panel.Controls.OfType<Label>().Any(c=>c.Text.Contains("PNG 只优化存储")),"PNG explains its actual lossless compression limit");
                Check(panel.Snapshot(asset).Edge==0&&panel.Snapshot(asset).Format=="png","recommended defaults keep image format and dimensions");
                Shot(form,Path.Combine(root,"compression-png.png"));
                Context();panel.Output.SelectedIndex=1;Check(panel.Preset.Visible&&panel.Preset.SelectedIndex==1&&!panel.Quality.Visible,"JPEG exposes three named presets while hiding manual quality");
                Application.DoEvents();var presetLabel=panel.Preset.Parent.Controls.OfType<Label>().Single(c=>c.Text=="压缩程度");Check(presetLabel.Bottom<=panel.Preset.Top&&panel.Preset.Bottom<=panel.Custom.Top,"switching formats preserves label preset and manual-control visual order");
                Check(panel.Snapshot(asset).Quality==80,"balanced preset is connected to the actual encoder quality");
                Context();panel.Preset.SelectedIndex=2;Check(panel.Snapshot(asset).Quality==55,"small-file preset changes encoder quality");
                Context();panel.Custom.Checked=true;panel.Quality.Value=68;Check(panel.Quality.Visible&&panel.Snapshot(asset).Quality==68,"optional quality slider controls actual output settings");panel.Custom.Checked=false;Check(panel.Snapshot(asset).Quality==55,"leaving manual quality restores the selected preset");
                Context();panel.Dimensions.SelectedIndex=2;Check(panel.Snapshot(asset).Edge==500,"50 percent size computes each image's own longest edge");
                Check(panel.Controls.OfType<Label>().Any(c=>c.Text.Contains("1000 × 600 → 500 × 300")),"size choice shows original and resulting dimensions");
                Shot(form,Path.Combine(root,"compression-jpg.png"));Run(w,"Process");string jpeg=Output(w);using(var image=Image.FromFile(jpeg))Check(image.Size==new Size(500,300)&&Path.GetExtension(jpeg)==".jpg","real compression export honors the selected format and percentage");
                Context();panel.Dimensions.SelectedIndex=0;Run(w,"Process");long small=new FileInfo(Output(w)).Length;panel.Preset.SelectedIndex=0;Run(w,"Process");Check(new FileInfo(Output(w)).Length>small,"clear-priority and small-file presets produce different actual file sizes");
                Context();panel.Output.SelectedIndex=3;panel.Lossless.Checked=true;Check(panel.Lossless.Visible&&!panel.Quality.Visible&&!panel.Preset.Visible&&panel.Snapshot(asset).Lossless,"WebP lossless hides lossy controls and reaches the encoder");
                Context();panel.Output.SelectedIndex=2;Check(!panel.Lossless.Visible&&!panel.Preset.Visible&&panel.Snapshot(asset).Format=="png","switching to PNG cannot expose irrelevant WebP or quality choices");
                Context();panel.Dimensions.SelectedIndex=2;Run(w,"Process");using(var image=Image.FromFile(Output(w)))Check(image.Size==new Size(500,300),"PNG still supports meaningful size reduction without a fake quality setting");
                asset.Frames=2;panel.Configure(asset,strip.Items);Check(panel.FirstFrame.Visible&&!panel.FirstFrame.Checked,"multi-frame input offers explicit flattening without dropping frames by default");asset.Frames=1;panel.Configure(asset,strip.Items);
                Call(w,"SetMode","文字识别");Wait(w);canvas.Words.Clear();foreach(var word in words){word.Selected=true;canvas.Words.Add(word);}Call(w,"UpdateOcrText");var text=Field<TextBox>(w,"ocrText");
                Check(text.Text=="power generation exceeded 5.2"&&canvas.SelectedText==text.Text,"right readout and canvas copy share the same no-newline text");
                Check(!Field<CheckBox>(w,"sparse").Checked,"recognition defaults to page layout rather than sparse fragments");
                Field<CheckBox>(w,"ocrLineBreaks").Checked=true;Check(text.Text.Contains("\r\n")&&canvas.SelectedText==text.Text,"line-break preference updates both text display and clipboard source");
                Field<CheckBox>(w,"ocrLineBreaks").Checked=false;text.Select(6,10);Check((string)w.GetType().GetProperty("CopyText",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(w,null)=="generation","partial selection remains copyable after joining fragments");
                Call(w,"SetMode","水印");Wait(w);var watermark=Field<ImageWatermarkSettings>(w,"watermark");var swatch=watermark.ColorValue;var label=swatch.Parent.Controls.OfType<Label>().Single(c=>c.Text=="颜色");
                Check(swatch.Left>label.Right&&Math.Abs(swatch.Top+swatch.Height/2-label.Top-label.Height/2)<12,"watermark color swatch sits immediately to the right of its label");
                Check(swatch.Width>=46&&swatch.Height>=46,"watermark color is a larger visible clickable square");
                options.AutoScrollPosition=new Point(0,Math.Max(0,watermark.Bottom-options.Height+50));Application.DoEvents();Shot(form,Path.Combine(root,"watermark-color.png"));
                watermark.ColorValue.SelectedColor=Color.Red;using(var stamp=watermark.Snapshot())Check(stamp.Text.Color==Color.Red,"larger color picker still drives preview and export color");
                watermark.Type.SelectedIndex=1;Check(!swatch.Parent.Visible,"logo mode hides the text-only color row");
                Check(bytes.SequenceEqual(File.ReadAllBytes(source)),"compression and OCR text display preserve original image bytes");Pump(w.StopAsync());form.Close();
            }
            Console.WriteLine("ALL "+count+" IMAGE USABILITY CHECKS PASSED");return 0;
        }catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
