using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace LocalConverter
{
    // 仅实现图片页所需的 PDF 1.4 子集；每页使用标准 JPEG XObject，无第三方运行库。
    public sealed class ImagePdfWriter : IDisposable
    {
        readonly FileStream stream;
        readonly Dictionary<int, long> offsets = new Dictionary<int, long>();
        readonly List<int> pages = new List<int>();
        int next = 3;
        public ImagePdfWriter(string path)
        {
            stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            Text("%PDF-1.4\n%LocalConverter\n");
        }
        void Text(string text) { byte[] b = Encoding.ASCII.GetBytes(text); stream.Write(b, 0, b.Length); }
        static string N(double n) { return n.ToString("0.####", CultureInfo.InvariantCulture); }
        void Begin(int id) { offsets[id] = stream.Position; Text(id + " 0 obj\n"); }
        public void AddPage(byte[] jpeg, int width, int height, double pageWidth, double pageHeight)
        {
            int page = next++, image = next++, content = next++;
            pages.Add(page);
            double scale = Math.Min((pageWidth - 56.7) / width, (pageHeight - 56.7) / height);
            double w = width * scale, h = height * scale;
            Begin(page);
            Text("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 " + N(pageWidth) + " " + N(pageHeight) + "] /Resources << /XObject << /Im0 " + image + " 0 R >> >> /Contents " + content + " 0 R >>\nendobj\n");
            Begin(image);
            Text("<< /Type /XObject /Subtype /Image /Width " + width + " /Height " + height + " /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length " + jpeg.Length + " >>\nstream\n");
            stream.Write(jpeg, 0, jpeg.Length); Text("\nendstream\nendobj\n");
            string drawing = "q\n" + N(w) + " 0 0 " + N(h) + " " + N((pageWidth - w) / 2) + " " + N((pageHeight - h) / 2) + " cm\n/Im0 Do\nQ\n";
            Begin(content); Text("<< /Length " + Encoding.ASCII.GetByteCount(drawing) + " >>\nstream\n" + drawing + "endstream\nendobj\n");
        }
        public void Complete()
        {
            Begin(1); Text("<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
            Begin(2); Text("<< /Type /Pages /Count " + pages.Count + " /Kids [");
            foreach (int id in pages) Text(id + " 0 R ");
            Text("] >>\nendobj\n");
            long xref = stream.Position;
            Text("xref\n0 " + next + "\n0000000000 65535 f \n");
            for (int id = 1; id < next; id++) Text(offsets[id].ToString("0000000000", CultureInfo.InvariantCulture) + " 00000 n \n");
            Text("trailer\n<< /Size " + next + " /Root 1 0 R >>\nstartxref\n" + xref + "\n%%EOF\n");
            stream.Flush(true);
        }
        public void Dispose() { stream.Dispose(); }
    }
}
