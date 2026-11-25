"""Probe and mixed-password routing across all PDF tools."""
import sys,json,subprocess,io,zipfile,hashlib
from pathlib import Path
from pypdf import PdfReader,PdfWriter
from pypdf.generic import NameObject,DictionaryObject,DecodedStreamObject

root=Path(sys.argv[1]).resolve();root.mkdir(parents=True,exist_ok=True)
files=[str(root/'a.pdf'),str(root/'b.pdf')];passwords=dict(zip(files,['alpha-test','beta-test']))
if len(sys.argv)>2 and sys.argv[2]=='fixtures':
    for name,password in [('plain',''),('a','alpha-test'),('b','beta-test'),('owner','')]:
        writer=PdfWriter();font=DictionaryObject({NameObject('/Type'):NameObject('/Font'),NameObject('/Subtype'):NameObject('/Type1'),NameObject('/BaseFont'):NameObject('/Helvetica')})
        for number in range(2):
            page=writer.add_blank_page(400,600);page[NameObject('/Resources')]=DictionaryObject({NameObject('/Font'):DictionaryObject({NameObject('/F1'):writer._add_object(font)})});stream=DecodedStreamObject();stream.set_data(f'BT /F1 20 Tf 40 500 Td ({name} page {number+1}) Tj ET'.encode());page[NameObject('/Contents')]=writer._add_object(stream)
        if name!='plain':writer.encrypt(password,owner_password='owner-test',algorithm='AES-256-R5')
        writer.write(root/f'{name}.pdf');writer.close()
    (root/'bad.pdf').write_bytes(b'not a PDF')
    print('ACCESS FIXTURES READY');raise SystemExit()
backend=Path(__file__).resolve().parents[1]/'engine/pdf_engine.py';packaged=Path(sys.argv[2]).resolve() if len(sys.argv)>2 else None;count=0;checks=0
def check(condition,name):
    global checks
    assert condition,name
    checks+=1;print('PASS',name)
def job(action,expect=True,**values):
    global count
    folder=root/f'job-{count:03d}';count+=1;folder.mkdir();output=folder/'out';output.mkdir();reply=folder/'reply.json';source=folder/'job.json'
    source.write_text(json.dumps({'action':action,'files':files,'passwords':passwords,'folder':str(output),'reply':str(reply),**values}),encoding='utf-8');command=[str(packaged),str(source)] if packaged else [sys.executable,str(backend),str(source)]
    subprocess.run(command,timeout=90,capture_output=True);answer=json.loads(reply.read_text(encoding='utf-8'));assert answer['ok']==expect,answer;return answer
before={p:hashlib.sha256(Path(p).read_bytes()).hexdigest() for p in files}
check(not job('probe',files=[str(root/'plain.pdf')])['needsPassword'],'plain PDF does not request password')
check(job('probe',files=[files[0]],passwords={})['needsPassword'],'locked PDF requests password before admission')
check(job('probe',files=[files[0]],passwords={},password='wrong')['needsPassword'],'wrong password rejected by probe')
check(not job('probe',files=[files[0]])['needsPassword'],'correct password accepted by probe')
check(not job('probe',files=[str(root/'owner.pdf')],passwords={})['needsPassword'],'owner-only protection with empty user password opens without unnecessary prompt')
check(not job('probe',files=[str(root/'bad.pdf')],expect=False)['ok'],'invalid PDF fails admission')
merged=job('merge')['outputs'][0];reader=PdfReader(merged)
check(len(reader.pages)==4 and 'a page' in reader.pages[0].extract_text() and 'b page' in reader.pages[2].extract_text(),'merge resolves each input password independently')
for action,values in [('decrypt',{}),('encrypt',{'newPassword':'new-test'}),('compress',{'quality':'lossless'}),('split',{'splitMode':'ratio','ratio':60,'axis':'horizontal'})]:
    outputs=job(action,**values)['outputs'];valid=len(outputs)==2
    for index,path in enumerate(outputs):
        reader=PdfReader(path);password='new-test' if action=='encrypt' else passwords[files[index]]
        valid=valid and (not reader.is_encrypted if action=='decrypt' else reader.is_encrypted and bool(reader.decrypt(password))) and len(reader.pages)==2
    check(valid,action+' handles mixed passwords and correct output encryption')
archive=job('split',splitMode='each')['outputs'][0]
with zipfile.ZipFile(archive) as parts:
    valid=len(parts.namelist())==4
    for name in parts.namelist():
        reader=PdfReader(io.BytesIO(parts.read(name)));password='alpha-test' if name.startswith('001_') else 'beta-test';valid=valid and reader.is_encrypted and bool(reader.decrypt(password)) and len(reader.pages)==1
    check(valid,'ZIP split preserves each source password separately')
edited=job('edit',files=[files[1]],pages=[{'index':1,'rotation':90}])['outputs'][0];reader=PdfReader(edited)
check(reader.is_encrypted and reader.decrypt('beta-test') and len(reader.pages)==1 and reader.pages[0].rotation==90,'page editing uses selected input password')
for fmt in ['png','txt','docx','xlsx','pptx','html']:
    outputs=job('convert',format=fmt)['outputs'];check(len(outputs)==(4 if fmt=='png' else 2) and all(Path(p).stat().st_size>0 for p in outputs),'mixed-password export '+fmt)
for action in ['compress','edit']:
    values={'files':[str(root/'owner.pdf')],'passwords':{}}
    if action=='edit':values['pages']=[{'index':0}]
    outputs=job(action,**values)['outputs'];reader=PdfReader(outputs[0]);check(reader.is_encrypted and reader.decrypt(''),'empty user password remains supported by '+action)
check(all(hashlib.sha256(Path(p).read_bytes()).hexdigest()==before[p] for p in files),'all operations preserve encrypted input bytes')
print('ALL',checks,'PDF ACCESS AND MIXED PASSWORD CHECKS PASSED')
