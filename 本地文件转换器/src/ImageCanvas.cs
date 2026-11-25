using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace LocalConverter
{
    public sealed class ImageWord
    {public RectangleF Bounds;public string Text;public bool NewLine,Selected;}

    public sealed class ImageCanvas : Control
    {
        Bitmap image,eraseBase,overlayPreview,mosaicPreview;string overlayPath;float zoom=1;PointF origin,panOrigin;Point dragStart,mouse,panStart;bool dragging,inside,panning,hasPanned;Rectangle selection;
        readonly List<Point> stroke=new List<Point>();ImageEdit draft;ImageStamp stamp;
        public string Tool="查看",SelectedTextId;
        public bool KeepTextLineBreaks;
        public float BrushDiameter=24;
        public bool ShowBrushGuide;
        public Color BrushColor=Theme.Blue;
        public Func<Rectangle,Point[],ImageEdit> DraftFactory;
        public readonly List<ImageWord> Words=new List<ImageWord>();
        public readonly List<ImageTextTarget> TextTargets=new List<ImageTextTarget>();
        public event Action<ImageEdit> EditCommitted;
        public event Action<ImageTextTarget> TextChosen;
        public event Action SelectionChanged,ViewChanged;
        public ImageStamp Stamp{get{return stamp;}set{if(stamp!=null&&!Object.ReferenceEquals(stamp,value))stamp.Dispose();stamp=value;Invalidate();}}
        public Bitmap Image{get{return image;}}
        public float Zoom{get{return zoom;}}
        public bool CanPan{get{return image!=null&&(image.Width*zoom>Width+1||image.Height*zoom>Height+1);}}
        bool BrushTool{get{return Tool=="画笔"||Tool=="马赛克"||Tool=="橡皮擦";}}
        public ImageCanvas(){DoubleBuffered=true;TabStop=true;BackColor=Color.FromArgb(231,237,246);Cursor=Cursors.Cross;SetStyle(ControlStyles.Selectable,true);}
        public void SetImage(Bitmap bitmap,bool fit)
        {
            CancelDraft();var old=image;image=bitmap;if(old!=null&&!Object.ReferenceEquals(old,bitmap))old.Dispose();
            if(fit)Fit();else{ClampOrigin();Invalidate();}
        }
        public void SetEraseBase(Bitmap bitmap){if(eraseBase!=null&&!Object.ReferenceEquals(eraseBase,bitmap))eraseBase.Dispose();eraseBase=bitmap;}
        public void Fit(){if(image==null)return;zoom=Math.Max(.01F,Math.Min((ClientSize.Width-48F)/image.Width,(ClientSize.Height-48F)/image.Height));hasPanned=false;Center();Changed();}
        public void ActualSize(){zoom=1;hasPanned=false;Center();Changed();}
        void Center(){if(image!=null)origin=new PointF((Width-image.Width*zoom)/2,(Height-image.Height*zoom)/2);}
        void ClampOrigin(){if(image==null)return;float w=image.Width*zoom,h=image.Height*zoom;origin=new PointF(w<=Width?(Width-w)/2:Math.Max(Width-w,Math.Min(0,origin.X)),h<=Height?(Height-h)/2:Math.Max(Height-h,Math.Min(0,origin.Y)));}
        void Changed(){Invalidate();if(ViewChanged!=null)ViewChanged();}
        public PointF ImagePoint(Point p){return new PointF((p.X-origin.X)/zoom,(p.Y-origin.Y)/zoom);}
        public Point ScreenPoint(PointF p){return Point.Round(new PointF(origin.X+p.X*zoom,origin.Y+p.Y*zoom));}
        public void ZoomAt(Point p,float factor){if(image==null)return;var center=ImagePoint(new Point(Width/2,Height/2));zoom=Math.Max(.01F,Math.Min(16,zoom*factor));if(hasPanned){origin=new PointF(Width/2F-center.X*zoom,Height/2F-center.Y*zoom);ClampOrigin();}else Center();Changed();}
        protected override void OnMouseWheel(MouseEventArgs e){if(!dragging&&!panning)ZoomAt(e.Location,e.Delta>0?1.2F:1/1.2F);base.OnMouseWheel(e);}
        protected override void OnResize(EventArgs e){base.OnResize(e);if(image!=null)Fit();}
        void Board(Graphics g,RectangleF bounds)
        {
            g.FillRectangle(Brushes.White,bounds);
            using(var checker=new SolidBrush(Color.FromArgb(231,231,231)))for(int y=Math.Max(0,(int)bounds.Top);y<Math.Min(Height,bounds.Bottom);y+=16)for(int x=Math.Max(0,(int)bounds.Left);x<Math.Min(Width,bounds.Right);x+=16)if(((x/16)+(y/16))%2==0)g.FillRectangle(checker,x,y,16,16);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);var g=e.Graphics;
            if(image==null){using(var title=new Font(Font.FontFamily,18,FontStyle.Bold))Theme.Text(g,"把图片拖到这里",title,Theme.Ink,new RectangleF(0,Height/2-46,Width,45),StringAlignment.Center);Theme.Text(g,"滚轮缩放 · 放大后按住右键拖动",Font,Theme.Muted,new RectangleF(0,Height/2+4,Width,35),StringAlignment.Center);return;}
            var bounds=new RectangleF(origin.X,origin.Y,image.Width*zoom,image.Height*zoom);
            using(var shadow=new SolidBrush(Color.FromArgb(25,25,40,60)))g.FillRectangle(shadow,bounds.X+3,bounds.Y+4,bounds.Width,bounds.Height);
            var save=g.Save();g.SetClip(bounds);Board(g,bounds);g.InterpolationMode=zoom>3?InterpolationMode.NearestNeighbor:InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.Half;g.DrawImage(image,bounds);
            if(Stamp!=null)Stamp.Paint(g,bounds,image.Width,image.Height);
            if(dragging&&draft!=null&&draft.Kind=="erase"&&eraseBase!=null)
            {
                using(var path=ImageBrushes.Path(draft))using(var transform=new Matrix())
                {transform.Translate(origin.X,origin.Y);transform.Scale(zoom,zoom);path.Transform(transform);var clipped=g.Save();g.SetClip(path,CombineMode.Intersect);Board(g,bounds);g.DrawImage(eraseBase,bounds);g.Restore(clipped);}
            }
            else if(dragging&&draft!=null&&draft.Kind!="crop")
            {
                var state=g.Save();g.TranslateTransform(origin.X,origin.Y);g.ScaleTransform(zoom,zoom);
                if(draft.Kind=="mosaic"){if(mosaicPreview==null)mosaicPreview=ImageBrushes.MosaicTexture(image,null,(int)draft.Value);ImageBrushes.PaintMosaic(g,mosaicPreview,image.Size,draft);}
                else if(draft.Kind=="overlay"){if(overlayPreview==null||overlayPath!=draft.Path){if(overlayPreview!=null)overlayPreview.Dispose();overlayPreview=ImageEditing.Load(draft.Path);overlayPath=draft.Path;}ImageEditing.PaintOverlay(g,overlayPreview,draft);}
                else ImageEditing.PaintAnnotation(g,draft);g.Restore(state);
            }
            g.Restore(save);
            using(var outline=new Pen(Color.FromArgb(140,Theme.Blue),1))using(var fill=new SolidBrush(Color.FromArgb(60,Theme.Blue)))foreach(var word in Words)
            {var r=ScreenRect(word.Bounds);if(word.Selected)g.FillRectangle(fill,r);g.DrawRectangle(outline,r.X,r.Y,r.Width,r.Height);}
            if(Tool=="文字"&&SelectedTextId!=null)
            {
                var text=TextTargets.FirstOrDefault(t=>t.Edit.Id==SelectedTextId);if(text!=null){var r=ScreenRect(text.Bounds);using(var pen=new Pen(Theme.Blue,1.5F){DashStyle=DashStyle.Dash})g.DrawRectangle(pen,r.X-3,r.Y-3,r.Width+6,r.Height+6);}
            }
            if(dragging&&(Tool=="裁剪"||Tool=="文字选择"))
            {var r=ScreenRect(selection);using(var shade=new SolidBrush(Color.FromArgb(28,Theme.Blue)))using(var pen=new Pen(Theme.Blue,1.5F){DashStyle=DashStyle.Dash}){g.FillRectangle(shade,r);g.DrawRectangle(pen,r.X,r.Y,r.Width,r.Height);}}
            bool guide=ShowBrushGuide&&!inside;
            if(!panning&&BrushTool&&(guide||inside&&new RectangleF(0,0,image.Width,image.Height).Contains(ImagePoint(mouse))))
            {
                Point center=ScreenPoint(Pixel(guide?new Point(Width/2,Height/2):mouse));float diameter=BrushDiameter*zoom,x=center.X-diameter/2,y=center.Y-diameter/2;g.SmoothingMode=SmoothingMode.AntiAlias;
                using(var brush=new SolidBrush(Color.FromArgb(28,Tool=="橡皮擦"?Color.White:BrushColor)))using(var outer=new Pen(Color.FromArgb(220,Color.White),3))using(var inner=new Pen(Color.FromArgb(210,30,43,65),1))
                {g.FillEllipse(brush,x,y,diameter,diameter);g.DrawEllipse(outer,x,y,diameter,diameter);g.DrawEllipse(inner,x,y,diameter,diameter);}
            }
        }
        RectangleF ScreenRect(RectangleF r){return new RectangleF(origin.X+r.X*zoom,origin.Y+r.Y*zoom,r.Width*zoom,r.Height*zoom);}
        Rectangle ImageRegion(Point a,Point b)
        {var x=ImagePoint(a);var y=ImagePoint(b);return Rectangle.Intersect(new Rectangle(0,0,image.Width,image.Height),Rectangle.FromLTRB((int)Math.Min(x.X,y.X),(int)Math.Min(x.Y,y.Y),(int)Math.Ceiling(Math.Max(x.X,y.X)),(int)Math.Ceiling(Math.Max(x.Y,y.Y))));}
        Point Pixel(Point location){var p=ImagePoint(location);return new Point(Math.Max(0,Math.Min(image.Width-1,(int)p.X)),Math.Max(0,Math.Min(image.Height-1,(int)p.Y)));}
        void UpdateDraft(Point location)
        {
            selection=ImageRegion(dragStart,location);Point p=Pixel(location);
            if(BrushTool){if(stroke.Count<12000&&(stroke.Count==0||stroke.Last()!=p))stroke.Add(p);}
            else{if(stroke.Count==1)stroke.Add(p);else stroke[1]=p;}
            draft=DraftFactory==null?null:DraftFactory(selection,stroke.ToArray());
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);Focus();if(image==null)return;
            if(e.Button==MouseButtons.Right){if(CanPan&&!dragging){panning=true;panStart=e.Location;panOrigin=origin;Capture=true;Cursor=Cursors.Hand;}return;}
            if(e.Button!=MouseButtons.Left||Tool=="查看"||panning)return;
            var point=ImagePoint(e.Location);if(!new RectangleF(0,0,image.Width,image.Height).Contains(point))return;
            if(Tool=="文字")
            {var target=TextTargets.LastOrDefault(t=>t.Bounds.Contains(point));SelectedTextId=target==null?null:target.Edit.Id;if(TextChosen!=null)TextChosen(target);if(target!=null){Invalidate();return;}}
            dragging=true;Capture=true;dragStart=mouse=e.Location;stroke.Clear();stroke.Add(Pixel(e.Location));selection=Rectangle.Empty;UpdateDraft(e.Location);Invalidate();
        }
        protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);mouse=e.Location;inside=true;if(panning){origin=new PointF(panOrigin.X+e.X-panStart.X,panOrigin.Y+e.Y-panStart.Y);ClampOrigin();hasPanned=true;Invalidate();return;}if(dragging&&image!=null)UpdateDraft(e.Location);if(BrushTool||dragging)Invalidate();}
        protected override void OnMouseEnter(EventArgs e){inside=true;base.OnMouseEnter(e);}
        protected override void OnMouseLeave(EventArgs e){inside=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);if(panning&&e.Button==MouseButtons.Right){panning=false;Capture=false;Cursor=Cursors.Cross;Invalidate();return;}if(!dragging||image==null||e.Button!=MouseButtons.Left)return;UpdateDraft(e.Location);dragging=false;Capture=false;
            if(Tool=="文字选择")
            {var point=ImagePoint(e.Location);if((ModifierKeys&Keys.Control)==0)foreach(var word in Words)word.Selected=false;foreach(var word in Words)if(selection.Width<4&&selection.Height<4?word.Bounds.Contains(point):word.Bounds.IntersectsWith(selection))word.Selected=true;if(SelectionChanged!=null)SelectionChanged();}
            else if(draft!=null&&(BrushTool||draft.Kind=="text"||selection.Width>1&&selection.Height>1)&&EditCommitted!=null)EditCommitted(draft);
            draft=null;if(mosaicPreview!=null){mosaicPreview.Dispose();mosaicPreview=null;}Invalidate();
        }
        public void CancelDraft(){dragging=panning=false;draft=null;stroke.Clear();if(mosaicPreview!=null){mosaicPreview.Dispose();mosaicPreview=null;}Capture=false;Cursor=Cursors.Cross;Invalidate();}
        protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture&&(dragging||panning))CancelDraft();}
        public void SelectAllWords(){foreach(var word in Words)word.Selected=true;Invalidate();if(SelectionChanged!=null)SelectionChanged();}
        public string SelectedText{get{return OcrTextFlow.Join(Words.Where(w=>w.Selected),KeepTextLineBreaks);}}
        protected override void OnKeyDown(KeyEventArgs e){if(e.KeyCode==Keys.Escape)CancelDraft();if(e.Control&&e.KeyCode==Keys.A&&Tool=="文字选择"){SelectAllWords();e.SuppressKeyPress=true;}base.OnKeyDown(e);}
        protected override void Dispose(bool disposing){if(disposing){if(image!=null)image.Dispose();if(eraseBase!=null)eraseBase.Dispose();if(overlayPreview!=null)overlayPreview.Dispose();if(mosaicPreview!=null)mosaicPreview.Dispose();if(stamp!=null)stamp.Dispose();}base.Dispose(disposing);}
    }
}
