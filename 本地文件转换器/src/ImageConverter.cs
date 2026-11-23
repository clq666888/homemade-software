using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;

namespace LocalConverter
{
    public sealed class ImageConverter : IConverter
    {
        public string Id { get { return "images-pdf"; } }
        public string Title { get { return "图片转 PDF"; } }
        public string Category { get { return "转换为 PDF"; } }
        public string Description { get { return "将照片、扫描件整理成 PDF。支持调整顺序、旋转与多图合并。"; } }
        public string[] Extensions { get { return new[] { ".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff" }; } }
        public void Convert(IList<InputFile> files, ConversionOptions options, CancellationToken token, Action<int, string> progress)
        {
            if (files.Count == 0) throw new ArgumentException("请先添加图片。");
            Directory.CreateDirectory(options.OutputFolder);
            if (options.Merge) Write(files, options, options.MergeName, token, progress);
            else
            {
                var errors = new List<string>();
                for (int i = 0; i < files.Count; i++)
                {
                    token.ThrowIfCancellationRequested();
                    int at = i;
                    try { Write(new[] { files[i] }, options, Path.GetFileNameWithoutExtension(files[i].Path), token,
                        (p, s) => progress((at * 100 + p) / files.Count, s)); }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception e) { errors.Add(Path.GetFileName(files[i].Path) + "：" + e.Message); }
                }
                if (errors.Count > 0) throw new IOException("部分文件未能转换，已完成的 PDF 已保留。\r\n" + String.Join("\r\n", errors));
            }
        }
        static void Write(IList<InputFile> files, ConversionOptions o, string name, CancellationToken token, Action<int, string> progress)
        {
            OutputFiles.ValidateName(name);
            string temp = OutputFiles.Temporary(o.OutputFolder);
            try
            {
                using (var pdf = new ImagePdfWriter(temp))
                {
                    for (int i = 0; i < files.Count; i++)
                    {
                        token.ThrowIfCancellationRequested();
                        InputFile input = files[i];
                        progress(i * 100 / files.Count, "正在处理 " + Path.GetFileName(input.Path));
                        using (var image = LoadImage(input.Path))
                        {
                            if ((long)image.Width * image.Height > 80000000) throw new IOException("图片超过 8000 万像素，请先缩小后再转换。");
                            var frameDimension = new FrameDimension(image.FrameDimensionsList[0]);
                            int frames = image.GetFrameCount(frameDimension);
                            for (int f = 0; f < frames; f++)
                            {
                                token.ThrowIfCancellationRequested();
                                image.SelectActiveFrame(frameDimension, f);
                                using (var oriented = new Bitmap(image))
                                {
                                    ApplyExif(oriented, image);
                                    if (input.Rotation != 0) oriented.RotateFlip(input.Rotation == 90 ? RotateFlipType.Rotate90FlipNone : input.Rotation == 180 ? RotateFlipType.Rotate180FlipNone : RotateFlipType.Rotate270FlipNone);
                                    bool landscape = o.Orientation == 2 || (o.Orientation == 0 && oriented.Width > oriented.Height);
                                    double pw = landscape ? 841.8898 : 595.2756, ph = landscape ? 595.2756 : 841.8898;
                                    double scale = Math.Min(1.0, Math.Min((pw - 56.7) / 72 * 300 / oriented.Width, (ph - 56.7) / 72 * 300 / oriented.Height));
                                    using (var raster = new Bitmap(Math.Max(1, (int)(oriented.Width * scale)), Math.Max(1, (int)(oriented.Height * scale)), PixelFormat.Format24bppRgb))
                                    {
                                        using (var g = Graphics.FromImage(raster))
                                        {
                                            g.Clear(Color.White);
                                            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                                            g.DrawImage(oriented, 0, 0, raster.Width, raster.Height);
                                        }
                                        using (var bytes = new MemoryStream())
                                        using (var ep = new EncoderParameters(1))
                                        {
                                            ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 90L);
                                            raster.Save(bytes, ImageCodecInfo.GetImageEncoders().First(c => c.MimeType == "image/jpeg"), ep);
                                            pdf.AddPage(bytes.ToArray(), raster.Width, raster.Height, pw, ph);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    pdf.Complete();
                }
                token.ThrowIfCancellationRequested();
                string dest = OutputFiles.Commit(temp, o.OutputFolder, name);
                progress(100, "已生成 " + Path.GetFileName(dest));
            }
            finally { OutputFiles.DeleteTemporary(temp); }
        }
        static Image LoadImage(string path)
        {
            try { return Image.FromFile(path); }
            catch (OutOfMemoryException e) { throw new IOException("图片无法解码，可能已损坏、格式不受支持或尺寸过大。", e); }
            catch (ArgumentException e) { throw new IOException("图片无法解码，请检查文件格式和内容。", e); }
        }
        internal static void ApplyExif(Bitmap bitmap, Image source)
        {
            if (!source.PropertyIdList.Contains(0x112)) return;
            try
            {
                int n = BitConverter.ToUInt16(source.GetPropertyItem(0x112).Value, 0);
                var modes = new[] { RotateFlipType.RotateNoneFlipNone, RotateFlipType.RotateNoneFlipNone, RotateFlipType.RotateNoneFlipX, RotateFlipType.Rotate180FlipNone, RotateFlipType.Rotate180FlipX, RotateFlipType.Rotate90FlipX, RotateFlipType.Rotate90FlipNone, RotateFlipType.Rotate270FlipX, RotateFlipType.Rotate270FlipNone };
                if (n >= 1 && n <= 8) bitmap.RotateFlip(modes[n]);
            }
            catch (ArgumentException) { }
        }
    }
}
