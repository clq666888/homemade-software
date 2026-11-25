using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace LocalConverter
{
    public sealed class ImageFilterPreset
    {
        public string Key,Name;public float Saturation=1,Contrast=1,Brightness,Red=1,Green=1,Blue=1;public bool Sepia,Invert;
        public ImageFilterPreset(string key,string name){Key=key;Name=name;}
    }
    public static class ImageFilters
    {
        public static readonly ImageFilterPreset[] Presets={
            new ImageFilterPreset("original","原图"),
            new ImageFilterPreset("gray","经典黑白"){Saturation=0},
            new ImageFilterPreset("noir","黑白硬调"){Saturation=0,Contrast=1.65F,Brightness=-.02F},
            new ImageFilterPreset("sepia","复古棕色"){Sepia=true},
            new ImageFilterPreset("warm","温暖"){Red=1.08F,Green=1.01F,Blue=.88F,Brightness=.015F},
            new ImageFilterPreset("cool","清冷"){Red=.91F,Green=1.01F,Blue=1.09F},
            new ImageFilterPreset("vivid","鲜艳"){Saturation=1.45F,Contrast=1.08F},
            new ImageFilterPreset("soft","柔和"){Saturation=.8F,Contrast=.86F,Brightness=.035F},
            new ImageFilterPreset("fade","褪色"){Saturation=.62F,Contrast=.76F,Brightness=.04F},
            new ImageFilterPreset("film","胶片色调"){Saturation=.82F,Contrast=1.12F,Red=1.04F,Green=1.02F,Blue=.92F},
            new ImageFilterPreset("sunset","日落暖橙"){Red=1.16F,Green=.99F,Blue=.78F,Saturation=1.12F},
            new ImageFilterPreset("blue","青蓝色调"){Red=.83F,Green=1.05F,Blue=1.12F,Saturation=.85F},
            new ImageFilterPreset("contrast","高对比"){Contrast=1.35F},
            new ImageFilterPreset("bright","明亮"){Brightness=.12F,Contrast=.96F},
            new ImageFilterPreset("dark","低调"){Brightness=-.1F,Contrast=1.12F},
            new ImageFilterPreset("invert","反色"){Invert=true}
        };
        public static Bitmap Apply(Bitmap source,int index,float strength)
        {
            if(index<0||index>=Presets.Length)throw new ArgumentOutOfRangeException("index");var p=Presets[index];float mix=Math.Max(0,Math.Min(100,strength))/100;
            var m=new float[5][];for(int i=0;i<5;i++){m[i]=new float[5];m[i][i]=1;}
            if(p.Invert){for(int i=0;i<3;i++){m[i][i]=-1;m[4][i]=1;}}
            else if(p.Sepia){m[0]=new[]{.393F,.349F,.272F,0,0};m[1]=new[]{.769F,.686F,.534F,0,0};m[2]=new[]{.189F,.168F,.131F,0,0};}
            else
            {
                float[] lum={.299F,.587F,.114F},tint={p.Red,p.Green,p.Blue};
                for(int input=0;input<3;input++)for(int output=0;output<3;output++)m[input][output]=(lum[input]*(1-p.Saturation)+(input==output?p.Saturation:0))*p.Contrast*tint[output];
                for(int i=0;i<3;i++)m[4][i]=(1-p.Contrast)/2+p.Brightness;
            }
            for(int r=0;r<5;r++)for(int c=0;c<5;c++)m[r][c]=m[r][c]*mix+(r==c?1-mix:0);
            var result=new Bitmap(source.Width,source.Height,PixelFormat.Format32bppArgb);
            using(var g=Graphics.FromImage(result))using(var attributes=new ImageAttributes())
            {g.CompositingMode=CompositingMode.SourceCopy;attributes.SetColorMatrix(new ColorMatrix(m));g.DrawImage(source,new Rectangle(Point.Empty,source.Size),0,0,source.Width,source.Height,GraphicsUnit.Pixel,attributes);}
            return result;
        }
    }
    public sealed class ImageFilterPicker : FlowLayoutPanel
    {
        sealed class Tile : Button
        {
            public Bitmap Preview;public bool Selected;
            public Tile(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;UseVisualStyleBackColor=false;Cursor=Cursors.Hand;Margin=new Padding(2);}
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(Color.White);float scale=e.Graphics.DpiX/96F;var r=new RectangleF(1,1,Width-3,Height-3);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
                Theme.Fill(e.Graphics,Selected?Color.FromArgb(232,240,255):Theme.Background,r,6);
                if(Preview!=null){e.Graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;e.Graphics.DrawImage(Preview,new Rectangle(5,5,Width-10,Height-(int)(29*scale)));}
                Theme.Text(e.Graphics,Text,Font,Selected?Theme.Blue:Theme.Ink,new RectangleF(2,Height-25*scale,Width-4,24*scale),StringAlignment.Center);
                if(Selected)Theme.Border(e.Graphics,Theme.Blue,r,6,1.5F);
            }
            protected override void Dispose(bool disposing){if(disposing&&Preview!=null)Preview.Dispose();base.Dispose(disposing);}
        }
        readonly Tile[] tiles=new Tile[ImageFilters.Presets.Length];int selected;string sourcePath;
        public event Action SelectionChanged;
        public int SelectedIndex{get{return selected;}set{if(value<0||value>=tiles.Length||selected==value)return;selected=value;for(int i=0;i<tiles.Length;i++){tiles[i].Selected=i==selected;tiles[i].Invalidate();}if(SelectionChanged!=null)SelectionChanged();}}
        public ImageFilterPicker()
        {
            AutoSize=true;WrapContents=true;FlowDirection=FlowDirection.LeftToRight;Margin=Padding.Empty;Padding=Padding.Empty;Width=245;
            for(int i=0;i<tiles.Length;i++){int at=i;tiles[i]=new Tile{Text=ImageFilters.Presets[i].Name,Selected=i==0,Width=116,Height=84};tiles[i].Click+=(s,e)=>SelectedIndex=at;Controls.Add(tiles[i]);}SetSource(null);
        }
        public void FitWidth(int width,float scale){MaximumSize=new Size(width,0);MinimumSize=new Size(width,0);Width=width;foreach(var tile in tiles){tile.Width=Math.Max(65,(width-8)/2);tile.Height=(int)(84*scale);}}
        public void SetSource(string path)
        {
            if(path==sourcePath&&tiles[0].Preview!=null)return;sourcePath=path;
            using(var sample=new Bitmap(160,105))
            {
                using(var g=Graphics.FromImage(sample))
                {
                    if(path!=null&&File.Exists(path))using(var image=System.Drawing.Image.FromFile(path)){g.Clear(Color.White);g.InterpolationMode=InterpolationMode.HighQualityBicubic;float k=Math.Min(160F/image.Width,105F/image.Height);float w=image.Width*k,h=image.Height*k;g.DrawImage(image,(160-w)/2,(105-h)/2,w,h);}
                    else{using(var gradient=new LinearGradientBrush(new Rectangle(0,0,160,105),Color.FromArgb(253,191,115),Color.FromArgb(72,160,194),30))g.FillRectangle(gradient,0,0,160,105);g.FillEllipse(Brushes.Goldenrod,96,10,38,38);g.FillPolygon(Brushes.Teal,new[]{new Point(0,105),new Point(60,30),new Point(120,105)});}
                }
                for(int i=0;i<tiles.Length;i++){if(tiles[i].Preview!=null)tiles[i].Preview.Dispose();tiles[i].Preview=ImageFilters.Apply(sample,i,100);tiles[i].Invalidate();}
            }
        }
    }
}
