using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;

namespace LocalConverter
{
    public static class ImageBrushes
    {
        public static float Diameter(ImageEdit edit){return Math.Max(1,edit.Diameter>0?edit.Diameter:edit.Second);}
        public static GraphicsPath Path(ImageEdit edit)
        {
            var path=new GraphicsPath(FillMode.Winding);float d=Diameter(edit);var points=edit.Points;
            if(points==null||points.Length==0){path.AddRectangle(edit.Rect);return path;}
            if(points.Length==1||points.All(p=>p==points[0])){path.AddEllipse(points[0].X-d/2,points[0].Y-d/2,d,d);return path;}
            path.AddLines(points);using(var pen=new Pen(Color.Black,d){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round})path.Widen(pen);return path;
        }
        public static void Solid(Graphics g,ImageEdit edit)
        {using(var path=Path(edit))using(var brush=new SolidBrush(Color.FromArgb((int)(255*edit.Opacity/100),edit.Color)))g.FillPath(brush,path);}
        public static void Erase(Bitmap layer,ImageEdit edit)
        {
            using(var g=Graphics.FromImage(layer))using(var path=Path(edit))using(var clear=new SolidBrush(Color.Transparent))
            {g.CompositingMode=CompositingMode.SourceCopy;g.SmoothingMode=SmoothingMode.None;g.FillPath(clear,path);}
        }
        public static void Mosaic(Graphics graphics,Bitmap source,Bitmap existingLayer,ImageEdit edit)
        {
            using(var texture=MosaicTexture(source,existingLayer,(int)edit.Value))PaintMosaic(graphics,texture,source.Size,edit);
        }
        public static Bitmap MosaicTexture(Bitmap source,Bitmap existingLayer,int block)
        {
            block=Math.Max(2,block);var small=new Bitmap(Math.Max(1,(source.Width+block-1)/block),Math.Max(1,(source.Height+block-1)/block));
            using(var g=Graphics.FromImage(small)){g.InterpolationMode=InterpolationMode.HighQualityBilinear;g.DrawImage(source,new Rectangle(Point.Empty,small.Size));if(existingLayer!=null)g.DrawImage(existingLayer,new Rectangle(Point.Empty,small.Size));}return small;
        }
        public static void PaintMosaic(Graphics graphics,Bitmap texture,Size size,ImageEdit edit)
        {
            using(var path=Path(edit)){var state=graphics.Save();try{graphics.SetClip(path,CombineMode.Intersect);graphics.InterpolationMode=InterpolationMode.NearestNeighbor;graphics.PixelOffsetMode=PixelOffsetMode.Half;graphics.DrawImage(texture,new Rectangle(Point.Empty,size));}finally{graphics.Restore(state);}}
        }
    }
    public sealed class ImageRenderResult : IDisposable
    {
        public Bitmap Picture,EraseBase;
        public void Dispose(){if(Picture!=null)Picture.Dispose();if(EraseBase!=null)EraseBase.Dispose();}
    }
    public sealed class ImageTextTarget
    {public ImageEdit Edit;public RectangleF Bounds;}
}
