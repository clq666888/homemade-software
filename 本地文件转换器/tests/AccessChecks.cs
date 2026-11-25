using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using LocalConverter;

static class AccessChecks
{
    static int checks;
    static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
    static T Field<T>(object obj,string name){return (T)obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(obj);}
    static object Call(object obj,string name,params object[] args){if(obj is MainForm)SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());return obj.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(obj,args);}
    static void Pump(Task task){var clock=System.Diagnostics.Stopwatch.StartNew();while(!task.IsCompleted){Application.DoEvents();Thread.Sleep(5);if(clock.Elapsed.TotalSeconds>45)throw new Exception("access UI timeout");}task.GetAwaiter().GetResult();Application.DoEvents();}
    static void Shot(Form form,string path){Application.DoEvents();using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(path);}}
    static async Task StoreChecks(string root)
    {
        using(var engine=new PdfEngine())using(var access=new PdfAccessStore())
        {
            string a=Path.Combine(root,"a.pdf"),b=Path.Combine(root,"b.pdf");int asked=0;
            Func<string,Func<string,CancellationToken,Task<bool>>,Task<string>> ask=async(path,verify)=>{asked++;Check(!access.IsCurrent(path),"locked input absent before verification");Check(!await verify("wrong",CancellationToken.None)&&!access.IsCurrent(path),"wrong password cannot admit file");string secret=path==a?"alpha-test":"beta-test";Check(await verify(secret,CancellationToken.None),"correct password validates");return secret;};
            Check(await access.AdmitAsync(a,engine,CancellationToken.None,ask)&&await access.AdmitAsync(b,engine,CancellationToken.None,ask),"different-password inputs both admitted");
            Check(access.PasswordFor(a)=="alpha-test"&&access.PasswordFor(b)=="beta-test","credentials isolated per input");
            Check(await access.AdmitAsync(a,engine,CancellationToken.None,ask)&&asked==2,"unchanged input does not prompt again");
            access.Forget(b);Check(!await access.AdmitAsync(b,engine,CancellationToken.None,(path,verify)=>Task.FromResult<string>(null))&&!access.IsCurrent(b),"cancel skips locked file without cached credential");
            bool forged=false;try{await access.AdmitAsync(b,engine,CancellationToken.None,(path,verify)=>Task.FromResult("beta-test"));}catch(IOException){forged=true;}Check(forged&&!access.IsCurrent(b),"unverified response cannot bypass admission");
            File.SetLastWriteTimeUtc(a,File.GetLastWriteTimeUtc(a).AddSeconds(2));bool stale=false;try{access.PasswordFor(a);}catch(IOException){stale=true;}Check(!access.IsCurrent(a)&&stale,"external file change invalidates stored verification");
            Check(await access.AdmitAsync(a,engine,CancellationToken.None,ask)&&asked==3,"changed input requires fresh verification");access.Forget(a);Check(!access.IsCurrent(a),"removing input releases credential");
            Check(!Directory.GetDirectories(engine.Workspace).Any(),"probe jobs cleaned after password checks");
        }
    }
    [STAThread]static int Main(string[] args)
    {
        try
        {
            string root=Path.GetFullPath(args[0]);Task.Run(()=>StoreChecks(root)).GetAwaiter().GetResult();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            using(var dialog=new PdfPasswordDialog("加密文件.pdf",(candidate,token)=>Task.FromResult(candidate=="correct"),CancellationToken.None))
            {
                dialog.Show();Application.DoEvents();SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());var input=Field<TextBox>(dialog,"input");var confirm=Field<ModernButton>(dialog,"confirm");input.Text="wrong";confirm.PerformClick();Pump(Field<Task>(dialog,"verification"));Check(!dialog.IsDisposed&&Field<Label>(dialog,"message").Text.Contains("密码不正确")&&input.UseSystemPasswordChar,"password dialog masks input and keeps inline retry");Shot(dialog,Path.Combine(root,"password-retry.png"));input.Text="correct";confirm.PerformClick();Pump(Field<Task>(dialog,"verification"));Check(dialog.DialogResult==DialogResult.OK&&dialog.Password=="correct","validated dialog completes with accepted password");
            }
            using(var dialog=new PdfPasswordDialog("等待验证.pdf",async(candidate,token)=>{await Task.Delay(5000,token);return true;},CancellationToken.None))
            {
                dialog.Show();Application.DoEvents();SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());Field<ModernButton>(dialog,"confirm").PerformClick();Field<ModernButton>(dialog,"cancel").PerformClick();Pump(Field<Task>(dialog,"verification"));Check(dialog.DialogResult==DialogResult.Cancel&&dialog.Password==null,"cancel during verification waits for cancellation without admitting file");
            }
            using(var form=new MainForm(new string[0]))using(var timer=new System.Windows.Forms.Timer{Interval=40})
            {
                form.Show();Application.DoEvents();SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());var access=Field<PdfAccessStore>(form,"pdfAccess");int phase=0;bool cancelB=true;Exception failure=null;
                timer.Tick+=(s,e)=>{try{var dialog=Application.OpenForms.OfType<PdfPasswordDialog>().FirstOrDefault();if(dialog==null)return;var input=Field<TextBox>(dialog,"input");var confirm=Field<ModernButton>(dialog,"confirm");if(!confirm.Enabled)return;if(cancelB&&phase>=2){Field<ModernButton>(dialog,"cancel").PerformClick();phase++;return;}if(phase==0){Check(!Field<FileCards>(form,"cards").Items.Any(f=>f.Path.EndsWith("a.pdf")),"real import excludes encrypted file before password accepted");input.Text="wrong";confirm.PerformClick();phase=1;}else if(phase==1&&Field<Label>(dialog,"message").Text.Contains("密码不正确")){Check(!access.IsCurrent(Path.Combine(root,"a.pdf")),"wrong password keeps real file outside access store");input.Text="alpha-test";confirm.PerformClick();phase=2;}else if(!cancelB){input.Text="beta-test";confirm.PerformClick();phase++;}}catch(Exception error){failure=error;timer.Stop();}};timer.Start();
                Call(form,"AddFiles",(object)new[]{Path.Combine(root,"plain.pdf"),Path.Combine(root,"a.pdf"),Path.Combine(root,"b.pdf")});Pump(Field<Task>(form,"admissionTask"));if(failure!=null)throw failure;Pump(Field<Task>(form,"previewTask"));
                Check(Field<FileCards>(form,"cards").Items.Count==2&&access.IsCurrent(Path.Combine(root,"a.pdf"))&&!access.IsCurrent(Path.Combine(root,"b.pdf")),"real import admits verified files and skips cancelled file");
                cancelB=false;Call(form,"AddFiles",(object)new[]{Path.Combine(root,"b.pdf")});Pump(Field<Task>(form,"admissionTask"));timer.Stop();if(failure!=null)throw failure;Pump(Field<Task>(form,"previewTask"));Check(Field<FileCards>(form,"cards").Items.Count==3&&Field<PdfPagePreview>(form,"pagePicture").Visible,"different-password file previews after admission");
                Call(form,"SetMode","水印");Pump(Field<Task>(form,"previewTask"));var wm=Field<WatermarkSettings>(form,"watermarkSettings");wm.StylePicker.SelectedIndex=6;
                for(int position=0;position<9;position++){Call(wm.PositionPicker,"OnMouseDown",new MouseEventArgs(MouseButtons.Left,1,12+position%3*26,12+position/3*26,0));Check(wm.Snapshot().Position==position&&Field<PdfPagePreview>(form,"pagePicture").Watermark.Position==position,"nine-grid click updates preview position "+position);}
                Check(wm.PositionPicker is PositionGrid&&wm.ColorPicker.Text==""&&wm.ColorPicker.SelectedColor==wm.Snapshot().Color,"position uses grid and color uses actual swatch without code");Shot(form,Path.Combine(root,"watermark-grid.png"));
                Pump((Task)Call(form,"ProcessFiles"));var results=(ResultSession)typeof(MainForm).GetProperty("results",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form,null);results.Downloaded=true;Check(results.Files.Length==3,"real watermark job handles a mixed-password batch");
                foreach(string mode in new[]{"转换","页面编辑","合并","拆分","水印","加密","移除加密","压缩"}){Call(form,"SetMode",mode);if(Field<Task>(form,"previewTask")!=null)Pump(Field<Task>(form,"previewTask"));if(Field<CancellationTokenSource>(form,"loadCancel")!=null){var clock=System.Diagnostics.Stopwatch.StartNew();while(Field<CancellationTokenSource>(form,"loadCancel")!=null){Application.DoEvents();Thread.Sleep(5);if(clock.Elapsed.TotalSeconds>20)throw new Exception("page load timeout");}}var inputs=Field<FlowLayoutPanel>(form,"options").Controls.OfType<TextBox>().ToArray();Check(inputs.All(t=>t.AccessibleName=="新的打开密码"),"no original-password box in "+mode);}
                Call(form,"SetMode","水印");wm.StylePicker.SelectedIndex=6;form.Size=form.MinimumSize;Application.DoEvents();Shot(form,Path.Combine(root,"watermark-grid-minimum.png"));Check(wm.PositionPicker.Height>=70&&wm.PositionPicker.Width>=75&&!Field<FlowLayoutPanel>(form,"options").HorizontalScroll.Visible,"nine-grid remains complete at minimum window width");form.Close();while(!form.IsDisposed){Application.DoEvents();Thread.Sleep(5);}
            }
            Console.WriteLine("ALL "+checks+" ADMISSION AND VISUAL PICKER CHECKS PASSED");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
