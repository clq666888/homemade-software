from pathlib import Path
import json, sys, hashlib, subprocess, time, zipfile, io
from pypdf import PdfReader, PdfWriter
from pypdf.generic import DictionaryObject,NameObject,DecodedStreamObject,ArrayObject,NumberObject,TextStringObject
from PIL import Image

root=Path(sys.argv[1]).resolve();root.mkdir(parents=True,exist_ok=True)
backend=Path(__file__).resolve().parents[1]/'engine/pdf_engine.py'
packaged=Path(sys.argv[2]).resolve() if len(sys.argv)>2 else None
checks=0
def check(condition,label):
    global checks
    assert condition,label;checks+=1;print('PASS',label,flush=True)
def make_pdf(dest,names):
    writer=PdfWriter()
    for name in names:
        page=writer.add_blank_page(420,595)
        font=DictionaryObject({NameObject('/Type'):NameObject('/Font'),NameObject('/Subtype'):NameObject('/Type1'),NameObject('/BaseFont'):NameObject('/Helvetica')})
        page[NameObject('/Resources')]=DictionaryObject({NameObject('/Font'):DictionaryObject({NameObject('/F1'):writer._add_object(font)})})
        content=DecodedStreamObject();content.set_data((f'0.18 0.4 0.9 rg 40 370 340 100 re f\nBT /F1 32 Tf 40 510 Td ({name}) Tj ET\n'+'q 1 0 0 1 0 0 cm Q\n'*3000).encode())
        page[NameObject('/Contents')]=writer._add_object(content)
    writer.write(dest);writer.close()
def job(action,expect=True,**values):
    folder=root/f'job-{len(list(root.glob("job-*"))):03d}';folder.mkdir()
    output=folder/'out';output.mkdir();reply=folder/'reply.json'
    payload={'action':action,'files':[str(root/'a.pdf')],'folder':str(output),'reply':str(reply),**values}
    source=folder/'job.json';source.write_text(json.dumps(payload,ensure_ascii=False),encoding='utf-8')
    command=[str(packaged),str(source)] if packaged else [sys.executable,str(backend),str(source)]
    p=subprocess.run(command,timeout=90,capture_output=True)
    assert reply.exists(),(p.returncode,p.stdout,p.stderr)
    result=json.loads(reply.read_text(encoding='utf-8'));assert result['ok']==expect,result
    return result

make_pdf(root/'a.pdf',['Alpha','Bravo','Charlie']);make_pdf(root/'b.pdf',['Delta','Echo'])
before=hashlib.sha256((root/'a.pdf').read_bytes()).hexdigest()
check(job('inspect')['count']==3,'inspect all page count')
merged=job('merge',files=[str(root/'a.pdf'),str(root/'b.pdf')])['outputs'][0]
reader=PdfReader(merged);check(len(reader.pages)==5 and 'Delta' in reader.pages[3].extract_text(),'merge preserves file and page order')
edited=job('edit',pages=[{'index':2,'rotation':90},{'index':0,'rotation':0}])['outputs'][0]
reader=PdfReader(edited);check(len(reader.pages)==2 and 'Charlie' in reader.pages[0].extract_text() and reader.pages[0].rotation==90,'edit deletion order and rotation')
check('Alpha' in reader.pages[1].extract_text(),'edit second page correct')
check(not job('edit',expect=False,pages=[])['ok'],'cannot delete every page')
def archive_pages(path,password=''):
    with zipfile.ZipFile(path) as archive:
        assert archive.testzip() is None
        readers=[PdfReader(io.BytesIO(archive.read(name))) for name in archive.namelist()]
        if password:
            assert all(reader.is_encrypted and reader.decrypt(password) for reader in readers)
        return readers
split=job('split')['outputs'];parts=archive_pages(split[0])
check(len(split)==1 and Path(split[0]).suffix=='.zip' and len(parts)==3 and all(len(r.pages)==1 for r in parts),'per-page split delivered in one valid ZIP')
check([p.name for p in Path(split[0]).parent.iterdir()]==['拆分结果.zip'],'split leaves no loose PDF downloads')
custom=job('split',splitMode='ranges',ranges=['3,1','2'])['outputs'][0];parts=archive_pages(custom)
check(len(parts)==2 and ['Charlie' in parts[0].pages[0].extract_text(),'Alpha' in parts[0].pages[1].extract_text(),'Bravo' in parts[1].pages[0].extract_text()]==[True]*3,'custom groups preserve requested page order')
batch=job('split',files=[str(root/'a.pdf'),str(root/'b.pdf')])['outputs'][0]
with zipfile.ZipFile(batch) as archive:check(len(archive.namelist())==5 and len(set(n.split('/')[0] for n in archive.namelist()))==2,'multiple inputs share one ZIP with separate source folders')
for invalid in [[],[''],['0'],['4'],['3-1'],['1,1'],['one']]:
    check(not job('split',expect=False,splitMode='ranges',ranges=invalid)['ok'],'invalid page groups rejected: '+repr(invalid))
job('split',expect=False,files=[str(root/'a.pdf'),str(root/'b.pdf')],splitMode='ranges',ranges=['3'])
check(not list((root/f'job-{len(list(root.glob("job-*")))-1:03d}'/'out').iterdir()),'invalid batch creates no partial ZIP')
horizontal=job('split',splitMode='ratio')['outputs'][0];parts=PdfReader(horizontal)
check(Path(horizontal).suffix=='.pdf' and len(parts.pages)==3 and all(abs(float(p.mediabox.height)-297.5)<0.01 for p in parts.pages),'ratio crop produces one PDF with all original pages')
check(len(list(Path(horizontal).parent.iterdir()))==1,'ratio crop produces no ZIP or per-page files')
check(['Alpha' in parts.pages[0].extract_text(),'Bravo' in parts.pages[1].extract_text(),'Charlie' in parts.pages[2].extract_text()]==[True]*3,'crop preserves page order')
vertical=job('split',splitMode='ratio',axis='vertical',ratio=30)['outputs'][0];parts=PdfReader(vertical)
check(len(parts.pages)==3 and all(abs(float(p.mediabox.width)-126)<0.01 and float(p.mediabox.left)==0 for p in parts.pages),'left crop retains requested thirty percent across all pages')
right=job('split',splitMode='ratio',axis='vertical',ratio=30,keep='second')['outputs'][0];parts=PdfReader(right)
check(all(abs(float(p.mediabox.width)-126)<0.01 and abs(float(p.mediabox.left)-294)<0.01 for p in parts.pages),'right crop retains thirty percent and removes gray left region')
lower=job('split',splitMode='ratio',ratio=30,keep='second')['outputs'][0];parts=PdfReader(lower)
check(len(parts.pages)==3 and all(abs(float(p.mediabox.height)-178.5)<0.01 and float(p.mediabox.bottom)==0 for p in parts.pages),'bottom crop retains requested thirty percent in one document')
batch_crop=job('split',splitMode='ratio',files=[str(root/'a.pdf'),str(root/'b.pdf')])['outputs']
check(len(batch_crop)==2 and [len(PdfReader(p).pages) for p in batch_crop]==[3,2] and all(Path(p).suffix=='.pdf' for p in batch_crop),'batch crop returns one PDF per input without merging or splitting pages')
rotated=PdfWriter();rotated.append(root/'a.pdf',pages=[0]);rotated.pages[0].cropbox.lower_left=(20,40);rotated.pages[0].cropbox.upper_right=(400,550);rotated.pages[0].rotate(90);rotated.write(root/'rotated.pdf');rotated.close()
rotated_crop=job('split',files=[str(root/'rotated.pdf')],splitMode='ratio',axis='vertical',ratio=25)['outputs'][0];parts=PdfReader(rotated_crop)
check(parts.pages[0].rotation==0 and abs(float(parts.pages[0].mediabox.width)-127.5)<0.01 and abs(float(parts.pages[0].mediabox.height)-380)<0.01,'crop respects displayed rotation and original crop box')
import shutil
shutil.copyfile(horizontal,root/'crop-upper.pdf');shutil.copyfile(lower,root/'crop-lower.pdf');shutil.copyfile(right,root/'crop-right.pdf')
check(not job('split',expect=False,splitMode='ratio',keep='invalid')['ok'],'invalid retained side is rejected')
for invalid in [0,100,-5,'nan']:
    check(not job('split',expect=False,splitMode='ratio',ratio=invalid)['ok'],'invalid cut ratio rejected: '+str(invalid))
encrypted=job('encrypt',newPassword='本地测试-123')['outputs'][0]
protected=PdfReader(encrypted);check(protected.is_encrypted and not protected.decrypt('wrong'),'AES encryption rejects wrong password')
protected_zip=job('split',files=[encrypted],password='本地测试-123',splitMode='ranges',ranges=['1-2','3'])['outputs'][0]
check([len(r.pages) for r in archive_pages(protected_zip,'本地测试-123')]==[2,1],'split retains source PDF password in ZIP entries')
protected_crop=job('split',files=[encrypted],password='本地测试-123',splitMode='ratio',keep='second')['outputs'][0];r=PdfReader(protected_crop)
check(r.is_encrypted and r.decrypt('本地测试-123') and len(r.pages)==3,'cropped PDF retains source encryption and all pages')
check(not job('decrypt',files=[encrypted],password='wrong',expect=False)['ok'],'wrong password is a visible failure')
decrypted=job('decrypt',files=[encrypted],password='本地测试-123')['outputs'][0]
check(not PdfReader(decrypted).is_encrypted and len(PdfReader(decrypted).pages)==3,'correct password removes encryption')
encrypted_edit=job('edit',files=[encrypted],password='本地测试-123',pages=[{'index':1,'rotation':180}])['outputs'][0]
r=PdfReader(encrypted_edit);check(r.is_encrypted and r.decrypt('本地测试-123') and len(r.pages)==1,'editing encrypted input retains password')
compressed=job('compress',quality='lossless')['outputs'][0]
check(Path(compressed).stat().st_size<(root/'a.pdf').stat().st_size and 'Bravo' in PdfReader(compressed).pages[1].extract_text(),'lossless compression shrinks repetitive content and preserves text')
Image.effect_noise((1300,1800),90).convert('RGB').save(root/'scanned.pdf',resolution=150)
smaller=job('compress',files=[str(root/'scanned.pdf')],quality='small')['outputs'][0]
check(Path(smaller).stat().st_size<=(root/'scanned.pdf').stat().st_size,'image compression never increases output size')
rendered=job('render',indices=[0,2],edge=400)['outputs'];check(len(rendered)==2 and all(max(Image.open(p).size)<=402 for p in rendered),'thumbnail render bounded dimensions')
png=job('convert',format='png')['outputs'];check(len(png)==3 and Image.open(png[0]).width>400,'PDF to PNG all pages')
jpg=job('convert',format='jpg')['outputs'];check(len(jpg)==3 and Image.open(jpg[0]).format=='JPEG','PDF to JPEG all pages')
text=job('convert',format='txt')['outputs'][0];check('Charlie' in Path(text).read_text(encoding='utf-8-sig'),'PDF text export')
docx=job('convert',format='docx')['outputs'][0]
with zipfile.ZipFile(docx) as package:check('Charlie' in package.read('word/document.xml').decode(),'PDF to editable DOCX paragraphs')
scanned_docx=job('convert',files=[str(root/'scanned.pdf')],format='docx')['outputs'][0]
with zipfile.ZipFile(scanned_docx) as package:check(any(n.startswith('word/media/') for n in package.namelist()),'scanned PDF retained as DOCX image without fake OCR')
check(hashlib.sha256((root/'a.pdf').read_bytes()).hexdigest()==before,'all PDF operations preserve source bytes')
form=PdfWriter();page=form.add_blank_page(420,595)
widget=DictionaryObject({NameObject('/Type'):NameObject('/Annot'),NameObject('/Subtype'):NameObject('/Widget'),NameObject('/FT'):NameObject('/Tx'),NameObject('/T'):TextStringObject('Sample'),NameObject('/V'):TextStringObject('Retained'),NameObject('/Rect'):ArrayObject([NumberObject(n) for n in [20,20,160,45]]),NameObject('/P'):page.indirect_reference})
ref=form._add_object(widget);page[NameObject('/Annots')]=ArrayObject([ref]);form._root_object[NameObject('/AcroForm')]=form._add_object(DictionaryObject({NameObject('/Fields'):ArrayObject([ref])}));form.write(root/'form.pdf');form.close()
form_edit=job('edit',files=[str(root/'form.pdf')],pages=[{'index':0,'rotation':90}])['outputs'][0]
check(PdfReader(form_edit).get_fields()['Sample']['/V']=='Retained','page edit retains canonical form field values')
form_merge=job('merge',files=[str(root/'form.pdf'),str(root/'form.pdf')])['outputs'][0]
fields=PdfReader(form_merge).get_fields();check('document1.Sample' in fields and 'document2.Sample' in fields,'merge namespaces duplicate form names')
form_split=job('split',files=[str(root/'form.pdf')])['outputs'][0]
check(archive_pages(form_split)[0].get_fields()['Sample']['/V']=='Retained','ZIP split retains canonical form fields')
with zipfile.ZipFile(root/'fixture.docx','w') as z:
    z.writestr('[Content_Types].xml','<?xml version="1.0"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/></Types>')
    z.writestr('_rels/.rels','<?xml version="1.0"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/></Relationships>')
    z.writestr('word/document.xml','<?xml version="1.0" encoding="utf-8"?><w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:body><w:p><w:r><w:t>本地预览验收 Local preview</w:t></w:r></w:p><w:p><w:r><w:t>小文件也应快速预览。Alpha Bravo Charlie.</w:t></w:r></w:p><w:sectPr/></w:body></w:document>')
print(f'ALL {checks} PDF CHECKS PASSED',flush=True)
