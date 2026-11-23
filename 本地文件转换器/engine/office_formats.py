"""Offline PDF exports and quick Office/HTML text previews."""
import base64
import io
from contextlib import closing
from html import escape
from html.parser import HTMLParser
from pathlib import Path
import pypdfium2 as pdfium


def page_image(document, index):
    with closing(document[index]) as page:
        width,height=page.get_size()
        with closing(page.render(scale=min(150/72,4000/max(width,height)))) as bitmap:
            image=bitmap.to_pil().convert('RGB')
            data=io.BytesIO();image.save(data,format='PNG');image.close()
    data.seek(0)
    return data,width,height


def export_pdf(reader, source, password, dest, fmt, html_mode, report):
    if fmt=='html':
        with dest.open('w',encoding='utf-8') as output:
            output.write('<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>'+escape(Path(source).stem)+'</title><style>body{margin:0;background:#eef1f5;font:16px/1.7 sans-serif}main{max-width:1100px;margin:auto;padding:20px}section{background:white;margin:0 0 20px;padding:20px}img{max-width:100%;height:auto;display:block;margin:auto}pre{white-space:pre-wrap;overflow-wrap:anywhere;font:inherit}@media print{body{background:white}main,section{padding:0}section{break-after:page}}</style><main>')
            with pdfium.PdfDocument(source,password=password or None) as pdf:
                for index,page in enumerate(reader.pages):
                    output.write('<section aria-label="第 '+str(index+1)+' 页">')
                    text=page.extract_text() or ''
                    if html_mode=='text' and text.strip():output.write('<pre>'+escape(text)+'</pre>')
                    else:
                        data,_,_=page_image(pdf,index)
                        output.write('<img alt="第 '+str(index+1)+' 页" src="data:image/png;base64,'+base64.b64encode(data.getvalue()).decode('ascii')+'">')
                    output.write('</section>');report(index+1,len(reader.pages))
            output.write('</main></html>')
        return 'HTML 为离线单文件，图片内嵌。'+('正文可编辑，复杂排版不还原；扫描页保留图片。' if html_mode=='text' else '以页面图片保留外观，文字不是可编辑网页元素。')
    if fmt=='pptx':
        from pptx import Presentation
        deck=Presentation()
        with pdfium.PdfDocument(source,password=password or None) as pdf:
            with closing(pdf[0]) as first:width,height=first.get_size()
            # Use a bounded standard slide width and keep the first page's aspect ratio.
            deck.slide_width=9144000;deck.slide_height=max(914400,min(51206400,int(deck.slide_width*height/width)))
            for index in range(len(pdf)):
                data,w,h=page_image(pdf,index);slide=deck.slides.add_slide(deck.slide_layouts[6])
                scale=min(deck.slide_width/w,deck.slide_height/h);sw=int(w*scale);sh=int(h*scale)
                slide.shapes.add_picture(data,(deck.slide_width-sw)//2,(deck.slide_height-sh)//2,width=sw,height=sh)
                report(index+1,len(pdf))
        deck.save(dest)
        return 'PPTX 每个 PDF 页面生成一张幻灯片，以图片保留页面外观；不拆解为可编辑文字、表格或图形。'
    import pdfplumber
    from openpyxl import Workbook
    from openpyxl.cell.cell import ILLEGAL_CHARACTERS_RE
    from openpyxl.drawing.image import Image as SheetImage
    workbook=Workbook();workbook.remove(workbook.active);streams=[]
    def cell_text(value):return ILLEGAL_CHARACTERS_RE.sub('',str(value or ''))[:32767]
    try:
        with pdfplumber.open(source,password=password or '') as pdf, pdfium.PdfDocument(source,password=password or None) as rendered:
            for index,page in enumerate(pdf.pages):
                tables=page.extract_tables()
                if tables:
                    for number,table in enumerate(tables):
                        sheet=workbook.create_sheet(f'第{index+1}页_表{number+1}')
                        for row_index,row in enumerate(table,1):
                            for column,value in enumerate(row,1):
                                cell=sheet.cell(row_index,column,cell_text(value));cell.data_type='s'
                        sheet.freeze_panes='A2'
                        for column in sheet.columns:sheet.column_dimensions[column[0].column_letter].width=24
                else:
                    sheet=workbook.create_sheet(f'第{index+1}页_正文');text=page.extract_text() or ''
                    if text.strip():
                        for row_index,line in enumerate(text.splitlines(),1):
                            cell=sheet.cell(row_index,1,cell_text(line));cell.data_type='s'
                        sheet.column_dimensions['A'].width=100
                    else:
                        sheet['A1']='扫描页：无文字层，保留页面图片；未进行 OCR。'
                        data,_,_=page_image(rendered,index);streams.append(data);image=SheetImage(data);factor=min(1,850/image.width);image.width=int(image.width*factor);image.height=int(image.height*factor)
                        sheet.add_image(image,'A3')
                report(index+1,len(pdf.pages))
        if not workbook.worksheets:raise ValueError('PDF 没有可导出页面。')
        workbook.save(dest)
    finally:
        workbook.close()
        for stream in streams:stream.close()
    return 'XLSX 提取可识别表格；无表格的文字页按行保存，扫描页保留图片。不恢复公式，不含 OCR。'


class HtmlText(HTMLParser):
    def __init__(self):super().__init__(convert_charrefs=True);self.parts=[];self.hidden=0
    def handle_starttag(self,tag,attrs):
        if tag in ('script','style','template'):self.hidden+=1
        if tag in ('p','div','br','tr','h1','h2','li'):self.parts.append('\n')
    def handle_endtag(self,tag):
        if tag in ('script','style','template'):self.hidden=max(0,self.hidden-1)
        if tag in ('p','div','tr','li'):self.parts.append('\n')
    def handle_data(self,data):
        if not self.hidden:self.parts.append(data)


class PrintHtml(HTMLParser):
    def __init__(self):super().__init__(convert_charrefs=False);self.parts=[];self.hidden=0
    def handle_starttag(self,tag,attrs):
        if tag in ('script','iframe','object','template'):self.hidden+=1;return
        if self.hidden or tag in ('base','embed','frame','frameset'):return
        if tag=='meta' and any(k=='http-equiv' and (v or '').strip().lower()=='refresh' for k,v in attrs):return
        attrs=[(k,v) for k,v in attrs if not k.lower().startswith('on') and k.lower()!='srcdoc']
        self.parts.append('<'+tag+''.join(' '+k+('="'+escape(v,quote=True)+'"' if v is not None else '') for k,v in attrs)+'>')
    def handle_endtag(self,tag):
        if tag in ('script','iframe','object','template'):self.hidden=max(0,self.hidden-1);return
        if not self.hidden and tag not in ('base','embed','frame','frameset'):self.parts.append('</'+tag+'>')
    def handle_startendtag(self,tag,attrs):self.handle_starttag(tag,attrs)
    def handle_data(self,data):
        if not self.hidden:self.parts.append(data)
    def handle_entityref(self,name):
        if not self.hidden:self.parts.append('&'+name+';')
    def handle_charref(self,name):
        if not self.hidden:self.parts.append('&#'+name+';')


def html_source(source):
    import re
    if Path(source).stat().st_size>50_000_000:raise ValueError('HTML 超过 50 MB，请先缩小文件。')
    raw=Path(source).read_bytes()
    if len(raw)>50_000_000:raise ValueError('HTML 超过 50 MB，请先缩小文件。')
    if raw.startswith((b'\xff\xfe',b'\xfe\xff')):return raw.decode('utf-16')
    match=re.search(br'charset\s*=\s*["\']?([A-Za-z0-9_-]+)',raw[:4096],re.I)
    encoding=match[1].decode('ascii') if match else 'utf-8-sig'
    try:return raw.decode(encoding)
    except (LookupError,UnicodeDecodeError):return raw.decode('gb18030',errors='replace')


def prepare_html(source,folder):
    parser=PrintHtml();parser.feed(html_source(source));folder=Path(folder);folder.mkdir(parents=True,exist_ok=True)
    policy="default-src 'none'; script-src 'none'; connect-src 'none'; img-src file: data: blob:; style-src 'unsafe-inline' file: data:; font-src file: data:; frame-src 'none'; object-src 'none'; form-action 'none'; base-uri file:"
    header='<!doctype html><meta charset="utf-8"><meta http-equiv="Content-Security-Policy" content="'+escape(policy,quote=True)+'"><base href="'+escape(Path(source).resolve().parent.as_uri()+'/',quote=True)+'">'
    dest=folder/'print.html';dest.write_text(header+''.join(parser.parts),encoding='utf-8');return str(dest)


def preview(source):
    suffix=Path(source).suffix.lower()
    if suffix=='.xlsx':
        from openpyxl import load_workbook
        book=load_workbook(source,read_only=True,data_only=False,keep_links=False)
        try:
            lines=[];length=0
            for sheet in book:
                lines.append('【'+sheet.title+'】')
                for row in sheet.iter_rows(max_col=30,max_row=1000):
                    line='  |  '.join('' if c.value is None else str(c.value) for c in row);lines.append(line);length+=len(line)
                    if length>16000:return '\n'.join(lines)[:16000]
            return '\n'.join(lines)
        finally:book.close()
    if suffix=='.pptx':
        from pptx import Presentation
        deck=Presentation(source);lines=[]
        for number,slide in enumerate(deck.slides):
            lines.append('【幻灯片 '+str(number+1)+'】')
            lines.extend(shape.text for shape in slide.shapes if shape.has_text_frame)
            if sum(map(len,lines))>16000:break
        return '\n'.join(lines)[:16000]
    if suffix in ('.html','.htm'):
        parser=HtmlText();parser.feed(html_source(source));return ''.join(parser.parts)[:16000]
    return '此旧版 Office 文件请点击“排版预览”查看实际页面。'
