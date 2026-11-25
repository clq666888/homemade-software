using System;
using System.Drawing;
using System.Windows.Forms;

namespace LocalConverter
{
    // The slider describes the visible footprint; ImageWorkspace converts it to image units.
    public sealed class BrushSizePicker : UserControl
    {
        public readonly TrackBar Slider=new TrackBar{Minimum=1,Maximum=200,Value=40,TickStyle=TickStyle.None,Dock=DockStyle.Fill,AccessibleName="笔刷大小滑块",SmallChange=1,LargeChange=10};
        readonly Label hint=new Label{Text="画布圆圈就是涂抹范围",Dock=DockStyle.Fill,ForeColor=Theme.Muted,AutoEllipsis=true};
        public event Action ValueChanged;
        public event Action<bool> GuideChanged;
        public int Value{get{return Slider.Value;}set{Slider.Value=Math.Max(Slider.Minimum,Math.Min(Slider.Maximum,value));}}
        public BrushSizePicker()
        {
            Height=78;Width=245;Margin=Padding.Empty;BackColor=Color.White;
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=3,Margin=Padding.Empty};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,20));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,22));Controls.Add(layout);
            layout.Controls.Add(new Label{Text="小",Dock=DockStyle.Fill,ForeColor=Theme.Muted,Margin=Padding.Empty},0,0);layout.Controls.Add(new Label{Text="大",Dock=DockStyle.Fill,ForeColor=Theme.Muted,TextAlign=ContentAlignment.TopRight,Margin=Padding.Empty},1,0);layout.Controls.Add(Slider,0,1);layout.SetColumnSpan(Slider,2);layout.Controls.Add(hint,0,2);layout.SetColumnSpan(hint,2);
            Slider.ValueChanged+=(s,e)=>{if(ValueChanged!=null)ValueChanged();if(Slider.ContainsFocus)Guide(true);};Slider.Enter+=(s,e)=>Guide(true);Slider.Leave+=(s,e)=>Guide(false);Slider.MouseEnter+=(s,e)=>Guide(true);Slider.MouseLeave+=(s,e)=>{if(!Slider.ContainsFocus)Guide(false);};
        }
        void Guide(bool value){if(GuideChanged!=null)GuideChanged(value);}
        public void Configure(int max,int value,bool brush){Slider.Maximum=max;Value=value;hint.Text=brush?"画布圆圈就是涂抹范围":"拖动时预览实际线条粗细";}
    }
}
