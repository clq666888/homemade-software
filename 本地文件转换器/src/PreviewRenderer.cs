using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace LocalConverter
{
    public sealed class PreviewPage
    {
        public string Path;
        public int Count;
        public bool Cached;
        public float Width,Height;
    }
    public sealed class PreviewRenderer : IDisposable
    {
        readonly object gate=new object();
        readonly string workspace=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"LocalConverterPreview-"+Guid.NewGuid().ToString("N"));
        readonly JavaScriptSerializer json=new JavaScriptSerializer{MaxJsonLength=1000000};
        Process worker;ProcessJob group;StreamWriter input;StreamReader output;bool disposed;
        public int WorkerId{get{lock(gate)return worker==null?0:worker.Id;}}
        void Start()
        {
            if(worker!=null&&!worker.HasExited)return;Stop();Directory.CreateDirectory(workspace);
            try
            {
                group=new ProcessJob();worker=Process.Start(new ProcessStartInfo(PdfEngine.Executable,"--preview-server \""+workspace+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true});group.Add(worker);
                input=new StreamWriter(worker.StandardInput.BaseStream,new UTF8Encoding(false)){AutoFlush=true};output=new StreamReader(worker.StandardOutput.BaseStream,Encoding.UTF8);
            }
            catch{Stop();throw;}
        }
        public PreviewPage Render(string source,int index,int rotation,int edge,string password,CancellationToken token)
        {
            lock(gate)
            {
                token.ThrowIfCancellationRequested();if(disposed)throw new ObjectDisposedException("PreviewRenderer");Start();
                try
                {
                    input.WriteLine(json.Serialize(new Dictionary<string,object>{{"source",source},{"index",index},{"rotation",rotation},{"edge",edge},{"password",password}}));
                    var reply=output.ReadLineAsync();var watch=Stopwatch.StartNew();long cancelledAt=-1;
                    while(!reply.Wait(15))
                    {
                        if(token.IsCancellationRequested){if(cancelledAt<0)cancelledAt=watch.ElapsedMilliseconds;if(watch.ElapsedMilliseconds-cancelledAt>250){Stop();token.ThrowIfCancellationRequested();}}
                        if(watch.Elapsed.TotalSeconds>20){Stop();throw new IOException("页面预览超时，请检查 PDF 或重试。");}
                    }
                    token.ThrowIfCancellationRequested();if(reply.Result==null){Stop();throw new IOException("预览进程已退出，请重试。");}
                    var result=json.Deserialize<Dictionary<string,object>>(reply.Result);if(!(bool)result["ok"])throw new IOException((string)result["error"]);
                    return new PreviewPage{Path=(string)result["path"],Count=System.Convert.ToInt32(result["count"]),Cached=(bool)result["cached"],Width=result.ContainsKey("width")?System.Convert.ToSingle(result["width"]):0,Height=result.ContainsKey("height")?System.Convert.ToSingle(result["height"]):0};
                }
                catch(OperationCanceledException){throw;}
                catch(IOException){throw;}
                catch(Exception error){Stop();token.ThrowIfCancellationRequested();throw new IOException("页面预览失败："+error.Message,error);}
            }
        }
        void Stop()
        {
            if(worker!=null){try{if(!worker.HasExited){worker.Kill();worker.WaitForExit(1500);}}catch(InvalidOperationException){}worker.Dispose();worker=null;}
            if(group!=null){group.Dispose();group=null;}
            if(input!=null){input.Dispose();input=null;}if(output!=null){output.Dispose();output=null;}
            try{if(Directory.Exists(workspace))Directory.Delete(workspace,true);}catch(IOException){}catch(UnauthorizedAccessException){}
        }
        public void Dispose(){lock(gate){disposed=true;Stop();}}
    }
}
