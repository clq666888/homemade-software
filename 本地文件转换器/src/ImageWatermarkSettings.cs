using System;
using System.Drawing;
using System.Windows.Forms;

namespace LocalConverter
{
    public sealed class ImageWatermarkSettings : FlowLayoutPanel
    {
        public readonly ComboBox Type=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=245};
        public readonly ComboBox Style=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=245};
        public readonly TextBox Content=new TextBox{Text="© 我的作品",Width=245,MaxLength=120};
        public readonly PositionGrid Position=new PositionGrid{SelectedIndex=8};
        public readonly NumericUpDown SizeValue=Number(8,600,48),OpacityValue=Number(1,100,35),AngleValue=Number(-180,180,0),LogoSize=Number(2,90,20),Spacing=Number(50,300,130);
        public readonly ColorSwatch ColorValue=new ColorSwatch{SelectedColor=Color.White,Size=new Size(46,46)};public readonly CheckBox Outline=new CheckBox{Text="空心文字",AutoSize=true};
        readonly FlowLayoutPanel colorRow=new FlowLayoutPanel{Width=245,Height=52,WrapContents=false,Margin=new Padding(0,8,0,4),AccessibleName="水印颜色"};
        readonly ModernButton font=new ModernButton{Text="选择字体…",Width=245,Height=34},logo=new ModernButton{Text="选择透明 Logo 图片…",Width=245,Height=34};
        readonly Label fontName=new Label{AutoSize=true,MaximumSize=new Size(245,0),Text="Microsoft YaHei"};
        string family="Microsoft YaHei";FontStyle fontStyle;public string LogoPath;public event Action Changed,LogoRequested;
        static NumericUpDown Number(int min,int max,int value){return new NumericUpDown{Minimum=min,Maximum=max,Value=value,Width=245};}
        void Caption(string text){Controls.Add(new Label{Text=text,AutoSize=true,ForeColor=Theme.Muted,Margin=new Padding(0,10,0,4)});}
        public ImageWatermarkSettings()
        {
            FlowDirection=FlowDirection.TopDown;WrapContents=false;AutoSize=true;Width=252;Margin=Padding.Empty;BackColor=Color.White;
            Type.Items.AddRange(new object[]{"文字水印","Logo / 图片水印"});Type.SelectedIndex=0;
            Style.Items.AddRange(new object[]{"居中大字","斜向大字","斜向重复平铺","水平重复平铺","顶部署名","底部署名","九宫格定位"});Style.SelectedIndex=6;
            Caption("水印类型");Controls.Add(Type);Caption("布局样式");Controls.Add(Style);Caption("水印文字 / Logo");Controls.Add(Content);Controls.Add(logo);Caption("字体");Controls.Add(font);Controls.Add(fontName);Caption("位置（点击九宫格）");Controls.Add(Position);Caption("文字大小（像素）");Controls.Add(SizeValue);Caption("Logo 宽度占图片 %");Controls.Add(LogoSize);Caption("旋转角度 °");Controls.Add(AngleValue);Caption("不透明度 %");Controls.Add(OpacityValue);Caption("平铺间距 %");Controls.Add(Spacing);colorRow.Controls.Add(new Label{Text="颜色",AutoSize=true,ForeColor=Theme.Muted,Margin=new Padding(0,14,10,0)});colorRow.Controls.Add(ColorValue);colorRow.Controls.Add(new Label{Name="ColorPickerHint",Text="←（点击选择颜色）",AutoSize=true,Font=new Font(Font.FontFamily,8.5F),ForeColor=Color.FromArgb(150,157,168),Margin=new Padding(4,15,0,0)});ColorValue.AccessibleDescription="点击色块打开颜色选择窗口";Controls.Add(colorRow);Controls.Add(Outline);
            font.Click+=(s,e)=>{using(var d=new FontDialog{Font=new Font(family,(float)Math.Min(180,SizeValue.Value),fontStyle),FontMustExist=true,ShowEffects=false})if(d.ShowDialog(this)==DialogResult.OK){family=d.Font.FontFamily.Name;fontStyle=d.Font.Style&(FontStyle.Bold|FontStyle.Italic);fontName.Text=family;Notify();}};
            ColorValue.Click+=(s,e)=>{using(var d=new ColorDialog{Color=ColorValue.SelectedColor,FullOpen=true})if(d.ShowDialog(this)==DialogResult.OK){ColorValue.SelectedColor=d.Color;Notify();}};
            logo.Click+=(s,e)=>{if(LogoRequested!=null)LogoRequested();};
            Type.SelectedIndexChanged+=(s,e)=>Notify();Style.SelectedIndexChanged+=(s,e)=>{AngleValue.Value=Style.SelectedIndex==1||Style.SelectedIndex==2?-35:0;Notify();};Content.TextChanged+=(s,e)=>Notify();Position.SelectedIndexChanged+=(s,e)=>Notify();Outline.CheckedChanged+=(s,e)=>Notify();foreach(var n in new[]{SizeValue,OpacityValue,AngleValue,LogoSize,Spacing})n.ValueChanged+=(s,e)=>Notify();Notify();
        }
        void Notify(){bool text=Type.SelectedIndex==0;Content.Visible=font.Visible=fontName.Visible=SizeValue.Visible=colorRow.Visible=ColorValue.Visible=Outline.Visible=text;logo.Visible=LogoSize.Visible=!text;Position.Visible=Style.SelectedIndex==6;Spacing.Visible=Style.SelectedIndex==2||Style.SelectedIndex==3;foreach(Control c in Controls){if(c is Label){if(c.Text=="字体"||c.Text=="文字大小（像素）"||c.Text=="颜色")c.Visible=text;else if(c.Text=="Logo 宽度占图片 %")c.Visible=!text;else if(c.Text=="位置（点击九宫格）")c.Visible=Position.Visible;else if(c.Text=="平铺间距 %")c.Visible=Spacing.Visible;}}if(Changed!=null)Changed();}
        public ImageStamp Snapshot()
        {
            int style=Style.SelectedIndex;var spec=new WatermarkSpec{Text=Content.Text,Family=family,FontStyle=fontStyle,Style=style,Position=Position.SelectedIndex,Size=(float)SizeValue.Value,Angle=(float)AngleValue.Value,Opacity=(float)OpacityValue.Value,Spacing=(float)Spacing.Value,Margin=28,Color=ColorValue.SelectedColor,Outline=Outline.Checked};
            return new ImageStamp{Text=Type.SelectedIndex==0?spec:null,Logo=Type.SelectedIndex==1?LogoPath:null,Position=style==0||style==1?4:style==4?1:style==5?7:Position.SelectedIndex,WidthPercent=(float)LogoSize.Value,Opacity=(float)OpacityValue.Value,Angle=(float)AngleValue.Value,Tiled=style==2||style==3};
        }
        public string Context{get{var s=Snapshot();return Type.SelectedIndex+"|"+Style.SelectedIndex+"|"+s.Logo+"|"+(s.Text==null?"":s.Text.Context)+"|"+s.Position+"|"+s.WidthPercent+"|"+s.Opacity+"|"+s.Angle;}}
    }
}
