using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace LocalConverter
{
    public sealed class EngineDownloadsForm : Form
    {
        sealed class Row : TableLayoutPanel
        {
            readonly Label name=new Label{Dock=DockStyle.Fill,AutoEllipsis=true},message=new Label{Dock=DockStyle.Fill,AutoEllipsis=true,ForeColor=Theme.Muted};readonly SmoothProgress progress=new SmoothProgress{Dock=DockStyle.Fill};readonly ModernButton action=new ModernButton{Width=84,Height=32,Anchor=AnchorStyles.Right};EngineTaskSnapshot snapshot;
            public Row(EngineDownloadManager manager)
            {
                Height=112;ColumnCount=2;RowCount=3;BackColor=Color.White;Padding=new Padding(12,10,12,10);Margin=new Padding(0,0,0,8);ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100));RowStyles.Add(new RowStyle(SizeType.Absolute,26));RowStyles.Add(new RowStyle(SizeType.Percent,100));RowStyles.Add(new RowStyle(SizeType.Absolute,10));Controls.Add(name,0,0);Controls.Add(message,0,1);Controls.Add(progress,0,2);Controls.Add(action,1,0);SetRowSpan(action,2);action.Click+=(s,e)=>{if(snapshot==null)return;if(snapshot.Active)manager.Cancel(snapshot.Package.Id);else manager.Enqueue(new[]{snapshot.Package});};
            }
            public void Update(EngineTaskSnapshot value)
            {
                snapshot=value;name.Text=value.Package.Name+" · "+(value.State==EngineTaskState.Queued?"排队中":value.State==EngineTaskState.Downloading?"下载中":value.State==EngineTaskState.Installing?"校验 / 安装中":value.State==EngineTaskState.Completed?"已完成":value.State==EngineTaskState.Cancelled?"已取消":"下载失败");
                message.Text=value.Message;message.ForeColor=value.State==EngineTaskState.Failed?Color.FromArgb(205,60,70):Theme.Muted;progress.Indeterminate=value.State==EngineTaskState.Installing;progress.Value=value.Percent;action.Text=value.Cancelling&&value.Active?"正在取消":value.Active?"取消":value.State==EngineTaskState.Cancelled?"继续":value.State==EngineTaskState.Failed?"重试":"已完成";action.Enabled=value.State!=EngineTaskState.Completed&&!(value.Cancelling&&value.Active);action.Danger=value.Active;
            }
        }
        readonly EngineDownloadManager manager;readonly FlowLayoutPanel list=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(14)};readonly Dictionary<string,Row> rows=new Dictionary<string,Row>();readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer{Interval=250};long revision=-1;
        public EngineDownloadsForm(EngineDownloadManager manager)
        {
            this.manager=manager;Text="引擎下载任务";Size=new Size(800,560);MinimumSize=new Size(620,380);StartPosition=FormStartPosition.CenterParent;Font=new Font("Microsoft YaHei UI",9.5F);BackColor=Theme.Background;ShowInTaskbar=false;var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2};root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,58));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.Controls.Add(new Label{Text="最多同时下载 / 安装 2 个资源，其余自动排队。共用引擎只下载一次。\r\n关闭此窗口不会停止任务，可以切到其他页面；取消任务后可继续下载。",Dock=DockStyle.Fill,Padding=new Padding(16,8,10,4),ForeColor=Theme.Muted},0,0);root.Controls.Add(list,0,1);Controls.Add(root);list.SizeChanged+=(s,e)=>FitRows();timer.Tick+=(s,e)=>RefreshTasks();timer.Start();RefreshTasks();
        }
        void FitRows(){foreach(var row in rows.Values)row.Width=Math.Max(360,list.ClientSize.Width-list.Padding.Horizontal-SystemInformation.VerticalScrollBarWidth-3);}
        void RefreshTasks(){if(revision==manager.Revision)return;revision=manager.Revision;foreach(var task in manager.Snapshots){Row row;if(!rows.TryGetValue(task.Package.Id,out row)){row=new Row(manager);rows.Add(task.Package.Id,row);list.Controls.Add(row);}row.Update(task);}FitRows();}
        protected override void Dispose(bool disposing){if(disposing){timer.Stop();timer.Dispose();}base.Dispose(disposing);}
    }
}
