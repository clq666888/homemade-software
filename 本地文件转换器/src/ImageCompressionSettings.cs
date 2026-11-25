using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace LocalConverter
{
    public sealed class ImageCompressionSpec
    {
        public string Format;public int Quality,Edge;public bool Lossless,FirstOnly;
    }
    public sealed class ImageCompressionSettings : FlowLayoutPanel
    {
        public readonly ComboBox Output=Combo(new[]{"保持原格式（推荐）","JPG · 照片，兼容性好","PNG · 截图，保留透明背景","WebP · 文件较小，支持透明","AVIF · 文件较小，处理较慢"});
        public readonly ComboBox Preset=Combo(new[]{"清晰优先 · 轻度压缩","均衡压缩（推荐）","体积优先 · 画面可能变糊"});
        public readonly ComboBox Dimensions=Combo(new[]{"保持原尺寸","宽高缩小到 75%","宽高缩小到 50%","宽高缩小到 25%"});
        public readonly CheckBox Custom=new CheckBox{Text="手动调整画质",AutoSize=true},Lossless=new CheckBox{Text="WebP 保留全部细节（文件可能较大）",AutoSize=true},FirstFrame=new CheckBox{Text="只保留第一张（动画将变成静态）",AutoSize=true};
        public readonly TrackBar Quality=new TrackBar{Minimum=1,Maximum=100,Value=80,TickStyle=TickStyle.None,AutoSize=false,Height=36,AccessibleName="压缩画质：左边更小，右边更清晰"};
        readonly Label presetLabel=Caption("压缩程度"),guide=Caption("← 文件更小                 更清晰 →"),sizeNote=Caption(""),formatNote=Caption("");
        readonly FlowLayoutPanel qualitySection=new FlowLayoutPanel{FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Width=245,Margin=Padding.Empty};
        ImageAsset selected;ImageAsset[] assets=new ImageAsset[0];bool updating;
        public event Action Changed;
        static ComboBox Combo(string[] items){var c=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=245};c.Items.AddRange(items);c.SelectedIndex=0;return c;}
        static Label Caption(string value){return new Label{Text=value,AutoSize=true,MaximumSize=new Size(245,0),ForeColor=Theme.Muted,Margin=new Padding(0,10,0,4)};}
        public ImageCompressionSettings()
        {
            FlowDirection=FlowDirection.TopDown;WrapContents=false;AutoSize=true;AutoSizeMode=AutoSizeMode.GrowAndShrink;Width=245;Margin=Padding.Empty;BackColor=Color.White;Preset.SelectedIndex=1;Preset.Margin=new Padding(3,3,3,8);
            qualitySection.Controls.AddRange(new Control[]{presetLabel,Preset,Custom,Quality,guide});Controls.Add(Caption("保存为什么格式"));Controls.Add(Output);Controls.Add(Lossless);Controls.Add(qualitySection);Controls.Add(new Panel{Height=1,Width=245,BackColor=Color.FromArgb(220,227,237),Margin=new Padding(0,12,0,2)});Controls.Add(Caption("图片尺寸"));Controls.Add(Dimensions);Controls.Add(sizeNote);Controls.Add(FirstFrame);Controls.Add(formatNote);
            foreach(var c in new[]{Output,Preset,Dimensions})c.SelectedIndexChanged+=(s,e)=>Notify();foreach(var c in new[]{Custom,Lossless,FirstFrame})c.CheckedChanged+=(s,e)=>Notify();Quality.ValueChanged+=(s,e)=>Notify();SizeChanged+=(s,e)=>FitWidth();RefreshFields();
        }
        public void Configure(ImageAsset asset,IEnumerable<ImageAsset> all){selected=asset;assets=all.ToArray();RefreshFields();FitWidth();}
        string Target(ImageAsset asset)
        {
            if(Output.SelectedIndex>0)return new[]{"","jpg","png","webp","avif"}[Output.SelectedIndex];
            string source=asset==null?"PNG":asset.Format.ToUpperInvariant();return source=="JPEG"?"jpg":source=="JPEG2000"?"jp2":source.ToLowerInvariant();
        }
        static bool HasQuality(string format){return format=="jpg"||format=="webp"||format=="avif";}
        int Percent{get{return new[]{100,75,50,25}[Dimensions.SelectedIndex];}}
        void Notify(){if(updating)return;RefreshFields();if(Changed!=null)Changed();}
        void RefreshFields()
        {
            updating=true;SuspendLayout();qualitySection.SuspendLayout();var formats=assets.Length==0?new[]{Target(selected)}:assets.Select(Target).ToArray();bool webp=formats.Contains("webp"),quality=formats.Any(f=>HasQuality(f)&&!(f=="webp"&&Lossless.Checked));
            qualitySection.Visible=quality;Quality.Visible=guide.Visible=quality&&Custom.Checked;Lossless.Visible=webp;FirstFrame.Visible=assets.Any(a=>a.Frames>1);
            if(!Custom.Checked)Quality.Value=new[]{92,80,55}[Preset.SelectedIndex];
            string target=Target(selected);formatNote.Text=target=="png"?"PNG 只优化存储，不降低画质，文件可能只缩小一点。想明显变小，可改用 WebP 或缩小图片尺寸。":target=="jpg"?"适合照片。压得越小，细节越容易变糊；透明背景会变成白色。":target=="webp"?"支持透明背景与动画，通常适合减小文件；部分旧软件可能无法打开。":target=="avif"?"适合减小文件，编码会慢一些；部分旧软件可能无法打开。":"保持原格式；不同格式的压缩空间不同，不保证一定变小。";
            formatNote.Text+="\r\n设置用于列表内全部图片。处理后可预览实际画质，并查看前后大小。";
            sizeNote.Text=selected==null?"缩小宽高会减少细节，并保持图片比例。":selected.Width+" × "+selected.Height+" → "+Math.Max(1,(int)Math.Round(selected.Width*Percent/100.0))+" × "+Math.Max(1,(int)Math.Round(selected.Height*Percent/100.0))+" 像素。"+(Percent==100?"不改变宽高。":"尺寸越小，放大后越容易变糊。");
            qualitySection.ResumeLayout(true);ResumeLayout(true);updating=false;
        }
        void FitWidth(){int width=Math.Max(130,ClientSize.Width-Padding.Horizontal-6);foreach(Control c in Controls.Cast<Control>().Concat(qualitySection.Controls.Cast<Control>())){var label=c as Label;if(label!=null)label.MaximumSize=new Size(width,0);else if(c is CheckBox)((CheckBox)c).MaximumSize=new Size(width,0);else c.Width=width;}}
        public ImageCompressionSpec Snapshot(ImageAsset asset){return new ImageCompressionSpec{Format=Target(asset),Quality=(int)Quality.Value,Edge=Percent==100?0:Math.Max(1,(int)Math.Round(Math.Max(asset.Width,asset.Height)*Percent/100.0)),Lossless=Lossless.Checked,FirstOnly=FirstFrame.Checked};}
        public string Context{get{return Output.SelectedIndex+"|"+Preset.SelectedIndex+"|"+Dimensions.SelectedIndex+"|"+Quality.Value+"|"+Lossless.Checked+"|"+FirstFrame.Checked;}}
    }
}
