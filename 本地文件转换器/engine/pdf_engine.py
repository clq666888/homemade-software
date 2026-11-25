"""Isolated offline PDF worker. Receives private JSON jobs, never reads a password from argv."""
from __future__ import annotations
import json, os, re, struct, sys, time
from contextlib import closing
from pathlib import Path
from pypdf import PdfReader, PdfWriter
import pypdfium2 as pdfium
from PIL import Image
import olefile
import zipfile
import uuid
from xml.sax.saxutils import escape
from pdf_split import execute_split
from office_formats import export_pdf, preview as office_preview, prepare_html
from pdf_watermark import plan as watermark_plan, execute as watermark_execute
from pdf_access import password_for, probe

def atomic_path(dest, action):
    temporary=dest.with_name('.working-'+uuid.uuid4().hex+'.tmp')
    try:
        action(temporary);os.replace(temporary,dest)
    finally:
        temporary.unlink(missing_ok=True)

def write_pdf(writer,dest):
    atomic_path(dest,lambda temp:writer.write(temp))

def export_docx(reader, source, password, dest):
    """Editable extracted paragraphs; image-only source pages are embedded, not fake OCR."""
    document_ns='http://schemas.openxmlformats.org/wordprocessingml/2006/main'
    image_relationships=[];media=[];parts=[]
    for number,page in enumerate(reader.pages):
        if number: parts.append('<w:p><w:r><w:br w:type="page"/></w:r></w:p>')
        text=page.extract_text() or ''
        if text.strip():
            for line in text.splitlines():
                safe=escape(re.sub(r'[\x00-\x08\x0b\x0c\x0e-\x1f]','',line))
                parts.append('<w:p><w:r><w:t xml:space="preserve">'+safe+'</w:t></w:r></w:p>')
        else:
            with pdfium.PdfDocument(source,password=password or None) as pdf:
                with closing(pdf[number]) as rendered:
                    w,h=rendered.get_size()
                    with closing(rendered.render(scale=min(2,2000/max(w,h)))) as bitmap:
                        image=bitmap.to_pil();import io
                        data=io.BytesIO();image.save(data,format='PNG');image.close()
            width=5_700_000;height=int(width*h/w)
            if height>8_200_000: width=int(width*8_200_000/height);height=8_200_000
            name=f'page{number+1}.png';rid=f'rImage{number+1}';media.append((name,data.getvalue()))
            image_relationships.append(f'<Relationship Id="{rid}" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" Target="media/{name}"/>')
            parts.append(f'<w:p><w:r><w:drawing><wp:inline><wp:extent cx="{width}" cy="{height}"/><wp:docPr id="{number+1}" name="Page {number+1}"/><a:graphic><a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/picture"><pic:pic><pic:nvPicPr><pic:cNvPr id="{number+1}" name="{name}"/><pic:cNvPicPr/></pic:nvPicPr><pic:blipFill><a:blip r:embed="{rid}"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill><pic:spPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="{width}" cy="{height}"/></a:xfrm><a:prstGeom prst="rect"><a:avLst/></a:prstGeom></pic:spPr></pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing></w:r></w:p>')
    xml=f'<?xml version="1.0" encoding="UTF-8" standalone="yes"?><w:document xmlns:w="{document_ns}" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:pic="http://schemas.openxmlformats.org/drawingml/2006/picture"><w:body>'+''.join(parts)+'<w:sectPr><w:pgSz w:w="11906" w:h="16838"/><w:pgMar w:top="850" w:right="850" w:bottom="850" w:left="850"/></w:sectPr></w:body></w:document>'
    with zipfile.ZipFile(dest,'w',compression=zipfile.ZIP_DEFLATED) as output:
        output.writestr('[Content_Types].xml','<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="png" ContentType="image/png"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/><Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/></Types>')
        output.writestr('_rels/.rels','<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rDoc" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>')
        output.writestr('word/_rels/document.xml.rels','<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rStyles" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>'+''.join(image_relationships)+'</Relationships>')
        output.writestr('word/styles.xml',f'<w:styles xmlns:w="{document_ns}"><w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii="Calibri" w:eastAsia="Microsoft YaHei" w:hAnsi="Calibri"/><w:sz w:val="22"/></w:rPr></w:rPrDefault></w:docDefaults><w:style w:type="paragraph" w:default="1" w:styleId="Normal"><w:name w:val="Normal"/></w:style></w:styles>')
        output.writestr('word/document.xml',xml)
        for name,data in media:output.writestr('word/media/'+name,data)


def read_pdf(path, password=''):
    reader = PdfReader(path)
    if reader.is_encrypted and not reader.decrypt(password):
        raise ValueError('文件已加密，请输入正确的打开密码。')
    return reader


def progress(job, value, message):
    target = Path(job['reply']).with_suffix('.progress')
    temp = target.with_suffix('.pending')
    temp.write_text(json.dumps({'percent': value, 'message': message}, ensure_ascii=False), encoding='utf-8')
    os.replace(temp, target)


def doc_text(path):
    """MS-DOC FibRgFcLcb97.fcClx -> PlcPcd; no Word process or macro execution."""
    if not olefile.isOleFile(path):
        raise ValueError('该 DOC 不是标准 Word 97–2003 文件，无法快速预览；可点击“排版预览”使用 Word。')
    with olefile.OleFileIO(path) as ole:
        word = ole.openstream('WordDocument').read()
        if len(word) < 426 or struct.unpack_from('<H', word)[0] != 0xA5EC:
            raise ValueError('DOC 文件格式不受支持。')
        flags = struct.unpack_from('<H', word, 10)[0]
        if flags & 0x100:
            raise ValueError('加密的 Word 文档无法快速预览。')
        table = ole.openstream('1Table' if flags & 0x200 else '0Table').read()
        fc, length = struct.unpack_from('<II', word, 418)
        if length > 20_000_000 or fc + length > len(table):
            raise ValueError('DOC 分段表无效。')
        clx = table[fc:fc+length]; at = 0
        while at < len(clx) and clx[at] == 1:
            at += 3 + struct.unpack_from('<H', clx, at+1)[0]
        if at+5 > len(clx) or clx[at] != 2:
            raise ValueError('无法读取 DOC 正文分段。')
        size = struct.unpack_from('<I', clx, at+1)[0]
        plc = clx[at+5:at+5+size]; count = (size-4)//12
        if size < 4 or len(plc) != size or count > 200000:
            raise ValueError('DOC 正文分段无效。')
        ccp = struct.unpack_from('<I', word, 76)[0]
        out = []; total = 0
        for i in range(count):
            first, last = struct.unpack_from('<II', plc, i*4)
            if first >= ccp or total >= 16000: break
            chars = min(last, ccp)-first
            if chars < 0: raise ValueError('DOC 字符范围错误。')
            encoded = struct.unpack_from('<I', plc, (count+1)*4+i*8+2)[0]
            offset = encoded & 0x3fffffff
            if encoded & 0x40000000:
                offset //= 2; text = word[offset:offset+chars].decode('cp1252', errors='replace')
            else:
                text = word[offset:offset+chars*2].decode('utf-16le', errors='replace')
            out.append(text); total += len(text)
        text = ''.join(out)[:16000].replace('\r', '\n').replace('\x07', '  ').replace('\x0b', '\n')
        return re.sub(r'[\x00-\x08\x0e-\x1f]', '', text)


def run(job):
    if job.get('action','').startswith('image_'):
        from image_tools import execute
        return execute(job,progress,atomic_path)
    if job['action']=='prepare_html':return {'outputs':[prepare_html(job['files'][0],job['folder'])]}
    if job['action']=='office_text':return {'text':office_preview(job['files'][0])}
    action = job['action']; files = job.get('files', []); password = job.get('password', '')
    if action == 'probe':return probe(files[0],password_for(job,files[0]))
    if files:password=password_for(job,files[0])
    if action == 'doc_text':
        return {'text': doc_text(files[0])}
    if action == 'watermark_info':return watermark_plan(job,read_pdf)
    if action == 'inspect':
        reader = read_pdf(files[0], password)
        return {'count': len(reader.pages), 'encrypted': reader.is_encrypted,
                'sizes': [[float(p.mediabox.width), float(p.mediabox.height)] for p in reader.pages]}
    if action == 'render':
        target = Path(job['folder']); target.mkdir(parents=True, exist_ok=True)
        with pdfium.PdfDocument(files[0], password=password or None) as document:
            outputs = []
            for index in job.get('indices', [0]):
                with closing(document[index]) as page:
                    width, height = page.get_size(); edge = int(job.get('edge', 1000))
                    scale = min(edge/max(width, height), 3)
                    with closing(page.render(scale=scale, rotation=int(job.get('rotation', 0)))) as bitmap:
                        image = bitmap.to_pil(); dest = target / f'{index}-{edge}-{job.get("rotation", 0)}.png'
                        image.save(dest); image.close(); outputs.append(str(dest))
            return {'outputs': outputs}
    folder = Path(job['folder']); folder.mkdir(parents=True, exist_ok=True)
    outputs = []; notes = []
    if action=='watermark':return watermark_execute(job,read_pdf,progress,write_pdf)
    if action=='split':return execute_split(job,read_pdf,progress,atomic_path)
    if action == 'merge':
        if len(files) < 2: raise ValueError('请至少添加两份文件。')
        writer = PdfWriter()
        try:
            for i, source in enumerate(files):
                reader = read_pdf(source, password_for(job,source))
                if reader.get_fields(): reader.add_form_topname(f'document{i+1}')
                writer.append(reader)
                progress(job, int(90*(i+1)/len(files)), f'正在合并第 {i+1} 份文件')
            dest = folder / '合并文件.pdf'; write_pdf(writer,dest); outputs.append(str(dest))
        finally: writer.close()
    elif action == 'edit':
        reader = read_pdf(files[0], password); pages = job.get('pages', [])
        if not pages: raise ValueError('至少保留一页。')
        writer = PdfWriter()
        try:
            for item in pages:
                index = int(item['index'])
                if not 0 <= index < len(reader.pages): raise ValueError('页面范围无效。')
            writer.append(reader,pages=[int(item['index']) for item in pages])
            for added,item in zip(writer.pages,pages):
                angle = int(item.get('rotation', 0)) % 360
                if angle: added.rotate(angle)
            if reader.metadata: writer.add_metadata({k:str(v) for k,v in reader.metadata.items() if v is not None})
            # Editing an encrypted input retains its original password unless explicitly decrypting.
            if reader.is_encrypted:
                writer.encrypt(password, algorithm='AES-256-R5')
            dest = folder / (Path(files[0]).stem + '_编辑.pdf'); write_pdf(writer,dest); outputs.append(str(dest))
        finally: writer.close()
    elif action == 'convert' and job.get('format') in ('png', 'jpg', 'txt', 'docx','xlsx','pptx','html'):
        fmt = job['format']
        for number, source in enumerate(files):
            password=password_for(job,source)
            reader = read_pdf(source, password)
            if fmt in ('xlsx','pptx','html'):
                dest=folder/f'{number+1}_{Path(source).stem}.{fmt}'
                def build(temp):
                    notes.append(export_pdf(reader,source,password,temp,fmt,job.get('htmlMode','appearance'),lambda done,total:progress(job,int(95*(number+done/max(1,total))/len(files)),f'正在导出第 {done} 页')))
                atomic_path(dest,build);outputs.append(str(dest))
            elif fmt == 'docx':
                dest=folder/f'{number+1}_{Path(source).stem}.docx'
                atomic_path(dest,lambda temp:export_docx(reader,source,password,temp));outputs.append(str(dest));notes.append('DOCX 为可编辑正文重建，扫描页保留为图片；不还原复杂排版。')
            elif fmt == 'txt':
                dest = folder / f'{number+1}_{Path(source).stem}.txt'
                atomic_path(dest,lambda temp:temp.write_text('\n\n'.join(p.extract_text() or '' for p in reader.pages), encoding='utf-8-sig')); outputs.append(str(dest))
                if not any((p.extract_text() or '').strip() for p in reader.pages): notes.append('部分 PDF 无文字层，文本输出为空；当前不含 OCR。')
            else:
                with pdfium.PdfDocument(source, password=password or None) as document:
                    for index in range(len(document)):
                        with closing(document[index]) as page:
                            scale = min(int(job.get('dpi', 150))/72, 6000/max(page.get_size()))
                            with closing(page.render(scale=scale)) as bitmap:
                                image = bitmap.to_pil().convert('RGB'); dest = folder / f'{number+1}_{Path(source).stem}_第{index+1:03d}页.{fmt}'
                                atomic_path(dest,lambda temp:image.save(temp,format='JPEG' if fmt=='jpg' else 'PNG',quality=90)); image.close(); outputs.append(str(dest))
                        progress(job, int(95*(number+(index+1)/len(document))/len(files)), f'正在导出第 {index+1} 页')
    else:
        for number, source in enumerate(files):
            password=password_for(job,source)
            reader = read_pdf(source, password); writer = PdfWriter(clone_from=reader)
            try:
                suffix = action
                if action == 'encrypt':
                    secret = job.get('newPassword', '')
                    if not secret: raise ValueError('加密密码不能为空。')
                    writer.encrypt(secret, algorithm='AES-256-R5'); suffix = '已加密'
                elif action == 'decrypt':
                    if not reader.is_encrypted: notes.append(f'{Path(source).name} 本身没有加密。')
                    suffix = '已解密'
                elif action == 'compress':
                    mode = job.get('quality', 'lossless')
                    for i, page in enumerate(writer.pages):
                        if mode != 'lossless':
                            for image in list(page.images):
                                if image.indirect_reference is None: continue
                                source_image = image.image
                                # Leave transparency intact; do not flatten masks into black backgrounds.
                                if source_image.mode in ('RGBA', 'LA') or 'transparency' in source_image.info: continue
                                reduced = source_image.convert('RGB'); reduced.thumbnail((1800,1800) if mode == 'balanced' else (1200,1200))
                                image.replace(reduced, quality=78 if mode == 'balanced' else 55); reduced.close()
                        page.compress_content_streams(level=9)
                        progress(job, int(90*(number+(i+1)/len(writer.pages))/len(files)), f'正在压缩第 {i+1} 页')
                    writer.compress_identical_objects(remove_duplicates=True, remove_unreferenced=True); suffix = '压缩'
                    if reader.is_encrypted:
                        writer.encrypt(password, algorithm='AES-256-R5')
                else: raise ValueError('不支持的 PDF 操作。')
                dest = folder / f'{number+1}_{Path(source).stem}_{suffix}.pdf'; write_pdf(writer,dest)
                if action == 'compress' and dest.stat().st_size >= Path(source).stat().st_size:
                    import shutil
                    atomic_path(dest,lambda temp:shutil.copyfile(source,temp)); notes.append(f'{Path(source).name} 已较紧凑，本次保留原大小，未强行变大。')
                outputs.append(str(dest)); progress(job, int(95*(number+1)/len(files)), f'已完成 {number+1}/{len(files)}')
            finally: writer.close()
    return {'outputs': outputs, 'note': '\n'.join(dict.fromkeys(notes))}


def main():
    if len(sys.argv)==3 and sys.argv[1]=='--preview-server':
        from preview_worker import serve
        serve(sys.argv[2]);return 0
    jobfile = Path(sys.argv[1]); job = json.loads(jobfile.read_text(encoding='utf-8-sig'))
    reply = Path(job['reply'])
    try:
        started = time.perf_counter(); result = run(job); result.update(ok=True, elapsed=time.perf_counter()-started)
    except Exception as error:
        result = {'ok': False, 'error': str(error) or type(error).__name__}
    reply.write_text(json.dumps(result, ensure_ascii=False), encoding='utf-8')
    return 0 if result['ok'] else 1

if __name__ == '__main__':
    raise SystemExit(main())
