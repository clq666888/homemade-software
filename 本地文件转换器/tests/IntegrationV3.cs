using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using LocalConverter;
static class IntegrationV3
{
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
    [STAThread]static int Main(string[] args)
    {
        if(args.Length==4&&args[0]=="--word-worker")return WordConverter.RunWorker(args[1],args[2],args[3]);
        try
        {
            string folder=Path.GetFullPath(args[0]);var doc=new PageDocument();doc.Pages=Enumerable.Range(0,5).Select(i=>new PageItem{Index=i}).ToList();
            string conversionFolder,compressionFolder;
            using(var store=new ToolResultStore())
            {
                var conversion=new ResultSession();conversionFolder=conversion.Folder;File.WriteAllText(Path.Combine(conversion.Folder,"conversion.pdf"),"convert result");store.Set("转换",conversion,"PDF|input-a");
                Check(store.Get("压缩")==null,"unprocessed tool has no foreign download result");
                var compression=new ResultSession();compressionFolder=compression.Folder;File.WriteAllText(Path.Combine(compression.Folder,"compression.pdf"),"compress result");store.Set("压缩",compression,"balanced|input-a");
                Check(store.Get("转换")==conversion&&store.Get("压缩")==compression,"switching tools restores only own result");
                Check(!store.Matches("转换","PNG|input-a")&&store.Matches("压缩","balanced|input-a"),"changed output options distinguish old results");
                store.Invalidate("压缩");Check(!store.Matches("压缩","balanced|input-a"),"password changes invalidate only current tool context");
                Check(store.PendingTools.Length==2,"undownloaded results across tools preserved");compression.Downloaded=true;Check(store.PendingTools.SequenceEqual(new[]{"转换"}),"close tracks all tools' pending downloads");
            }
            Check(!Directory.Exists(conversionFolder)&&!Directory.Exists(compressionFolder),"all tool sessions cleaned on close");
            doc.Move(new[]{1,3},5);Check(doc.Pages.Select(p=>p.Index).SequenceEqual(new[]{0,2,4,1,3}),"multi-page reorder");doc.Delete(new[]{0,2});Check(doc.Pages.Select(p=>p.Index).SequenceEqual(new[]{2,1,3}),"multi-delete");doc.Rotate(new[]{0,2});Check(doc.Pages[0].Rotation==90&&doc.Pages[2].Rotation==90,"multi-rotate");doc.Undo();Check(doc.Pages.All(p=>p.Rotation==0),"undo rotation");doc.Undo();doc.Undo();Check(doc.Pages.Select(p=>p.Index).SequenceEqual(new[]{0,1,2,3,4}),"undo full edit sequence");
            string original=Path.Combine(folder,"overwrite.pdf"),processed=Path.Combine(folder,"a.pdf");File.Copy(processed,original,true);var info=new FileInfo(original);string backup=PageDocument.Overwrite(Path.Combine(folder,"b.pdf"),original,info.Length,info.LastWriteTimeUtc);Check(File.Exists(backup)&&File.ReadAllBytes(backup).SequenceEqual(File.ReadAllBytes(processed)),"atomic overwrite backup preserves original");
            bool changed=false;try{PageDocument.Overwrite(processed,original,-1,DateTime.MinValue);}catch(IOException){changed=true;}Check(changed,"changed original rejected");
            using(var engine=new PdfEngine())
            {
                var inspect=engine.Run("inspect",new Dictionary<string,object>{{"files",new[]{processed}}},CancellationToken.None);Check(System.Convert.ToInt32(inspect["count"])==3,"packaged PDF engine IPC");
                var cancelled=new CancellationTokenSource();cancelled.Cancel();bool cancel=false;try{engine.Run("inspect",new Dictionary<string,object>{{"files",new[]{processed}}},cancelled.Token);}catch(OperationCanceledException){cancel=true;}Check(cancel,"cancelled engine request");
            }
            string docx=Path.Combine(folder,"fixture.docx"),binary=Path.Combine(folder,"fixture.doc"),pdf=Path.Combine(folder,"word.pdf"),back=Path.Combine(folder,"roundtrip.docx");
            WordConverter.ExportFile(docx,binary,CancellationToken.None);WordConverter.ExportFile(docx,pdf,CancellationToken.None);
            using(var engine=new PdfEngine())
            {
                var inverse=engine.Run("convert",new Dictionary<string,object>{{"files",new[]{pdf}},{"format","docx"},{"folder",Path.Combine(folder,"docx-back")}},CancellationToken.None);string generated=PdfEngine.Outputs(inverse)[0];Check(File.Exists(generated)&&new FileInfo(generated).Length>1000,"Word PDF bidirectional conversion");
                WordConverter.ExportFile(generated,Path.Combine(folder,"rebuilt-word.pdf"),CancellationToken.None);Check(File.Exists(Path.Combine(folder,"rebuilt-word.pdf")),"generated DOCX opens in Word and exports");
            }
            var watch=Stopwatch.StartNew();using(var preview=FilePreview.Load(new InputFile(binary),CancellationToken.None)){Check(preview.Text.Contains("本地预览验收")&&preview.Text.Contains("Local preview"),"DOC piece table fast Chinese text");}Console.WriteLine("DOC_PREVIEW_MS="+watch.ElapsedMilliseconds);
            using(var result=new ResultSession())
            {
                string image=Path.Combine(result.Folder,"sample.txt");File.WriteAllText(image,"text");Check(result.Files.Length==1,"non-PDF results discoverable");string saved=ResultSession.SaveCopy(image,folder,"downloaded",CancellationToken.None);Check(saved.EndsWith(".txt")&&File.ReadAllText(saved)=="text","download keeps selected output format");
            }
            Console.WriteLine("ALL INTEGRATION CHECKS PASSED");return 0;
        }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
