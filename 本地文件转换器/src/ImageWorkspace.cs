using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LocalConverter
{
    public sealed partial class ImageWorkspace : UserControl
    {
        readonly PdfEngine engine=new PdfEngine();readonly ToolResultStore outcomes=new ToolResultStore();readonly Settings settings=Settings.Load();
        readonly string work=Path.Combine(Path.GetTempPath(),"FileToolsImages-"+Guid.NewGuid().ToString("N"));
        readonly ImageFileStrip strip=new ImageFileStrip{Dock=DockStyle.Fill};readonly ImageCanvas canvas=new ImageCanvas{Dock=DockStyle.Fill};
        readonly Dictionary<string,FunctionTab> tabs=new Dictionary<string,FunctionTab>();readonly List<Control> commands=new List<Control>();
        readonly FlowLayoutPanel options=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,BackColor=Color.White,Padding=new Padding(14,8,12,12)};
        readonly FlowLayoutPanel editTools=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,WrapContents=false,BackColor=Color.White};
        readonly ComboBox format=Combo(new[]{"PNG","JPG","WebP","AVIF","GIF","BMP","TIFF","ICO","JP2"}),grayMode=Combo(new[]{"灰度照片（保留层次）","纯黑白（二值化）"}),editTool=Combo(new[]{"裁剪","马赛克","添加文字","添加图标","叠加图片","画笔","矩形","椭圆","箭头","尺寸调整","亮度 / 对比度","橡皮擦"}),icons=Combo(new[]{"星","勾","叉","圆","方","三角"}),language=Combo(new[]{"简体中文 + English","English"});
        readonly ImageFilterPicker filterPicker=new ImageFilterPicker();
        readonly BrushSizePicker sizePicker=new BrushSizePicker();
        readonly TextBox ocrText=new TextBox{Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,WordWrap=true,HideSelection=false,Height=170,Width=245,BackColor=Color.White,AccessibleName="识别文字内容"};
        readonly Label ocrSelection=new Label{Text="识别后，点击图中文字行查看内容",AutoSize=true,MaximumSize=new Size(245,0),ForeColor=Theme.Muted};
        readonly ComboBox ocrEnhance=Combo(new[]{"自动增强（推荐）","原图识别","浅色文档","深色背景","票证 / 浅色底纹"});
        readonly NumericUpDown brushSize=Number(1,1000,40),filterStrength=Number(0,100,100);
        readonly NumericUpDown quality=Number(1,100,85),edge=Number(0,30000,0),threshold=Number(0,255,128),block=Number(2,150,18),textSize=Number(6,600,40),strokeWidth=Number(1,100,5),opacity=Number(1,100,100),resizeWidth=Number(1,30000,1200),resizeHeight=Number(1,30000,800),brightness=Number(-100,100,0),contrast=Number(10,300,100);
        readonly CheckBox firstOnly=new CheckBox{Text="仅取第一帧 / 第一页",AutoSize=true},lossless=new CheckBox{Text="WebP 无损压缩",AutoSize=true},aspect=new CheckBox{Text="保持宽高比例",Checked=true,AutoSize=true},sparse=new CheckBox{Text="零散标签模式（适合分散的短文字）",AutoSize=true};
        readonly TextBox content=new TextBox{Text="请输入文字",Multiline=true,Height=75,Width=245,MaxLength=1000};readonly ColorSwatch color=new ColorSwatch{SelectedColor=Color.FromArgb(53,105,239)};
        readonly ImageWatermarkSettings watermark=new ImageWatermarkSettings();
        readonly ImageCompressionSettings compression=new ImageCompressionSettings();
        readonly CheckBox ocrLineBreaks=new CheckBox{Text="保留原图换行",AutoSize=true};
        readonly ModernButton start=Button("开始转换",true),download=Button("↓ 下载处理结果",true),resultPreview=Button("查看实际结果"),undo=Button("↶ 撤销"),redo=Button("↷ 重做"),copy=Button("复制选中文字");
        readonly Label title=new Label{Text="图片处理",Dock=DockStyle.Fill,Font=new Font("Microsoft YaHei UI",23)},detail=new Label{Dock=DockStyle.Fill,ForeColor=Theme.Ink,AutoEllipsis=true,Text="居中画布 · 原件保留"},status=new Label{Dock=DockStyle.Fill,ForeColor=Theme.Muted,AutoEllipsis=true},viewInfo=new Label{AutoSize=false,Width=130,Height=32,TextAlign=ContentAlignment.MiddleLeft,ForeColor=Theme.Muted};
        readonly SmoothProgress progress=new SmoothProgress{Dock=DockStyle.Fill};readonly System.Windows.Forms.Timer previewTimer=new System.Windows.Forms.Timer{Interval=180};
        readonly TableLayoutPanel body=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty},center=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Margin=Padding.Empty};
        string mode="格式转换",overlayPath,fontFamily="Microsoft YaHei";FontStyle textStyle;CancellationTokenSource cancellation;Task operation=Task.FromResult(0);int revision;bool changing,focusCanvas,reporting,pendingText,viewingResult;ImageAsset selectedTextAsset;ImageEdit selectedTextOriginal;public event Action BusyChanged;
        public bool Busy{get{return cancellation!=null;}}public string[] PendingTools{get{return outcomes.PendingTools;}}public bool HasEdits{get{return pendingText||strip.Items.Any(a=>a.Dirty);}}
        ImageAsset Selected{get{return strip.SelectedIndex>=0&&strip.SelectedIndex<strip.Items.Count?strip.Items[strip.SelectedIndex]:null;}}
        static ComboBox Combo(string[] values){var c=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=245};c.Items.AddRange(values);c.SelectedIndex=0;return c;}
        static NumericUpDown Number(int min,int max,int value){return new NumericUpDown{Minimum=min,Maximum=max,Value=value,Width=245};}
        static ModernButton Button(string text,bool primary=false){return new ModernButton{Text=text,Primary=primary,Height=34,Width=122,Margin=new Padding(3)};}
        static Panel Divider(){return new Panel{Width=1,Height=20,BackColor=Color.FromArgb(211,221,235),Margin=new Padding(6,8,6,4)};}
        public ImageWorkspace()
        {
            Directory.CreateDirectory(work);Dock=DockStyle.Fill;Font=new Font("Microsoft YaHei UI",9.5F);BackColor=Theme.Background;Padding=new Padding(22,16,22,12);
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=6,Margin=Padding.Empty};root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));foreach(int h in new[]{64,48,44,34,0,122})root.RowStyles.Add(new RowStyle(h==0?SizeType.Percent:SizeType.Absolute,h==0?100:h));Controls.Add(root);
            var heading=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2};heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));heading.RowStyles.Add(new RowStyle(SizeType.Absolute,42));heading.RowStyles.Add(new RowStyle(SizeType.Percent,100));heading.Controls.Add(title,0,0);heading.Controls.Add(new Label{Text="滚轮缩放 · 放大后右键拖动 · 适应画布恢复全图",Dock=DockStyle.Fill,ForeColor=Theme.Muted},0,1);root.Controls.Add(heading,0,0);
            var nav=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,AutoScroll=true,BackColor=Color.White,Margin=new Padding(0,4,0,4)};
            foreach(string name in new[]{"格式转换","图片编辑","滤镜","黑白化","水印","压缩","文字识别"}){if(nav.Controls.Count>0)nav.Controls.Add(Divider());string key=name;var tab=new FunctionTab{Text=name,Width=name.Length>3?97:70,Height=40,Margin=Padding.Empty};tab.Click+=(s,e)=>SetMode(key);tabs[name]=tab;nav.Controls.Add(tab);commands.Add(tab);}root.Controls.Add(nav,0,1);
            var fileTools=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,BackColor=Color.White};var add=Button("＋ 添加图片");add.Accent=true;ModernButton previous=Button("‹ 上一张"),next=Button("下一张 ›");previous.Appearance=next.Appearance=ButtonAppearance.Toolbar;previous.Width=next.Width=90;fileTools.Controls.AddRange(new Control[]{add,Divider(),previous,Divider(),next,Divider(),resultPreview});root.Controls.Add(fileTools,0,2);commands.AddRange(new Control[]{add,previous,next,resultPreview});add.Click+=(s,e)=>PickImages();previous.Click+=(s,e)=>MoveSelection(-1);next.Click+=(s,e)=>MoveSelection(1);resultPreview.Width=150;resultPreview.Click+=(s,e)=>Queue(viewingResult?(Func<CancellationToken,Task>)RenderSelected:ShowResult);
            root.Controls.Add(editTools,0,3);BuildEditToolbar();root.Controls.Add(body,0,4);body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,285));body.RowStyles.Add(new RowStyle(SizeType.Percent,100));body.Controls.Add(center,0,0);body.Controls.Add(options,1,0);options.Margin=new Padding(12,0,0,0);
            center.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));foreach(int h in new[]{30,0,37,94,1})center.RowStyles.Add(new RowStyle(h==0?SizeType.Percent:SizeType.Absolute,h==0?100:h));center.Controls.Add(detail,0,0);center.Controls.Add(canvas,0,1);center.Controls.Add(strip,0,3);
            var viewTools=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,BackColor=Color.White};ModernButton fit=Button("适应画布"),actual=Button("100%"),focus=Button("专注画布");foreach(var b in new[]{fit,actual,focus}){b.Appearance=ButtonAppearance.Toolbar;b.Width=92;}viewTools.Controls.AddRange(new Control[]{fit,Divider(),actual,Divider(),viewInfo,focus});center.Controls.Add(viewTools,0,2);fit.Click+=(s,e)=>canvas.Fit();actual.Click+=(s,e)=>canvas.ActualSize();focus.Click+=(s,e)=>{focusCanvas=!focusCanvas;options.Visible=!focusCanvas;body.ColumnStyles[1].Width=focusCanvas?0:285*ScaleFactor;strip.Visible=!focusCanvas;center.RowStyles[3].Height=focusCanvas?0:94*ScaleFactor;focus.Text=focusCanvas?"退出专注":"专注画布";};
            canvas.ViewChanged+=()=>{viewInfo.Text=(canvas.Zoom*100).ToString("0")+"%";UpdateCanvasStyle();};canvas.DraftFactory=CreateDraft;canvas.EditCommitted+=CommitEdit;canvas.TextChosen+=ChooseText;canvas.SelectionChanged+=()=>{UpdateOcrText();status.Text=canvas.Words.Any(w=>w.Selected)?"已选 "+canvas.Words.Count(w=>w.Selected)+" 行，右侧可查看、选择和复制文字。":"右侧显示全部识别文字。";};canvas.KeyDown+=(s,e)=>{if(e.Control&&e.KeyCode==Keys.C){CopyWords();e.SuppressKeyPress=true;}};canvas.AllowDrop=true;canvas.DragEnter+=(s,e)=>e.Effect=e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;canvas.DragDrop+=(s,e)=>QueueAdd((string[])e.Data.GetData(DataFormats.FileDrop));strip.FilesDropped+=QueueAdd;strip.SelectionChanged+=()=>{FlushText(false);ClearTextSelection();Queue(RenderSelected);};strip.RemoveRequested+=Remove;
            var footer=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(0,8,0,0),Margin=Padding.Empty};footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));foreach(int h in new[]{38,10,38,0})footer.RowStyles.Add(new RowStyle(h==0?SizeType.Percent:SizeType.Absolute,h==0?100:h));start.Dock=download.Dock=DockStyle.Fill;footer.Controls.Add(start,0,0);footer.Controls.Add(progress,0,1);footer.Controls.Add(download,0,2);footer.Controls.Add(status,0,3);footer.Paint+=(s,e)=>{using(var p=new Pen(Color.FromArgb(211,221,235)))e.Graphics.DrawLine(p,0,0,footer.Width,0);};root.Controls.Add(footer,0,5);
            start.Click+=(s,e)=>{if(Busy){cancellation.Cancel();start.Enabled=false;status.Text="正在安全取消…";}else Queue(Process);};download.Click+=(s,e)=>Download();
            foreach(var c in new[]{format,grayMode,editTool,icons,language})c.SelectedIndexChanged+=(s,e)=>{if(changing)return;if(c==editTool){FlushText(false);ClearTextSelection();}if(c==editTool||c==grayMode)BuildOptions();Changed(c==grayMode);if(c==editTool&&Selected!=null)Queue(RenderSelected);};foreach(var n in new[]{quality,edge,threshold,block,textSize,strokeWidth,opacity,resizeWidth,resizeHeight,brightness,contrast,brushSize,filterStrength})n.ValueChanged+=(s,e)=>Changed(n==threshold||n==filterStrength);foreach(var c in new[]{firstOnly,lossless,aspect,sparse})c.CheckedChanged+=(s,e)=>Changed(false);content.TextChanged+=(s,e)=>Changed(false);filterPicker.SelectionChanged+=()=>Changed(true);
            compression.Changed+=()=>Changed(false);ocrLineBreaks.CheckedChanged+=(s,e)=>{canvas.KeepTextLineBreaks=ocrLineBreaks.Checked;UpdateOcrText();};sizePicker.ValueChanged+=()=>{if(changing)return;var number=UsesStrokeSize?strokeWidth:brushSize;number.Value=sizePicker.Value;UpdateCanvasStyle();};sizePicker.GuideChanged+=shown=>{canvas.ShowBrushGuide=shown&&mode=="图片编辑";canvas.Invalidate();};
            content.KeyDown+=(s,e)=>{if(e.Control&&e.KeyCode==Keys.Enter){FlushText(true);e.SuppressKeyPress=true;}};content.Leave+=(s,e)=>textUpdateId=null;canvas.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Delete&&canvas.Tool=="文字"&&canvas.SelectedTextId!=null){DeleteSelectedText();e.SuppressKeyPress=true;}};color.Click+=(s,e)=>{using(var d=new ColorDialog{Color=color.SelectedColor,FullOpen=true})if(d.ShowDialog(this)==DialogResult.OK){color.SelectedColor=d.Color;Changed(false);}};watermark.Changed+=()=>Changed(false);watermark.LogoRequested+=()=>PickOverlay(true);previewTimer.Tick+=(s,e)=>{if(Busy)return;previewTimer.Stop();Queue(RenderSelected);};options.SizeChanged+=(s,e)=>FitOptions();SetMode("格式转换");
        }
        float ScaleFactor{get{using(var g=CreateGraphics())return g.DpiX/96F;}}
        void BuildEditToolbar()
        {
            editTools.Controls.Clear();var reset=Button("重置当前图");reset.Width=120;
            foreach(var b in new[]{undo,redo,reset}){b.Appearance=ButtonAppearance.Toolbar;editTools.Controls.Add(b);editTools.Controls.Add(Divider());commands.Add(b);}
            undo.Click+=(s,e)=>{FlushText(false);if(Selected==null||Selected.Edits.Count==0)return;var edit=Selected.Edits.Last();Selected.Edits.RemoveAt(Selected.Edits.Count-1);Selected.Redo.Add(edit);Selected.Dirty=Selected.Edits.Count>0;revision++;Queue(RenderSelected);};
            redo.Click+=(s,e)=>{FlushText(false);if(Selected==null||Selected.Redo.Count==0)return;var edit=Selected.Redo.Last();Selected.Redo.RemoveAt(Selected.Redo.Count-1);Selected.Edits.Add(edit);Selected.Dirty=true;revision++;Queue(RenderSelected);};
            reset.Click+=(s,e)=>{if(Selected==null||Selected.Edits.Count==0)return;if(MessageBox.Show(this,"放弃当前图的全部编辑？原文件保持不变。","重置图片",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;Selected.Edits.Clear();Selected.Redo.Clear();Selected.Dirty=false;revision++;Queue(RenderSelected);};
        }
        void Section(string text){if(options.Controls.Count>0)options.Controls.Add(new Panel{Height=1,Width=245,BackColor=Color.FromArgb(220,227,237),Margin=new Padding(0,14,0,8)});options.Controls.Add(new Label{Text=text,Font=new Font(Font,FontStyle.Bold),AutoSize=true,Margin=new Padding(0,5,0,5)});}
        void Field(string caption,Control control){options.Controls.Add(new Label{Text=caption,AutoSize=true,ForeColor=Theme.Muted,MaximumSize=new Size(245,0),Margin=new Padding(0,8,0,4)});options.Controls.Add(control);}
        void Hint(string value){options.Controls.Add(new Label{Text=value,AutoSize=true,MaximumSize=new Size(245,0),ForeColor=Theme.Muted,Margin=new Padding(0,12,0,5)});}
        void FitOptions(){float scale=ScaleFactor;int width=Math.Max(160,options.ClientSize.Width-options.Padding.Horizontal-(int)(18*scale));foreach(Control c in options.Controls){if(c is Label)((Label)c).MaximumSize=new Size(width,0);else if(!(c is ColorSwatch)&&!(c is CheckBox))c.Width=width;}filterPicker.FitWidth(width,scale);watermark.MaximumSize=new Size(width,0);watermark.MinimumSize=new Size(width,0);watermark.Width=width;foreach(Control c in watermark.Controls){if(c is Label)((Label)c).MaximumSize=new Size(width-8,0);else if(!(c is PositionGrid)&&!(c is ColorSwatch)&&!(c is CheckBox))c.Width=Math.Max(130,width-8);}}
        void BuildOptions()
        {
            changing=true;options.SuspendLayout();foreach(Control old in options.Controls.Cast<Control>().ToArray())if(old is Label&&old!=ocrSelection||old.GetType()==typeof(Panel)||old is ModernButton&&old!=copy)old.Dispose();options.Controls.Clear();Section(mode=="文字识别"?"识别结果":"处理设置");
            if(mode=="黑白化"){Field("黑白方式",grayMode);if(grayMode.SelectedIndex==1)Field("黑白阈值",threshold);Hint("预览与导出使用相同处理；灰度保留照片明暗，纯黑白适合文字稿。");}
            else if(mode=="滤镜"){Field("滤镜强度 %",filterStrength);Hint("点击下方缩略图选择，中央实时预览。0% 保留原图；按当前滤镜批量生成新图片。");options.Controls.Add(filterPicker);}
            else if(mode=="水印"){options.Controls.Add(watermark);Hint("文字或透明 Logo；预览与导出共用绘制。无透明通道的 Logo 会保留底色。");}
            else if(mode=="压缩"){compression.Configure(Selected,strip.Items);options.Controls.Add(compression);}
            else if(mode=="图片编辑")
            {
                Field("编辑工具",editTool);int tool=editTool.SelectedIndex;
                if(tool==1){Field("笔刷大小",sizePicker);Field("马赛克颗粒大小",block);Hint("拖动滑块看画布圆圈，圆圈内就是实际涂抹范围；颗粒大小只改变模糊块的粗细。");}
                if(tool==2){Field(selectedTextOriginal==null?"新文字内容":"修改已选文字",content);Field("字号（像素）",textSize);var font=Button("选择字体…");font.Click+=(s,e)=>{using(var d=new FontDialog{Font=new Font(fontFamily,Math.Min(180,(float)textSize.Value),textStyle),FontMustExist=true})if(d.ShowDialog(this)==DialogResult.OK){fontFamily=d.Font.FontFamily.Name;textStyle=d.Font.Style;Changed(false);}};options.Controls.Add(font);if(selectedTextOriginal!=null){var remove=Button("删除选中文字");remove.Danger=true;remove.Click+=(s,e)=>DeleteSelectedText();options.Controls.Add(remove);}Hint("点击画布上已有文字可重新编辑，输入及样式修改会实时显示。点击空白处可放置新文字；选中画布文字后按 Delete 可删除，撤销可恢复。");}
                if(tool==3)Field("图标（原创几何图形）",icons);
                if(tool==4){var add=Button("选择叠加图片…");add.Click+=(s,e)=>PickOverlay(false);options.Controls.Add(add);Hint(overlayPath==null?"先选择图片，再在画布拖出放置区域。":"已选择叠加图片，可重复放置。拖出的区域决定大小。");}
                if(tool>=2&&tool<=8){Field("颜色",color);Field("不透明度 %",opacity);if(tool>=5)Field(tool==5?"笔刷大小":"线条粗细",sizePicker);}
                if(tool==9){Field("宽度（像素）",resizeWidth);Field("高度（像素）",resizeHeight);options.Controls.Add(aspect);var apply=Button("应用尺寸");apply.Click+=(s,e)=>{if(canvas.Image==null)return;int w=(int)resizeWidth.Value,h=aspect.Checked?(int)Math.Max(1,Math.Round(w*(double)canvas.Image.Height/canvas.Image.Width)):(int)resizeHeight.Value;CommitEdit(new ImageEdit{Kind="resize",Rect=new Rectangle(0,0,w,h)});};options.Controls.Add(apply);}
                if(tool==10){Field("亮度（-100～100）",brightness);Field("对比度 %",contrast);var apply=Button("应用调整");apply.Click+=(s,e)=>CommitEdit(new ImageEdit{Kind="adjust",Value=(float)brightness.Value,Second=(float)contrast.Value/100});options.Controls.Add(apply);}
                if(tool==11){Field("橡皮大小",sizePicker);Hint("拖动滑块看圆圈，圈内就是擦除范围。只擦后加的文字、笔迹、图形、叠图和马赛克，不擦原照片。");}
                ModernButton rotate=Button("↻ 旋转 90°"),flipH=Button("水平翻转"),flipV=Button("垂直翻转");rotate.Click+=(s,e)=>CommitEdit(new ImageEdit{Kind="rotate"});flipH.Click+=(s,e)=>CommitEdit(new ImageEdit{Kind="flipH"});flipV.Click+=(s,e)=>CommitEdit(new ImageEdit{Kind="flipV"});Section("方向");options.Controls.AddRange(new Control[]{rotate,flipH,flipV});Hint("左键编辑，放大后右键拖动画布。拖动时预览图形，松开应用；笔刷沿圆圈涂抹。完成后点击下方生成结果，再预览文件或下载。");
            }
            else if(mode=="文字识别"){options.Controls.Add(ocrSelection);options.Controls.Add(ocrLineBreaks);options.Controls.Add(ocrText);var all=Button("显示 / 选择全部");all.Width=245;all.Click+=(s,e)=>canvas.SelectAllWords();copy.Text="复制显示文字";copy.Width=245;copy.Click-=CopyClick;copy.Click+=CopyClick;options.Controls.AddRange(new Control[]{all,copy});Section("识别设置");Field("识别语言",language);Field("识别增强",ocrEnhance);options.Controls.Add(sparse);Hint("点击或框选图中文字，右侧可查看、选择和复制。默认连成一段，不随识别框强制换行；需要时勾选保留原图换行。连续文章无需勾选零散标签模式。复杂图片请核对识别结果。");}
            if(mode!="文字识别"&&mode!="压缩")
            {
                Section("输出");Field("保存格式",format);Field("质量（JPG / WebP / AVIF）",quality);options.Controls.Add(lossless);if(mode=="压缩")Field("最长边（像素，0 表示不缩放）",edge);options.Controls.Add(firstOnly);Hint(mode=="格式转换"?"支持九种常见位图格式。动画 / 多页可转多帧格式；转静态格式须勾选仅取第一帧。透明图片转 JPG/BMP 时填白底。":mode=="压缩"?"降低有损质量或最长边可减小文件。PNG 无损优化不保证明显变小。处理后可查看实际结果与大小。":"黑白、水印和编辑按静态图片处理；多帧 / 多页必须明确勾选仅取第一帧。编辑只导出当前图，其他工具按全部图片批量处理。");
            }
            options.ResumeLayout();changing=false;UpdateCanvasStyle();FitOptions();
        }
        void CopyClick(object sender,EventArgs args){CopyWords();}
        void UpdateOcrText(){var selected=canvas.Words.Where(w=>w.Selected).ToArray();var shown=selected.Length>0?selected:canvas.Words.ToArray();string text=OcrTextFlow.Join(shown,ocrLineBreaks.Checked);if(ocrText.Text!=text){ocrText.Text=text;ocrText.SelectionStart=0;}ocrSelection.Text=shown.Length==0?"识别后点击图中文字，这里显示内容":selected.Length>0?"已选 "+selected.Length+" 个区域 · 可在下方选择文字":"全部 "+shown.Length+" 个区域 · 点击图中文字可单独查看";copy.Enabled=!Busy&&text.Length>0;}
        string CopyText{get{return ocrText.SelectionLength>0?ocrText.SelectedText:canvas.SelectedText.Length>0?canvas.SelectedText:mode=="文字识别"?ocrText.Text:"";}}
        void CopyWords(){string text=CopyText;if(text.Length==0)return;try{Clipboard.SetText(text);status.Text="已复制显示文字。";}catch(System.Runtime.InteropServices.ExternalException){status.Text="剪贴板正被占用，请稍后重试。";}}
        void SetMode(string value){if(Busy)return;FlushText(false);ClearTextSelection();previewTimer.Stop();viewingResult=false;mode=value;foreach(var t in tabs)t.Value.Active=t.Key==mode;title.Text="图片处理 / "+mode;editTools.Visible=mode=="图片编辑";((TableLayoutPanel)editTools.Parent).RowStyles[3].Height=mode=="图片编辑"?34*ScaleFactor:0;BuildOptions();canvas.Stamp=null;canvas.Words.Clear();UpdateOcrText();progress.Value=0;RefreshResult();if(Selected!=null)Queue(RenderSelected);else status.Text="添加图片并开始处理，完成后可预览生成文件、下载。";}
        string Context
        {
            get
            {
                string key=mode+"|"+String.Join("|",strip.Items.Select(a=>a.Identity));if(mode=="压缩")return key+"|"+compression.Context;key+="|"+format.SelectedItem+"|"+quality.Value+"|"+lossless.Checked+"|"+firstOnly.Checked;
                if(mode=="图片编辑")key+="|"+strip.SelectedIndex+"|"+revision;
                if(mode=="压缩")key+="|"+edge.Value;
                if(mode=="黑白化")key+="|"+grayMode.SelectedIndex+"|"+threshold.Value;
                if(mode=="水印")key+="|"+watermark.Context;
                if(mode=="滤镜")key+="|"+filterPicker.SelectedIndex+"|"+filterStrength.Value;
                if(mode=="图片编辑"&&pendingText)key+="|pending|"+content.Text+"|"+textSize.Value+"|"+opacity.Value+"|"+color.SelectedColor.ToArgb()+"|"+fontFamily+"|"+textStyle;
                return key;
            }
        }
        void Changed(bool render){if(changing)return;UpdateCanvasStyle();if(mode=="图片编辑"&&selectedTextOriginal!=null&&editTool.SelectedIndex==2){pendingText=true;FlushText(false);ScheduleTextPreview();}RefreshResult();if(mode=="水印"){canvas.Stamp=watermark.Snapshot();canvas.Invalidate();}if(render&&(mode=="黑白化"||mode=="滤镜")){previewTimer.Stop();previewTimer.Start();}}
        void MoveSelection(int delta){if(Busy||strip.Items.Count==0)return;FlushText(false);ClearTextSelection();strip.SelectedIndex=Math.Max(0,Math.Min(strip.Items.Count-1,strip.SelectedIndex+delta));strip.RefreshItems();Queue(RenderSelected);}
        void Remove(int index){if(Busy||index<0||index>=strip.Items.Count)return;var asset=strip.Items[index];if(asset.Edits.Count>0&&MessageBox.Show(this,"此图有尚未导出的编辑，移除并放弃？","移除图片",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;strip.Items.RemoveAt(index);strip.SelectedIndex=Math.Min(index,strip.Items.Count-1);strip.RefreshItems();revision++;RefreshResult();Queue(RenderSelected);}
        void PickImages(){using(var d=new OpenFileDialog{Title="选择本地图片",Multiselect=true,Filter="常见图片|*.jpg;*.jpeg;*.jfif;*.png;*.apng;*.webp;*.avif;*.gif;*.bmp;*.tif;*.tiff;*.ico;*.jp2;*.j2k|所有文件|*.*"})if(d.ShowDialog(this)==DialogResult.OK)QueueAdd(d.FileNames);}
        void QueueAdd(string[] paths){if(Busy)return;Queue(async token=>{var failures=new List<string>();foreach(var path in paths){token.ThrowIfCancellationRequested();if(strip.Items.Any(a=>String.Equals(a.Source,Path.GetFullPath(path),StringComparison.OrdinalIgnoreCase)))continue;try{var asset=await Prepare(path,token);strip.Items.Add(asset);}catch(OperationCanceledException){throw;}catch(Exception e){failures.Add(Path.GetFileName(path)+"："+e.Message);}}strip.SelectedIndex=strip.Items.Count-1;strip.RefreshItems();await RenderSelected(token);if(failures.Count>0)MessageBox.Show(this,String.Join("\r\n",failures.Take(8)),"部分图片未添加");});}
        async Task<ImageAsset> Prepare(string path,CancellationToken token)
        {
            path=Path.GetFullPath(path);var before=new FileInfo(path);long length=before.Length,ticks=before.LastWriteTimeUtc.Ticks;string folder=Path.Combine(work,Guid.NewGuid().ToString("N"));
            var info=await Task.Run(()=>engine.Run("image_preview",new Dictionary<string,object>{{"files",new[]{path}},{"folder",folder}},token));before.Refresh();if(before.Length!=length||before.LastWriteTimeUtc.Ticks!=ticks)throw new IOException("读取时图片发生变化，请重新添加。");
            return new ImageAsset{Source=path,Prepared=PdfEngine.Outputs(info)[0],Format=(string)info["format"],Frames=Convert.ToInt32(info["frames"]),Width=Convert.ToInt32(info["width"]),Height=Convert.ToInt32(info["height"]),Length=length,Ticks=ticks};
        }
        void PickOverlay(bool stamp)
        {
            if(Busy)return;using(var d=new OpenFileDialog{Title=stamp?"选择 Logo 图片":"选择叠加图片",Filter="常见图片|*.png;*.jpg;*.jpeg;*.webp;*.avif;*.bmp;*.gif;*.tif;*.tiff;*.ico;*.jp2"})if(d.ShowDialog(this)==DialogResult.OK){string path=d.FileName;Queue(async token=>{var asset=await Prepare(path,token);if(asset.Frames>1)throw new IOException("Logo / 叠加请使用静态图片。");if(stamp){watermark.LogoPath=asset.Prepared;Changed(false);}else{overlayPath=asset.Prepared;BuildOptions();}});}
        }
        async Task RenderSelected(CancellationToken token)
        {
            previewTimer.Stop();viewingResult=false;canvas.Words.Clear();UpdateOcrText();canvas.Stamp=null;var asset=Selected;if(mode=="压缩")compression.Configure(asset,strip.Items);
            if(asset==null){canvas.SetImage(null,false);canvas.SetEraseBase(null);canvas.TextTargets.Clear();detail.Text="居中画布 · 原件保留";return;}
            asset.Validate();var edits=mode=="图片编辑"?asset.Edits.ToArray():new ImageEdit[0];string current=mode;bool binary=grayMode.SelectedIndex==1;float cut=(float)threshold.Value;
            int preset=filterPicker.SelectedIndex;float intensity=(float)filterStrength.Value;bool erasing=current=="图片编辑"&&editTool.SelectedIndex==11;
            var rendered=await Task.Run(()=>{
                var result=ImageEditing.RenderLayers(asset.Prepared,edits,erasing);
                try{if(current=="黑白化"){var next=ImageEditing.Apply(result.Picture,new ImageEdit{Kind="gray"});result.Picture.Dispose();result.Picture=next;if(binary)ImageEditing.Apply(result.Picture,new ImageEdit{Kind="threshold",Value=cut});}
                if(current=="滤镜"){var next=ImageFilters.Apply(result.Picture,preset,intensity);result.Picture.Dispose();result.Picture=next;}return result;}catch{result.Dispose();throw;}
            },token);
            if(token.IsCancellationRequested){rendered.Dispose();token.ThrowIfCancellationRequested();}
            var bitmap=rendered.Picture;bool fit=canvas.Image==null||canvas.Image.Size!=bitmap.Size||!detail.Text.StartsWith(Path.GetFileName(asset.Source));canvas.SetImage(bitmap,fit);canvas.SetEraseBase(rendered.EraseBase);canvas.Stamp=mode=="水印"?watermark.Snapshot():null;
            canvas.TextTargets.Clear();if(mode=="图片编辑")canvas.TextTargets.AddRange(ImageEditing.TextTargets(edits,new Size(asset.Width,asset.Height)));
            if(selectedTextOriginal!=null){var target=canvas.TextTargets.FirstOrDefault(t=>t.Edit.Id==selectedTextOriginal.Id);if(target==null)ClearTextSelection();else LoadTextFields(target.Edit);}
            if(mode=="滤镜")filterPicker.SetSource(asset.Prepared);UpdateCanvasStyle();detail.Text=Path.GetFileName(asset.Source)+" · "+bitmap.Width+" × "+bitmap.Height+" · "+DownloadJob.SizeText(asset.Length)+(asset.Frames>1?" · "+asset.Frames+" 帧/页（预览第一帧）":"");
            undo.Enabled=asset.Edits.Count>0;redo.Enabled=asset.Redo.Count>0;status.Text=mode=="图片编辑"?"当前图 "+asset.Edits.Count+" 步编辑 · 左键编辑 / 放大后右键拖动 · 完成后生成结果。":mode=="滤镜"?ImageFilters.Presets[preset].Name+" · 强度 "+intensity+"% · 预览与导出一致。":"已加载 "+strip.Items.Count+" 张图片。开始处理后，可预览生成文件或下载。";RefreshResult();
        }
        bool UsesStrokeSize{get{return editTool.SelectedIndex>=5&&editTool.SelectedIndex<=8;}}
        float ImageDiameter(decimal visibleSize){return (float)visibleSize*canvas.DeviceDpi/96F/Math.Max(.01F,canvas.Zoom);}
        void UpdateCanvasStyle()
        {
            canvas.Tool=viewingResult?"查看":mode=="文字识别"?"文字选择":mode=="图片编辑"?new[]{"裁剪","马赛克","文字","图标","叠加图片","画笔","矩形","椭圆","箭头","查看","查看","橡皮擦"}[editTool.SelectedIndex]:"查看";
            canvas.BrushDiameter=ImageDiameter(editTool.SelectedIndex==5?strokeWidth.Value:brushSize.Value);canvas.BrushColor=color.SelectedColor;
            bool before=changing;changing=true;sizePicker.Configure(UsesStrokeSize?100:200,(int)(UsesStrokeSize?strokeWidth.Value:brushSize.Value),editTool.SelectedIndex==1||editTool.SelectedIndex==5||editTool.SelectedIndex==11);changing=before;canvas.Invalidate();
        }
        ImageEdit CreateDraft(Rectangle region,Point[] points)
        {
            if(mode!="图片编辑"||Selected==null||viewingResult)return null;int tool=editTool.SelectedIndex;
            string[] kinds={"crop","mosaic","text","icon","overlay","pen","rectangle","ellipse","arrow","","","erase"};if(kinds[tool]==""||tool==4&&overlayPath==null)return null;
            if(tool==2&&region.Width<3&&points.Length>0)region=new Rectangle(points[0],new Size(Math.Max(60,Math.Min(400,canvas.Image.Width-points[0].X)),Math.Max(30,(int)(textSize.Value*2))));
            return new ImageEdit{Kind=kinds[tool],Rect=region,Value=tool==1?(float)block.Value:(float)textSize.Value,Second=ImageDiameter(strokeWidth.Value),Diameter=canvas.BrushDiameter,Text=tool==3?icons.Text:content.Text,Family=fontFamily,FontStyle=textStyle,Path=overlayPath,Color=color.SelectedColor,Opacity=(float)opacity.Value,Points=points};
        }
        void ClearTextSelection(){textUpdateId=null;selectedTextOriginal=null;selectedTextAsset=null;pendingText=false;canvas.SelectedTextId=null;}
        void LoadTextFields(ImageEdit edit)
        {
            selectedTextOriginal=edit;selectedTextAsset=Selected;canvas.SelectedTextId=edit.Id;pendingText=false;changing=true;int caret=content.SelectionStart;
            if(content.Text!=edit.Text)content.Text=edit.Text;content.SelectionStart=Math.Min(caret,content.TextLength);textSize.Value=Math.Max(textSize.Minimum,Math.Min(textSize.Maximum,(decimal)edit.Value));opacity.Value=(decimal)edit.Opacity;color.SelectedColor=edit.Color;fontFamily=edit.Family;textStyle=edit.FontStyle;changing=false;
        }
        void ChooseText(ImageTextTarget target){FlushText(false);textUpdateId=null;if(target==null)ClearTextSelection();else{var latest=ImageEditing.TextTargets(Selected.Edits,new Size(Selected.Width,Selected.Height)).FirstOrDefault(t=>t.Edit.Id==target.Edit.Id);if(latest==null)ClearTextSelection();else LoadTextFields(latest.Edit);}BuildOptions();canvas.Invalidate();}
        void FlushText(bool render)
        {
            if(!pendingText||selectedTextOriginal==null||selectedTextAsset==null){if(render&&!textPreview.IsCompleted)Queue(RenderSelected);return;}previewTimer.Stop();pendingText=false;var original=selectedTextOriginal;
            if(original.Text==content.Text&&original.Value==(float)textSize.Value&&original.Opacity==(float)opacity.Value&&original.Color==color.SelectedColor&&original.Family==fontFamily&&original.FontStyle==textStyle)return;
            bool coalesce=textUpdateId!=null&&selectedTextAsset.Edits.Count>0&&selectedTextAsset.Edits.Last().Id==textUpdateId;
            if(!coalesce&&selectedTextAsset.Edits.Count>=100){status.Text="已达到100步编辑，请先导出。当前文字修改未应用。";LoadTextFields(original);return;}
            var edit=original.Copy();edit.Id=Guid.NewGuid().ToString("N");edit.Kind="retext";edit.TargetId=original.Id;edit.Text=content.Text;edit.Value=(float)textSize.Value;edit.Opacity=(float)opacity.Value;edit.Color=color.SelectedColor;edit.Family=fontFamily;edit.FontStyle=textStyle;
            using(var font=new Font(edit.Family,edit.Value,edit.FontStyle,GraphicsUnit.Pixel))using(var measure=new Bitmap(1,1))using(var g=Graphics.FromImage(measure))edit.Rect=new Rectangle(edit.Rect.Location,new Size(edit.Rect.Width,Math.Max(edit.Rect.Height,(int)Math.Ceiling(g.MeasureString(edit.Text,font,Math.Max(1,edit.Rect.Width)).Height)+4)));
            if(coalesce)selectedTextAsset.Edits[selectedTextAsset.Edits.Count-1]=edit;else selectedTextAsset.Edits.Add(edit);textUpdateId=edit.Id;selectedTextAsset.Redo.Clear();selectedTextAsset.Dirty=true;revision++;selectedTextOriginal=edit.Copy();selectedTextOriginal.Id=edit.TargetId;selectedTextOriginal.Kind="text";
            if(render&&Selected==selectedTextAsset)Queue(RenderSelected);else RefreshResult();
        }
        void CommitEdit(ImageEdit edit)
        {
            if(Busy||Selected==null)return;FlushText(false);
            if(edit.Kind=="resize"&&((long)edit.Rect.Width*edit.Rect.Height>40000000||edit.Rect.Width<1||edit.Rect.Height<1)){MessageBox.Show(this,"新尺寸需为正数且不超过 4000 万像素。","图片尺寸");return;}
            if(Selected.Edits.Count>=100){MessageBox.Show(this,"单张图片最多保留 100 步编辑，请先导出结果后继续。","图片编辑");return;}
            if(Selected.Frames>1&&!firstOnly.Checked){MessageBox.Show(this,"当前图含多帧/多页，编辑前请明确勾选“仅取第一帧 / 第一页”。","静态编辑");return;}
            Selected.Edits.Add(edit);Selected.Redo.Clear();Selected.Dirty=true;revision++;
            if(edit.Kind=="text"){LoadTextFields(edit);BuildOptions();}Queue(RenderSelected);
        }
        void Queue(Func<CancellationToken,Task> action){if(Busy)return;textUpdateId=null;textPreviewGeneration++;operation=RunAction(action);}
        async Task RunAction(Func<CancellationToken,Task> action)
        {
            cancellation=new CancellationTokenSource();SetBusy();progress.Indeterminate=true;try{await textPreview;cancellation.Token.ThrowIfCancellationRequested();await action(cancellation.Token);}catch(OperationCanceledException){status.Text="已取消；完整结果保留，未完成文件已清理。";}catch(Exception e){status.Text=e.Message;MessageBox.Show(this,e.Message,"图片处理提示",MessageBoxButtons.OK,MessageBoxIcon.Warning);}finally{reporting=false;progress.Indeterminate=false;cancellation.Dispose();cancellation=null;SetBusy();RefreshResult();}
        }
        void SetBusy(){foreach(var c in commands)c.Enabled=!Busy;options.Enabled=editTools.Enabled=!Busy&&!viewingResult;strip.Enabled=canvas.Enabled=!Busy;start.Enabled=true;start.Text=Busy?"取消当前操作":mode=="文字识别"?"开始识别文字":mode=="图片编辑"?"生成当前图编辑结果":"开始"+(mode=="格式转换"?"转换":mode);download.Enabled=!Busy;copy.Enabled=!Busy&&ocrText.TextLength>0;undo.Enabled=!Busy&&Selected!=null&&Selected.Edits.Count>0;redo.Enabled=!Busy&&Selected!=null&&Selected.Redo.Count>0;if(BusyChanged!=null)BusyChanged();}
        void RefreshResult(){var r=outcomes.Get(mode);bool available=mode!="文字识别"&&r!=null&&r.Files.Length>0;download.Visible=available;download.Text=outcomes.Matches(mode,Context)?"↓ 下载处理结果":"↓ 下载上次结果（设置或输入已改变）";resultPreview.Visible=available;resultPreview.Enabled=!Busy&&available;resultPreview.Text=viewingResult?"返回工作预览":outcomes.Matches(mode,Context)?"预览生成文件":"预览上次生成文件";if(!Busy)start.Enabled=Selected!=null;}
        async Task Process(CancellationToken token)
        {
            if(Selected==null)return;FlushText(false);previewTimer.Stop();if(viewingResult)await RenderSelected(token);if(mode=="文字识别"){await Recognize(token);return;}
            var old=outcomes.Get(mode);if(old!=null&&!old.Downloaded&&old.Files.Length>0&&MessageBox.Show(this,"重新处理会清除本功能未下载的结果，是否继续？","重新处理",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
            var assets=mode=="图片编辑"?new[]{Selected}:strip.Items.ToArray();bool first=firstOnly.Checked;string fmt=format.Text.ToLowerInvariant();bool webpLossless=lossless.Checked;int q=(int)quality.Value,longEdge=0;bool binary=grayMode.SelectedIndex==1;int cut=(int)threshold.Value;string current=mode,key=Context;var stamp=watermark.Snapshot();var edits=Selected.Edits.ToArray();int preset=filterPicker.SelectedIndex;float intensity=(float)filterStrength.Value;var compressionSpecs=current=="压缩"?assets.Select(a=>compression.Snapshot(a)).ToArray():null;
            foreach(var a in assets){a.Validate();if(mode!="格式转换"&&mode!="压缩"&&a.Frames>1&&!first)throw new IOException("黑白、水印和编辑仅处理静态图；多帧图片需勾选仅取第一帧。");}
            if(current=="水印"){if(stamp.Text!=null)stamp.Text.Validate();else if(String.IsNullOrEmpty(stamp.Logo))throw new IOException("请先选择 Logo 图片。");}
            var session=new ResultSession();outcomes.Set(mode,session,key);download.Visible=false;reporting=true;var reporter=new Progress<Tuple<int,string>>(p=>{if(!reporting||IsDisposed)return;progress.Indeterminate=false;progress.Value=p.Item1;status.Text=p.Item2;});
            try{await Task.Run(()=>{
                string staging=Path.Combine(work,"export-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(staging);
                try{for(int i=0;i<assets.Length;i++)
                {
                    token.ThrowIfCancellationRequested();int at=i;var a=assets[i];string path=a.Source;
                    if(current!="格式转换"&&current!="压缩")
                    {
                        using(var bitmap=ImageEditing.Render(a.Prepared,current=="图片编辑"?edits:new ImageEdit[0]))
                        {
                            Bitmap raster=bitmap;
                            try{if(current=="黑白化"){raster=ImageEditing.Apply(bitmap,new ImageEdit{Kind="gray"});if(binary)ImageEditing.Apply(raster,new ImageEdit{Kind="threshold",Value=cut});}if(current=="滤镜")raster=ImageFilters.Apply(bitmap,preset,intensity);if(current=="水印")using(var g=Graphics.FromImage(raster))stamp.Paint(g,new RectangleF(0,0,raster.Width,raster.Height),raster.Width,raster.Height);path=Path.Combine(staging,Path.GetFileNameWithoutExtension(a.Source)+".png");raster.Save(path,ImageFormat.Png);}finally{if(!Object.ReferenceEquals(raster,bitmap))raster.Dispose();}
                        }
                    }
                    var spec=compressionSpecs==null?new ImageCompressionSpec{Format=fmt,Quality=q,Edge=longEdge,Lossless=webpLossless,FirstOnly=first}:compressionSpecs[i];string folder=Path.Combine(staging,"out-"+i);var result=engine.Run("image_convert",new Dictionary<string,object>{{"files",new[]{path}},{"folder",folder},{"format",spec.Format},{"quality",spec.Quality},{"edge",spec.Edge},{"lossless",spec.Lossless},{"firstOnly",spec.FirstOnly}},token,(p,s)=>((IProgress<Tuple<int,string>>)reporter).Report(Tuple.Create((at*100+p)/assets.Length,s)));
                    foreach(string output in PdfEngine.Outputs(result)){token.ThrowIfCancellationRequested();string target=Path.Combine(session.Folder,(i+1)+"_"+Path.GetFileNameWithoutExtension(a.Source)+"_"+current+Path.GetExtension(output));File.Move(output,target);}
                    ((IProgress<Tuple<int,string>>)reporter).Report(Tuple.Create((i+1)*100/assets.Length,"已处理 "+(i+1)+" / "+assets.Length));
                }}finally{try{Directory.Delete(staging,true);}catch(IOException){}catch(UnauthorizedAccessException){}}
            },token);}finally{reporting=false;stamp.Dispose();}
            if(current=="图片编辑")assets[0].Dirty=false;progress.Value=100;long bytes=session.Files.Sum(p=>new FileInfo(p).Length),before=assets.Sum(a=>a.Length);status.Text="已完成 "+session.Files.Length+" 张 · "+DownloadJob.SizeText(before)+" → "+DownloadJob.SizeText(bytes)+"。可查看实际结果或下载。";
        }
        async Task Recognize(CancellationToken token)
        {
            var a=Selected;a.Validate();if(a.Frames>1&&!firstOnly.Checked){throw new IOException("文字识别仅展示第一帧/页，请先在转换中生成静态图片后再识别。");}status.Text="正在本地识别文字，首次加载模型需要稍等…";canvas.Words.Clear();UpdateOcrText();canvas.SetImage(await Task.Run(()=>ImageEditing.Load(a.Prepared),token),false);canvas.Stamp=null;
            var values=new Dictionary<string,object>{{"files",new[]{a.Prepared}},{"folder",work},{"language",language.SelectedIndex==1?"eng":"chi_sim+eng"},{"enhance",new[]{"auto","original","paper","dark","card"}[ocrEnhance.SelectedIndex]},{"sparse",sparse.Checked}};var result=await Task.Run(()=>engine.Run("image_ocr",values,token));foreach(Dictionary<string,object> word in (IEnumerable)result["words"])canvas.Words.Add(new ImageWord{Text=(string)word["text"],NewLine=true,Bounds=new RectangleF(Convert.ToSingle(word["x"]),Convert.ToSingle(word["y"]),Convert.ToSingle(word["w"]),Convert.ToSingle(word["h"]))});UpdateOcrText();canvas.Tool="文字选择";canvas.Invalidate();progress.Value=100;status.Text="识别到 "+canvas.Words.Count+" 行。点击图中文字行，右侧可查看、选择并复制内容。";
        }
        async Task ShowResult(CancellationToken token){var r=outcomes.Get(mode);if(r==null||r.Files.Length==0)return;FlushText(false);string path=r.Files[Math.Max(0,Math.Min(strip.SelectedIndex,r.Files.Length-1))];var a=await Prepare(path,token);canvas.Words.Clear();UpdateOcrText();canvas.TextTargets.Clear();canvas.Stamp=null;canvas.SetEraseBase(null);viewingResult=true;canvas.SetImage(await Task.Run(()=>ImageEditing.Load(a.Prepared),token),true);UpdateCanvasStyle();detail.Text="已生成文件 · "+Path.GetFileName(path)+" · "+a.Width+" × "+a.Height+" · "+DownloadJob.SizeText(a.Length);status.Text="正在预览已生成的文件；点击“返回工作预览”继续编辑。";}
        void Download()
        {
            if(Busy)return;var r=outcomes.Get(mode);if(r==null||r.Files.Length==0)return;string folder,name=null;var files=r.Files;
            if(files.Length==1){using(var d=new SaveFileDialog{Title="下载图片到…（同名自动编号）",FileName=Path.GetFileName(files[0]),Filter="图片文件|*"+Path.GetExtension(files[0]),OverwritePrompt=false,InitialDirectory=settings.OutputFolder}){if(d.ShowDialog(this)!=DialogResult.OK)return;folder=Path.GetDirectoryName(d.FileName);name=Path.GetFileNameWithoutExtension(d.FileName);}}
            else using(var d=new FolderBrowserDialog{Description="选择批量图片保存位置",SelectedPath=settings.OutputFolder}){if(d.ShowDialog(this)!=DialogResult.OK)return;folder=d.SelectedPath;}
            Queue(async token=>{bool active=true;status.Text="正在准备保存…";var reporter=new Progress<DownloadUpdate>(p=>{if(!active||IsDisposed)return;progress.Indeterminate=p.Phase==SavePhase.Flushing;progress.Value=p.Percent;download.Text="正在下载 · "+p.Percent+"% · "+DownloadJob.SizeText(p.Written)+" / "+DownloadJob.SizeText(p.Total);status.Text=p.FileIndex+" / "+p.FileCount+" · "+DownloadJob.SizeText(p.BytesPerSecond)+"/s · "+p.FileName;});try{await Task.Run(()=>DownloadJob.Save(files,folder,name,token,p=>((IProgress<DownloadUpdate>)reporter).Report(p)));active=false;r.Downloaded=true;progress.Value=100;status.Text="图片已下载到 "+folder;settings.OutputFolder=folder;try{settings.Save();}catch(IOException){}catch(UnauthorizedAccessException){}}finally{active=false;}});
        }
        public async Task StopAsync(){previewTimer.Stop();stoppingTextPreview=true;textPreviewGeneration++;try{if(cancellation!=null)cancellation.Cancel();await operation;await textPreview;}finally{stoppingTextPreview=false;}}
        protected override void Dispose(bool disposing){if(disposing){stoppingTextPreview=true;textPreviewGeneration++;previewTimer.Stop();previewTimer.Dispose();outcomes.Dispose();engine.Dispose();try{Directory.Delete(work,true);}catch(IOException){}catch(UnauthorizedAccessException){}}base.Dispose(disposing);}
    }
}
