"""Restore only locked x64 OCR dependencies; never install system-wide runtimes."""
import hashlib
import json
import shutil
import subprocess
import tempfile
import urllib.request
import zipfile
from pathlib import Path

root=Path(__file__).resolve().parents[1]
lock=json.loads((root/'assets/image-runtime.lock.json').read_text('utf-8'))
stage=root/'.work/release/ocr'
def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def valid(folder):
    return all((folder/name).is_file() and (folder/name).stat().st_size==info['bytes'] and digest(folder/name)==info['sha256'] for name,info in lock['files'].items())
def fetch(url,path,sha=None):
    with urllib.request.urlopen(url,timeout=90) as response,path.open('wb') as out:shutil.copyfileobj(response,out)
    if sha and digest(path)!=sha:raise RuntimeError('Dependency hash changed; review it before updating the lock: '+url)
def expand(cab,target,pattern='*'):
    target.mkdir(parents=True,exist_ok=True)
    subprocess.run(['expand.exe',str(cab),'-F:'+pattern,str(target)],check=True,stdout=subprocess.DEVNULL)

if not valid(stage):
    published=root/'dist/文件万能处理器/ocr'
    if valid(published):shutil.copytree(published,stage,dirs_exist_ok=True)
    else:
        (root/'.work').mkdir(exist_ok=True)
        with tempfile.TemporaryDirectory(prefix='ocr-restore-',dir=root/'.work') as temp:
            temp=Path(temp);native=temp/'runtime';(native/'licenses').mkdir(parents=True);(native/'tessdata').mkdir()
            package=temp/'tesseract.zip';fetch(lock['nuget']['url'],package,lock['nuget']['sha256'])
            with zipfile.ZipFile(package) as z:
                for name in ('tesseract50.dll','leptonica-1.82.0.dll'):(native/name).write_bytes(z.read('x64/'+name))
            rev=lock['modelsRevision'];base=f'https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/{rev}/'
            for name in ('chi_sim','eng'):fetch(base+name+'.traineddata',native/'tessdata'/f'{name}.traineddata')
            fetch(base+'LICENSE',native/'licenses/models-LICENSE.txt');shutil.copyfile(native/'licenses/models-LICENSE.txt',native/'licenses/Tesseract-LICENSE.txt')
            fetch('https://raw.githubusercontent.com/DanBloomberg/leptonica/1.82.0/leptonica-license.txt',native/'licenses/Leptonica-LICENSE.txt')
            redist=temp/'vc.exe';fetch(lock['redist']['url'],redist,lock['redist']['sha256'])
            data=redist.read_bytes();offset=0
            while True:
                at=data.find(b'MSCF',offset)
                if at<0:break
                offset=at+4;size=int.from_bytes(data[at+8:at+12],'little')
                if not 36<size<=len(data)-at:continue
                cab=temp/f'{at}.cab';cab.write_bytes(data[at:at+size]);expand(cab,temp/'cabs')
            # Burn's signed, hash-pinned manifest maps license.rtf to u4.
            shutil.copyfile(temp/'cabs/u4',native/'licenses/VC-LICENSE.rtf')
            for payload in (temp/'cabs').iterdir():
                b=payload.read_bytes()
                if b[:4]==b'MSCF':
                    for name in ('msvcp140','vcruntime140','vcruntime140_1'):
                        # expand returns success when there is no matching member as well.
                        expand(payload,temp/'dlls',name+'.dll_amd64')
            for name in ('msvcp140','vcruntime140','vcruntime140_1'):shutil.copyfile(temp/'dlls'/f'{name}.dll_amd64',native/f'{name}.dll')
            if not valid(native):raise RuntimeError('Restored OCR runtime differs from the lock')
            shutil.copytree(native,stage,dirs_exist_ok=True)
if not valid(stage):raise RuntimeError('OCR runtime validation failed')
shutil.copyfile(root/'assets/image-sources.txt',stage/'SOURCES.txt')
print('OCR_RUNTIME_BYTES='+str(sum(p.stat().st_size for p in stage.rglob('*') if p.is_file())))
