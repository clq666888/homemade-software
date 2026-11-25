using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace LocalConverter
{
    // A horizontal filmstrip leaves the middle of the workspace available for the image.
    public sealed class ImageFileStrip : ScrollableControl
    {
        public readonly List<ImageAsset> Items=new List<ImageAsset>();readonly Dictionary<ImageAsset,Bitmap> thumbs=new Dictionary<ImageAsset,Bitmap>();readonly ToolTip tips=new ToolTip();
        public int SelectedIndex=-1;public event Action SelectionChanged;public event Action<int> RemoveRequested;public event Action<string[]> FilesDropped;
        public ImageFileStrip(){DoubleBuffered=true;AutoScroll=true;AllowDrop=true;BackColor=Color.White;DragEnter+=(s,e)=>e.Effect=e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;DragDrop+=(s,e)=>{if(FilesDropped!=null)FilesDropped((string[])e.Data.GetData(DataFormats.FileDrop));};}
        public void RefreshItems(){AutoScrollMinSize=new Size(Items.Count*166+12,0);foreach(var key in new List<ImageAsset>(thumbs.Keys))if(!Items.Contains(key)){thumbs[key].Dispose();thumbs.Remove(key);}Invalidate();}
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.TranslateTransform(AutoScrollPosition.X,0);
            for(int i=0;i<Items.Count;i++)
            {
                var asset=Items[i];var r=new Rectangle(6+i*166,5,158,73);if(r.Right< -AutoScrollPosition.X||r.Left>Width-AutoScrollPosition.X)continue;
                Theme.Fill(g,i==SelectedIndex?Color.FromArgb(235,242,255):Theme.Background,r,7);if(i==SelectedIndex)Theme.Border(g,Theme.Blue,r,7);
                Bitmap thumb;if(!thumbs.TryGetValue(asset,out thumb)){using(var image=Image.FromFile(asset.Prepared)){float k=Math.Min(46F/image.Width,44F/image.Height);thumb=new Bitmap(Math.Max(1,(int)(image.Width*k)),Math.Max(1,(int)(image.Height*k)));using(var tg=Graphics.FromImage(thumb)){tg.InterpolationMode=InterpolationMode.HighQualityBicubic;tg.DrawImage(image,new Rectangle(Point.Empty,thumb.Size));}}thumbs[asset]=thumb;}
                g.DrawImageUnscaled(thumb,r.X+8+(46-thumb.Width)/2,r.Y+10+(44-thumb.Height)/2);
                using(var small=new Font(Font.FontFamily,8)){Theme.Text(g,Path.GetFileName(asset.Source),small,Theme.Ink,new RectangleF(r.X+60,r.Y+8,73,26));Theme.Text(g,asset.Width+" × "+asset.Height,small,Theme.Muted,new RectangleF(r.X+60,r.Y+36,90,24));}Theme.Trash(g,new RectangleF(r.Right-27,r.Y+9,22,23));
            }
            if(Items.Count==0)Theme.Text(g,"添加多张图片后，可在这里切换预览",Font,Theme.Muted,new RectangleF(0,0,Width,Height),StringAlignment.Center);
        }
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);int i=(e.X-AutoScrollPosition.X-6)/166;if(i<0||i>=Items.Count)return;if(e.X-AutoScrollPosition.X-(6+i*166)>131&&e.Y<38){if(RemoveRequested!=null)RemoveRequested(i);return;}SelectedIndex=i;Invalidate();if(SelectionChanged!=null)SelectionChanged();}
        protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);int i=(e.X-AutoScrollPosition.X-6)/166;tips.SetToolTip(this,i>=0&&i<Items.Count?Items[i].Source:"");}
        protected override void Dispose(bool disposing){if(disposing){tips.Dispose();foreach(var image in thumbs.Values)image.Dispose();thumbs.Clear();}base.Dispose(disposing);}
    }
}
