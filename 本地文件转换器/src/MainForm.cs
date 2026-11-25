using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LocalConverter
{
    public sealed class MainForm : Form
    {
        readonly PdfEngine engine=new PdfEngine();
        readonly PreviewRenderer previewRenderer=new PreviewRenderer();
        readonly Settings settings=Settings.Load();
        readonly PdfAccessStore pdfAccess=new PdfAccessStore();
        AudioWorkspace audioWorkspace;ImageWorkspace imageWorkspace;Control pdfWorkspace;ModernButton pdfSection,audioSection,imageSection;
        readonly LinkLabel engineDownloadsLink=new LinkLabel{Dock=DockStyle.Bottom,Height=42,Text="引擎下载任务",LinkColor=Color.FromArgb(163,193,245),ActiveLinkColor=Color.White,VisitedLinkColor=Color.FromArgb(163,193,245),Visible=false,Font=new Font("Microsoft YaHei UI",9)};
        readonly System.Windows.Forms.Timer engineDownloadsTimer=new System.Windows.Forms.Timer{Interval=400};
        readonly string[] operations={"转换","页面编辑","合并","拆分","水印","加密","移除加密","压缩"};
        readonly Dictionary<string,FunctionTab> tabs=new Dictionary<string,FunctionTab>();
        readonly List<Control> controls=new List<Control>();
        string mode="转换",previewSource="";
        int currentPage,pageCount,previewRotation;
        FileCards cards;
        PageCanvas pages;
        PageDocument document;
        PreviewPanel quickPreview;
        PdfPagePreview pagePicture;
        Label heading,subtitle,fileCount,previewTitle,pageNumber,status;
        Panel previewHost,optionHost;
        TableLayoutPanel leftLayout;
        FlowLayoutPanel editToolbar,options;
        ModernButton start,download,layoutPreview,undo;
        ModernButton upFile,downFile,rotateFile,removePagesButton,rotatePagesButton,selectPagesButton;
        SmoothProgress progress;
        CheckBox merge,overwrite;
        ComboBox format,quality,htmlMode,previewPart;
        SplitSettings splitSettings;
        WatermarkSettings watermarkSettings;
        bool layingOutOptions,optionsLayoutPending;
        Control rotateDivider;
        Label splitOutputHint;
        string ActionText{get{return mode=="页面编辑"?"生成编辑结果":mode=="拆分"&&splitSettings.Method.SelectedIndex==2?"开始裁剪":"开始"+mode;}}
        TextBox newPassword;
        readonly ToolResultStore toolResults=new ToolResultStore();
        ResultSession results { get { return toolResults.Get(mode); } set { toolResults.Set(mode,value,ResultContext()); } }
        CancellationTokenSource running,previewCancel,thumbCancel,loadCancel;
        Task previewTask=Task.FromResult(0),thumbTask=Task.FromResult(0);
        Task admissionTask=Task.FromResult(0);
        bool changing,loading,closeReady,closing,closeAfterTask;
        float uiScale=1;
        public MainForm(string[] args)
        {
            Text="文件万能处理器 · PDF 处理";Font=new Font("Microsoft YaHei UI",10F);ForeColor=Theme.Ink;BackColor=Theme.Background;
            Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);AutoScaleMode=AutoScaleMode.None;ClientSize=new Size(1320,840);MinimumSize=new Size(1100,760);StartPosition=FormStartPosition.CenterScreen;
            Build();using(var g=CreateGraphics())uiScale=g.DpiX/96F;if(Math.Abs(uiScale-1)>0.01)Scale(new SizeF(uiScale,uiScale));SetMode("转换");if(args.Length>0)Shown+=(s,e)=>AddFiles(args);
            engineDownloadsLink.LinkClicked+=(s,e)=>audioWorkspace.ShowDownloads();engineDownloadsTimer.Tick+=(s,e)=>{var tasks=audioWorkspace.BackgroundDownloads.Snapshots;engineDownloadsLink.Visible=tasks.Length>0;int active=tasks.Count(t=>t.Active);engineDownloadsLink.Text=active>0?"引擎下载（"+active+"项）":tasks.Any(t=>t.State==EngineTaskState.Failed||t.State==EngineTaskState.Cancelled)?"引擎下载 · 查看任务":"引擎资源 · 已完成";};engineDownloadsTimer.Start();Disposed+=(s,e)=>engineDownloadsTimer.Dispose();
            FormClosing+=ClosingForm;FormClosed+=(s,e)=>{engineDownloadsTimer.Stop();if(pagePicture.Image!=null)pagePicture.Image.Dispose();pdfAccess.Dispose();toolResults.Dispose();previewRenderer.Dispose();engine.Dispose();};
        }
        Label Label(string text,float size,Color color){return new Label{Text=text,Font=new Font(Font.FontFamily,size),ForeColor=color,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true};}
        ModernButton Button(string text,int width,EventHandler click,bool primary=false){var b=new ModernButton{Text=text,Width=(int)(width*uiScale),Height=(int)(40*uiScale),Primary=primary,Font=Font};b.Click+=click;return b;}
        ModernButton ToolButton(string text,int width,EventHandler click){var b=Button(text,width,click);b.Appearance=ButtonAppearance.Toolbar;b.Height=36;return b;}
        Panel Divider(){return new Panel{Width=1,Height=20,BackColor=Color.FromArgb(205,215,230),Margin=new Padding(8,8,8,0)};}
        void Build()
        {
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,184));root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));Controls.Add(root);
            root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            var side=new TableLayoutPanel{Dock=DockStyle.Fill,BackColor=Color.FromArgb(24,40,67),Padding=new Padding(20,30,20,22),Margin=Padding.Empty,ColumnCount=1,RowCount=5};
            side.RowStyles.Add(new RowStyle(SizeType.Absolute,60));side.RowStyles.Add(new RowStyle(SizeType.Absolute,35));side.RowStyles.Add(new RowStyle(SizeType.Absolute,174));side.RowStyles.Add(new RowStyle(SizeType.Percent,100));side.RowStyles.Add(new RowStyle(SizeType.Absolute,85));root.Controls.Add(side,0,0);
            side.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));var brand=Label("文件万能\r\n处理器",17,Color.White);brand.Name="AppBrand";brand.TextAlign=ContentAlignment.MiddleCenter;side.Controls.Add(brand,0,0);side.Controls.Add(Label("工作空间",9,Color.FromArgb(144,166,198)),0,1);
            var navigation=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Margin=Padding.Empty};navigation.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));side.Controls.Add(navigation,0,2);
            pdfSection=Button("▤  PDF 处理",140,(s,e)=>SwitchWorkspace(false),true);audioSection=Button("♫  音频处理",140,(s,e)=>SwitchWorkspace(true));imageSection=Button("▧  图片处理",140,(s,e)=>ChangeWorkspace(2));
            int sectionRow=0;foreach(var section in new[]{pdfSection,audioSection,imageSection}){navigation.RowStyles.Add(new RowStyle(SizeType.Percent,100F/3));section.Dock=DockStyle.Fill;section.Margin=new Padding(0,0,0,12);section.Appearance=ButtonAppearance.Sidebar;navigation.Controls.Add(section,0,sectionRow++);}
            var sideInfo=new Panel{Dock=DockStyle.Fill,Margin=new Padding(0,28,0,0)};sideInfo.Controls.Add(new Label{Text="更多文件工具\r\n将在后续版本加入",Dock=DockStyle.Top,Height=60,ForeColor=Color.FromArgb(144,166,198),Font=new Font(Font.FontFamily,9)});sideInfo.Controls.Add(engineDownloadsLink);side.Controls.Add(sideInfo,0,3);side.Controls.Add(Label("离线处理 · 文件不上传\r\nv"+typeof(MainForm).Assembly.GetName().Version.ToString(3),8.5F,Color.FromArgb(155,180,211)),0,4);
            var workspaceHost=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty};root.Controls.Add(workspaceHost,1,0);
            var main=new TableLayoutPanel{Dock=DockStyle.Fill,Margin=Padding.Empty,Padding=new Padding(26,20,26,18),ColumnCount=1,RowCount=4};workspaceHost.Controls.Add(main);pdfWorkspace=main;
            audioWorkspace=new AudioWorkspace{Visible=false};workspaceHost.Controls.Add(audioWorkspace);imageWorkspace=new ImageWorkspace{Visible=false};workspaceHost.Controls.Add(imageWorkspace);audioWorkspace.BusyChanged+=RefreshSections;imageWorkspace.BusyChanged+=RefreshSections;
            side.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            foreach(int h in new[]{85,55,0,150})main.RowStyles.Add(new RowStyle(h==0?SizeType.Percent:SizeType.Absolute,h==0?100:h));
            var header=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2};header.RowStyles.Add(new RowStyle(SizeType.Absolute,49));header.RowStyles.Add(new RowStyle(SizeType.Percent,100));heading=Label("PDF 处理",25,Theme.Ink);subtitle=Label("",9.5F,Theme.Muted);header.Controls.Add(heading,0,0);header.Controls.Add(subtitle,0,1);main.Controls.Add(header,0,0);
            var top=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,AutoScroll=true,BackColor=Color.White,Margin=new Padding(0,0,0,12)};
            top.Paint+=(s,e)=>{using(var pen=new Pen(Color.FromArgb(224,231,241)))e.Graphics.DrawLine(pen,0,top.ClientSize.Height-1,top.ClientSize.Width,top.ClientSize.Height-1);};
            foreach(string op in operations){if(top.Controls.Count>0){var divider=Divider();divider.Height=20;divider.Margin=new Padding(7,11,7,0);top.Controls.Add(divider);}string value=op;var tab=new FunctionTab{Text=op,Width=op=="移除加密"||op=="页面编辑"?104:82,Height=42,Font=Font,Margin=Padding.Empty};tab.Click+=(s,e)=>SetMode(value);tabs[op]=tab;controls.Add(tab);top.Controls.Add(tab);}main.Controls.Add(top,0,1);
            var body=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,65));body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35));main.Controls.Add(body,0,2);
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));body.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            body.Paint+=(s,e)=>{using(var pen=new Pen(Color.FromArgb(211,221,235))) {float x=body.Width*0.65F-9*uiScale;e.Graphics.DrawLine(pen,x,0,x,body.Height);}};
            leftLayout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Margin=new Padding(0,0,18,0)};
            leftLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            leftLayout.SizeChanged+=(s,e)=>QueueOptionsLayout();
            foreach(int h in new[]{47,0,0,0,165})leftLayout.RowStyles.Add(new RowStyle(h==0?SizeType.Percent:SizeType.Absolute,h==0?100:h));body.Controls.Add(leftLayout,0,0);
            var tools=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,BackColor=Color.White};var add=Button("＋ 添加文件",116,(s,e)=>PickFiles());add.Accent=true;add.Height=36;
            upFile=ToolButton("↑ 上移",76,(s,e)=>MoveFile(-1));downFile=ToolButton("↓ 下移",76,(s,e)=>MoveFile(1));rotateFile=ToolButton("↻ 旋转图片",100,(s,e)=>{if(cards.SelectedFile!=null){cards.SelectedFile.Rotation=(cards.SelectedFile.Rotation+90)%360;cards.Invalidate();RefreshOutcome(false);SelectFile();}});
            upFile.Margin=downFile.Margin=rotateFile.Margin=Padding.Empty;rotateDivider=Divider();
            fileCount=Label("0 个文件",9,Theme.Muted);fileCount.Dock=DockStyle.None;fileCount.Size=new Size(85,36);tools.Controls.AddRange(new Control[]{add,Divider(),upFile,Divider(),downFile,rotateDivider,rotateFile,Divider(),fileCount});controls.AddRange(new Control[]{add,upFile,downFile,rotateFile});leftLayout.Controls.Add(tools,0,0);
            cards=new FileCards{Dock=DockStyle.Fill,Font=Font,Margin=new Padding(0,0,0,10)};cards.SelectionChanged+=SelectFile;cards.AddRequested+=PickFiles;cards.FilesDropped+=AddFiles;cards.RemoveRequested+=RemoveFile;leftLayout.Controls.Add(cards,0,1);controls.Add(cards);
            editToolbar=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=true,BackColor=Color.White};removePagesButton=ToolButton("删除选中页",112,(s,e)=>DeletePages(pages.Selected.ToArray()));removePagesButton.Danger=true;rotatePagesButton=ToolButton("↻ 旋转页面",112,(s,e)=>{if(document!=null){document.Rotate(pages.Selected);PagesChanged();}});undo=ToolButton("↶ 撤销",90,(s,e)=>{if(document!=null){document.Undo();pages.Selected.Clear();PagesChanged();}});selectPagesButton=ToolButton("全选",70,(s,e)=>{if(document==null)return;for(int i=0;i<document.Pages.Count;i++)pages.Selected.Add(i);pages.Invalidate();PageSelection();});removePagesButton.Margin=rotatePagesButton.Margin=undo.Margin=selectPagesButton.Margin=Padding.Empty;editToolbar.Controls.AddRange(new Control[]{removePagesButton,Divider(),rotatePagesButton,Divider(),undo,Divider(),selectPagesButton});leftLayout.Controls.Add(editToolbar,0,2);controls.Add(editToolbar);
            pages=new PageCanvas{Dock=DockStyle.Fill,Font=Font};pages.SelectionChanged+=PageSelection;pages.DeleteRequested+=DeletePages;pages.MoveRequested+=(indices,before)=>{if(document!=null){document.Move(indices,before);pages.Selected.Clear();PagesChanged();}};pages.VisiblePagesChanged+=ScheduleThumbnails;leftLayout.Controls.Add(pages,0,3);controls.Add(pages);
            optionHost=new Panel{Dock=DockStyle.Fill,Padding=new Padding(12,8,12,8),BackColor=Color.White};options=new WrappingOptionsPanel{Dock=DockStyle.Fill,WrapContents=true,AutoScroll=true,Margin=Padding.Empty,BackColor=Color.White};optionHost.Controls.Add(options);
            var optionsTitle=Label("处理选项",9.5F,Theme.Ink);optionsTitle.Font=new Font(Font.FontFamily,9.5F,FontStyle.Bold);optionsTitle.Dock=DockStyle.Top;optionsTitle.Height=30;optionHost.Controls.Add(optionsTitle);
            optionHost.Paint+=(s,e)=>{using(var pen=new Pen(Color.FromArgb(205,215,230))){e.Graphics.DrawLine(pen,0,0,optionHost.Width,0);e.Graphics.DrawLine(pen,0,optionHost.Height-1,optionHost.Width,optionHost.Height-1);}};
            options.SizeChanged+=(s,e)=>{LayoutOptions();QueueOptionsLayout();};leftLayout.Controls.Add(optionHost,0,4);
            format=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=110,AccessibleName="输出格式"};format.Items.AddRange(new object[]{"PDF","PNG","JPG","TXT","DOCX","XLSX","PPTX","HTML"});format.SelectedIndex=0;format.SelectedIndexChanged+=(s,e)=>{if(!changing)BuildOptions();};
            htmlMode=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,AccessibleName="HTML 输出方式"};htmlMode.Items.AddRange(new object[]{"保留页面外观（图片）","可编辑正文（文字）"});htmlMode.SelectedIndex=0;htmlMode.SelectedIndexChanged+=(s,e)=>RefreshOutcome(false);
            quality=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=220,AccessibleName="压缩方式"};quality.Items.AddRange(new object[]{"无损压缩（保留画质）","均衡压缩（降低图片质量）","优先小体积（图片更模糊）"});quality.SelectedIndex=0;
            splitSettings=new SplitSettings{Font=Font};splitSettings.SettingsChanged+=()=>{LayoutOptions();RefreshOutcome(false);UpdateCutPreview();};
            watermarkSettings=new WatermarkSettings{Font=Font};watermarkSettings.SettingsChanged+=()=>{if(changing)return;LayoutOptions();RefreshOutcome(false);UpdateWatermarkPreview();};
            merge=new CheckBox{Text="合并多个文件为一份 PDF",AutoSize=true,Checked=true,ForeColor=Theme.Ink,Margin=new Padding(4,6,12,8)};
            overwrite=new CheckBox{Text="直接修改原文件（自动备份原件）",AutoSize=true,Margin=new Padding(4,6,12,8)};
            newPassword=new TextBox{Width=175,UseSystemPasswordChar=true,AccessibleName="新的打开密码",Margin=new Padding(0,4,12,4)};
            controls.Add(optionHost);
            var previewBox=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,BackColor=Color.White,Padding=new Padding(14),Margin=Padding.Empty};previewBox.RowStyles.Add(new RowStyle(SizeType.Absolute,64));previewBox.RowStyles.Add(new RowStyle(SizeType.Percent,100));previewBox.RowStyles.Add(new RowStyle(SizeType.Absolute,48));body.Controls.Add(previewBox,1,0);
            previewBox.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            previewTitle=Label("文件预览",11,Theme.Ink);previewBox.Controls.Add(previewTitle,0,0);previewHost=new Panel{Dock=DockStyle.Fill,BackColor=Color.FromArgb(239,242,248)};pagePicture=new PdfPagePreview{Dock=DockStyle.Fill,BackColor=Color.FromArgb(239,242,248),Visible=false};quickPreview=new PreviewPanel();previewHost.Controls.Add(quickPreview);previewHost.Controls.Add(pagePicture);previewBox.Controls.Add(previewHost,0,1);
            var previewTools=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,Padding=new Padding(0,7,0,0)};var previous=Button("‹",34,(s,e)=>TurnPreview(-1));var next=Button("›",34,(s,e)=>TurnPreview(1));pageNumber=Label("",8.5F,Theme.Muted);pageNumber.Dock=DockStyle.None;pageNumber.Size=new Size(52,34);layoutPreview=Button("排版预览",92,async(s,e)=>await LayoutPreview());layoutPreview.Font=new Font(Font.FontFamily,8.5F);
            previous.Margin=next.Margin=new Padding(0,0,4,0);pageNumber.Margin=Padding.Empty;
            previewPart=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=122,Font=new Font(Font.FontFamily,8.5F),AccessibleName="裁剪保留区域",Visible=false};previewPart.Items.AddRange(new object[]{"保留：上方","保留：下方"});previewPart.SelectedIndex=0;previewPart.SelectedIndexChanged+=(s,e)=>{splitSettings.KeepSecond=previewPart.SelectedIndex==1;UpdateCutPreview();};
            previewTools.Controls.AddRange(new Control[]{previous,pageNumber,next,layoutPreview,previewPart});previewBox.Controls.Add(previewTools,0,2);controls.AddRange(new Control[]{previous,next,layoutPreview,previewPart});
            var footer=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(0,14,0,0)};footer.RowStyles.Add(new RowStyle(SizeType.Absolute,50));footer.RowStyles.Add(new RowStyle(SizeType.Absolute,14));footer.RowStyles.Add(new RowStyle(SizeType.Absolute,48));footer.RowStyles.Add(new RowStyle(SizeType.Percent,100));main.Controls.Add(footer,0,3);
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            footer.Paint+=(s,e)=>{using(var pen=new Pen(Color.FromArgb(205,215,230)))e.Graphics.DrawLine(pen,0,0,footer.Width,0);};
            start=Button("开始转换",220,async(s,e)=>{if(loadCancel!=null){loadCancel.Cancel();start.Enabled=false;status.Text="正在取消加载…";}else if(running!=null){running.Cancel();start.Enabled=false;status.Text="正在安全取消…";}else await ProcessFiles();},true);start.Dock=DockStyle.Fill;start.Font=new Font(Font.FontFamily,11,FontStyle.Bold);footer.Controls.Add(start,0,0);
            progress=new SmoothProgress{Dock=DockStyle.Fill,Margin=new Padding(4,4,4,3)};footer.Controls.Add(progress,0,1);
            download=Button("↓  下载处理结果",220,async(s,e)=>await Download());download.Primary=true;download.BackColor=Theme.Background;download.Visible=false;download.Dock=DockStyle.Fill;footer.Controls.Add(download,0,2);
            status=Label("添加文件开始，本地处理，不上传原文件。",8.5F,Theme.Muted);footer.Controls.Add(status,0,3);
            merge.CheckedChanged+=(s,e)=>RefreshOutcome(false);quality.SelectedIndexChanged+=(s,e)=>RefreshOutcome(false);overwrite.CheckedChanged+=(s,e)=>RefreshOutcome(false);
            newPassword.TextChanged+=(s,e)=>{if(!changing&&running==null&&!loading){toolResults.Invalidate(mode);RefreshOutcome(false);}};
        }
        void SetMode(string value)
        {
            if(running!=null||loading)return;
            if(document!=null&&document.CanUndo&&value!="页面编辑"&&MessageBox.Show(this,"离开页面编辑将清除尚未生成的页面修改，是否继续？",Text,MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
            mode=value;foreach(var pair in tabs){pair.Value.Active=pair.Key==value;pair.Value.Invalidate();}
            heading.Text="PDF 处理  /  "+value;
            subtitle.Text=value=="页面编辑"?"框选或 Ctrl / Shift 多选 · 拖动页面调整顺序 · 红色叉号删除单页 · 撤销可恢复":"转换、整理与保护你的文件。所有操作均在本机完成。";
            bool editing=value=="页面编辑";editToolbar.Visible=pages.Visible=editing;
            leftLayout.RowStyles[1].SizeType=editing?SizeType.Absolute:SizeType.Percent;leftLayout.RowStyles[1].Height=editing?105*uiScale:100;
            leftLayout.RowStyles[2].SizeType=SizeType.Absolute;leftLayout.RowStyles[2].Height=editing?48*uiScale:0;
            leftLayout.RowStyles[3].SizeType=editing?SizeType.Percent:SizeType.Absolute;leftLayout.RowStyles[3].Height=editing?100:0;
            leftLayout.RowStyles[4].Height=(editing?110:145)*uiScale;
            if(!editing){document=null;pages.Document=null;pages.ClearImages();pages.Selected.Clear();}
            BuildOptions();start.Text=ActionText;RefreshOutcome(true);SelectFile();
        }
        void SwitchWorkspace(bool audio){ChangeWorkspace(audio?1:0);}
        void RefreshSections(){pdfSection.Enabled=audioSection.Enabled=imageSection.Enabled=running==null&&!loading&&!audioWorkspace.Busy&&!imageWorkspace.Busy;}
        async void ChangeWorkspace(int section)
        {
            if(running!=null||loading||audioWorkspace.Busy||imageWorkspace.Busy)return;
            audioWorkspace.StopPlayback();if(section!=0)await StopVisuals();pdfWorkspace.Visible=section==0;audioWorkspace.Visible=section==1;imageWorkspace.Visible=section==2;
            if(section==0){pdfWorkspace.BringToFront();SelectFile();}else if(section==1)audioWorkspace.BringToFront();else imageWorkspace.BringToFront();
            pdfSection.Primary=section==0;audioSection.Primary=section==1;imageSection.Primary=section==2;pdfSection.Invalidate();audioSection.Invalidate();imageSection.Invalidate();Text="文件万能处理器 · "+(section==0?"PDF 处理":section==1?"音频处理":"图片处理");
        }
        void Caption(string value){options.Controls.Add(new Label{Text=value,AutoSize=true,Margin=new Padding(0,8,8,4),ForeColor=Theme.Muted});}
        Label Hint(string value){if(options.Controls.Count>0)options.SetFlowBreak(options.Controls[options.Controls.Count-1],true);var h=new Label{Text=value,AutoSize=true,Tag="hint",MaximumSize=new Size(Math.Max(100,options.ClientSize.Width-25),0),ForeColor=Theme.Muted,Font=new Font(Font.FontFamily,8.5F),Margin=new Padding(0,6,0,0)};options.SetFlowBreak(h,true);options.Controls.Add(h);return h;}
        void QueueOptionsLayout(){if(optionsLayoutPending||!IsHandleCreated||IsDisposed)return;optionsLayoutPending=true;BeginInvoke(new Action(()=>{optionsLayoutPending=false;if(!IsDisposed)LayoutOptions();}));}
        void LayoutOptions()
        {
            if(changing||layingOutOptions||options==null||quality==null||splitSettings==null||watermarkSettings==null)return;layingOutOptions=true;
            try
            {
                int width=Math.Max(180,options.ClientSize.Width-(int)(22*uiScale));
                foreach(Control control in options.Controls)if((string)control.Tag=="hint")control.MaximumSize=new Size(width,0);
                foreach(var combo in new[]{quality,format,htmlMode})
                {
                    combo.Font=Font;int textWidth=combo.Items.Cast<object>().Select(item=>TextRenderer.MeasureText(item.ToString(),combo.Font).Width).DefaultIfEmpty(80).Max();
                    int desired=textWidth+(int)(36*uiScale);combo.Width=Math.Min(width,desired);combo.DropDownWidth=desired;
                }
                splitSettings.SetScale(uiScale);splitSettings.Width=width;
                watermarkSettings.SetScale(uiScale);watermarkSettings.Width=width;
                options.PerformLayout();
                int content=options.Controls.Cast<Control>().Where(c=>c.Visible).Select(c=>c.Bottom+c.Margin.Bottom-options.AutoScrollPosition.Y).DefaultIfEmpty(0).Max();
                int cap=Math.Max((int)(145*uiScale),Math.Min((int)(340*uiScale),leftLayout.ClientSize.Height-(int)((mode=="页面编辑"?300:180)*uiScale)));
                leftLayout.RowStyles[4].Height=Math.Max((int)(112*uiScale),Math.Min(cap,content+optionHost.Padding.Vertical+(int)(36*uiScale)));
            }
            finally{layingOutOptions=false;}
        }
        void BuildOptions()
        {
            changing=true;options.SuspendLayout();options.Controls.Clear();
            if(mode=="转换")
            {
                Caption("输出格式");options.Controls.Add(format);options.SetFlowBreak(format,true);
                if((string)format.SelectedItem=="HTML"){Caption("网页方式");options.Controls.Add(htmlMode);options.SetFlowBreak(htmlMode,true);}
                merge.Visible=cards.Items.Count>1&&(string)format.SelectedItem=="PDF";
                if(merge.Visible){options.Controls.Add(merge);options.SetFlowBreak(merge,true);}
                string target=(string)format.SelectedItem;
                Hint(target=="PDF"?"支持 PDF、Word、Excel、PPT、图片和本地 HTML。Office 输入需对应桌面版；HTML 需 Edge/Chrome，远程资源不加载。":target=="XLSX"?"提取可识别表格；无表格时按行保存正文，扫描页保留图片。不恢复公式，不含 OCR。":target=="PPTX"?"每页一张幻灯片，以图片保留外观；文字、表格和图形不拆成可编辑对象。":target=="HTML"?"离线单个 HTML，图片内嵌；保留外观使用页面图片，可编辑正文不保留复杂排版。":target=="DOCX"?"重建可编辑正文，扫描页保留为图片；不还原复杂排版，不含 OCR。":"图片按页分别导出，TXT 提取文字层。其他输入会先转为 PDF，再导出所选格式。");
            }
            else if(mode=="压缩"){Caption("压缩方式");options.Controls.Add(quality);options.SetFlowBreak(quality,true);Hint("压缩率取决于原文件。已足够紧凑时保留原大小，不强行增大。");}
            else if(mode=="水印"){options.Controls.Add(watermarkSettings);options.SetFlowBreak(watermarkSettings,true);Hint("右侧实时预览，样式切换可继续调整角度、字号与透明度；过长文字自动缩小以适应页面。每个输入另存一份 PDF。");}
            else if(mode=="加密"){Caption("新的打开密码");options.Controls.Add(newPassword);options.SetFlowBreak(newPassword,true);Hint("使用 AES-256 加密。密码仅用于本次本地处理，不写入配置。");}
            else if(mode=="页面编辑"){options.Controls.Add(overwrite);options.SetFlowBreak(overwrite,true);var load=Button("加载 / 刷新页面",145,async(s,e)=>await LoadPages());options.Controls.Add(load);Hint("默认生成新文件；直接修改需确认，并在原目录保留 .bak 备份。");}
            // A full-width first control wraps naturally. Explicit FlowBreak on the first item
            // makes Framework FlowLayout include the next item's width in its scroll extent.
            else if(mode=="拆分"){options.Controls.Add(splitSettings);splitOutputHint=Hint("");}
            else {Hint(mode=="移除加密"?"文件在添加时验证打开密码；此处生成无密码副本，不提供密码破解。":"按列表顺序合并 PDF；也可加入 Word / 图片，先转为 PDF 后合并。");}
            options.ResumeLayout();changing=false;LayoutOptions();RefreshOutcome(false);RefreshCommands();UpdateCutPreview();UpdateWatermarkPreview();
        }
        void RefreshCommands()
        {
            if(cards==null||upFile==null)return;bool idle=running==null&&!loading;int selected=cards.SelectedIndex;
            upFile.Enabled=idle&&selected>0;downFile.Enabled=idle&&selected>=0&&selected<cards.Items.Count-1;
            Func<InputFile,bool> image=f=>new[]{".jpg",".jpeg",".png",".bmp",".tif",".tiff"}.Contains(Path.GetExtension(f.Path).ToLowerInvariant());
            rotateFile.Visible=mode=="转换"&&(string)format.SelectedItem=="PDF"&&cards.Items.Any(image);
            rotateDivider.Visible=rotateFile.Visible;
            rotateFile.Enabled=idle&&cards.SelectedFile!=null&&image(cards.SelectedFile);
            if(layoutPreview!=null)layoutPreview.Visible=cards.SelectedFile!=null&&(new[]{".doc",".docx"}.Contains(Path.GetExtension(cards.SelectedFile.Path).ToLowerInvariant())||OfficeConverter.Supports(cards.SelectedFile.Path)||HtmlConverter.Supports(cards.SelectedFile.Path))&&mode!="页面编辑";
            if(removePagesButton!=null){removePagesButton.Enabled=rotatePagesButton.Enabled=idle&&document!=null&&pages.Selected.Count>0;selectPagesButton.Enabled=idle&&document!=null&&document.Pages.Count>0;undo.Enabled=idle&&document!=null&&document.CanUndo;}
        }
        string ResultContext()
        {
            var text=new System.Text.StringBuilder(mode);
            foreach(var file in cards.Items){text.Append('|').Append(file.Path).Append(':').Append(file.Rotation);try{var info=new FileInfo(file.Path);text.Append(':').Append(info.Length).Append(':').Append(info.LastWriteTimeUtc.Ticks);}catch(IOException){}catch(UnauthorizedAccessException){}}
            if(mode=="转换"){text.Append('|').Append(format.SelectedItem).Append('|').Append(merge.Checked);if((string)format.SelectedItem=="HTML")text.Append('|').Append(htmlMode.SelectedIndex);}
            if(mode=="压缩")text.Append('|').Append(quality.SelectedIndex);
            if(mode=="拆分")text.Append('|').Append(splitSettings.Context);
            if(mode=="水印")text.Append('|').Append(watermarkSettings.Snapshot().Context);
            if(mode=="页面编辑"&&document!=null){text.Append('|').Append(document.Source);foreach(var page in document.Pages)text.Append('|').Append(page.Index).Append(':').Append(page.Rotation);}
            return text.ToString();
        }
        void RefreshOutcome(bool restoreStatus)
        {
            if(download==null||progress==null||running!=null||loading)return;
            var current=results;bool ready=current!=null&&current.Files.Length>0;bool same=ready&&toolResults.Matches(mode,ResultContext());
            download.Visible=ready;download.Text=same?"↓  下载"+(mode=="拆分"&&splitSettings.Method.SelectedIndex==2?"裁剪":mode)+"结果":"↓  下载"+mode+"上次结果（输入或参数已改变）";
            start.Primary=!ready;start.Invalidate();
            progress.Value=same?100:0;
            if(restoreStatus)status.Text=ready?(same?mode+"已有处理结果，可下载。":"当前输入或参数已改变，下载按钮仅保存本功能上次的结果。"):("当前功能尚未处理，请点击“"+ActionText+"”。");
        }
        void PickFiles()
        {
            using(var dialog=new OpenFileDialog{Multiselect=mode!="页面编辑",Title="添加本地文件",Filter=(mode=="转换"||mode=="合并")?"支持的文件|*.pdf;*.doc;*.docx;*.xls;*.xlsx;*.ppt;*.pptx;*.html;*.htm;*.jpg;*.jpeg;*.png;*.bmp;*.tif;*.tiff":"PDF 文件|*.pdf"})if(dialog.ShowDialog(this)==DialogResult.OK)AddFiles(dialog.FileNames);
        }
        void AddFiles(string[] paths)
        {
            if(running!=null||loading)return;
            admissionTask=AddFilesAsync(paths);
        }
        Task<string> AskPdfPassword(string path,Func<string,CancellationToken,Task<bool>> verify)
        {
            using(var dialog=new PdfPasswordDialog(path,verify,loadCancel.Token))return Task.FromResult(dialog.ShowDialog(this)==DialogResult.OK?dialog.Password:null);
        }
        async Task AddFilesAsync(string[] paths)
        {
            if(mode=="页面编辑"&&document!=null&&document.CanUndo&&MessageBox.Show(this,"重新加载会清除尚未生成的页面修改，是否继续？",Text,MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
            var supported=new[]{".pdf",".doc",".docx",".xls",".xlsx",".ppt",".pptx",".html",".htm",".jpg",".jpeg",".png",".bmp",".tif",".tiff"};int skipped=0;string problem="";
            loading=true;loadCancel=new CancellationTokenSource();SetBusy(true);start.Text="取消添加";progress.Indeterminate=true;
            try
            {
                await StopVisuals();int index=0;
                foreach(string raw in paths)
                {
                    loadCancel.Token.ThrowIfCancellationRequested();index++;string path=Path.GetFullPath(raw),ext=Path.GetExtension(path).ToLowerInvariant();
                    if(!File.Exists(path)||!supported.Contains(ext)||((mode!="转换"&&mode!="合并")&&ext!=".pdf")){skipped++;continue;}
                    status.Text="正在检查文件 "+index+" / "+paths.Length+" · "+Path.GetFileName(path);
                    try
                    {
                        if(ext==".pdf")
                        {
                            if(!pdfAccess.IsCurrent(path)){var old=cards.Items.Find(f=>f.Path.Equals(path,StringComparison.OrdinalIgnoreCase));if(old!=null)cards.Items.Remove(old);if(document!=null&&document.Source.Equals(path,StringComparison.OrdinalIgnoreCase)){document=null;pages.Document=null;pages.ClearImages();}}
                            if(!await pdfAccess.AdmitAsync(path,engine,loadCancel.Token,AskPdfPassword)){skipped++;continue;}
                        }
                        if(mode=="页面编辑")cards.Items.Clear();if(!cards.Items.Any(f=>f.Path.Equals(path,StringComparison.OrdinalIgnoreCase)))cards.Items.Add(new InputFile(path));
                    }
                    catch(IOException error){skipped++;problem=error.Message;}
                }
            }
            catch(OperationCanceledException){problem="已取消添加，已验证的文件保留。";}
            catch(Exception error){problem=error.Message;}
            finally{pdfAccess.Retain(cards.Items);loadCancel.Dispose();loadCancel=null;loading=false;progress.Indeterminate=false;SetBusy(false);cards.SelectedIndex=cards.Items.Count-1;cards.RefreshLayout();fileCount.Text=cards.Items.Count+" 个文件";BuildOptions();status.Text=problem!=""?problem:skipped>0?"已跳过 "+skipped+" 个未验证、取消或不支持的文件。":"文件已添加，可点击文件查看预览。";start.Text=ActionText;RefreshOutcome(false);SelectFile();if(closeAfterTask){closeAfterTask=false;Close();}}
        }
        void RemoveFile(int index){if(running!=null||loading)return;if(index<0||index>=cards.Items.Count)return;pdfAccess.Forget(cards.Items[index].Path);cards.Items.RemoveAt(index);cards.SelectedIndex=Math.Min(index,cards.Items.Count-1);cards.RefreshLayout();fileCount.Text=cards.Items.Count+" 个文件";BuildOptions();SelectFile();}
        void MoveFile(int delta){int i=cards.SelectedIndex,j=i+delta;if(i<0||j<0||j>=cards.Items.Count)return;var f=cards.Items[i];cards.Items[i]=cards.Items[j];cards.Items[j]=f;cards.SelectedIndex=j;cards.Invalidate();RefreshOutcome(false);RefreshCommands();}
        void SelectFile()
        {
            if(running!=null||loading)return;RefreshCommands();var file=cards.SelectedFile;
            if(mode=="页面编辑")
            {
                if(file!=null&&Path.GetExtension(file.Path).Equals(".pdf",StringComparison.OrdinalIgnoreCase)&& (document==null||document.Source!=file.Path))
                {
                    if(document!=null&&document.CanUndo&&MessageBox.Show(this,"切换文件将清除当前尚未生成的页面修改，是否继续？",Text,MessageBoxButtons.YesNo)!=DialogResult.Yes){cards.SelectedIndex=cards.Items.FindIndex(f=>f.Path==document.Source);cards.RefreshLayout();return;}
                    var ignored=LoadPages();
                }
                return;
            }
            if(file==null){CancelPreview();previewSource="";pageCount=0;pageNumber.Text="";quickPreview.ShowFile(null);pagePicture.Visible=false;quickPreview.Visible=true;previewTitle.Text="文件预览";return;}
            if(Path.GetExtension(file.Path).Equals(".pdf",StringComparison.OrdinalIgnoreCase)){previewSource=file.Path;currentPage=0;previewRotation=0;SchedulePreview();}
            else {CancelPreview();pagePicture.Visible=false;quickPreview.Visible=true;quickPreview.ShowFile(file);previewTitle.Text="源文件预览\r\n"+Path.GetFileName(file.Path);pageNumber.Text="正文 / 图片";previewSource="";}
        }
        void CancelPreview(){if(previewCancel!=null)previewCancel.Cancel();}
        void UpdateWatermarkPreview()
        {
            if(pagePicture==null)return;pagePicture.Watermark=mode=="水印"?watermarkSettings.Snapshot():null;pagePicture.Invalidate();
            if(mode=="水印"&&pagePicture.Visible&&pagePicture.Image!=null)previewTitle.Text=(pagePicture.Watermark.Applies(currentPage+1)?"水印预览 · 修改即生效":"水印预览 · 此页不加水印")+"\r\n"+Path.GetFileName(previewSource);
        }
        void UpdateCutPreview()
        {
            if(previewPart==null||splitSettings==null)return;
            bool active=mode=="拆分"&&splitSettings.Method.SelectedIndex==2;
            previewPart.Visible=active;pagePicture.CropEnabled=active;pagePicture.Horizontal=splitSettings.TopBottom.Checked;pagePicture.SecondPart=splitSettings.KeepSecond;float kept=(float)splitSettings.RatioPicker.Value/100F;pagePicture.Ratio=pagePicture.SecondPart?1-kept:kept;
            string first=pagePicture.Horizontal?"保留：上方":"保留：左侧",second=pagePicture.Horizontal?"保留：下方":"保留：右侧";
            if((string)previewPart.Items[0]!=first){previewPart.Items[0]=first;previewPart.Items[1]=second;}
            if(splitOutputHint!=null&&splitOutputHint.Parent==options){string hint=active?"每个输入文件生成一份裁剪后的 PDF，保留全部页数与顺序，不打包 ZIP。":"拆分结果统一下载为一个 ZIP；批量文件在 ZIP 中分文件夹保存。";if(splitOutputHint.Text!=hint){splitOutputHint.Text=hint;QueueOptionsLayout();}}
            if(start!=null&&running==null&&!loading)start.Text=ActionText;
            if(pagePicture.Visible&&pagePicture.Image!=null&&previewSource!="")previewTitle.Text=(active?"裁剪预览 · "+(pagePicture.SecondPart?second:first):"页面预览")+"\r\n"+Path.GetFileName(previewSource);
            pagePicture.Invalidate();
        }
        void SchedulePreview()
        {
            CancelPreview();var cts=new CancellationTokenSource();previewCancel=cts;Task old=previewTask;
            string path=previewSource,secret="";try{secret=pdfAccess.PasswordFor(path);}catch(IOException error){previewTitle.Text=error.Message;pagePicture.Visible=false;cts.Dispose();previewCancel=null;return;}int index=currentPage,rotation=previewRotation;
            previewTask=RenderPreview(old,path,index,rotation,secret,cts);
        }
        async Task RenderPreview(Task old,string source,int index,int rotation,string secret,CancellationTokenSource cts)
        {
            try
            {
                await old;cts.Token.ThrowIfCancellationRequested();await quickPreview.StopAsync();
                quickPreview.Visible=false;pagePicture.Visible=true;previewTitle.Text="正在加载第 "+(index+1)+" 页…\r\n"+Path.GetFileName(source);
                var rendered=await Task.Run(()=>previewRenderer.Render(source,index,rotation,1300,secret,cts.Token));
                cts.Token.ThrowIfCancellationRequested();pageCount=rendered.Count;pagePicture.PageWidth=rendered.Width;pagePicture.PageHeight=rendered.Height;pagePicture.PageIndex=index;
                using(var image=Image.FromFile(rendered.Path)){var bitmap=new Bitmap(image);var previous=pagePicture.Image;pagePicture.Image=bitmap;if(previous!=null)previous.Dispose();}
                previewTitle.Text="页面预览\r\n"+Path.GetFileName(source);pageNumber.Text=(index+1)+" / "+pageCount;UpdateCutPreview();UpdateWatermarkPreview();
            }
            catch(OperationCanceledException){}
            catch(Exception e){if(!cts.IsCancellationRequested){quickPreview.Visible=true;quickPreview.ShowFile(null);pagePicture.Visible=false;previewTitle.Text="预览不可用\r\n"+e.Message;}}
            finally{if(previewCancel==cts)previewCancel=null;cts.Dispose();}
        }
        void TurnPreview(int delta){if(running!=null||previewSource==""||mode=="页面编辑")return;int next=currentPage+delta;if(next<0||next>=pageCount)return;currentPage=next;SchedulePreview();}
        async Task LayoutPreview()
        {
            var file=cards.SelectedFile;if(file==null||running!=null||loading)return;
            string ext=Path.GetExtension(file.Path).ToLowerInvariant();if(ext!=".doc"&&ext!=".docx"&&!OfficeConverter.Supports(file.Path)&&!HtmlConverter.Supports(file.Path))return;
            loading=true;loadCancel=new CancellationTokenSource();var token=loadCancel.Token;SetBusy(true);start.Text="取消加载";try
            {
                await StopVisuals();string target=Path.Combine(engine.Workspace,Guid.NewGuid().ToString("N")+".pdf");status.Text="正在生成排版预览，首次启动对应组件可能较慢…";
                await Task.Run(()=>ExportDocument(file.Path,target,token));previewSource=target;currentPage=0;previewRotation=0;SchedulePreview();status.Text="排版预览已生成。";
            }
            catch(OperationCanceledException){status.Text="已取消排版预览。";}
            catch(Exception e){Error(e);}finally{loadCancel.Dispose();loadCancel=null;loading=false;SetBusy(false);start.Text=ActionText;if(closeAfterTask){closeAfterTask=false;Close();}}
        }
        async Task LoadPages()
        {
            if(loading||running!=null)return;var file=cards.SelectedFile;if(file==null||!file.Path.EndsWith(".pdf",StringComparison.OrdinalIgnoreCase)){status.Text="页面编辑请选择一份 PDF。";return;}
            if(document!=null&&document.Source==file.Path&&document.CanUndo&&MessageBox.Show(this,"刷新会清除尚未生成的页面修改，是否继续？",Text,MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
            loading=true;loadCancel=new CancellationTokenSource();var token=loadCancel.Token;SetBusy(true);start.Text="取消加载";try
            {
                await StopVisuals();status.Text="正在读取 PDF 页面…";string secret=pdfAccess.PasswordFor(file.Path);
                var info=await Task.Run(()=>engine.Run("inspect",new Dictionary<string,object>{{"files",new[]{file.Path}},{"password",secret}},token));
                var source=new FileInfo(file.Path);document=new PageDocument{Source=file.Path,OriginalLength=source.Length,OriginalModified=source.LastWriteTimeUtc};int count=System.Convert.ToInt32(info["count"]);
                document.Pages=Enumerable.Range(0,count).Select(i=>new PageItem{Index=i}).ToList();pages.Document=document;pages.ClearImages();pages.Selected.Clear();if(count>0)pages.Selected.Add(0);pages.RefreshLayout();status.Text="已加载 "+count+" 页，可拖动排序、框选和旋转。";
            }
            catch(OperationCanceledException){status.Text="已取消页面加载。";}
            catch(Exception e){document=null;pages.Document=null;pages.ClearImages();pages.Invalidate();Error(e);}finally{loadCancel.Dispose();loadCancel=null;loading=false;SetBusy(false);start.Text="生成编辑结果";RefreshOutcome(false);ScheduleThumbnails();PageSelection();if(closeAfterTask){closeAfterTask=false;Close();}}
        }
        void PageSelection()
        {
            RefreshCommands();if(document==null)return;undo.Enabled=document.CanUndo;
            if(pages.Selected.Count!=1){CancelPreview();pagePicture.Visible=false;quickPreview.Visible=true;quickPreview.ShowFile(null);previewTitle.Text=pages.Selected.Count>1?"已选中 "+pages.Selected.Count+" 页\r\n可批量删除、旋转或拖动排序":"选择一页查看预览";return;}
            int i=pages.Selected.First();if(i>=document.Pages.Count)return;var p=document.Pages[i];previewSource=document.Source;currentPage=p.Index;previewRotation=p.Rotation;SchedulePreview();
        }
        void DeletePages(IEnumerable<int> indices){try{if(document==null)return;document.Delete(indices);pages.Selected.Clear();PagesChanged();}catch(Exception e){Error(e);}}
        void PagesChanged(){pages.Document=document;pages.RefreshLayout();undo.Enabled=document!=null&&document.CanUndo;RefreshOutcome(false);PageSelection();}
        void ScheduleThumbnails()
        {
            if(loading||document==null||mode!="页面编辑"||running!=null)return;
            string secret;try{secret=pdfAccess.PasswordFor(document.Source);}catch(IOException error){status.Text=error.Message;return;}
            if(thumbCancel!=null)thumbCancel.Cancel();var cts=new CancellationTokenSource();thumbCancel=cts;var source=document;Task old=thumbTask;thumbTask=RenderThumbnails(old,source,secret,cts);
        }
        async Task RenderThumbnails(Task old,PageDocument source,string secret,CancellationTokenSource cts)
        {
            try
            {
                await old;await Task.Delay(120,cts.Token);var needed=pages.VisibleOriginals().Where(i=>!pages.Thumbnails.ContainsKey(i)).ToArray();if(needed.Length==0)return;
                string folder=Path.Combine(engine.Workspace,"thumb-"+Guid.NewGuid().ToString("N"));
                var result=await Task.Run(()=>engine.Run("render",new Dictionary<string,object>{{"files",new[]{source.Source}},{"password",secret},{"folder",folder},{"indices",needed},{"edge",230}},cts.Token));
                cts.Token.ThrowIfCancellationRequested();var paths=PdfEngine.Outputs(result);for(int i=0;i<needed.Length;i++)pages.Thumbnails[needed[i]]=paths[i];
                while(pages.Thumbnails.Count>80){int first=pages.Thumbnails.Keys.First();OutputFiles.DeleteTemporary(pages.Thumbnails[first]);pages.Thumbnails.Remove(first);}pages.Invalidate();
            }
            catch(OperationCanceledException){}catch(Exception e){if(!cts.IsCancellationRequested)status.Text="缩略图加载失败："+e.Message;}
            finally{if(thumbCancel==cts)thumbCancel=null;cts.Dispose();}
        }
        async Task StopVisuals(){CancelPreview();if(thumbCancel!=null)thumbCancel.Cancel();await previewTask;await thumbTask;await quickPreview.StopAsync();}
        void SetBusy(bool busy){foreach(var c in controls)c.Enabled=!busy;optionHost.Enabled=!busy;download.Enabled=!busy;start.Enabled=true;RefreshCommands();RefreshSections();}
        void Error(Exception e){status.Text=e.Message;MessageBox.Show(this,e.Message,"处理提示",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        async Task ProcessFiles()
        {
            if(!admissionTask.IsCompleted)await admissionTask;if(loading||running!=null)return;
            var changed=cards.Items.Where(f=>f.Path.EndsWith(".pdf",StringComparison.OrdinalIgnoreCase)&&!pdfAccess.IsCurrent(f.Path)).Select(f=>f.Path).ToArray();if(changed.Length>0){await AddFilesAsync(changed);if(loading||changed.Any(p=>!pdfAccess.IsCurrent(p)))return;}
            if(cards.Items.Count==0){status.Text="请先添加文件。";return;}
            if(results!=null&&!results.Downloaded&&results.Files.Length>0&&MessageBox.Show(this,"上次结果还未下载，继续处理会清除上次结果，是否继续？",Text,MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
            if(mode=="页面编辑"&&document==null){status.Text="请先加载 PDF 页面。";return;}
            if(mode=="加密"&&newPassword.Text.Length==0){status.Text="请设置新的打开密码。";return;}
            Dictionary<string,object> splitValues=null;
            if(mode=="拆分"){try{splitValues=splitSettings.JobValues();}catch(Exception e){Error(e);return;}}
            WatermarkSpec watermark=null;if(mode=="水印"){try{watermark=watermarkSettings.Snapshot();watermark.Validate();}catch(Exception e){Error(e);return;}}
            bool direct=mode=="页面编辑"&&overwrite.Checked;
            if(direct&&MessageBox.Show(this,"将直接修改：\r\n"+document.Source+"\r\n原件会自动保存为同目录 .bak 备份。确认保存？",Text,MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
            var files=cards.Items.Select(f=>new InputFile(f.Path){Rotation=f.Rotation}).ToList();Dictionary<string,string> passwords;
            try{passwords=pdfAccess.Snapshot(files);}catch(Exception error){Error(error);return;}
            results=new ResultSession();download.Visible=false;
            string newSecret=newPassword.Text,target=(string)format.SelectedItem;bool combine=merge.Checked;string action=mode;
            string compression=quality.SelectedIndex==0?"lossless":quality.SelectedIndex==1?"balanced":"small";string htmlStyle=htmlMode.SelectedIndex==0?"appearance":"text";var editor=document;
            running=new CancellationTokenSource();SetBusy(true);start.Text="取消处理";progress.Value=0;
            var reporter=new Progress<Tuple<int,string>>(p=>{progress.Value=p.Item1;status.Text=p.Item2;});Action<int,string> report=(p,s)=>((IProgress<Tuple<int,string>>)reporter).Report(Tuple.Create(p,s));
            try
            {
                await StopVisuals();var token=running.Token;
                string note=await Task.Run(()=>action=="水印"?WatermarkJob.Run(engine,files.Select(f=>f.Path).ToArray(),results.Folder,"",watermark,token,report,passwords):Execute(files,action,target,combine,passwords,newSecret,compression,editor,splitValues,htmlStyle,token,report));
                token.ThrowIfCancellationRequested();
                if(direct)
                {
                    string backup=PageDocument.Overwrite(results.Files[0],editor.Source,editor.OriginalLength,editor.OriginalModified);results.Downloaded=true;status.Text="已修改原文件；备份："+backup;document=null;pages.Document=null;
                }
                else status.Text="处理完成，点击进度条下方下载。"+(String.IsNullOrEmpty(note)?"":" "+note);
                progress.Value=100;
            }
            catch(OperationCanceledException){status.Text="已取消。已完成的结果仍可下载，原文件未改动。";}
            catch(Exception e){Error(e);}
            finally{running.Dispose();running=null;SetBusy(false);start.Enabled=true;start.Text=ActionText;RefreshOutcome(false);changing=true;newPassword.Clear();changing=false;if(closeAfterTask){closeAfterTask=false;Close();}}
        }
        List<string> PreparePdf(List<InputFile> files,string folder,CancellationToken token,Action<int,string> report)
        {
            Directory.CreateDirectory(folder);var prepared=new List<string>();
            for(int i=0;i<files.Count;i++)
            {
                token.ThrowIfCancellationRequested();var input=files[i];string ext=Path.GetExtension(input.Path).ToLowerInvariant();report(i*45/files.Count,"正在准备 "+Path.GetFileName(input.Path));
                if(ext==".pdf")prepared.Add(input.Path);
                else if(ext==".doc"||ext==".docx"||OfficeConverter.Supports(input.Path)||HtmlConverter.Supports(input.Path)){string dest=Path.Combine(folder,(i+1)+"_"+Path.GetFileNameWithoutExtension(input.Path)+".pdf");ExportDocument(input.Path,dest,token);prepared.Add(dest);}
                else {string sub=Path.Combine(folder,"image-"+i);new ImageConverter().Convert(new[]{input},new ConversionOptions{OutputFolder=sub,MergeName="image"},token,(p,s)=>{});prepared.Add(Directory.GetFiles(sub,"*.pdf").Single());}
            }
            return prepared;
        }
        static void ExportDocument(string input,string output,CancellationToken token){if(OfficeConverter.Supports(input))OfficeConverter.ExportFile(input,output,token);else if(HtmlConverter.Supports(input))HtmlConverter.ExportFile(input,output,token);else WordConverter.ExportFile(input,output,token);}
        string Execute(List<InputFile> files,string action,string target,bool combine,Dictionary<string,string> passwords,string newSecret,string compression,PageDocument editor,Dictionary<string,object> splitValues,string htmlStyle,CancellationToken token,Action<int,string> report)
        {
            var values=new Dictionary<string,object>{{"files",files.Select(f=>f.Path).ToArray()},{"folder",results.Folder},{"passwords",passwords}};
            string code=action=="合并"?"merge":action=="加密"?"encrypt":action=="移除加密"?"decrypt":action=="压缩"?"compress":action=="页面编辑"?"edit":action=="拆分"?"split":"convert";
            if(action=="转换"&&target=="PDF"||action=="合并")
            {
                string preparedFolder=Path.Combine(engine.Workspace,"prepared-"+Guid.NewGuid().ToString("N"));var prepared=PreparePdf(files,preparedFolder,token,report);
                if(action=="合并"||(combine&&prepared.Count>1)){values["files"]=prepared.ToArray();return Note(engine.Run("merge",values,token,report));}
                for(int i=0;i<prepared.Count;i++){token.ThrowIfCancellationRequested();ResultSession.SaveCopy(prepared[i],results.Folder,Path.GetFileNameWithoutExtension(files[i].Path),token);report((i+1)*100/prepared.Count,"已生成 "+Path.GetFileName(files[i].Path));}return "";
            }
            if(action=="转换"&&files.Any(f=>!f.Path.EndsWith(".pdf",StringComparison.OrdinalIgnoreCase)))values["files"]=PreparePdf(files,Path.Combine(engine.Workspace,"prepared-"+Guid.NewGuid().ToString("N")),token,report).ToArray();
            else if(files.Any(f=>!f.Path.EndsWith(".pdf",StringComparison.OrdinalIgnoreCase)))throw new IOException("当前功能只处理 PDF，请先使用转换功能转为 PDF。");
            if(action=="转换"&&target=="DOCX")
            {
                values["format"]="docx";return Note(engine.Run("convert",values,token,report));
            }
            if(action=="转换")values["format"]=target.ToLowerInvariant();
            if(action=="转换"&&target=="HTML")values["htmlMode"]=htmlStyle;
            if(action=="加密")values["newPassword"]=newSecret;
            if(action=="压缩")values["quality"]=compression;
            if(action=="拆分")foreach(var entry in splitValues)values[entry.Key]=entry.Value;
            if(action=="页面编辑"){values["files"]=new[]{editor.Source};values["pages"]=editor.Pages.Select(p=>new Dictionary<string,object>{{"index",p.Index},{"rotation",p.Rotation}}).ToArray();}
            return Note(engine.Run(code,values,token,report));
        }
        static string Note(Dictionary<string,object> result){return result.ContainsKey("note")?(string)result["note"]:"";}
        async Task Download()
        {
            if(results==null||running!=null||results.Files.Length==0)return;string[] files=results.Files;string folder,name=null;
            if(files.Length==1)
            {
                string ext=Path.GetExtension(files[0]);using(var dialog=new SaveFileDialog{Title="下载到…（同名自动编号）",Filter=ext.TrimStart('.').ToUpperInvariant()+" 文件|*"+ext,DefaultExt=ext.TrimStart('.'),FileName=Path.GetFileName(files[0]),OverwritePrompt=false,InitialDirectory=Directory.Exists(settings.OutputFolder)?settings.OutputFolder:Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}){if(dialog.ShowDialog(this)!=DialogResult.OK)return;folder=Path.GetDirectoryName(dialog.FileName);name=Path.GetFileNameWithoutExtension(dialog.FileName);}
            }
            else using(var dialog=new FolderBrowserDialog{Description="选择 "+files.Length+" 份结果的下载位置（同名自动编号）",SelectedPath=settings.OutputFolder}){if(dialog.ShowDialog(this)!=DialogResult.OK)return;folder=dialog.SelectedPath;}
            await SaveResults(files,folder,name);
        }
        void ShowDownloadProgress(DownloadUpdate update)
        {
            progress.Value=update.Percent;progress.Indeterminate=update.Phase==SavePhase.Flushing;
            string size=DownloadJob.SizeText(update.Written)+" / "+DownloadJob.SizeText(update.Total);
            download.Text=update.Phase==SavePhase.Flushing?"正在完成保存 · "+size:"正在下载 · "+update.Percent+"% · "+size;
            string file=update.FileName.Length>48?update.FileName.Substring(0,45)+"…":update.FileName;
            status.Text=update.FileIndex+" / "+update.FileCount+" 份 · "+(update.Phase==SavePhase.Flushing?"正在写入磁盘，请稍候":DownloadJob.SizeText(update.BytesPerSecond)+"/s")+" · "+file;
            progress.AccessibleName="下载进度";progress.AccessibleDescription=update.Percent+"%";
        }
        async Task SaveResults(string[] files,string folder,string name)
        {
            var session=results;if(session==null||running!=null)return;
            string idleDownloadText=download.Text;int savedFiles=0;bool reporting=true;
            running=new CancellationTokenSource();SetBusy(true);start.Text="取消下载";download.Visible=true;download.Text="正在准备下载 · 0%";progress.Value=0;progress.Indeterminate=true;status.Text="正在准备保存 "+files.Length+" 份结果，统计文件大小…";
            var reporter=new Progress<DownloadUpdate>(update=>{if(!reporting||IsDisposed||running.Token.IsCancellationRequested)return;ShowDownloadProgress(update);});
            var token=running.Token;try
            {
                var saved=await Task.Run(()=>DownloadJob.Save(files,folder,name,token,update=>{Interlocked.Exchange(ref savedFiles,update.SavedFiles);((IProgress<DownloadUpdate>)reporter).Report(update);}));reporting=false;progress.Indeterminate=false;progress.Value=100;session.Downloaded=true;status.Text="已下载 "+saved.Length+" 份结果到 "+folder;settings.OutputFolder=folder;try{settings.Save();}catch(IOException){}catch(UnauthorizedAccessException){}
            }
            catch(OperationCanceledException){reporting=false;status.Text="已取消下载，已保存 "+savedFiles+" / "+files.Length+" 份；处理结果仍保留，可重试。";}
            catch(Exception e){reporting=false;Error(e);}
            finally{reporting=false;progress.Indeterminate=false;download.Text=idleDownloadText;running.Dispose();running=null;SetBusy(false);start.Text=ActionText;if(closeAfterTask){closeAfterTask=false;Close();}}
        }
        async void ClosingForm(object sender,FormClosingEventArgs e)
        {
            if(closeReady)return;e.Cancel=true;
            if(audioWorkspace.Busy||audioWorkspace.BackgroundDownloads.HasActive){if(MessageBox.Show(this,"取消音频任务和后台引擎下载并关闭？已下载片段会保留，之后可续传。",Text,MessageBoxButtons.YesNo)!=DialogResult.Yes)return;await audioWorkspace.StopAsync();}
            if(imageWorkspace.Busy){if(MessageBox.Show(this,"取消图片任务并关闭？",Text,MessageBoxButtons.YesNo)!=DialogResult.Yes)return;await imageWorkspace.StopAsync();}
            if(loading){if(!closeAfterTask&&MessageBox.Show(this,"取消加载并关闭软件？",Text,MessageBoxButtons.YesNo)==DialogResult.Yes){closeAfterTask=true;loadCancel.Cancel();}return;}
            if(running!=null){if(!closeAfterTask&&MessageBox.Show(this,"取消当前任务并关闭？",Text,MessageBoxButtons.YesNo)==DialogResult.Yes){closeAfterTask=true;running.Cancel();}return;}
            var pending=toolResults.PendingTools.Concat(audioWorkspace.PendingTools.Select(name=>"音频 / "+name)).Concat(imageWorkspace.PendingTools.Select(name=>"图片 / "+name)).ToArray();
            if(pending.Length>0&&MessageBox.Show(this,"以下功能还有结果未下载："+String.Join("、",pending)+"。关闭会清理临时结果，确认关闭？",Text,MessageBoxButtons.YesNo)!=DialogResult.Yes){closing=false;return;}
            if(document!=null&&document.CanUndo&&!closing&&MessageBox.Show(this,"页面修改尚在工作区，确认放弃并关闭？",Text,MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
            if(imageWorkspace.HasEdits&&!closing&&MessageBox.Show(this,"图片编辑仍在工作区，确认放弃并关闭？",Text,MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
            if(closing)return;closing=true;Enabled=false;await imageWorkspace.StopAsync();await StopVisuals();closeReady=true;BeginInvoke(new Action(Close));
        }
    }
}
