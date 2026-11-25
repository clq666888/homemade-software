using System;
using System.Drawing;
using System.Windows.Forms;

namespace LocalConverter
{
    public sealed class WatermarkSettings : UserControl
    {
        public readonly TextBox Content=new TextBox{Text="仅供内部使用",MaxLength=120,AccessibleName="水印文字"};
        public readonly ComboBox StylePicker=Combo("水印样式",new[]{"居中大字","斜向大字","斜向平铺","水平平铺","页眉","页脚","九宫格定位"});
        public readonly PositionGrid PositionPicker=new PositionGrid();
        public readonly ComboBox ScopePicker=Combo("应用页面",new[]{"全部页面","仅奇数页","仅偶数页","指定范围"});
        public readonly NumericUpDown SizePicker=Number("水印字号",8,180,30),AnglePicker=Number("旋转角度",-180,180,-35),OpacityPicker=Number("不透明度",1,100,20),SpacingPicker=Number("平铺间距",50,300,130),MarginPicker=Number("边距",0,150,28),FirstPicker=Number("水印起始页",1,999999,1),LastPicker=Number("水印结束页",1,999999,1);
        public readonly CheckBox Outline=new CheckBox{Text="空心描边",AutoSize=true};
        readonly ModernButton fontButton=new ModernButton{Text="选择字体…"};
        public readonly ColorSwatch ColorPicker=new ColorSwatch();
        readonly Label fontLabel=new Label{AutoEllipsis=true,TextAlign=ContentAlignment.MiddleLeft};
        readonly TableLayoutPanel layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=8,Margin=Padding.Empty};
        readonly Control positionRow,spacingRow,rangeRow;
        string family="Microsoft YaHei";FontStyle fontStyle=FontStyle.Regular;Color color=Color.FromArgb(90,105,125);float unit=1;bool updating;
        public event Action SettingsChanged;
        static ComboBox Combo(string label,string[] items){var c=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,AccessibleName=label};c.Items.AddRange(items);c.SelectedIndex=0;return c;}
        static NumericUpDown Number(string label,int min,int max,int value){return new NumericUpDown{AccessibleName=label,Minimum=min,Maximum=max,Value=value};}
        public WatermarkSettings()
        {
            BackColor=Color.White;Margin=Padding.Empty;Font=new Font("Microsoft YaHei UI",10F);StylePicker.SelectedIndex=2;PositionPicker.SelectedIndex=4;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));for(int i=0;i<8;i++)layout.RowStyles.Add(new RowStyle(SizeType.Absolute,38));Controls.Add(layout);
            var text=Row("水印文字",Content);layout.Controls.Add(text,0,0);layout.SetColumnSpan(text,2);
            layout.Controls.Add(Row("样式",StylePicker),0,1);positionRow=Row("位置",PositionPicker);PositionPicker.Dock=DockStyle.None;PositionPicker.Anchor=AnchorStyles.Left;layout.Controls.Add(positionRow,1,1);
            var fonts=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty};fontButton.Width=104;fontLabel.Width=200;fontLabel.Height=32;fonts.Controls.AddRange(new Control[]{fontButton,fontLabel});layout.Controls.Add(fonts,0,2);layout.SetColumnSpan(fonts,2);
            layout.Controls.Add(Row("字号（磅）",SizePicker),0,3);layout.Controls.Add(Row("角度（°）",AnglePicker),1,3);
            layout.Controls.Add(Row("不透明度 %",OpacityPicker),0,4);layout.Controls.Add(Row("颜色",ColorPicker),1,4);ColorPicker.Dock=DockStyle.None;ColorPicker.Anchor=AnchorStyles.Left;
            spacingRow=Row("间距 %",SpacingPicker);layout.Controls.Add(spacingRow,0,5);layout.Controls.Add(Row("边距（磅）",MarginPicker),1,5);
            layout.Controls.Add(Row("应用页面",ScopePicker),0,6);layout.Controls.Add(Outline,1,6);
            var range=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty};range.Controls.AddRange(new Control[]{FirstPicker,new Label{Text="~",AutoSize=true,Margin=new Padding(8,5,8,0)},LastPicker});rangeRow=Row("页码范围",range);layout.Controls.Add(rangeRow,0,7);layout.SetColumnSpan(rangeRow,2);
            fontButton.Click+=(s,e)=>{using(var dialog=new FontDialog{Font=new Font(family,(float)SizePicker.Value,fontStyle),ShowEffects=false,MinSize=8,MaxSize=180,FontMustExist=true})if(dialog.ShowDialog(this)==DialogResult.OK){family=dialog.Font.FontFamily.Name;fontStyle=dialog.Font.Style&(FontStyle.Bold|FontStyle.Italic);SizePicker.Value=Math.Max(SizePicker.Minimum,Math.Min(SizePicker.Maximum,(decimal)dialog.Font.SizeInPoints));Notify();}};
            ColorPicker.Click+=(s,e)=>{using(var dialog=new ColorDialog{Color=color,FullOpen=true})if(dialog.ShowDialog(this)==DialogResult.OK){color=dialog.Color;Notify();}};
            Content.TextChanged+=(s,e)=>Notify();Outline.CheckedChanged+=(s,e)=>Notify();PositionPicker.SelectedIndexChanged+=(s,e)=>Notify();ScopePicker.SelectedIndexChanged+=(s,e)=>Notify();
            foreach(var n in new[]{SizePicker,AnglePicker,OpacityPicker,SpacingPicker,MarginPicker,FirstPicker,LastPicker})n.ValueChanged+=(s,e)=>Notify();
            StylePicker.SelectedIndexChanged+=(s,e)=>{updating=true;AnglePicker.Value=StylePicker.SelectedIndex==1||StylePicker.SelectedIndex==2?-35:0;SizePicker.Value=StylePicker.SelectedIndex<=1?64:StylePicker.SelectedIndex<=3?30:18;updating=false;Notify();};Notify();
        }
        Control Row(string caption,Control control)
        {
            var row=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,102));row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));row.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            row.Controls.Add(new Label{Text=caption,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=Theme.Muted,Margin=Padding.Empty},0,0);control.Dock=DockStyle.Fill;if(control is ComboBox||control is NumericUpDown){control.Dock=DockStyle.None;control.Anchor=AnchorStyles.Left|AnchorStyles.Right;}control.Margin=new Padding(0,4,12,4);row.Controls.Add(control,1,0);return row;
        }
        void Notify(){if(updating)return;bool grid=StylePicker.SelectedIndex==6;positionRow.Visible=grid;spacingRow.Visible=StylePicker.SelectedIndex==2||StylePicker.SelectedIndex==3;rangeRow.Visible=ScopePicker.SelectedIndex==3;layout.RowStyles[1].Height=(grid?88:38)*unit;layout.RowStyles[7].Height=rangeRow.Visible?38*unit:0;Height=(int)(((rangeRow.Visible?8:7)*38+(grid?50:0))*unit);fontLabel.Text=family+((fontStyle&FontStyle.Bold)!=0?" · 粗体":"")+((fontStyle&FontStyle.Italic)!=0?" · 斜体":"");ColorPicker.SelectedColor=color;if(SettingsChanged!=null)SettingsChanged();}
        public void SetScale(float scale){if(Math.Abs(unit-scale)<0.01)return;unit=scale;foreach(RowStyle row in layout.RowStyles)row.Height=38*scale;foreach(Control control in layout.Controls){var row=control as TableLayoutPanel;if(row!=null&&row.ColumnCount==2)row.ColumnStyles[0].Width=102*scale;}PositionPicker.Size=new Size((int)(84*scale),(int)(78*scale));ColorPicker.Size=new Size((int)(42*scale),(int)(30*scale));fontButton.Width=(int)(104*scale);fontLabel.Width=(int)(210*scale);fontLabel.Height=(int)(32*scale);FirstPicker.Width=LastPicker.Width=(int)(88*scale);Notify();}
        public WatermarkSpec Snapshot(){return new WatermarkSpec{Text=Content.Text,Family=family,FontStyle=fontStyle,Style=StylePicker.SelectedIndex,Position=PositionPicker.SelectedIndex,Scope=ScopePicker.SelectedIndex,Size=(float)SizePicker.Value,Angle=(float)AnglePicker.Value,Opacity=(float)OpacityPicker.Value,Spacing=(float)SpacingPicker.Value,Margin=(float)MarginPicker.Value,Outline=Outline.Checked,Color=color,First=(int)FirstPicker.Value,Last=(int)LastPicker.Value};}
    }
}
