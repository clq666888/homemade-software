"""Explicit opt-in downloads for native speech regression, never bundled by build."""
import argparse,concurrent.futures,hashlib,re,urllib.request
from pathlib import Path
root=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser();parser.add_argument('--download',action='store_true');args=parser.parse_args()
source=(root/'src/SpeechCatalog.cs').read_text('utf-8')
items=re.findall(r'P\("([^"]+)","[^"]*","[^"]*","([^"]+)",(\d+),"([0-9a-f]{64})"',source)
folder=root/'.work/speech-artifacts';folder.mkdir(parents=True,exist_ok=True)
def restore(item):
    key,url,size,digest=item
    if key not in ('whisper-runtime','sherpa-runtime','whisper-tiny','kokoro-11'):return
    path=folder/key
    def valid():return path.exists() and path.stat().st_size==int(size) and hashlib.sha256(path.read_bytes()).hexdigest()==digest
    if not valid():
        if not args.download:raise RuntimeError('Missing official artifact '+key+'; rerun with --download to fetch test resources.')
        with urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'FileTools-development-tests'}),timeout=90) as response,path.open('wb') as output:
            while chunk:=response.read(1024*1024):output.write(chunk)
        if not valid():raise RuntimeError('SHA256/size mismatch: '+key)
    print(key+' verified',flush=True)
with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:list(pool.map(restore,items))
