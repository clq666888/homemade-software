using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LocalConverter
{
    public sealed class PdfPasswordDialog : Form
    {
        readonly TextBox input=new TextBox{UseSystemPasswordChar=true,Dock=DockStyle.Fill,AccessibleName="PDF 打开密码"};
        readonly Label message=new Label{Text="验证通过后才会加入文件列表。",AutoSize=false,Dock=DockStyle.Fill,ForeColor=Theme.Muted};
        readonly ModernButton confirm=new ModernButton{Text="验证并添加",Primary=true,Width=130},cancel=new ModernButton{Text="取消导入",Width=100};
        readonly Func<string,CancellationToken,Task<bool>> verify;readonly CancellationTokenSource cancellation;
        bool busy,closeAfter;Task verification=Task.FromResult(0);
        public string Password{get;private set;}
        public PdfPasswordDialog(string file,Func<string,CancellationToken,Task<bool>> check,CancellationToken token)
        {
            verify=check;cancellation=CancellationTokenSource.CreateLinkedTokenSource(token);Text="此 PDF 需要打开密码";Font=new Font("Microsoft YaHei UI",10);ForeColor=Theme.Ink;BackColor=Color.White;FormBorderStyle=FormBorderStyle.FixedDialog;StartPosition=FormStartPosition.CenterParent;MaximizeBox=MinimizeBox=false;ShowInTaskbar=false;ClientSize=new Size(440,230);AutoScaleMode=AutoScaleMode.Dpi;
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(22),ColumnCount=1,RowCount=5};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));foreach(int height in new[]{34,30,36,48,42})layout.RowStyles.Add(new RowStyle(SizeType.Absolute,height));Controls.Add(layout);
            layout.Controls.Add(new Label{Text=Path.GetFileName(file),Dock=DockStyle.Fill,AutoEllipsis=true,Font=new Font(Font,FontStyle.Bold)},0,0);layout.Controls.Add(new Label{Text="请输入该文件的打开密码",Dock=DockStyle.Fill},0,1);layout.Controls.Add(input,0,2);layout.Controls.Add(message,0,3);
            var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};buttons.Controls.AddRange(new Control[]{confirm,cancel});layout.Controls.Add(buttons,0,4);AcceptButton=confirm;CancelButton=cancel;
            confirm.Click+=(s,e)=>{if(!busy)verification=Verify();};cancel.Click+=(s,e)=>{closeAfter=true;cancellation.Cancel();if(!busy){DialogResult=DialogResult.Cancel;Close();}};Shown+=(s,e)=>input.Focus();
            FormClosing+=(s,e)=>{if(busy){e.Cancel=true;closeAfter=true;cancellation.Cancel();}};
        }
        async Task Verify()
        {
            busy=true;input.Enabled=confirm.Enabled=false;message.ForeColor=Theme.Muted;message.Text="正在验证密码，请稍候…";string candidate=input.Text;
            try
            {
                bool accepted=await verify(candidate,cancellation.Token);cancellation.Token.ThrowIfCancellationRequested();
                if(accepted){Password=candidate;}
                else{message.ForeColor=Theme.Red;message.Text="密码不正确，请重新输入。文件尚未添加。";}
            }
            catch(OperationCanceledException){closeAfter=true;}
            catch(Exception error){message.ForeColor=Theme.Red;message.Text="无法验证："+error.Message;}
            finally{busy=false;if(closeAfter){DialogResult=DialogResult.Cancel;Close();}else if(Password!=null){DialogResult=DialogResult.OK;Close();}else{input.Enabled=confirm.Enabled=true;input.SelectAll();input.Focus();}}
        }
        protected override void Dispose(bool disposing){if(disposing){input.Clear();cancellation.Dispose();}base.Dispose(disposing);}
    }
}
