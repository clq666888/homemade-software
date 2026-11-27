using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace LocalConverter
{
    public static class Theme
    {
        public static readonly Color Blue=Color.FromArgb(53,105,239), Ink=Color.FromArgb(30,43,65), Muted=Color.FromArgb(120,136,157), Red=Color.FromArgb(230,81,93), Background=Color.FromArgb(245,248,253);
        public static GraphicsPath Round(RectangleF r,float radius)
        {var p=new GraphicsPath();float d=radius*2;p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
        public static void Fill(Graphics g,Color color,RectangleF r,float radius){using(var p=Round(r,radius))using(var b=new SolidBrush(color))g.FillPath(b,p);}
        public static void Border(Graphics g,Color color,RectangleF r,float radius,float width=1){using(var p=Round(r,radius))using(var pen=new Pen(color,width))g.DrawPath(pen,p);}
        public static void Trash(Graphics g,RectangleF r)
        {using(var p=new Pen(Red,1.8F)){g.DrawLine(p,r.X+3,r.Y+5,r.Right-3,r.Y+5);g.DrawLine(p,r.X+7,r.Y+2,r.Right-7,r.Y+2);g.DrawRectangle(p,r.X+5,r.Y+6,r.Width-10,r.Height-8);g.DrawLine(p,r.X+9,r.Y+9,r.X+9,r.Bottom-5);g.DrawLine(p,r.Right-9,r.Y+9,r.Right-9,r.Bottom-5);}}
        public static void Text(Graphics g,string value,Font font,Color color,RectangleF r,StringAlignment alignment=StringAlignment.Near)
        {
            using(var logical=new Font(font.FontFamily,font.SizeInPoints*96/72,font.Style,GraphicsUnit.Pixel))
            using(var b=new SolidBrush(color))using(var f=new StringFormat{LineAlignment=StringAlignment.Center,Alignment=alignment,Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap})
                g.DrawString(value,Math.Abs(g.Transform.Elements[0]-1)>0.05?logical:font,b,r,f);
        }
    }
    public sealed class WrappingOptionsPanel : FlowLayoutPanel
    {
        // FlowLayout must wrap to the viewport; a stale virtual width otherwise keeps
        // a horizontal scrollbar alive after the user narrows the window.
        public override Rectangle DisplayRectangle{get{var r=base.DisplayRectangle;r.X=Padding.Left;r.Width=Math.Max(0,ClientSize.Width-Padding.Horizontal);return r;}}
    }
    public sealed class FunctionTab : Button
    {
        bool active,hover;
        public bool Active{get{return active;}set{active=value;Invalidate();}}
        public FunctionTab(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;UseVisualStyleBackColor=false;BackColor=Color.White;Cursor=Cursors.Hand;AccessibleRole=AccessibleRole.PageTab;SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);}
        protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
        protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
        protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
        protected override void OnPaint(PaintEventArgs e)
        {
            var g=e.Graphics;g.Clear(Parent==null?Color.White:Parent.BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;
            float scale=g.DpiX/96F;
            if(hover&&Enabled)Theme.Fill(g,Color.FromArgb(244,247,252),new RectangleF(2,2,Width-4,Height-5),5*scale);
            using(var font=new Font(Font,active?FontStyle.Bold:FontStyle.Regular))Theme.Text(g,Text,font,!Enabled?Theme.Muted:active?Theme.Blue:Color.FromArgb(85,99,120),new RectangleF(0,0,Width,Height-4*scale),StringAlignment.Center);
            if(active){float inset=17*scale;using(var pen=new Pen(Theme.Blue,3*scale)){pen.StartCap=pen.EndCap=LineCap.Round;g.DrawLine(pen,inset,Height-3*scale,Width-inset,Height-3*scale);}}
            if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,new Rectangle(5,5,Width-10,Height-(int)(10*scale)));
        }
    }
    public enum ButtonAppearance { Standard, Toolbar, Sidebar }
    public sealed class ModernButton : Button
    {
        public bool Primary;
        public bool Danger;
        public bool Active;
        public bool Accent;
        public ButtonAppearance Appearance;
        bool hover;
        public ModernButton(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;UseVisualStyleBackColor=false;BackColor=Theme.Background;Cursor=Cursors.Hand;SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);Height=40;Margin=new Padding(0,0,8,0);}
        protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
        protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent==null?Theme.Background:Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;var r=new RectangleF(1,1,Width-3,Height-3);
            if(Appearance==ButtonAppearance.Sidebar)
            {
                if(Primary||hover&&Enabled)Theme.Fill(e.Graphics,Primary?Theme.Blue:Color.FromArgb(39,57,84),r,8);
                Theme.Text(e.Graphics,Text,Font,!Enabled?Theme.Muted:Primary?Color.White:Color.FromArgb(207,220,239),new RectangleF(16,0,Width-25,Height));
                if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(6,6,Width-12,Height-12));return;
            }
            if(Appearance==ButtonAppearance.Toolbar)
            {
                if(hover&&Enabled)Theme.Fill(e.Graphics,Color.FromArgb(235,240,248),r,6);
                Theme.Text(e.Graphics,Text,Font,!Enabled?Color.FromArgb(173,184,199):Danger?Theme.Red:Theme.Ink,r,StringAlignment.Center);
                if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(5,5,Width-10,Height-10));
                return;
            }
            Color fill=!Enabled?Color.FromArgb(231,236,245):(Primary?Theme.Blue:(Active?Color.FromArgb(228,237,255):(hover?Color.FromArgb(233,239,250):Color.White)));
            Theme.Fill(e.Graphics,fill,r,Math.Max(7,Height/5));
            if(!Primary)Theme.Border(e.Graphics,Active||Accent?Theme.Blue:Color.FromArgb(222,230,242),r,Math.Max(7,Height/5));
            Theme.Text(e.Graphics,Text,Font,!Enabled?Theme.Muted:(Primary?Color.White:(Danger?Theme.Red:Active||Accent?Theme.Blue:Theme.Ink)),r,StringAlignment.Center);
            if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(6,6,Width-12,Height-12));
        }
    }
    public sealed class SmoothProgress : Control
    {
        int value;
        readonly Timer animation=new Timer{Interval=40};int phase;bool indeterminate;
        public bool Indeterminate{get{return indeterminate;}set{indeterminate=value;animation.Enabled=value;Invalidate();}}
        public int Value {get{return value;}set{this.value=Math.Max(0,Math.Min(100,value));Invalidate();}}
        public SmoothProgress(){Height=8;DoubleBuffered=true;animation.Tick+=(s,e)=>{phase=(phase+4)%200;Invalidate();};}
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;Theme.Fill(e.Graphics,Color.FromArgb(226,234,245),new RectangleF(0,0,Width,Height),Height/2F);
            if(indeterminate){float band=Width*0.24F,x=(Width+band)*phase/200F-band;float left=Math.Max(0,x),right=Math.Min(Width,x+band);if(right-left>=Height)Theme.Fill(e.Graphics,Theme.Blue,new RectangleF(left,0,right-left,Height),Height/2F);}
            else if(value>0)Theme.Fill(e.Graphics,Color.FromArgb(45,184,139),new RectangleF(0,0,Math.Max(Height,Width*value/100F),Height),Height/2F);
        }
        protected override void Dispose(bool disposing){if(disposing)animation.Dispose();base.Dispose(disposing);}
    }
    public sealed class FileCards : ScrollableControl
    {
        public readonly List<InputFile> Items=new List<InputFile>();
        public int SelectedIndex=-1;
        public event Action SelectionChanged;
        public event Action<int> RemoveRequested;
        public event Action<string[]> FilesDropped;
        public event Action AddRequested;
        float unit=1;
        public FileCards(){DoubleBuffered=true;AutoScroll=true;AllowDrop=true;BackColor=Color.FromArgb(235,243,255);TabStop=true;Resize+=(s,e)=>RefreshLayout();DragEnter+=(s,e)=>e.Effect=e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;DragDrop+=(s,e)=>{if(FilesDropped!=null)FilesDropped((string[])e.Data.GetData(DataFormats.FileDrop));};}
        public InputFile SelectedFile{get{return SelectedIndex>=0&&SelectedIndex<Items.Count?Items[SelectedIndex]:null;}}
        public void RefreshLayout()
        {
            using(var g=CreateGraphics())unit=g.DpiX/96F;AutoScrollMinSize=new Size(0,(int)((Items.Count*88+24)*unit));
            if(SelectedIndex>=0){int top=(int)((12+SelectedIndex*88)*unit),bottom=top+(int)(76*unit);int scroll=-AutoScrollPosition.Y;if(bottom>scroll+ClientSize.Height)AutoScrollPosition=new Point(0,Math.Max(0,bottom-ClientSize.Height+(int)(12*unit)));else if(top<scroll)AutoScrollPosition=new Point(0,Math.Max(0,top-(int)(12*unit)));}
            Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.ScaleTransform(unit,unit);g.TranslateTransform(0,AutoScrollPosition.Y/unit);
            float w=ClientSize.Width/unit-24;
            if(Items.Count==0)
            {
                float height=ClientSize.Height/unit,top=Math.Max(4,(height-145)/2);
                if(height<110){Theme.Text(g,"＋  添加文件或拖入此处",Font,Theme.Ink,new RectangleF(10,0,w,height),StringAlignment.Center);return;}
                using(var large=new Font(Font.FontFamily,30))Theme.Text(g,"＋",large,Theme.Blue,new RectangleF(10,top,w,60),StringAlignment.Center);
                Theme.Text(g,"拖入文件，或点击这里添加",Font,Theme.Ink,new RectangleF(10,top+61,w,36),StringAlignment.Center);
                return;
            }
            for(int i=0;i<Items.Count;i++)
            {
                var r=new RectangleF(12,12+i*88,w,76);if(r.Bottom< -AutoScrollPosition.Y/unit||r.Top>(-AutoScrollPosition.Y+Height)/unit)continue;
                Theme.Fill(g,Color.White,r,12);if(i==SelectedIndex)Theme.Border(g,Theme.Blue,r,12,1.5F);
                string ext=Path.GetExtension(Items[i].Path).TrimStart('.').ToUpperInvariant();
                var badge=new RectangleF(r.X+12,r.Y+14,48,48);Theme.Fill(g,Color.FromArgb(239,244,252),badge,10);using(var font=new Font(Font.FontFamily,9,FontStyle.Bold))Theme.Text(g,ext,font,ext=="PDF"?Theme.Red:Theme.Blue,badge,StringAlignment.Center);
                Theme.Text(g,Path.GetFileName(Items[i].Path),Font,Theme.Ink,new RectangleF(r.X+75,r.Y+8,r.Width-133,31));
                string size="文件不可用";try{long bytes=new FileInfo(Items[i].Path).Length;size=bytes>1048576?(bytes/1048576.0).ToString("0.0")+" MB":(bytes/1024.0).ToString("0.0")+" KB";}catch(IOException){}catch(UnauthorizedAccessException){}
                using(var font=new Font(Font.FontFamily,8.5F))Theme.Text(g,ext+"   ·   "+size+(Items[i].Rotation!=0?"   ·   旋转 "+Items[i].Rotation+"°":""),font,Theme.Muted,new RectangleF(r.X+75,r.Y+38,r.Width-133,23));
                Theme.Trash(g,new RectangleF(r.Right-39,r.Y+25,24,26));
            }
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);Focus();if(Items.Count==0){if(AddRequested!=null)AddRequested();return;}
            float y=(e.Y-AutoScrollPosition.Y)/unit;int i=(int)((y-12)/88);if(i<0||i>=Items.Count)return;
            if(e.X/unit>ClientSize.Width/unit-65){if(RemoveRequested!=null)RemoveRequested(i);return;}
            SelectedIndex=i;Invalidate();if(SelectionChanged!=null)SelectionChanged();
        }
    }
    public sealed class PageCanvas : ScrollableControl
    {
        public PageDocument Document;
        public readonly HashSet<int> Selected=new HashSet<int>();
        public readonly Dictionary<int,string> Thumbnails=new Dictionary<int,string>();
        readonly Dictionary<string,Image> images=new Dictionary<string,Image>();
        public event Action SelectionChanged;
        public event Action<IEnumerable<int>> DeleteRequested;
        public event Action<IEnumerable<int>,int> MoveRequested;
        public event Action VisiblePagesChanged;
        float unit=1;int columns=3;Point down,mouse;bool rubber,dragging;int dragIndex=-1,insert=-1;
        Rectangle band;
        public PageCanvas(){DoubleBuffered=true;AutoScroll=true;TabStop=true;BackColor=Color.FromArgb(237,243,252);Resize+=(s,e)=>RefreshLayout();Scroll+=(s,e)=>{Invalidate();if(VisiblePagesChanged!=null)VisiblePagesChanged();};}
        public void RefreshLayout(){using(var g=CreateGraphics())unit=g.DpiX/96F;columns=Math.Max(1,(int)(ClientSize.Width/unit)/145);int count=Document==null?0:Document.Pages.Count;AutoScrollMinSize=new Size(0,(int)((((count+columns-1)/columns)*202+28)*unit));Invalidate();if(VisiblePagesChanged!=null)VisiblePagesChanged();}
        RectangleF Rect(int i){return new RectangleF(14+(i%columns)*145,14+(i/columns)*202,130,185);}
        public IEnumerable<int> VisibleOriginals(){if(Document==null)return new int[0];float top=-AutoScrollPosition.Y/unit,bottom=top+Height/unit;return Document.Pages.Where((p,i)=>Rect(i).Bottom>=top-220&&Rect(i).Top<=bottom+220).Select(p=>p.Index).Distinct().ToArray();}
        public void ClearImages(){foreach(var image in images.Values)image.Dispose();images.Clear();Thumbnails.Clear();}
        Image GetImage(string path){Image image;if(images.TryGetValue(path,out image))return image;try{using(var s=Image.FromFile(path))image=new Bitmap(s);if(images.Count>72){foreach(var x in images.Values)x.Dispose();images.Clear();}images[path]=image;return image;}catch(IOException){return null;}}
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.ScaleTransform(unit,unit);g.TranslateTransform(0,AutoScrollPosition.Y/unit);
            if(Document==null){Theme.Text(g,"页面总览",Font,Theme.Muted,new RectangleF(12,60,ClientSize.Width/unit-24,60),StringAlignment.Center);return;}
            float top=-AutoScrollPosition.Y/unit,bottom=top+Height/unit;
            for(int i=0;i<Document.Pages.Count;i++)
            {
                var r=Rect(i);if(r.Bottom<top||r.Top>bottom)continue;var item=Document.Pages[i];
                Theme.Fill(g,Color.White,r,10);Theme.Border(g,Selected.Contains(i)?Theme.Blue:Color.FromArgb(215,225,240),r,10,Selected.Contains(i)?2:1);
                string path;Image image=null;if(Thumbnails.TryGetValue(item.Index,out path))image=GetImage(path);
                if(image!=null)
                {
                    var state=g.Save();g.TranslateTransform(r.X+65,r.Y+80);g.RotateTransform(item.Rotation);float maxW=item.Rotation%180==0?108:138,maxH=item.Rotation%180==0?138:108;float scale=Math.Min(maxW/image.Width,maxH/image.Height);g.DrawImage(image,-image.Width*scale/2,-image.Height*scale/2,image.Width*scale,image.Height*scale);g.Restore(state);
                }
                else Theme.Text(g,"加载中…",Font,Theme.Muted,new RectangleF(r.X,r.Y+40,r.Width,70),StringAlignment.Center);
                using(var small=new Font(Font.FontFamily,8))Theme.Text(g,(i+1)+"  /  原第 "+(item.Index+1)+" 页",small,Theme.Muted,new RectangleF(r.X+6,r.Bottom-28,r.Width-12,22),StringAlignment.Center);
                Theme.Fill(g,Theme.Red,new RectangleF(r.Right-20,r.Y+3,17,17),8);
                using(var pen=new Pen(Color.White,1.5F)){g.DrawLine(pen,r.Right-15,r.Y+8,r.Right-8,r.Y+15);g.DrawLine(pen,r.Right-8,r.Y+8,r.Right-15,r.Y+15);}
            }
            if(rubber){using(var b=new SolidBrush(Color.FromArgb(35,Theme.Blue)))g.FillRectangle(b,band);using(var pen=new Pen(Theme.Blue))g.DrawRectangle(pen,band);}
            if(dragging&&insert>=0){var r=Rect(Math.Min(insert,Document.Pages.Count-1));float x=insert>=Document.Pages.Count?r.Right+4:r.X-5;using(var pen=new Pen(Theme.Blue,3))g.DrawLine(pen,x,r.Y,x,r.Bottom);}
        }
        Point Logical(Point p){return new Point((int)(p.X/unit),(int)((p.Y-AutoScrollPosition.Y)/unit));}
        int Hit(Point p){if(Document==null)return -1;for(int i=0;i<Document.Pages.Count;i++)if(Rect(i).Contains(p))return i;return -1;}
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);Focus();if(Document==null)return;down=Logical(e.Location);mouse=down;dragIndex=Hit(down);Capture=true;
            if(dragIndex>=0)
            {
                var r=Rect(dragIndex);if(down.X>=r.Right-24&&down.Y<r.Y+24){if(DeleteRequested!=null)DeleteRequested(new[]{dragIndex});Capture=false;return;}
                if((ModifierKeys&Keys.Control)!=0){if(!Selected.Add(dragIndex))Selected.Remove(dragIndex);}
                else if((ModifierKeys&Keys.Shift)!=0&&Selected.Count>0){int a=Selected.Min();for(int i=Math.Min(a,dragIndex);i<=Math.Max(a,dragIndex);i++)Selected.Add(i);}
                else if(!Selected.Contains(dragIndex)){Selected.Clear();Selected.Add(dragIndex);}
            }
            else {rubber=true;if((ModifierKeys&Keys.Control)==0)Selected.Clear();band=new Rectangle(down,Size.Empty);}
            Invalidate();if(SelectionChanged!=null)SelectionChanged();
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);if(!Capture||Document==null)return;mouse=Logical(e.Location);
            if(rubber){band=Rectangle.FromLTRB(Math.Min(down.X,mouse.X),Math.Min(down.Y,mouse.Y),Math.Max(down.X,mouse.X),Math.Max(down.Y,mouse.Y));Selected.Clear();for(int i=0;i<Document.Pages.Count;i++)if(Rect(i).IntersectsWith(band))Selected.Add(i);if(SelectionChanged!=null)SelectionChanged();}
            else if(dragIndex>=0&&Math.Abs(mouse.X-down.X)+Math.Abs(mouse.Y-down.Y)>8){dragging=true;int hit=Hit(mouse);insert=hit>=0?hit+(mouse.X>Rect(hit).Left+65?1:0):Document.Pages.Count;}
            if(e.Y>Height-30)AutoScrollPosition=new Point(0,-AutoScrollPosition.Y+22);else if(e.Y<30)AutoScrollPosition=new Point(0,Math.Max(0,-AutoScrollPosition.Y-22));
            Invalidate();
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);Capture=false;if(dragging&&MoveRequested!=null&&insert>=0)MoveRequested(Selected.ToArray(),insert);rubber=dragging=false;dragIndex=insert=-1;Invalidate();if(SelectionChanged!=null)SelectionChanged();
        }
        protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(Document==null)return;if(e.Control&&e.KeyCode==Keys.A){Selected.Clear();for(int i=0;i<Document.Pages.Count;i++)Selected.Add(i);Invalidate();if(SelectionChanged!=null)SelectionChanged();}if(e.KeyCode==Keys.Delete&&DeleteRequested!=null)DeleteRequested(Selected.ToArray());}
        protected override void Dispose(bool disposing){if(disposing)ClearImages();base.Dispose(disposing);}
    }
}
