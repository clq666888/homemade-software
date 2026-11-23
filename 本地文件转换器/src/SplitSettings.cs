using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace LocalConverter
{
    public sealed class SplitSettings : UserControl
    {
        readonly TableLayoutPanel layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Margin=Padding.Empty};
        public readonly ComboBox Method=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,AccessibleName="拆分方式"};
        public readonly NumericUpDown CountPicker=new NumericUpDown{Minimum=1,Maximum=100,Value=2,AccessibleName="拆成几份"};
        public readonly NumericUpDown RatioPicker=new NumericUpDown{Minimum=0.1M,Maximum=99.9M,Value=50,DecimalPlaces=1,AccessibleName="切割比例百分比"};
        public readonly CheckBox TopBottom=new CheckBox{Text="上下切割",Checked=true,AutoSize=true};
        public readonly CheckBox LeftRight=new CheckBox{Text="左右切割",AutoSize=true};
        readonly TableLayoutPanel methodRow,countRow;
        readonly FlowLayoutPanel groupRows=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Margin=Padding.Empty};
        readonly TableLayoutPanel cropRows=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=Padding.Empty};
        readonly Label hint=new Label{Dock=DockStyle.Fill,ForeColor=Theme.Muted,Margin=Padding.Empty,AutoEllipsis=false};
        readonly List<TextBox[]> ranges=new List<TextBox[]>();
        float unit=1;bool updating;
        bool keepSecond;
        public bool KeepSecond{get{return keepSecond;}set{if(keepSecond==value)return;keepSecond=value;Notify();}}
        public event Action SettingsChanged;
        public SplitSettings()
        {
            Margin=Padding.Empty;BackColor=Color.White;Font=new Font("Microsoft YaHei UI",10F);hint.Font=new Font(Font.FontFamily,8.5F);
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));cropRows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));for(int i=0;i<5;i++)layout.RowStyles.Add(new RowStyle(SizeType.Absolute,0));Controls.Add(layout);
            Method.Items.AddRange(new object[]{"每页","指定页数拆分","按比例切割"});Method.SelectedIndex=0;
            methodRow=Row("拆分方式",Method);countRow=Row("拆成几份",CountPicker);
            layout.Controls.Add(methodRow,0,0);layout.Controls.Add(countRow,0,1);layout.Controls.Add(groupRows,0,2);layout.Controls.Add(cropRows,0,3);layout.Controls.Add(hint,0,4);
            cropRows.RowStyles.Add(new RowStyle(SizeType.Percent,50));cropRows.RowStyles.Add(new RowStyle(SizeType.Percent,50));
            var directions=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty};directions.Controls.AddRange(new Control[]{TopBottom,LeftRight});cropRows.Controls.Add(directions,0,0);
            var percent=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty};percent.Controls.AddRange(new Control[]{new Label{Text="保留比例",AutoSize=true,Margin=new Padding(0,7,10,0)},RatioPicker,new Label{Text="%",AutoSize=true,Margin=new Padding(5,7,0,0)}});cropRows.Controls.Add(percent,0,1);
            Method.SelectedIndexChanged+=(s,e)=>{ApplyLayout();Notify();};CountPicker.ValueChanged+=(s,e)=>{RebuildRanges();ApplyLayout();Notify();};RatioPicker.ValueChanged+=(s,e)=>Notify();
            TopBottom.CheckedChanged+=(s,e)=>Choose(TopBottom,LeftRight);LeftRight.CheckedChanged+=(s,e)=>Choose(LeftRight,TopBottom);
            Resize+=(s,e)=>ResizeRanges();RebuildRanges();ApplyLayout();
        }
        TableLayoutPanel Row(string title,Control control)
        {
            var row=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,110));row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            row.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            row.Controls.Add(new Label{Text=title,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=Theme.Ink,Margin=Padding.Empty},0,0);
            control.Dock=DockStyle.Left;control.Margin=new Padding(0,4,0,4);row.Controls.Add(control,1,0);return row;
        }
        void Notify(){if(!updating&&SettingsChanged!=null)SettingsChanged();}
        void Choose(CheckBox current,CheckBox other)
        {
            if(updating)return;updating=true;if(current.Checked)other.Checked=false;else if(!other.Checked)current.Checked=true;updating=false;Notify();
        }
        void RebuildRanges()
        {
            updating=true;string[][] old=ranges.Select(x=>x.Select(t=>t.Text).ToArray()).ToArray();foreach(Control row in groupRows.Controls.Cast<Control>().ToArray())row.Dispose();groupRows.Controls.Clear();ranges.Clear();
            for(int i=0;i<(int)CountPicker.Value;i++)
            {
                var input=new TableLayoutPanel{ColumnCount=3,RowCount=1,Margin=Padding.Empty};input.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));input.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,28*unit));input.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));input.RowStyles.Add(new RowStyle(SizeType.Percent,100));
                var pair=new TextBox[2];
                for(int side=0;side<2;side++)
                {
                    var box=new TextBox{AccessibleName="第 "+(i+1)+" 份"+(side==0?"起始页":"结束页"),Text=i<old.Length?old[i][side]:"",Anchor=AnchorStyles.Left|AnchorStyles.Right,MaxLength=9,Margin=new Padding(0,4,0,4)};
                    box.KeyPress+=(s,e)=>{if(!Char.IsControl(e.KeyChar)&&(e.KeyChar<'0'||e.KeyChar>'9'))e.Handled=true;};box.TextChanged+=(s,e)=>Notify();pair[side]=box;input.Controls.Add(box,side*2,0);
                }
                input.Controls.Add(new Label{Text="~",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,Margin=Padding.Empty,ForeColor=Theme.Muted},1,0);
                var row=Row("第 "+(i+1)+" 份页码",input);input.Margin=Padding.Empty;row.Dock=DockStyle.None;row.Margin=new Padding(0,0,0,3);groupRows.Controls.Add(row);ranges.Add(pair);
            }
            updating=false;ResizeRanges();
        }
        void ResizeRanges(){foreach(TableLayoutPanel row in groupRows.Controls){row.Width=Math.Max(120,groupRows.ClientSize.Width-(int)(22*unit));row.Height=(int)(38*unit);row.ColumnStyles[0].Width=110*unit;var input=(TableLayoutPanel)row.GetControlFromPosition(1,0);input.Width=(int)(260*unit);input.ColumnStyles[1].Width=28*unit;}}
        public void SetScale(float value){if(Math.Abs(unit-value)<0.01)return;unit=value;ApplyLayout();}
        void ApplyLayout()
        {
            int selection=Method.SelectedIndex;bool custom=selection==1,ratio=selection==2;
            methodRow.ColumnStyles[0].Width=countRow.ColumnStyles[0].Width=110*unit;
            Method.Width=(int)(245*unit);Method.DropDownWidth=Method.Width;CountPicker.Width=RatioPicker.Width=(int)(88*unit);
            TopBottom.Margin=new Padding(0,(int)(7*unit),(int)(25*unit),0);LeftRight.Margin=TopBottom.Margin;
            countRow.Visible=groupRows.Visible=custom;cropRows.Visible=ratio;
            float[] heights={42,custom?40:0,custom?Math.Min((int)CountPicker.Value*41,130):0,ratio?82:0,custom?48:36};
            for(int i=0;i<5;i++)layout.RowStyles[i].Height=heights[i]*unit;
            hint.Text=custom?"左框填写起始页，右框填写结束页；单页填写相同页码。未选页码不导出，批量文件分别应用相同设置。":ratio?"在右侧选择保留区域，灰色部分会裁掉；全部页面保存在同一份 PDF 中。":"每一页拆为一份 PDF，最终统一打包 ZIP。";
            Height=(int)Math.Ceiling(heights.Sum()*unit);ResizeRanges();
        }
        public Dictionary<string,object> JobValues()
        {
            var values=new Dictionary<string,object>{{"splitMode",Method.SelectedIndex==1?"ranges":Method.SelectedIndex==2?"ratio":"each"}};
            if(Method.SelectedIndex==1)
            {
                var selected=new List<string>();
                for(int i=0;i<ranges.Count;i++)
                {
                    int first,last;
                    if(!Int32.TryParse(ranges[i][0].Text.Trim(),NumberStyles.None,CultureInfo.InvariantCulture,out first)||first<1||!Int32.TryParse(ranges[i][1].Text.Trim(),NumberStyles.None,CultureInfo.InvariantCulture,out last)||last<1)throw new System.IO.IOException("第 "+(i+1)+" 份：请在两个框中分别填写大于 0 的起始页和结束页。");
                    if(first>last)throw new System.IO.IOException("第 "+(i+1)+" 份：起始页不能大于结束页。");
                    selected.Add(first.ToString(CultureInfo.InvariantCulture)+"-"+last.ToString(CultureInfo.InvariantCulture));
                }
                values["ranges"]=selected.ToArray();
            }
            if(Method.SelectedIndex==2){values["axis"]=TopBottom.Checked?"horizontal":"vertical";values["ratio"]=(double)RatioPicker.Value;values["keep"]=KeepSecond?"second":"first";}
            return values;
        }
        public string Context{get{return Method.SelectedIndex==1?"ranges|"+String.Join(";",ranges.Select(r=>r[0].Text+"~"+r[1].Text)):Method.SelectedIndex==2?"ratio|"+TopBottom.Checked+"|"+RatioPicker.Value.ToString(CultureInfo.InvariantCulture)+"|"+KeepSecond:"each";}}
    }
}
