using System.Drawing;
using System.Windows.Forms;
namespace LocalConverter
{
    public sealed class CancelDownloadDialog : Form
    {
        public CancelDownloadDialog(string resource)
        {
            Text="取消下载？";Font=new Font("Microsoft YaHei UI",10);AutoScaleMode=AutoScaleMode.Dpi;ClientSize=new Size(480,235);FormBorderStyle=FormBorderStyle.FixedDialog;StartPosition=FormStartPosition.CenterParent;MaximizeBox=MinimizeBox=false;ShowInTaskbar=false;BackColor=Color.White;ForeColor=Theme.Ink;
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(22),ColumnCount=1,RowCount=3};root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,42));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,42));Controls.Add(root);
            root.Controls.Add(new Label{Text="取消“"+resource+"”的下载？",Dock=DockStyle.Fill,AutoEllipsis=true,Font=new Font(Font,FontStyle.Bold)},0,0);
            root.Controls.Add(new Label{Text="已下载部分将被删除，当前进度会丢失。\r\n之后需要重新下载。\r\n\r\n如果只想暂时停止，请使用“暂停”保留进度。",Dock=DockStyle.Fill},0,1);
            var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,WrapContents=false};var keep=new Button{Text="保留下载",DialogResult=DialogResult.Cancel,Width=110,Height=32};var remove=new Button{Text="取消下载",DialogResult=DialogResult.OK,Width=110,Height=32,ForeColor=Theme.Red};buttons.Controls.Add(keep);buttons.Controls.Add(remove);root.Controls.Add(buttons,0,2);CancelButton=keep;AcceptButton=keep;Shown+=(s,e)=>keep.Focus();
        }
    }
}
