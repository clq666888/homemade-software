using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Threading;

namespace LocalConverter
{
    // One layout implementation paints both the preview and the transparent export layer.
    public sealed class WatermarkSpec
    {
        public string Text,Family;
        public FontStyle FontStyle;
        public int Style,Position,Scope;
        public float Size,Angle,Opacity,Spacing,Margin;
        public bool Outline;
        public Color Color;
        public int First=1,Last=999999;
        public bool Applies(int page){return Scope==0||Scope==1&&page%2==1||Scope==2&&page%2==0||Scope==3&&page>=First&&page<=Last;}
        public string Context{get{return String.Join("|",new object[]{Text,Family,(int)FontStyle,Style,Position,Scope,Size,Angle,Opacity,Spacing,Margin,Outline,Color.ToArgb(),First,Last});}}
        public void Validate(){if(String.IsNullOrWhiteSpace(Text))throw new IOException("请输入水印文字。");if(Scope==3&&(First<1||Last<First))throw new IOException("请填写有效的起始页和结束页。");}
        public void Paint(Graphics graphics,RectangleF target,float width,float height)
        {
            if(String.IsNullOrWhiteSpace(Text)||width<=0||height<=0)return;
            var state=graphics.Save();
            try
            {
                graphics.SetClip(target,CombineMode.Intersect);graphics.TranslateTransform(target.X,target.Y);graphics.ScaleTransform(target.Width/width,target.Height/height);
                graphics.SmoothingMode=SmoothingMode.AntiAlias;
                using(var family=new FontFamily(Family))using(var path=new GraphicsPath())
                using(var format=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center})
                {
                    path.AddString(Text,family,(int)FontStyle,Size,PointF.Empty,format);
                    var bounds=path.GetBounds();if(bounds.Width<=0||bounds.Height<=0)return;
                    using(var center=new Matrix()){center.Translate(-bounds.X-bounds.Width/2,-bounds.Y-bounds.Height/2);path.Transform(center);}
                    float radians=Angle*(float)Math.PI/180,cos=Math.Abs((float)Math.Cos(radians)),sin=Math.Abs((float)Math.Sin(radians));
                    float rotatedWidth=bounds.Width*cos+bounds.Height*sin,rotatedHeight=bounds.Width*sin+bounds.Height*cos;
                    float margin=Math.Min(Margin,Math.Min(width,height)*0.2F);
                    float fit=Math.Min(1,Math.Min(Math.Max(1,width-2*margin)/rotatedWidth,Math.Max(1,height-2*margin)/rotatedHeight));
                    bool tiled=Style==2||Style==3;
                    if(tiled)fit=Math.Min(fit,width*0.55F/Math.Max(1,rotatedWidth));
                    using(var transform=new Matrix()){transform.Scale(fit,fit);transform.Rotate(Angle);path.Transform(transform);}
                    bounds=path.GetBounds();
                    using(var brush=new SolidBrush(System.Drawing.Color.FromArgb((int)(255*Opacity/100),Color)))
                    using(var pen=new Pen(brush,Math.Max(0.55F,Size*fit/28)))
                    {
                        Action<float,float> stamp=(x,y)=>{var saved=graphics.Save();graphics.TranslateTransform(x,y);if(Outline)graphics.DrawPath(pen,path);else graphics.FillPath(brush,path);graphics.Restore(saved);};
                        if(tiled)
                        {
                            float dx=Math.Max(bounds.Width+Size,90)*Spacing/100,dy=Math.Max(bounds.Height+Size,55)*Spacing/100;
                            // Bound work even for unusually large engineering sheets and tiny text.
                            float factor=(float)Math.Sqrt(Math.Max(1,(width/dx+2)*(height/dy+2)/1500));dx*=factor;dy*=factor;
                            int row=0;for(float y=dy/2;y<height+dy/2;y+=dy,row++)for(float x=dx/2-(row%2==1?dx/2:0);x<width+dx/2;x+=dx)stamp(x,y);
                        }
                        else
                        {
                            int position=Style==4?1:Style==5?7:Style==6?Position:4;
                            int col=position%3,row=position/3;float x=col==0?margin+bounds.Width/2:col==2?width-margin-bounds.Width/2:width/2;
                            float y=row==0?margin+bounds.Height/2:row==2?height-margin-bounds.Height/2:height/2;
                            stamp(x,y);
                        }
                    }
                }
            }
            finally{graphics.Restore(state);}
        }
        public Bitmap Render(float width,float height)
        {
            float scale=Math.Min(2,3200/Math.Max(width,height));var bitmap=new Bitmap(Math.Max(1,(int)Math.Ceiling(width*scale)),Math.Max(1,(int)Math.Ceiling(height*scale)),PixelFormat.Format32bppArgb);
            try{using(var g=Graphics.FromImage(bitmap)){g.Clear(System.Drawing.Color.Transparent);Paint(g,new RectangleF(0,0,bitmap.Width,bitmap.Height),width,height);}return bitmap;}
            catch{bitmap.Dispose();throw;}
        }
    }
    public static class WatermarkJob
    {
        public static string Run(PdfEngine engine,string[] files,string folder,string password,WatermarkSpec spec,CancellationToken token,Action<int,string> report,IDictionary<string,string> passwords=null)
        {
            spec.Validate();string work=Path.Combine(engine.Workspace,"watermark-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(work);
            try
            {
                report(0,"正在读取水印页面尺寸…");
                var values=new Dictionary<string,object>{{"files",files},{"folder",folder},{"password",password},{"scope",spec.Scope},{"first",spec.First},{"last",spec.Last}};
                if(passwords!=null)values["passwords"]=passwords;
                var info=engine.Run("watermark_info",values,token);var layers=new List<Dictionary<string,object>>();int count=0;
                foreach(Dictionary<string,object> size in (IEnumerable)info["sizes"])
                {
                    token.ThrowIfCancellationRequested();float width=Convert.ToSingle(size["width"],CultureInfo.InvariantCulture),height=Convert.ToSingle(size["height"],CultureInfo.InvariantCulture);
                    string path=Path.Combine(work,(count++)+".png");using(var bitmap=spec.Render(width,height))bitmap.Save(path,ImageFormat.Png);
                    layers.Add(new Dictionary<string,object>{{"key",size["key"]},{"path",path}});report(Math.Min(20,count),"正在准备水印图层…");
                }
                values["layers"]=layers;return (string)engine.Run("watermark",values,token,(p,s)=>report(20+p*80/100,s))["note"];
            }
            finally{try{Directory.Delete(work,true);}catch(IOException){}catch(UnauthorizedAccessException){}}
        }
    }
}
