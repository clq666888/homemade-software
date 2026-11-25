"""Fixtures and independent render/structure checks for the shared watermark layer."""
import sys,json,hashlib
from pathlib import Path
from contextlib import closing
from pypdf import PdfWriter,PdfReader
from pypdf.generic import NameObject,DictionaryObject,ArrayObject,NumberObject,FloatObject,TextStringObject,DecodedStreamObject,RectangleObject
from PIL import Image,ImageChops,ImageStat
import pypdfium2 as pdfium

root=Path(sys.argv[1]).resolve();root.mkdir(parents=True,exist_ok=True)
if len(sys.argv)>2 and sys.argv[2]=='fixtures':
    writer=PdfWriter();font=DictionaryObject({NameObject('/Type'):NameObject('/Font'),NameObject('/Subtype'):NameObject('/Type1'),NameObject('/BaseFont'):NameObject('/Helvetica')})
    for i,rotation in enumerate([0,90,180,270,0,0]):
        page=writer.add_blank_page(595,842);page.rotate(rotation)
        if i==4:page.mediabox=RectangleObject([20,40,615,882]);page.cropbox=RectangleObject([60,100,560,800])
        if i==5:page[NameObject('/UserUnit')]=FloatObject(1.5)
        page[NameObject('/Resources')]=DictionaryObject({NameObject('/Font'):DictionaryObject({NameObject('/F1'):writer._add_object(font)})})
        stream=DecodedStreamObject();stream.set_data(f'0.2 0.5 0.9 rg 100 250 320 100 re f BT /F1 24 Tf 100 500 Td (Original page {i+1}) Tj ET'.encode());page[NameObject('/Contents')]=writer._add_object(stream)
    field=DictionaryObject({NameObject('/Type'):NameObject('/Annot'),NameObject('/Subtype'):NameObject('/Widget'),NameObject('/FT'):NameObject('/Tx'),NameObject('/T'):TextStringObject('name'),NameObject('/V'):TextStringObject('preserved'),NameObject('/Rect'):RectangleObject([80,80,180,100])})
    ref=writer._add_object(field);writer.pages[0][NameObject('/Annots')]=ArrayObject([ref]);writer.root_object[NameObject('/AcroForm')]=writer._add_object(DictionaryObject({NameObject('/Fields'):ArrayObject([ref])}))
    writer.write(root/'sample.pdf');writer.encrypt('local-test',algorithm='AES-256-R5');writer.write(root/'encrypted.pdf');writer.close()
    (root/'original.sha256').write_text(hashlib.sha256((root/'sample.pdf').read_bytes()).hexdigest())
    print('WATERMARK FIXTURES READY');raise SystemExit()

checks=0
def check(condition,name):
    global checks
    assert condition,name
    checks+=1;print('PASS',name)

with pdfium.PdfDocument(root/'sample.pdf') as pdf:
    for i in range(6):
        with closing(pdf[i]) as page:print('PDFIUM PAGE',i,page.get_size())
for style in range(7):
    output=next((root/f'style-{style}').glob('*.pdf'));reader=PdfReader(output);original=PdfReader(root/'sample.pdf')
    check(len(reader.pages)==6 and [p.rotation for p in reader.pages]==[p.rotation for p in original.pages],f'style {style} preserves page count and rotations')
    check(reader.get_fields()['name']['/V']=='preserved' and all(f'Original page {i+1}' in p.extract_text() for i,p in enumerate(reader.pages)),f'style {style} retains text layer and form field tree')
    with pdfium.PdfDocument(output) as pdf:
        for index in range(6):
            with closing(pdf[index]) as page:
                w,h=page.get_size()
                with closing(page.render(scale=min(1300/max(w,h),3))) as bitmap:actual=bitmap.to_pil().convert('RGB')
            with Image.open(root/f'expected-{style}-{index}.png') as expected:
                check(actual.size==expected.size,f'style {style} page {index+1} keeps visible dimensions')
                error=sum(ImageStat.Stat(ImageChops.difference(actual,expected.convert('RGB'))).mean)/3
                check(error<0.8,f'style {style} page {index+1} preview/export pixel agreement: {error:.3f}')
            actual.save(root/f'actual-{style}-{index}.png');actual.close()
for scope in [1,2,3]:
    reader=PdfReader(next((root/f'scope-{scope}').glob('*.pdf')))
    marked=[i+1 for i,p in enumerate(reader.pages) if any(str(key).startswith('/LocalWatermark') for key in p['/Resources'].get('/XObject',{}))]
    check(marked==({1:[1,3,5],2:[2,4,6],3:[2,3]}[scope]),f'scope {scope} only marks selected pages')
reader=PdfReader(next((root/'password').glob('*.pdf')))
check(reader.is_encrypted and reader.decrypt('local-test') and len(reader.pages)==6,'watermark retains original encryption password')
check(hashlib.sha256((root/'sample.pdf').read_bytes()).hexdigest()==(root/'original.sha256').read_text(),'all watermark operations leave original bytes unchanged')
print('ALL',checks,'WATERMARK STRUCTURE AND RENDER CHECKS PASSED')
