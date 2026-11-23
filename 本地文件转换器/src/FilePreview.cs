using System;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;

namespace LocalConverter
{
    public sealed class PreviewContent : IDisposable
    {
        public Bitmap Image;
        public string Text;
        public void Dispose() { if (Image != null) Image.Dispose(); }
    }
    public static class FilePreview
    {
        public static PreviewContent Load(InputFile input, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            string ext = Path.GetExtension(input.Path).ToLowerInvariant();
            if(OfficeConverter.Supports(input.Path)||HtmlConverter.Supports(input.Path))
            {
                using(var engine=new PdfEngine()){var result=engine.Run("office_text",new System.Collections.Generic.Dictionary<string,object>{{"files",new[]{input.Path}}},token);return new PreviewContent{Text="快速内容预览 · 省略图片与排版（最多约 16000 字）\r\n\r\n"+(string)result["text"]};}
            }
            if (ext == ".docx")
            {
                using (var archive = ZipFile.OpenRead(input.Path))
                {
                    var entry = archive.GetEntry("word/document.xml");
                    if (entry == null || entry.Length > 10000000) throw new IOException("文档过大或内容格式不正确。");
                    using (var stream = entry.Open())
                    using (var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 10000000 }))
                    {
                        var xml = XDocument.Load(reader); XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
                        var text = new StringBuilder();
                        foreach (var paragraph in xml.Descendants(w + "p"))
                        {
                            token.ThrowIfCancellationRequested();
                            foreach (var node in paragraph.Descendants()) { if (node.Name == w + "t") text.Append(node.Value); else if (node.Name == w + "tab") text.Append("  "); else if (node.Name == w + "br") text.AppendLine(); }
                            text.AppendLine(); if (text.Length > 16000) break;
                        }
                        return new PreviewContent { Text = "正文预览（省略图片与原始排版，最多约 16000 字）\r\n\r\n" + text.ToString() };
                    }
                }
            }
            if (ext == ".doc")
            {
                using(var engine=new PdfEngine())
                {
                    var result=engine.Run("doc_text",new System.Collections.Generic.Dictionary<string,object>{{"files",new[]{input.Path}}},token);
                    return new PreviewContent{Text="快速正文预览 · 不启动 Word\r\n\r\n"+(string)result["text"]};
                }
            }
            using (var source = Image.FromFile(input.Path))
            {
                if ((long)source.Width * source.Height > 80000000) throw new IOException("图片超过 8000 万像素，请先缩小后再预览。");
                using (var oriented = new Bitmap(source))
                {
                ImageConverter.ApplyExif(oriented, source);
                if (input.Rotation != 0) oriented.RotateFlip(input.Rotation == 90 ? System.Drawing.RotateFlipType.Rotate90FlipNone : input.Rotation == 180 ? System.Drawing.RotateFlipType.Rotate180FlipNone : System.Drawing.RotateFlipType.Rotate270FlipNone);
                double ratio = Math.Min(1, 1100.0 / Math.Max(oriented.Width, oriented.Height));
                var thumbnail = new Bitmap(Math.Max(1, (int)(oriented.Width * ratio)), Math.Max(1, (int)(oriented.Height * ratio)));
                using (var g = Graphics.FromImage(thumbnail)) { g.Clear(Color.White); g.DrawImage(oriented, 0, 0, thumbnail.Width, thumbnail.Height); }
                return new PreviewContent { Image = thumbnail, Text = Path.GetFileName(input.Path) + " · " + oriented.Width + " × " + oriented.Height + (ext == ".tif" || ext == ".tiff" ? " · 仅预览首页" : "") };
                }
            }
        }
    }
    public sealed class PreviewPanel : UserControl
    {
        readonly PictureBox picture = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.White };
        readonly RichTextBox text = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = Color.White, DetectUrls = false };
        readonly Label caption = new Label { Dock = DockStyle.Top, Height = 90, Text = "文件预览\r\n选择列表中的文件查看内容", Padding = new Padding(8), AutoEllipsis = true };
        readonly SemaphoreSlim gate = new SemaphoreSlim(1);
        CancellationTokenSource request;
        Task pending = Task.FromResult(0);
        readonly System.Collections.Generic.Dictionary<string,string> textCache=new System.Collections.Generic.Dictionary<string,string>();
        public PreviewPanel() { Dock = DockStyle.Fill; Padding = new Padding(20); BackColor = Color.White; Font=new Font("Microsoft YaHei UI",10F); text.Font=Font; text.ForeColor=Color.FromArgb(53,65,83);caption.ForeColor=Color.FromArgb(83,103,132);caption.Height=66; Controls.Add(picture); Controls.Add(text); Controls.Add(caption); picture.Visible = false; text.Text = "选择文件后，这里会显示图片或 Word 正文。"; }
        public Task StopAsync() { if (request != null) request.Cancel(); return pending; }
        public void ShowFile(InputFile file)
        {
            if (request != null) request.Cancel();
            var current = new CancellationTokenSource(); request = current;
            pending = RefreshAsync(file == null ? null : new InputFile(file.Path) { Rotation = file.Rotation }, current, pending);
        }
        async Task RefreshAsync(InputFile file, CancellationTokenSource current, Task previous)
        {
            await previous;
            if (current.IsCancellationRequested) { if (request == current) request = null; current.Dispose(); return; }
            if (picture.Image != null) { var old = picture.Image; picture.Image = null; old.Dispose(); }
            picture.Visible = false; text.Visible = true; text.Text = file == null ? "选择文件后，这里会显示图片或 Word 正文。" : "正在加载预览…";
            caption.Text = "文件预览\r\n" + (file == null ? "请选择一个文件" : Path.GetFileName(file.Path));
            try
            {
                if (file == null) return;
                await Task.Delay(180, current.Token);
                await gate.WaitAsync(current.Token);
                PreviewContent content;
                try
                {
                    string key=file.Path+"|"+File.GetLastWriteTimeUtc(file.Path).Ticks+"|"+new FileInfo(file.Path).Length;
                    string cached;
                    if(textCache.TryGetValue(key,out cached))content=new PreviewContent{Text=cached};
                    else {content = await Task.Run(() => FilePreview.Load(file, current.Token));if(content.Image==null){if(textCache.Count>=16)textCache.Clear();textCache[key]=content.Text;}}
                } finally { gate.Release(); }
                using (content)
                {
                    if (current.IsCancellationRequested || IsDisposed) return;
                    if (content.Image != null) { picture.Image = content.Image; content.Image = null; picture.Visible = true; text.Visible = false; caption.Text = "文件预览\r\n" + content.Text; }
                    else text.Text = content.Text;
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { if (!current.IsCancellationRequested && !IsDisposed) text.Text = "无法预览此文件：\r\n" + e.Message; }
            finally { if (request == current) request = null; current.Dispose(); }
        }
        protected override void Dispose(bool disposing) { if (disposing) { if (request != null) request.Cancel(); if (picture.Image != null) { picture.Image.Dispose(); picture.Image = null; } } base.Dispose(disposing); }
    }
}
