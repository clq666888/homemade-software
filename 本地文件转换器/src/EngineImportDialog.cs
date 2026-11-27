using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace LocalConverter
{
    public sealed class EngineImportDialog : Form
    {
        public static string Instructions(EnginePackage[] packages)
        {
            if(packages!=null)return "用途：在其他电脑下载官方原件，拷到本机后安装，与联网下载相同。\r\n\r\n只接受当前所选资源的指定版本，可多选。不必解压，请勿重新打包。软件核对大小和 SHA256，不匹配会拒绝安装。下方地址可选中复制到浏览器。\r\n\r\n"+(packages.Length==0?"当前是自定义包，没有对应官方原件；请使用导入自定义音色。":String.Join("\r\n\r\n",packages.Select(p=>p.Name+"\r\n下载地址："+p.Url+"\r\nSHA256："+p.Sha256)))+"\r\n\r\n普通 WAV/MP3 和其他软件的语音包不能在这里安装。";
            return "仅支持本软件 sherpa-onnx 2.0.3 / Kokoro 适配器兼容的完整数据 ZIP。这是高级导入功能。\r\n\r\n不能导入：普通 WAV/MP3 录音、聊天软件语音包、其他模型家族。这里不会从录音克隆声音或自动转换模型。\r\n\r\n来源：普通用户建议取消，直接下载列表中的官方音色包。官方原件使用“离线安装官方包”。没有可通用导入的语音包商店，自制或第三方包须按本软件规范封装。\r\n\r\nZIP 根目录须有 voice-pack.json，以及配套 ONNX 模型、音色向量、tokens、词典及模型所需数据。不能只改后缀。清单示例（文件名、编号必须与实际模型一致）：\r\n\r\n{\r\n  \"Schema\": 1, \"Name\": \"我的兼容包\", \"Adapter\": \"kokoro\",\r\n  \"Model\": \"model.int8.onnx\", \"VoicesFile\": \"voices.bin\",\r\n  \"Tokens\": \"tokens.txt\", \"Lexicons\": \"lexicon-us-en.txt,lexicon-zh.txt\",\r\n  \"Voices\": [{\"Id\": 0, \"Name\": \"实际音色名\", \"Language\": \"zh\", \"Gender\": \"女\"}]\r\n}\r\n\r\n路径须在包内，不接受程序、脚本。模型架构须受当前 Kokoro 适配器支持，各数据文件须来自配套版本。\r\n\r\n导入成功只代表结构及文件检查通过，不保证能合成；请先试听验证，确认发音与音色正常后再生成全文。不兼容会报告错误。\r\n\r\n模型来源与制作参考（可复制到浏览器）：\r\nhttps://k2-fsa.github.io/sherpa/onnx/tts/pretrained_models/kokoro.html";
        }
        public EngineImportDialog(EnginePackage[] packages)
        {
            Text=packages==null?"导入自定义音色 · 格式与限制":"离线安装官方包 · 文件来源";Font=new Font("Microsoft YaHei UI",10);AutoScaleMode=AutoScaleMode.Dpi;BackColor=Color.White;ForeColor=Theme.Ink;StartPosition=FormStartPosition.CenterParent;Size=new Size(720,570);MinimumSize=new Size(540,400);MinimizeBox=MaximizeBox=false;ShowInTaskbar=false;
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(20),ColumnCount=1,RowCount=3};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,42));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,50));Controls.Add(layout);
            layout.Controls.Add(new Label{Text=packages==null?"仅限兼容 Kokoro 的完整自定义包":"下载下列指定版本的官方原件",Font=new Font(Font,FontStyle.Bold),Dock=DockStyle.Fill},0,0);
            layout.Controls.Add(new TextBox{Text=Instructions(packages),ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Vertical,BackColor=Color.White,Dock=DockStyle.Fill},0,1);
            var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(0,10,0,0)};var cancel=new Button{Text="取消",DialogResult=DialogResult.Cancel,Width=90,Height=30};var select=new Button{Text="选择文件…",DialogResult=DialogResult.OK,Width=120,Height=30,Enabled=packages==null||packages.Length>0};buttons.Controls.Add(cancel);buttons.Controls.Add(select);layout.Controls.Add(buttons,0,2);CancelButton=cancel;AcceptButton=cancel;Shown+=(s,e)=>cancel.Focus();
        }
    }
}
