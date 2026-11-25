using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace LocalConverter
{
    public sealed class ImageAsset
    {
        public string Source,Prepared,Format;public int Width,Height,Frames;public long Length,Ticks;public bool Dirty;
        public string Identity{get{return Source+"|"+Length+"|"+Ticks;}}
        public void Validate(){var f=new FileInfo(Source);if(!f.Exists||f.Length!=Length||f.LastWriteTimeUtc.Ticks!=Ticks)throw new IOException("原图片已变更，请移除后重新添加："+Path.GetFileName(Source));}
        public readonly List<ImageEdit> Edits=new List<ImageEdit>(),Redo=new List<ImageEdit>();
    }
    public sealed class ImageEdit
    {
        public string Id=Guid.NewGuid().ToString("N"),TargetId,Kind,Text,Family="Microsoft YaHei",Path;public FontStyle FontStyle;public Rectangle Rect;public float Value=32,Second=1,Opacity=100,Diameter;public Color Color=Color.FromArgb(53,105,239);public Point[] Points;
        public ImageEdit Copy(){return (ImageEdit)MemberwiseClone();}
    }
    public static class ImageEditing
    {
        public static Bitmap Load(string path){using(var image=Image.FromFile(path)){var b=new Bitmap(image.Width,image.Height,PixelFormat.Format32bppArgb);using(var g=Graphics.FromImage(b)){g.CompositingMode=CompositingMode.SourceCopy;g.DrawImageUnscaled(image,0,0);}return b;}}
        public static Bitmap Render(string path,IEnumerable<ImageEdit> edits)
        {
            return RenderLayers(path,edits,false).Picture;
        }
        public static ImageEdit[] Effective(IEnumerable<ImageEdit> edits)
        {
            var list=new List<ImageEdit>();foreach(var edit in edits)
            {
                if(edit.Kind=="retext"||edit.Kind=="removeText")
                {int at=list.FindIndex(e=>e.Id==edit.TargetId&&e.Kind=="text");if(at<0)continue;if(edit.Kind=="removeText")list.RemoveAt(at);else{var replacement=edit.Copy();replacement.Kind="text";replacement.Id=edit.TargetId;list[at]=replacement;}}
                else list.Add(edit);
            }
            return list.ToArray();
        }
        static bool Annotation(string kind){return new[]{"pen","text","icon","overlay","rectangle","ellipse","arrow"}.Contains(kind);}
        public static ImageRenderResult RenderLayers(string path,IEnumerable<ImageEdit> edits,bool keepBase)
        {
            Bitmap basis=Load(path),layer=null;
            try
            {
                foreach(var edit in Effective(edits))
                {
                    if(Annotation(edit.Kind)||edit.Kind=="mosaic")
                    {
                        if(layer==null)layer=new Bitmap(basis.Width,basis.Height,PixelFormat.Format32bppArgb);
                        if(edit.Kind=="mosaic")using(var g=Graphics.FromImage(layer))ImageBrushes.Mosaic(g,basis,layer,edit);
                        else Apply(layer,edit);
                    }
                    else if(edit.Kind=="erase"){if(layer!=null)ImageBrushes.Erase(layer,edit);}
                    else
                    {
                        var next=Apply(basis,edit);if(!Object.ReferenceEquals(next,basis)){basis.Dispose();basis=next;}
                        if(layer!=null&&new[]{"crop","rotate","flipH","flipV","resize"}.Contains(edit.Kind)){next=Apply(layer,edit);if(!Object.ReferenceEquals(next,layer)){layer.Dispose();layer=next;}}
                    }
                }
                var picture=keepBase?(Bitmap)basis.Clone():basis;
                if(layer!=null)using(var g=Graphics.FromImage(picture))g.DrawImageUnscaled(layer,0,0);
                return new ImageRenderResult{Picture=picture,EraseBase=keepBase?basis:null};
            }
            catch{basis.Dispose();throw;}
            finally{if(layer!=null)layer.Dispose();}
        }
        public static ImageTextTarget[] TextTargets(IEnumerable<ImageEdit> edits,Size original)
        {
            var targets=new List<ImageTextTarget>();int width=original.Width,height=original.Height;
            foreach(var edit in Effective(edits))
            {
                if(edit.Kind=="text")targets.Add(new ImageTextTarget{Edit=edit,Bounds=edit.Rect});
                foreach(var item in targets)
                {
                    var r=item.Bounds;
                    if(edit.Kind=="crop")r=RectangleF.Intersect(new RectangleF(r.X-edit.Rect.X,r.Y-edit.Rect.Y,r.Width,r.Height),new RectangleF(0,0,edit.Rect.Width,edit.Rect.Height));
                    if(edit.Kind=="rotate")r=new RectangleF(height-r.Bottom,r.X,r.Height,r.Width);
                    if(edit.Kind=="flipH")r=new RectangleF(width-r.Right,r.Y,r.Width,r.Height);
                    if(edit.Kind=="flipV")r=new RectangleF(r.X,height-r.Bottom,r.Width,r.Height);
                    if(edit.Kind=="resize")r=new RectangleF(r.X*edit.Rect.Width/width,r.Y*edit.Rect.Height/height,r.Width*edit.Rect.Width/width,r.Height*edit.Rect.Height/height);
                    item.Bounds=r;
                }
                if(edit.Kind=="crop"||edit.Kind=="resize"){width=edit.Rect.Width;height=edit.Rect.Height;}
                if(edit.Kind=="rotate"){int swap=width;width=height;height=swap;}
            }
            return targets.Where(t=>t.Bounds.Width>0&&t.Bounds.Height>0).ToArray();
        }
        public static Bitmap Apply(Bitmap source,ImageEdit edit)
        {
            if(edit.Kind=="rotate"){source.RotateFlip(RotateFlipType.Rotate90FlipNone);return source;}
            if(edit.Kind=="flipH"||edit.Kind=="flipV"){source.RotateFlip(edit.Kind=="flipH"?RotateFlipType.RotateNoneFlipX:RotateFlipType.RotateNoneFlipY);return source;}
            Rectangle area=Rectangle.Intersect(new Rectangle(Point.Empty,source.Size),edit.Rect);
            if(edit.Kind=="crop"){if(area.Width<1||area.Height<1)throw new IOException("请在图片内拖出裁剪区域。");return source.Clone(area,PixelFormat.Format32bppArgb);}
            if(edit.Kind=="resize")
            {
                int w=edit.Rect.Width,h=edit.Rect.Height;if(w<1||h<1||(long)w*h>40000000)throw new IOException("新尺寸需为正数且不超过 4000 万像素。");
                var b=new Bitmap(w,h,PixelFormat.Format32bppArgb);using(var g=Graphics.FromImage(b)){g.CompositingMode=CompositingMode.SourceCopy;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(source,new Rectangle(0,0,w,h));}return b;
            }
            if(new[]{"gray","threshold","sepia","invert","adjust"}.Contains(edit.Kind))
            {
                if(edit.Kind=="threshold"||edit.Kind=="invert")
                {
                    var data=source.LockBits(new Rectangle(Point.Empty,source.Size),ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);
                    try{var row=new byte[source.Width*4];for(int y=0;y<source.Height;y++){IntPtr address=IntPtr.Add(data.Scan0,y*data.Stride);Marshal.Copy(address,row,0,row.Length);for(int x=0;x<row.Length;x+=4){if(edit.Kind=="invert"){row[x]=(byte)(255-row[x]);row[x+1]=(byte)(255-row[x+1]);row[x+2]=(byte)(255-row[x+2]);}else{byte v=(byte)(.114*row[x]+.587*row[x+1]+.299*row[x+2]>=edit.Value?255:0);row[x]=row[x+1]=row[x+2]=v;}}Marshal.Copy(row,0,address,row.Length);}}finally{source.UnlockBits(data);}return source;
                }
                float[][] matrix;
                if(edit.Kind=="gray")matrix=new[]{new[]{.299F,.299F,.299F,0,0},new[]{.587F,.587F,.587F,0,0},new[]{.114F,.114F,.114F,0,0},new[]{0F,0,0,1,0},new[]{0F,0,0,0,1}};
                else if(edit.Kind=="sepia")matrix=new[]{new[]{.393F,.349F,.272F,0,0},new[]{.769F,.686F,.534F,0,0},new[]{.189F,.168F,.131F,0,0},new[]{0F,0,0,1,0},new[]{0F,0,0,0,1}};
                else{float c=edit.Second,b=edit.Value/100F+(1-c)/2;matrix=new[]{new[]{c,0F,0,0,0},new[]{0F,c,0,0,0},new[]{0F,0,c,0,0},new[]{0F,0,0,1,0},new[]{b,b,b,0,1}};}
                var output=new Bitmap(source.Width,source.Height,PixelFormat.Format32bppArgb);using(var g=Graphics.FromImage(output))using(var attr=new ImageAttributes()){attr.SetColorMatrix(new ColorMatrix(matrix));g.DrawImage(source,new Rectangle(Point.Empty,source.Size),0,0,source.Width,source.Height,GraphicsUnit.Pixel,attr);}return output;
            }
            if(edit.Kind=="mosaic")
            {
                if(area.Width<1||area.Height<1)return source;int block=Math.Max(2,(int)edit.Value);
                using(var small=new Bitmap(Math.Max(1,area.Width/block),Math.Max(1,area.Height/block)))
                {using(var g=Graphics.FromImage(small)){g.InterpolationMode=InterpolationMode.HighQualityBilinear;g.DrawImage(source,new Rectangle(Point.Empty,small.Size),area,GraphicsUnit.Pixel);}using(var g=Graphics.FromImage(source)){g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;g.DrawImage(small,area);}}return source;
            }
            using(var g=Graphics.FromImage(source))PaintAnnotation(g,edit);
            return source;
        }
        public static void PaintAnnotation(Graphics g,ImageEdit edit)
        {
            using(var brush=new SolidBrush(Color.FromArgb((int)(255*edit.Opacity/100),edit.Color)))
            using(var pen=new Pen(brush,Math.Max(1,edit.Second)))
            {
                g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;pen.StartCap=pen.EndCap=LineCap.Round;
                if(edit.Kind=="text")using(var font=new Font(edit.Family,Math.Max(6,edit.Value),edit.FontStyle,GraphicsUnit.Pixel)){g.DrawString(edit.Text,font,brush,new RectangleF(edit.Rect.X,edit.Rect.Y,Math.Max(10,edit.Rect.Width),Math.Max(10,edit.Rect.Height)));}
                else if(edit.Kind=="overlay")using(var im=Load(edit.Path))PaintOverlay(g,im,edit);
                else if(edit.Kind=="rectangle")g.DrawRectangle(pen,edit.Rect);
                else if(edit.Kind=="ellipse")g.DrawEllipse(pen,edit.Rect);
                else if(edit.Kind=="arrow")using(var cap=new AdjustableArrowCap(4,5)){pen.CustomEndCap=cap;g.DrawLine(pen,edit.Points!=null&&edit.Points.Length>1?edit.Points[0]:edit.Rect.Location,edit.Points!=null&&edit.Points.Length>1?edit.Points[edit.Points.Length-1]:new Point(edit.Rect.Right,edit.Rect.Bottom));}
                else if(edit.Kind=="pen")ImageBrushes.Solid(g,edit);
                else if(edit.Kind=="icon")
                {
                    if(edit.Text=="圆")g.FillEllipse(brush,edit.Rect);
                    else if(edit.Text=="方")g.FillRectangle(brush,edit.Rect);
                    else if(edit.Text=="三角")g.FillPolygon(brush,new[]{new PointF(edit.Rect.Left+edit.Rect.Width/2F,edit.Rect.Top),new PointF(edit.Rect.Right,edit.Rect.Bottom),new PointF(edit.Rect.Left,edit.Rect.Bottom)});
                    else if(edit.Text=="星") {var points=Enumerable.Range(0,10).Select(i=>{double a=-Math.PI/2+i*Math.PI/5,r=i%2==0?.5:.21;return new PointF(edit.Rect.Left+edit.Rect.Width*(.5F+(float)(r*Math.Cos(a))),edit.Rect.Top+edit.Rect.Height*(.5F+(float)(r*Math.Sin(a))));}).ToArray();g.FillPolygon(brush,points);}
                    else{pen.Width=Math.Max(2,edit.Rect.Width/10F);if(edit.Text=="勾")g.DrawLines(pen,new[]{new Point(edit.Rect.Left,edit.Rect.Top+edit.Rect.Height/2),new Point(edit.Rect.Left+edit.Rect.Width/3,edit.Rect.Bottom),new Point(edit.Rect.Right,edit.Rect.Top)});else{g.DrawLine(pen,edit.Rect.Left,edit.Rect.Top,edit.Rect.Right,edit.Rect.Bottom);g.DrawLine(pen,edit.Rect.Right,edit.Rect.Top,edit.Rect.Left,edit.Rect.Bottom);}}
                }
            }
        }
        public static void PaintOverlay(Graphics g,Bitmap image,ImageEdit edit)
        {using(var attributes=new ImageAttributes()){var cm=new ColorMatrix();cm.Matrix33=edit.Opacity/100;attributes.SetColorMatrix(cm);g.DrawImage(image,edit.Rect,0,0,image.Width,image.Height,GraphicsUnit.Pixel,attributes);}}
    }
    public sealed class ImageStamp : IDisposable
    {
        Bitmap cachedLogo;
        public WatermarkSpec Text;public string Logo;public int Position=8;public float WidthPercent=20,Opacity=35,Angle=0;public bool Tiled;
        public void Paint(Graphics g,RectangleF target,int width,int height)
        {
            if(String.IsNullOrEmpty(Logo)){if(Text!=null)Text.Paint(g,target,width,height);return;}
            if(cachedLogo==null)cachedLogo=ImageEditing.Load(Logo);
            var image=cachedLogo;using(var attributes=new ImageAttributes())
            {
                var matrix=new ColorMatrix();matrix.Matrix33=Opacity/100;attributes.SetColorMatrix(matrix);float w=width*WidthPercent/100,h=w*image.Height/image.Width;
                float bound=w*(float)Math.Abs(Math.Cos(Angle*Math.PI/180))+h*(float)Math.Abs(Math.Sin(Angle*Math.PI/180));float high=h*(float)Math.Abs(Math.Cos(Angle*Math.PI/180))+w*(float)Math.Abs(Math.Sin(Angle*Math.PI/180));float fit=Math.Min(1,Math.Min(width*.9F/Math.Max(1,bound),height*.9F/Math.Max(1,high)));w*=fit;h*=fit;bound*=fit;high*=fit;
                var saved=g.Save();try{g.SetClip(target,CombineMode.Intersect);g.TranslateTransform(target.X,target.Y);g.ScaleTransform(target.Width/width,target.Height/height);
                Action<float,float> draw=(x,y)=>{var state=g.Save();g.TranslateTransform(x,y);g.RotateTransform(Angle);g.DrawImage(image,new[]{new PointF(-w/2,-h/2),new PointF(w/2,-h/2),new PointF(-w/2,h/2)},new RectangleF(0,0,image.Width,image.Height),GraphicsUnit.Pixel,attributes);g.Restore(state);};
                if(Tiled){float dx=Math.Max(bound+width*.08F,width/8F),dy=Math.Max(high+height*.08F,height/8F);for(float y=dy/2;y<height;y+=dy)for(float x=dx/2;x<width;x+=dx)draw(x,y);}
                else {float margin=Math.Min(width,height)*.035F;draw(Position%3==0?margin+bound/2:Position%3==2?width-margin-bound/2:width/2F,Position/3==0?margin+high/2:Position/3==2?height-margin-high/2:height/2F);}
                }finally{g.Restore(saved);}
            }
        }
        public void Dispose(){if(cachedLogo!=null){cachedLogo.Dispose();cachedLogo=null;}}
    }
}
