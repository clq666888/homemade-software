using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using LocalConverter;

static class EngineTests
{
    static void Check(bool value, string name) { if (!value) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); }
    [STAThread] static int Main(string[] args)
    {
        if (args.Length == 4 && args[0] == "--word-worker") return WordConverter.RunWorker(args[1], args[2], args[3]);
        try
        {
            string root = Path.GetFullPath(args[0]); string input = Path.Combine(root, "inputs"); string output = Path.Combine(root, "outputs"); Directory.CreateDirectory(output);
            var image = new ImageConverter(); var opts = new ConversionOptions { OutputFolder = output, MergeName = "合并验收" };
            var files = new List<InputFile> { new InputFile(Path.Combine(input, "竖版 图片.jpg")), new InputFile(Path.Combine(input, "透明 横图.png")) };
            Action<int,string> progress = (p,s) => { };
            image.Convert(files, opts, CancellationToken.None, progress);
            image.Convert(files, opts, CancellationToken.None, progress);
            Check(File.Exists(Path.Combine(output,"合并验收.pdf")) && File.Exists(Path.Combine(output,"合并验收 (1).pdf")), "merge and collision keep original");
            opts.Merge = false;
            image.Convert(files.Concat(new[] { new InputFile(Path.Combine(input,"多页 扫描.tiff")), new InputFile(Path.Combine(input,"普通.bmp")) }).ToList(),opts,CancellationToken.None,progress);
            Check(File.Exists(Path.Combine(output,"多页 扫描.pdf")),"multipage TIFF and separate conversion");
            opts.Merge = true; opts.MergeName = "旋转验收"; files[0].Rotation = 90;
            image.Convert(new[]{files[0]},opts,CancellationToken.None,progress);
            Check(File.Exists(Path.Combine(output,"旋转验收.pdf")),"rotation");
            opts.Orientation = 1; opts.MergeName = "强制纵向";
            image.Convert(new[]{files[1]},opts,CancellationToken.None,progress);
            bool badName = false; try { OutputFiles.ValidateName("../x"); } catch(IOException) { badName = true; }
            Check(badName,"invalid output name rejected");
            bool cancelled = false; var cts = new CancellationTokenSource(); cts.Cancel();
            opts.MergeName = "取消验收";
            try { image.Convert(files,opts,cts.Token,progress); } catch(OperationCanceledException) { cancelled = true; }
            Check(cancelled && !File.Exists(Path.Combine(output,"取消验收.pdf")),"cancel has no partial PDF");
            opts.MergeName = "损坏验收"; bool invalid = false;
            try { image.Convert(new[]{files[0],new InputFile(Path.Combine(input,"损坏.png"))},opts,CancellationToken.None,progress); } catch(IOException) { invalid = true; }
            Check(invalid && !File.Exists(Path.Combine(output,"损坏验收.pdf")),"corrupt merged input leaves no PDF");
            opts.Merge = false;
            try { image.Convert(new[]{new InputFile(Path.Combine(input,"损坏.png")),files[0]},opts,CancellationToken.None,progress); } catch(IOException) { }
            Check(File.Exists(Path.Combine(output,"竖版 图片 (1).pdf")),"batch continues after corrupt image");
            Check(Directory.GetFiles(output,"*.tmp").Length == 0,"image temporary files cleaned");
            using (var preview = FilePreview.Load(files[0], CancellationToken.None)) Check(preview.Image != null && preview.Image.Width > preview.Image.Height, "image preview follows rotation");
            using (var preview = FilePreview.Load(new InputFile(Path.Combine(input,"中文 测试.docx")), CancellationToken.None)) Check(preview.Text.Contains("本地转换验收文档"), "DOCX preview text");
            string sessionFolder;
            using (var session = new ResultSession())
            {
                sessionFolder = session.Folder;
                image.Convert(files,new ConversionOptions { OutputFolder = session.Folder, MergeName = "临时结果" },CancellationToken.None,progress);
                Check(session.Files.Length == 1 && !session.Downloaded,"conversion stages results before download");
                string downloadFolder = Path.Combine(root,"downloads");
                string first = ResultSession.SaveCopy(session.Files[0],downloadFolder,"自选名称",CancellationToken.None);
                string second = ResultSession.SaveCopy(session.Files[0],downloadFolder,"自选名称",CancellationToken.None);
                Check(first != second && File.ReadAllBytes(first).SequenceEqual(File.ReadAllBytes(session.Files[0])),"download copy and collision protection");
                bool saveCancelled = false;
                try { ResultSession.SaveCopy(session.Files[0],downloadFolder,"取消保存",cts.Token); } catch(OperationCanceledException) { saveCancelled = true; }
                Check(saveCancelled && Directory.GetFiles(downloadFolder,"*.tmp").Length == 0,"cancelled download leaves no partial file");
            }
            Check(!Directory.Exists(sessionFolder),"session cleanup");
            var before = System.Diagnostics.Process.GetProcessesByName("WINWORD").Select(p=>p.Id).ToArray();
            var word = new WordConverter();
            word.Convert(new[]{new InputFile(Path.Combine(input,"中文 测试.docx"))},opts,CancellationToken.None,progress);
            Check(File.Exists(Path.Combine(output,"中文 测试.pdf")),"Word DOCX conversion");
            Check(WordConverter.ReadPreview(Path.Combine(input,"中文 测试.docx"),CancellationToken.None).Contains("本地转换验收文档"),"Word worker text preview");
            bool badWord = false;
            try { word.Convert(new[]{new InputFile(Path.Combine(input,"损坏.docx"))},opts,CancellationToken.None,progress); } catch(IOException) { badWord = true; }
            Check(badWord,"corrupt Word gives actionable failure");
            Check(System.Diagnostics.Process.GetProcessesByName("WINWORD").All(p => before.Contains(p.Id)),"no conversion Word process remains");
            Check(Directory.GetFiles(output,"*.tmp").Length == 0,"all temporary PDF files cleaned");
            Console.WriteLine("ALL ENGINE CHECKS PASSED"); return 0;
        }
        catch(Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
