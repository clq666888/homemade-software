using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using LocalConverter;

static class ImageInteractionChecks
{
    static int checks;
    static void Check(bool condition,string name){if(!condition)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
    static string Hash(Bitmap bitmap){using(var stream=new MemoryStream())using(var sha=SHA256.Create()){bitmap.Save(stream,ImageFormat.Png);return Convert.ToBase64String(sha.ComputeHash(stream.ToArray()));}}
    static T Field<T>(object value,string name){return (T)value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(value);}
    static object Call(object value,string name,params object[] args){SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());return value.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(value,args);}
    static void Pump(Task task){var clock=System.Diagnostics.Stopwatch.StartNew();while(!task.IsCompleted){Application.DoEvents();Thread.Sleep(4);if(clock.Elapsed.TotalSeconds>90)throw new Exception("UI timeout");}task.GetAwaiter().GetResult();Application.DoEvents();}
    static void Wait(ImageWorkspace images){Pump(Field<Task>(images,"operation"));}
    static void Run(ImageWorkspace images,string name){Call(images,"Queue",new Func<CancellationToken,Task>(t=>(Task)Call(images,name,t)));Wait(images);}
    static void Tool(ImageWorkspace images,int index){SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());Field<ComboBox>(images,"editTool").SelectedIndex=index;Wait(images);}
    static void Mouse(ImageCanvas canvas,string action,Point pixel,MouseButtons button=MouseButtons.Left){Point point=canvas.ScreenPoint(pixel);Call(canvas,action,new MouseEventArgs(button,1,point.X,point.Y,0));Application.DoEvents();}
    static void Stroke(ImageCanvas canvas,params Point[] pixels){Mouse(canvas,"OnMouseDown",pixels[0]);foreach(var p in pixels.Skip(1))Mouse(canvas,"OnMouseMove",p);Mouse(canvas,"OnMouseUp",pixels.Last());}
    static void Shot(Form form,string file){Application.DoEvents();using(var b=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(b,new Rectangle(Point.Empty,form.Size));b.Save(file);}}
    [STAThread]static int Main(string[] args)
    {
        try
        {
            string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);string source=Path.Combine(root,"圆形笔刷样本.png");
            using(var bitmap=new Bitmap(160,120,PixelFormat.Format32bppArgb)){for(int y=0;y<120;y++)for(int x=0;x<160;x++)bitmap.SetPixel(x,y,(x/3+y/3)%2==0?Color.FromArgb(40,110,180):Color.FromArgb(230,220,180));bitmap.Save(source);}
            byte[] bytes=File.ReadAllBytes(source);
            var pen=new ImageEdit{Kind="pen",Points=new[]{new Point(70,60)},Diameter=40,Color=Color.Red};var erase=new ImageEdit{Kind="erase",Points=new[]{new Point(70,60)},Diameter=12};
            using(var original=ImageEditing.Load(source))using(var drawn=ImageEditing.Render(source,new[]{pen}))using(var erased=ImageEditing.Render(source,new[]{pen,erase}))
            {Check(drawn.GetPixel(70,60).R==255,"single circular pen tap paints a real dot");Check(drawn.GetPixel(51,41)==original.GetPixel(51,41),"round pen leaves square bounding-box corners untouched");Check(erased.GetPixel(70,60)==original.GetPixel(70,60),"eraser restores original photograph under annotations");Check(erased.GetPixel(85,60).R==255,"small eraser leaves the rest of the annotation intact");}
            var mosaic=new ImageEdit{Kind="mosaic",Points=new[]{new Point(70,60)},Diameter=36,Value=15};
            using(var original=ImageEditing.Load(source))using(var painted=ImageEditing.Render(source,new[]{mosaic}))using(var erased=ImageEditing.Render(source,new[]{mosaic,erase}))
            {Check(painted.GetPixel(70,60)!=original.GetPixel(70,60),"circular mosaic changes pixels beneath the brush");Check(painted.GetPixel(53,43)==original.GetPixel(53,43),"mosaic never fills the brush bounding rectangle");Check(erased.GetPixel(70,60)==original.GetPixel(70,60),"eraser removes mosaic and recovers underlying pixels");}
            using(var original=ImageEditing.Load(source))using(var untouched=ImageEditing.Render(source,new[]{erase}))Check(Hash(original)==Hash(untouched),"eraser cannot remove pixels from the original image");
            string transparent=Path.Combine(root,"透明原图.png");using(var b=new Bitmap(160,120)){using(var g=Graphics.FromImage(b)){g.Clear(Color.Transparent);g.FillRectangle(new SolidBrush(Color.FromArgb(120,50,100,150)),0,0,160,120);}b.Save(transparent);}
            using(var original=ImageEditing.Load(transparent))using(var restored=ImageEditing.Render(transparent,new[]{pen,erase}))Check(restored.GetPixel(70,60)==original.GetPixel(70,60),"eraser restores original transparency rather than painting white");
            using(var original=ImageEditing.Load(source))using(var texture=ImageBrushes.MosaicTexture(original,null,15))Check(texture.Width<original.Width/5&&texture.Height<original.Height/5,"mosaic preview cache is a small texture rather than full-size snapshots");
            var rotate=new ImageEdit{Kind="rotate"};var movedErase=new ImageEdit{Kind="erase",Points=new[]{new Point(60,70)},Diameter=14};
            using(var original=ImageEditing.Render(source,new[]{rotate}))using(var restored=ImageEditing.Render(source,new[]{pen,rotate,movedErase}))Check(restored.GetPixel(60,70)==original.GetPixel(60,70),"eraser stays aligned after rotating the image and annotations");
            var text=new ImageEdit{Kind="text",Text="OLD",Rect=new Rectangle(10,10,100,60),Value=30,Color=Color.Black};var replacement=text.Copy();replacement.Id=Guid.NewGuid().ToString("N");replacement.TargetId=text.Id;replacement.Kind="retext";replacement.Text="NEW";replacement.Color=Color.DarkRed;
            var expected=replacement.Copy();expected.Kind="text";
            using(var actual=ImageEditing.Render(source,new[]{text,replacement}))using(var wanted=ImageEditing.Render(source,new[]{expected}))Check(Hash(actual)==Hash(wanted),"editing a text object replaces its old glyphs instead of drawing over them");
            Check(ImageEditing.Effective(new[]{text,replacement}).Single().Id==text.Id,"text retains a stable identity for re-selection");
            var targets=ImageEditing.TextTargets(new[]{text,new ImageEdit{Kind="crop",Rect=new Rectangle(5,5,140,100)},rotate},new Size(160,120));Check(targets.Length==1&&targets[0].Bounds==new RectangleF(35,5,60,100),"text hit area follows crop and rotation");
            var filterHashes=new HashSet<string>();using(var original=ImageEditing.Load(source)){foreach(int i in Enumerable.Range(0,ImageFilters.Presets.Length)){using(var filtered=ImageFilters.Apply(original,i,100))filterHashes.Add(Hash(filtered));using(var zero=ImageFilters.Apply(original,i,0))Check(Hash(zero)==Hash(original),"zero strength restores original for "+ImageFilters.Presets[i].Key);}}
            Check(ImageFilters.Presets.Length==16&&filterHashes.Count==16,"sixteen filter presets produce distinct actual images");
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            using(var form=new MainForm(new string[0]))
            {
                form.Show();Application.DoEvents();var navigation=new[]{Field<ModernButton>(form,"pdfSection"),Field<ModernButton>(form,"audioSection"),Field<ModernButton>(form,"imageSection")};
                Check(navigation.Select(b=>b.Size).Distinct().Count()==1,"all sidebar entries have identical width and height");
                Check(navigation.All(b=>b.Parent==navigation[0].Parent)&&navigation[1].Top-navigation[0].Bottom==navigation[2].Top-navigation[1].Bottom,"sidebar entries share one grid and equal spacing");
                Call(form,"ChangeWorkspace",2);var images=Field<ImageWorkspace>(form,"imageWorkspace");Call(images,"QueueAdd",(object)new[]{source});Wait(images);Call(images,"SetMode","图片编辑");Wait(images);
                var canvas=Field<ImageCanvas>(images,"canvas");var strip=Field<ImageFileStrip>(images,"strip");var options=Field<FlowLayoutPanel>(images,"options");
                Check(!Field<ComboBox>(images,"editTool").Items.Cast<object>().Any(x=>x.ToString()=="滤镜")&&Field<Dictionary<string,FunctionTab>>(images,"tabs").ContainsKey("滤镜"),"filters are a separate top-level tool");
                var beforeCenter=canvas.ImagePoint(new Point(canvas.Width/2,canvas.Height/2));Mouse(canvas,"OnMouseDown",new Point(20,20),MouseButtons.Middle);Mouse(canvas,"OnMouseMove",new Point(90,80),MouseButtons.Middle);Mouse(canvas,"OnMouseUp",new Point(90,80),MouseButtons.Middle);var afterCenter=canvas.ImagePoint(new Point(canvas.Width/2,canvas.Height/2));Check(beforeCenter==afterCenter,"middle-button drag never moves the centered photograph");
                Tool(images,5);Field<NumericUpDown>(images,"strokeWidth").Value=12;Check(Math.Abs(canvas.BrushDiameter*canvas.Zoom-12*canvas.DeviceDpi/96F)<.01,"pen size control matches visible cursor diameter");
                Mouse(canvas,"OnMouseDown",new Point(25,30));Mouse(canvas,"OnMouseMove",new Point(110,30));Check(Field<ImageEdit>(canvas,"draft").Kind=="pen","dragging pen previews a stroke rather than a selection rectangle");
                using(var preview=new Bitmap(canvas.Width,canvas.Height)){canvas.DrawToBitmap(preview,new Rectangle(Point.Empty,canvas.Size));var p=canvas.ScreenPoint(new Point(65,30));Check(preview.GetPixel(p.X,p.Y).B>160,"pen shows real colored pixels before mouse release");}
                Mouse(canvas,"OnMouseUp",new Point(110,30));Wait(images);Check(strip.Items[0].Edits.Last().Kind=="pen"&&strip.Items[0].Edits.Last().Points.Length>=2,"mouse gesture commits an editable stroke path");
                Tool(images,3);Field<ComboBox>(images,"icons").SelectedItem="星";Mouse(canvas,"OnMouseDown",new Point(25,40));Mouse(canvas,"OnMouseMove",new Point(75,100));Check(Field<ImageEdit>(canvas,"draft").Kind=="icon"&&Field<ImageEdit>(canvas,"draft").Text=="星","icon drag previews the chosen star geometry");Shot(form,Path.Combine(root,"live-star.png"));Mouse(canvas,"OnMouseUp",new Point(75,100));Wait(images);
                Tool(images,8);Mouse(canvas,"OnMouseDown",new Point(130,100));Mouse(canvas,"OnMouseMove",new Point(90,40));var arrow=Field<ImageEdit>(canvas,"draft");Check(arrow.Kind=="arrow"&&arrow.Points[0].Y>arrow.Points.Last().Y,"arrow preview retains the actual upward drag direction");Mouse(canvas,"OnMouseUp",new Point(90,40));Wait(images);
                Tool(images,11);Field<NumericUpDown>(images,"brushSize").Value=24;Check(Math.Abs(canvas.BrushDiameter*canvas.Zoom-24*canvas.DeviceDpi/96F)<.01&&Field<Bitmap>(canvas,"eraseBase")!=null,"eraser slider size matches its visible circular footprint");Stroke(canvas,new Point(50,30),new Point(85,30));Wait(images);Check(strip.Items[0].Edits.Last().Kind=="erase","eraser mouse gesture records a reversible mask");
                Tool(images,1);Mouse(canvas,"OnMouseDown",new Point(100,80));Mouse(canvas,"OnMouseMove",new Point(120,80));using(var preview=new Bitmap(canvas.Width,canvas.Height))canvas.DrawToBitmap(preview,new Rectangle(Point.Empty,canvas.Size));var cached=Field<Bitmap>(canvas,"mosaicPreview");Mouse(canvas,"OnMouseMove",new Point(130,90));using(var preview=new Bitmap(canvas.Width,canvas.Height))canvas.DrawToBitmap(preview,new Rectangle(Point.Empty,canvas.Size));Check(cached!=null&&Object.ReferenceEquals(cached,Field<Bitmap>(canvas,"mosaicPreview")),"mosaic gesture reuses its texture while the pointer moves");Mouse(canvas,"OnMouseUp",new Point(130,90));Wait(images);
                Tool(images,2);Field<TextBox>(images,"content").Text="OLD";Stroke(canvas,new Point(10,5),new Point(150,55));Wait(images);var objectId=strip.Items[0].Edits.Last().Id;int count=strip.Items[0].Edits.Count;
                Mouse(canvas,"OnMouseDown",new Point(30,15));Mouse(canvas,"OnMouseUp",new Point(30,15));Check(strip.Items[0].Edits.Count==count&&canvas.SelectedTextId==objectId&&Field<TextBox>(images,"content").Text=="OLD","clicking existing text selects it without adding a second text box");
                Field<TextBox>(images,"content").Text="NEW";Call(images,"FlushText",true);Wait(images);Check(ImageEditing.Effective(strip.Items[0].Edits).Single(e=>e.Id==objectId).Text=="NEW","selected text is modified through the real editing controls");
                SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());Field<ModernButton>(images,"undo").PerformClick();Wait(images);Check(ImageEditing.Effective(strip.Items[0].Edits).Single(e=>e.Id==objectId).Text=="OLD","undo restores previous text content");
                Call(images,"SetMode","滤镜");Wait(images);Field<ImageFilterPicker>(images,"filterPicker").SelectedIndex=10;Run(images,"RenderSelected");Shot(form,Path.Combine(root,"filter-gallery.png"));Run(images,"Process");var store=Field<ToolResultStore>(images,"outcomes");using(var exported=ImageEditing.Load(store.Get("滤镜").Files[0]))Check(Hash(exported)==Hash(canvas.Image),"filter output is pixel-identical to the displayed PNG preview");store.Get("滤镜").Downloaded=true;
                Call(images,"SetMode","图片编辑");Wait(images);Check(!Field<ModernButton>(images,"download").Visible,"editor never shows the filter tool download button");
                form.Size=form.MinimumSize;Application.DoEvents();Shot(form,Path.Combine(root,"sidebar-minimum.png"));Check(navigation.Select(b=>b.Size).Distinct().Count()==1,"sidebar alignment survives minimum window size");
                Check(bytes.SequenceEqual(File.ReadAllBytes(source)),"brush mosaic eraser text filters preserve original file bytes");strip.Items[0].Dirty=false;ClearPending(images);form.Close();while(!form.IsDisposed){Application.DoEvents();Thread.Sleep(5);}
            }
            Console.WriteLine("ALL "+checks+" IMAGE INTERACTION CHECKS PASSED");return 0;
        }catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
    static void ClearPending(ImageWorkspace images){Call(images,"ClearTextSelection");}
}
