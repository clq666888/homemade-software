"""Explicit optional fixture download; source of truth is VideoCatalog.cs."""
import hashlib
import re
import sys
import urllib.request
from pathlib import Path

root=Path(__file__).resolve().parents[1]
catalog=(root/'src/VideoCatalog.cs').read_text('utf-8')
url=re.search(r'Url="([^"]+)"',catalog)[1]
expected=int(re.search(r'Bytes=(\d+)',catalog)[1])
sha=re.search(r'Sha256="([a-f0-9]+)"',catalog)[1]
archive=root/'.work/video-runtime.zip'
def valid():
    if not archive.is_file() or archive.stat().st_size!=expected:return False
    digest=hashlib.sha256()
    with archive.open('rb') as stream:
        for chunk in iter(lambda:stream.read(1024*1024),b''):digest.update(chunk)
    return digest.hexdigest()==sha
if not valid():
    if '--download' not in sys.argv:raise SystemExit('Video fixture missing. Run scripts/test-video.ps1 -DownloadFixture to explicitly download the 92.37 MB test engine.')
    archive.parent.mkdir(parents=True,exist_ok=True)
    temporary=archive.with_suffix('.zip.part')
    try:
        with urllib.request.urlopen(url,timeout=90) as response,temporary.open('wb') as output:
            while chunk:=response.read(1024*1024):output.write(chunk)
        temporary.replace(archive)
        if not valid():raise RuntimeError('Video test archive size or SHA256 mismatch')
    finally:
        temporary.unlink(missing_ok=True)
print('VIDEO_TEST_ARCHIVE='+str(archive))
