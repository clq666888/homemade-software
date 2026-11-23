"""Run with a workspace folder and optionally the packaged PDF engine."""
from pathlib import Path
import sys,json,subprocess,zipfile,hashlib
from pypdf import PdfWriter,PdfReader
from pypdf.generic import DictionaryObject,NameObject,DecodedStreamObject
from openpyxl import Workbook,load_workbook
from openpyxl.styles import Border,Side
from pptx import Presentation
from pptx.util import Inches
from PIL import Image

root=Path(sys.argv[1]).resolve();root.mkdir(parents=True,exist_ok=True)
backend=Path(__file__).resolve().parents[1]/'engine/pdf_engine.py';packaged=Path(sys.argv[2]).resolve() if len(sys.argv)>2 else None
count=0
def check(ok,label):
    global count
    assert ok,label;count+=1;print('PASS',label,flush=True)
def job(action,source,**values):
    folder=root/('job-'+str(len(list(root.glob('job-*')))));folder.mkdir();output=folder/'out';output.mkdir();reply=folder/'reply.json';request=folder/'job.json'
    request.write_text(json.dumps(dict(action=action,files=[str(source)],folder=str(output),reply=str(reply),**values)),encoding='utf-8')
    result=subprocess.run([str(packaged),str(request)] if packaged else [sys.executable,str(backend),str(request)],capture_output=True,timeout=120)
    assert reply.exists(),(result.returncode,result.stdout,result.stderr)
    answer=json.loads(reply.read_text(encoding='utf-8'));assert answer['ok'],answer;return answer
book=Workbook();sheet=book.active;sheet.title='Table';sheet.append(['Name','Value','Double']);sheet.append(['Alpha',42,'=B2*2']);sheet.append(['Bravo',84,'=B3*2'])
for row in sheet:
    for cell in row:cell.border=Border(left=Side(style='thin'),right=Side(style='thin'),top=Side(style='thin'),bottom=Side(style='thin'))
for column in ['A','B','C']:sheet.column_dimensions[column].width=24
sheet.print_area='A1:C3';sheet.page_setup.paperSize=sheet.PAPERSIZE_A4;sheet.page_setup.fitToWidth=1;sheet.page_setup.fitToHeight=1;sheet.sheet_properties.pageSetUpPr.fitToPage=True
book.save(root/'input.xlsx');book.close()
deck=Presentation()
for title in ['Local slide Alpha','Local slide Bravo']:
    slide=deck.slides.add_slide(deck.slide_layouts[6]);slide.shapes.add_textbox(Inches(1),Inches(1),Inches(7),Inches(1)).text=title
deck.save(root/'input.pptx')
(root/'input.html').write_text('<!doctype html><meta charset="utf-8"><style>body{font-family:Arial}h1{color:#3569ef}section{break-after:page}</style><section><h1>HTML Alpha 中文网页</h1><p>Local offline content</p><img src="http://127.0.0.1:18764/remote.png"><script>document.write("SCRIPT_EXECUTED")</script></section><h1>HTML Bravo</h1>',encoding='utf-8')
writer=PdfWriter();page=writer.add_blank_page(420,595)
font=DictionaryObject({NameObject('/Type'):NameObject('/Font'),NameObject('/Subtype'):NameObject('/Type1'),NameObject('/BaseFont'):NameObject('/Helvetica')})
page[NameObject('/Resources')]=DictionaryObject({NameObject('/Font'):DictionaryObject({NameObject('/F1'):writer._add_object(font)})})
stream=DecodedStreamObject();stream.set_data(b'0 0 0 RG 1 w 30 400 m 390 400 l S 30 450 m 390 450 l S 30 500 m 390 500 l S 30 400 m 30 500 l S 210 400 m 210 500 l S 390 400 m 390 500 l S BT /F1 14 Tf 40 470 Td (Name) Tj 180 0 Td (Value) Tj ET BT /F1 14 Tf 40 420 Td (Alpha) Tj 180 0 Td (42) Tj ET');page[NameObject('/Contents')]=writer._add_object(stream)
writer.add_blank_page(595,420);writer.write(root/'table.pdf');writer.close();before=hashlib.sha256((root/'table.pdf').read_bytes()).hexdigest()
for suffix in ['xlsx','pptx']:
    result=job('office_text',root/('input.'+suffix));check(('Alpha' in result['text']),'quick preview '+suffix)
html_preview=job('office_text',root/'input.html')['text'];check('中文网页' in html_preview and 'SCRIPT_EXECUTED' not in html_preview,'HTML preview excludes scripts')
prepared=Path(job('prepare_html',root/'input.html')['outputs'][0]).read_text(encoding='utf-8');check('SCRIPT_EXECUTED' not in prepared and 'Content-Security-Policy' in prepared and 'script-src' in prepared and '<base href="file:' in prepared,'HTML print copy strips scripts and keeps local resource base under CSP')
(root/'redirect.html').write_text('<meta http-equiv=" Refresh " content="0;url=file:///redirect.html"><p onclick="alert(1)">Safe</p>',encoding='utf-8')
prepared=Path(job('prepare_html',root/'redirect.html')['outputs'][0]).read_text(encoding='utf-8');check('redirect.html' not in prepared and 'onclick' not in prepared and 'Safe' in prepared,'HTML cleaning removes whitespace refresh redirects and event handlers')
html=job('convert',root/'table.pdf',format='html')['outputs'][0];content=Path(html).read_text(encoding='utf-8');check(content.count('data:image/png;base64,')==2 and 'http://' not in content,'appearance HTML contains all pages as embedded images')
html_text=job('convert',root/'table.pdf',format='html',htmlMode='text')['outputs'][0];content=Path(html_text).read_text(encoding='utf-8');check('Alpha' in content and content.count('data:image/png;base64,')==1,'editable HTML exports text and retains scanned page image')
pptx=job('convert',root/'table.pdf',format='pptx')['outputs'][0];presentation=Presentation(pptx);check(len(presentation.slides)==2 and all(len(s.shapes)==1 for s in presentation.slides),'PPTX preserves PDF page count as image slides')
xlsx=job('convert',root/'table.pdf',format='xlsx')['outputs'][0];book=load_workbook(xlsx);check(book.worksheets[0]['A2'].value=='Alpha' and book.worksheets[0]['B2'].value=='42','XLSX extracts table into editable cells');check(len(book.worksheets[1]._images)==1,'XLSX retains scanned page image');book.close()
writer=PdfWriter();writer.append(root/'table.pdf');writer.encrypt('test-pass',algorithm='AES-256-R5');writer.write(root/'encrypted.pdf');writer.close()
for fmt in ['xlsx','pptx','html']:
    result=job('convert',root/'encrypted.pdf',format=fmt,password='test-pass');check(Path(result['outputs'][0]).stat().st_size>200,'password-protected PDF to '+fmt)
check(hashlib.sha256((root/'table.pdf').read_bytes()).hexdigest()==before,'new exports preserve source bytes')
print('ALL',count,'OFFICE/HTML BACKEND CHECKS PASSED',flush=True)
