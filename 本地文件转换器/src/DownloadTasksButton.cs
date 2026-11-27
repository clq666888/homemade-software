using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LocalConverter
{
    public sealed class DownloadTasksButton : Button
    {
        int active;bool attention,hover;
        public int ActiveCount{get{return active;}set{if(active!=value){active=value;Invalidate();}}}
        public bool Attention{get{return attention;}set{if(attention!=value){attention=value;Invalidate();}}}
        public DownloadTasksButton(){Size=new Size(48,40);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;AccessibleName="下载任务";AccessibleRole=AccessibleRole.PushButton;SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);}
        protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
        protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            if(hover||Focused)Theme.Fill(e.Graphics,Color.FromArgb(231,238,252),new RectangleF(1,1,Width-2,Height-2),8);
            float scale=Height/40F,x=Width/2F,y=Height/2F;
            using(var pen=new Pen(active>0?Theme.Blue:Theme.Ink,1.8F*scale)){pen.StartCap=pen.EndCap=LineCap.Round;e.Graphics.DrawLine(pen,x,y-10*scale,x,y+3*scale);e.Graphics.DrawLines(pen,new[]{new PointF(x-5*scale,y-2*scale),new PointF(x,y+3*scale),new PointF(x+5*scale,y-2*scale)});e.Graphics.DrawLines(pen,new[]{new PointF(x-9*scale,y+3*scale),new PointF(x-9*scale,y+9*scale),new PointF(x+9*scale,y+9*scale),new PointF(x+9*scale,y+3*scale)});}
            if(active>0){var r=new RectangleF(Width-19*scale,0,19*scale,17*scale);Theme.Fill(e.Graphics,Theme.Blue,r,8*scale);using(var font=new Font(Font.FontFamily,7.5F,FontStyle.Bold))Theme.Text(e.Graphics,active>99?"99+":active.ToString(),font,Color.White,r,StringAlignment.Center);}
            else if(attention)using(var brush=new SolidBrush(Theme.Red))e.Graphics.FillEllipse(brush,Width-11*scale,4*scale,7*scale,7*scale);
            if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(3,3,Width-6,Height-6));
        }
    }
}
