using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace LocalConverter
{
    public sealed class PositionGrid : Control
    {
        int selected=4;
        static readonly string[] Names={"左上","上中","右上","左中","正中","右中","左下","下中","右下"};
        public event EventHandler SelectedIndexChanged;
        public int SelectedIndex{get{return selected;}set{int next=Math.Max(0,Math.Min(8,value));if(next==selected)return;selected=next;AccessibleDescription=Names[selected];Invalidate();if(SelectedIndexChanged!=null)SelectedIndexChanged(this,EventArgs.Empty);}}
        public PositionGrid(){Size=new Size(84,78);TabStop=true;Cursor=Cursors.Hand;AccessibleName="水印九宫格位置";AccessibleDescription="正中";AccessibleRole=AccessibleRole.List;DoubleBuffered=true;ResizeRedraw=true;}
        RectangleF Cell(int index){float cell=Math.Min((Width-4)/3F,(Height-4)/3F);return new RectangleF(2+index%3*cell,2+index/3*cell,cell-3,cell-3);}
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            for(int i=0;i<9;i++){var r=Cell(i);Theme.Fill(e.Graphics,i==selected?Theme.Blue:Color.FromArgb(242,246,252),r,3);Theme.Border(e.Graphics,i==selected?Theme.Blue:Color.FromArgb(199,212,231),r,3);using(var b=new SolidBrush(i==selected?Color.White:Color.FromArgb(151,169,194)))e.Graphics.FillEllipse(b,r.X+r.Width/2-2,r.Y+r.Height/2-2,4,4);}
            if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,ClientRectangle);
        }
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;Focus();for(int i=0;i<9;i++)if(Cell(i).Contains(e.Location)){SelectedIndex=i;break;}}
        protected override bool IsInputKey(Keys keyData){return (keyData&Keys.KeyCode)==Keys.Left||(keyData&Keys.KeyCode)==Keys.Right||(keyData&Keys.KeyCode)==Keys.Up||(keyData&Keys.KeyCode)==Keys.Down||base.IsInputKey(keyData);}
        protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);int next=selected;if(e.KeyCode==Keys.Left&&selected%3>0)next--;if(e.KeyCode==Keys.Right&&selected%3<2)next++;if(e.KeyCode==Keys.Up&&selected>=3)next-=3;if(e.KeyCode==Keys.Down&&selected<6)next+=3;if(next!=selected){SelectedIndex=next;e.Handled=true;}}
        protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
    }
    public sealed class ColorSwatch : Button
    {
        Color selectedColor=Color.FromArgb(90,105,125);
        public Color SelectedColor{get{return selectedColor;}set{selectedColor=value;Invalidate();}}
        public ColorSwatch(){Size=new Size(42,30);Cursor=Cursors.Hand;AccessibleName="选择水印颜色";FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);}
        protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Parent==null?Color.White:Parent.BackColor);float size=Math.Min(Width-6,Height-6);var r=new RectangleF(3,3,size,size);Theme.Fill(e.Graphics,SelectedColor,r,4);Theme.Border(e.Graphics,Color.FromArgb(160,174,194),r,4);if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,ClientRectangle);}
    }
}
