using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace LocalConverter
{
    public class EngineDownloadsForm : Form
    {
        sealed class ResourceList : ScrollableControl
        {
            bool arranging;
            public ResourceList(){AutoScroll=true;DoubleBuffered=true;}
            protected override void OnLayout(LayoutEventArgs e)
            {
                if(arranging)return;arranging=true;
                try
                {
                    // Lay out against the actual viewport, never FlowLayout's cached virtual width.
                    int width=Math.Max(1,ClientSize.Width-Padding.Horizontal-SystemInformation.VerticalScrollBarWidth),top=Padding.Top+AutoScrollPosition.Y;
                    foreach(Control child in Controls){top+=child.Margin.Top;child.SetBounds(Padding.Left,top,width,child.Height);top+=child.Height+child.Margin.Bottom;}
                    AutoScrollMinSize=new Size(0,Math.Max(0,top-AutoScrollPosition.Y+Padding.Bottom));base.OnLayout(e);
                }
                finally{arranging=false;}
            }
            protected override void OnScroll(ScrollEventArgs e){base.OnScroll(e);PerformLayout();}
        }
        sealed class InfoBadge : Control
        {
            public InfoBadge(){Cursor=Cursors.Help;AccessibleName="用途说明";DoubleBuffered=true;}
            protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;float d=Math.Min(Width,Height)-4;var r=new RectangleF((Width-d)/2,(Height-d)/2,d,d);Theme.Fill(e.Graphics,Color.FromArgb(238,243,251),r,d/2);using(var font=new Font("Microsoft YaHei UI",9,FontStyle.Bold))Theme.Text(e.Graphics,"!",font,Theme.Muted,r,StringAlignment.Center);}
        }
        sealed class GroupHeading : TableLayoutPanel
        {
            readonly Label Caption;
            public GroupHeading(EngineDownloadsForm owner,string text,int count,bool first)
            {
                Height=owner.S(44);Margin=new Padding(0,first?0:owner.S(12),0,0);ColumnCount=3;RowCount=1;Padding=new Padding(0,owner.S(8),0,0);BackColor=Theme.Background;AccessibleName=text;AccessibleRole=AccessibleRole.Grouping;
                ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,owner.S(14)));ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,owner.S(90)));RowStyles.Add(new RowStyle(SizeType.Percent,100));
                var accent=new Panel{Width=owner.S(3),Height=owner.S(19),Anchor=AnchorStyles.Left,BackColor=Theme.Blue,Margin=Padding.Empty};Controls.Add(accent,0,0);Caption=owner.Label(text,15,Theme.Ink);Caption.Font=new Font(owner.Font.FontFamily,15,FontStyle.Bold);Controls.Add(Caption,1,0);var quantity=owner.Label(count+" 项资源",9,Theme.Muted);quantity.TextAlign=ContentAlignment.MiddleRight;Controls.Add(quantity,2,0);
                if(!first)Paint+=(s,e)=>{using(var pen=new Pen(Color.FromArgb(216,225,238)))e.Graphics.DrawLine(pen,0,0,Width,0);};
            }
        }
        sealed class ResourceRow : TableLayoutPanel
        {
            readonly EngineDownloadsForm owner;readonly EngineResource resource;readonly Label title,status,detail;readonly SmoothProgress progress;readonly ModernButton primary,secondary,uninstall;readonly InfoBadge info;EngineTaskSnapshot snapshot;bool installed,rendered,primaryShown,secondaryShown,uninstallShown;string renderedState;
            public ResourceRow(EngineDownloadsForm owner,EngineResource resource)
            {
                this.owner=owner;this.resource=resource;Name=resource.Package.Id;Height=owner.S(76);Margin=Padding.Empty;Padding=new Padding(owner.S(14),owner.S(9),owner.S(14),owner.S(9));BackColor=Color.White;ColumnCount=2;RowCount=3;
                ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,owner.S(252)));RowStyles.Add(new RowStyle(SizeType.Absolute,owner.S(26)));RowStyles.Add(new RowStyle(SizeType.Percent,100));RowStyles.Add(new RowStyle(SizeType.Absolute,owner.S(8)));
                var heading=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,owner.S(26)));title=owner.Label(resource.Title,10.5F,Theme.Ink);title.Font=new Font(owner.Font.FontFamily,10.5F,FontStyle.Bold);info=new InfoBadge{Dock=DockStyle.Fill,AccessibleDescription=resource.Usage};heading.Controls.Add(title,0,0);heading.Controls.Add(info,1,0);Controls.Add(heading,0,0);
                status=owner.Label("",9,Theme.Muted);status.TextAlign=ContentAlignment.MiddleRight;Controls.Add(status,1,0);detail=owner.Label("",9,Theme.Muted);detail.Margin=new Padding(0,0,owner.S(14),0);Controls.Add(detail,0,1);progress=new SmoothProgress{Dock=DockStyle.Fill,Margin=new Padding(0,owner.S(2),owner.S(14),owner.S(2))};Controls.Add(progress,0,2);
                var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,WrapContents=false,Margin=Padding.Empty,Padding=Padding.Empty};primary=owner.Button("下载",true);secondary=owner.Button("取消下载");secondary.Danger=true;uninstall=owner.Button("卸载");uninstall.Danger=true;actions.Controls.AddRange(new Control[]{uninstall,secondary,primary});Controls.Add(actions,1,1);SetRowSpan(actions,2);
                owner.tips.SetToolTip(info,resource.Usage);owner.tips.SetToolTip(title,resource.Title+"\r\n"+resource.Package.License);primary.Click+=(s,e)=>owner.TryAction(()=>{if(snapshot!=null&&snapshot.Active)owner.manager.Pause(resource.Package.Id);else owner.manager.Enqueue(new[]{resource.Package});});secondary.Click+=(s,e)=>owner.CancelDownload(resource.Package);uninstall.Click+=(s,e)=>owner.Remove(resource);
                Paint+=(s,e)=>{using(var pen=new Pen(Color.FromArgb(224,231,241)))e.Graphics.DrawLine(pen,owner.S(16),Height-1,Width-owner.S(16),Height-1);};
            }
            public void Update(EngineTaskSnapshot value)
            {
                snapshot=value;var store=owner.manager.Store;installed=store.Installed(resource.Package);bool managed=store.CanUninstall(resource.Package),busy=value!=null&&value.Active,inUse=store.InUse(resource.Package.Id);long fragment=store.PartialBytes(resource.Package);bool paused=!busy&&!installed&&(fragment>0||value!=null&&value.State==EngineTaskState.Paused);bool failed=value!=null&&value.State==EngineTaskState.Failed;
                string state=String.Join("|",installed,managed,inUse,fragment,value==null?"none":value.State.ToString(),value==null?0:value.Written,value!=null&&value.Cancelling,value==null?"":value.Message);if(rendered&&state==renderedState)return;
                bool showPrimary=false,showSecondary=false,showUninstall=false,newIndeterminate=false,oldPrimary=primary.Primary;int newProgress=0;
                SuspendLayout();try{
                detail.ForeColor=failed?Theme.Red:Theme.Muted;status.ForeColor=Theme.Muted;
                if(busy)
                {
                    bool mutation=value.State==EngineTaskState.Removing||value.State==EngineTaskState.Clearing;status.Text=value.State==EngineTaskState.Queued?"等待下载":value.State==EngineTaskState.Removing?"正在卸载":value.State==EngineTaskState.Clearing?"正在清理":value.State==EngineTaskState.Installing?"校验 / 安装中":"下载中 · "+value.Percent+"%";status.ForeColor=Theme.Blue;detail.Text=value.Message;newProgress=value.Percent;newIndeterminate=mutation||value.State==EngineTaskState.Installing;
                    if(!mutation){showPrimary=showSecondary=true;primary.Text=value.Cancelling?"正在停止…":"暂停";primary.Primary=false;primary.Enabled=secondary.Enabled=!value.Cancelling;}
                }
                else if(installed){status.Text="已下载";status.ForeColor=Color.FromArgb(25,145,111);newProgress=100;detail.Text=failed?value.Message:inUse?"正在处理文件，结束后可卸载。":InstalledHint();showUninstall=true;uninstall.Enabled=!inUse;}
                else
                {
                    newProgress=resource.Package.Bytes<=0?0:(int)Math.Min(99,fragment*100/resource.Package.Bytes);status.Text=failed?"需要重试":paused?"已暂停":managed?"需要修复":"未下载";detail.Text=failed?value.Message:paused?EngineResources.SizeText(fragment)+" / "+EngineResources.SizeText(resource.Package.Bytes)+" · 进度已保留":resource.Downloadable?"下载 "+EngineResources.SizeText(resource.Package.Bytes)+" · "+InstalledHint():"本地导入资源，没有固定下载源。";
                    showPrimary=true;primary.Enabled=resource.Downloadable;primary.Primary=true;primary.Text=resource.Downloadable?(failed?"重试":paused?"继续下载":"下载"):"需重新导入";showSecondary=paused||fragment>0;secondary.Enabled=true;showUninstall=managed;uninstall.Enabled=!inUse;
                }
                // Apply each final visibility once. Never hide/show buttons as a refresh reset.
                Show(primary,showPrimary,ref primaryShown);Show(secondary,showSecondary,ref secondaryShown);Show(uninstall,showUninstall,ref uninstallShown);
                if(progress.Value!=newProgress)progress.Value=newProgress;if(progress.Indeterminate!=newIndeterminate)progress.Indeterminate=newIndeterminate;if(oldPrimary!=primary.Primary)primary.Invalidate();
                if(owner.tips.GetToolTip(detail)!=detail.Text)owner.tips.SetToolTip(detail,detail.Text);string uninstallTip=inUse?"该资源正在使用，处理结束后可卸载。":resource.RemovalHint;if(owner.tips.GetToolTip(uninstall)!=uninstallTip)owner.tips.SetToolTip(uninstall,uninstallTip);progress.AccessibleName=resource.Title+"进度";progress.AccessibleDescription=installed?"已下载":"进度 "+newProgress+"%";renderedState=state;rendered=true;
                }finally{ResumeLayout();}
            }
            string InstalledHint(){return resource.Package.Id==SpeechCatalog.Sherpa.Id?"共用组件，只装一次；还需任一音色包。":resource.Package.Id==SpeechCatalog.Kokoro11.Id?"中文男女声 + 英语女声；与 v1.0 任选一套。":resource.Package.Id==SpeechCatalog.Kokoro10.Id?"英语男女声 + 中文男女声；与 v1.1 任选一套。":installed?"已安装，可离线使用。":"安装后离线使用。";}
            void Show(Control control,bool visible,ref bool previous){if(!rendered||previous!=visible){control.Visible=visible;previous=visible;}}
        }
        readonly EngineDownloadManager manager;readonly ToolTip tips=new ToolTip{AutoPopDelay=20000,InitialDelay=300,ReshowDelay=100};readonly ResourceList list=new ResourceList{Dock=DockStyle.Fill,Margin=Padding.Empty};readonly Dictionary<string,ResourceRow> rows=new Dictionary<string,ResourceRow>();readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer{Interval=600};readonly Label summary;readonly float scale;string inventory="";
        int S(int value){return (int)Math.Round(value*scale);}
        Label Label(string text,float size,Color color){return new Label{Text=text,Dock=DockStyle.Fill,Font=new Font(Font.FontFamily,size),ForeColor=color,TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true,Margin=Padding.Empty};}
        ModernButton Button(string text,bool primary=false){return new ModernButton{Text=text,Primary=primary,Font=Font,Width=S(76),Height=S(28),Margin=new Padding(S(6),0,0,0)};}
        public EngineDownloadsForm(EngineDownloadManager manager)
        {
            this.manager=manager;AutoScaleMode=AutoScaleMode.None;Font=new Font("Microsoft YaHei UI",9.5F);using(var g=CreateGraphics())scale=g.DpiX/96F;Text="功能扩展包下载";Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);var area=Screen.FromControl(this).WorkingArea;Size=new Size(Math.Min(S(980),area.Width-S(36)),Math.Min(S(760),area.Height-S(36)));MinimumSize=new Size(S(740),S(520));StartPosition=FormStartPosition.CenterParent;BackColor=Theme.Background;ShowInTaskbar=false;
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Padding=new Padding(S(22),S(15),S(22),S(10))};root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,S(56)));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));Controls.Add(root);
            var head=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};head.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));head.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,S(280)));head.RowStyles.Add(new RowStyle(SizeType.Percent,100));head.Controls.Add(Label("功能扩展包下载",21,Theme.Ink),0,0);summary=Label("",9,Theme.Muted);summary.TextAlign=ContentAlignment.MiddleRight;head.Controls.Add(summary,1,0);root.Controls.Add(head,0,0);root.Controls.Add(list,0,1);list.SizeChanged+=(s,e)=>FitRows();timer.Tick+=(s,e)=>RefreshTasks();RefreshTasks();timer.Start();
        }
        protected virtual bool ConfirmCancelDownload(EnginePackage package){using(var dialog=new CancelDownloadDialog(package.Name))return dialog.ShowDialog(this)==DialogResult.OK;}
        void CancelDownload(EnginePackage package){if(ConfirmCancelDownload(package))TryAction(()=>manager.Cancel(package));}
        void TryAction(Action action){try{action();RefreshTasks();}catch(Exception error){MessageBox.Show(this,error.Message,"引擎管理提示",MessageBoxButtons.OK,MessageBoxIcon.Warning);}}
        void Remove(EngineResource resource){if(manager.Store.InUse(resource.Package.Id)){RefreshTasks();return;}if(MessageBox.Show(this,"卸载“"+resource.Title+"”？\r\n\r\n"+resource.RemovalHint+"\r\n只删除此资源，不删除你的输入文件或处理结果。","卸载外置资源",MessageBoxButtons.OKCancel,MessageBoxIcon.Question,MessageBoxDefaultButton.Button2)!=DialogResult.OK)return;TryAction(()=>manager.Uninstall(resource.Package));}
        void FitRows(){list.PerformLayout();}
        void RefreshTasks()
        {
            var snapshots=manager.Snapshots;var resources=EngineResources.All(manager.Store).ToList();foreach(var task in snapshots)if(!resources.Any(r=>r.Package.Id==task.Package.Id))resources.Add(new EngineResource{Package=task.Package,Group="其他资源",Title=task.Package.Name,Usage="此资源由后台任务添加。",RemovalHint="对应功能暂不可用。"});string signature=String.Join("|",resources.Select(r=>r.Package.Id+":"+r.Title));
            if(signature!=inventory){inventory=signature;var scroll=list.AutoScrollPosition;list.SuspendLayout();foreach(Control control in list.Controls.Cast<Control>().ToArray())control.Dispose();list.Controls.Clear();rows.Clear();foreach(var group in resources.GroupBy(r=>r.Group)){list.Controls.Add(new GroupHeading(this,group.Key,group.Count(),list.Controls.Count==0));foreach(var resource in group){var row=new ResourceRow(this,resource);rows[resource.Package.Id]=row;list.Controls.Add(row);}}FitRows();list.ResumeLayout();list.AutoScrollPosition=new Point(-scroll.X,-scroll.Y);}
            int installed=0;foreach(var resource in resources){if(manager.Store.Installed(resource.Package))installed++;rows[resource.Package.Id].Update(snapshots.FirstOrDefault(s=>s.Package.Id==resource.Package.Id));}int active=snapshots.Count(s=>s.Active);summary.Text="已下载 "+installed+" / "+resources.Count+(active>0?"　·　进行中 "+active:" ");
        }
        protected override void Dispose(bool disposing){if(disposing){timer.Stop();timer.Dispose();tips.Dispose();}base.Dispose(disposing);}
    }
}
