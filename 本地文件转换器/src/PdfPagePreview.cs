using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LocalConverter
{
    public sealed class PdfPagePreview : PictureBox
    {
        public bool CropEnabled;
        public bool Horizontal=true;
        public bool SecondPart;
        public float Ratio=0.5F;
        public WatermarkSpec Watermark;
        public float PageWidth,PageHeight;
        public int PageIndex;
        public PdfPagePreview(){SizeMode=PictureBoxSizeMode.Zoom;DoubleBuffered=true;ResizeRedraw=true;}
        public static RectangleF ImageBounds(Size view,Size image)
        {
            if(image.Width<=0||image.Height<=0)return RectangleF.Empty;
            float scale=Math.Min((float)view.Width/image.Width,(float)view.Height/image.Height);float w=image.Width*scale,h=image.Height*scale;
            return new RectangleF((view.Width-w)/2,(view.Height-h)/2,w,h);
        }
        public RectangleF ShadedBounds
        {
            get
            {
                if(Image==null||!CropEnabled)return RectangleF.Empty;
                var r=ImageBounds(ClientSize,Image.Size);float ratio=Math.Max(0.001F,Math.Min(0.999F,Ratio));
                if(Horizontal)return SecondPart?new RectangleF(r.X,r.Y,r.Width,r.Height*ratio):new RectangleF(r.X,r.Y+r.Height*ratio,r.Width,r.Height*(1-ratio));
                return SecondPart?new RectangleF(r.X,r.Y,r.Width*ratio,r.Height):new RectangleF(r.X+r.Width*ratio,r.Y,r.Width*(1-ratio),r.Height);
            }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if(Image!=null&&Watermark!=null&&Watermark.Applies(PageIndex+1))Watermark.Paint(e.Graphics,ImageBounds(ClientSize,Image.Size),PageWidth,PageHeight);
            if(!CropEnabled||Image==null)return;
            var r=ImageBounds(ClientSize,Image.Size);using(var brush=new SolidBrush(Color.FromArgb(135,104,112,126)))e.Graphics.FillRectangle(brush,ShadedBounds);
            float split=Horizontal?r.Top+r.Height*Ratio:r.Left+r.Width*Ratio;
            using(var pen=new Pen(Theme.Blue,2*e.Graphics.DpiX/96F)){pen.DashStyle=DashStyle.Dash;if(Horizontal)e.Graphics.DrawLine(pen,r.Left,split,r.Right,split);else e.Graphics.DrawLine(pen,split,r.Top,split,r.Bottom);}
        }
    }
}
