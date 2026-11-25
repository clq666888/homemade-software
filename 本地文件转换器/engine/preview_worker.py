"""One serial PDFium preview process with an open document and bounded page cache."""
import json
import queue
import shutil
import sys
import threading
import uuid
from collections import OrderedDict
from contextlib import closing
from pathlib import Path
import pypdfium2 as pdfium
from pypdf import PdfReader


def serve(folder):
    # PyInstaller's windowed bootloader sets sys.std* to None. The GUI still
    # supplies real inherited pipe handles; restore wrappers without a console.
    if sys.stdin is None or sys.stdout is None:
        import ctypes
        from ctypes import wintypes
        import msvcrt
        import os
        get_handle=ctypes.windll.kernel32.GetStdHandle;get_handle.argtypes=[wintypes.DWORD];get_handle.restype=wintypes.HANDLE
        if sys.stdin is None:sys.stdin=os.fdopen(msvcrt.open_osfhandle(get_handle(wintypes.DWORD(-10)),os.O_RDONLY|os.O_BINARY),'r',encoding='utf-8')
        if sys.stdout is None:sys.stdout=os.fdopen(msvcrt.open_osfhandle(get_handle(wintypes.DWORD(-11)),os.O_WRONLY|os.O_BINARY),'w',encoding='utf-8',buffering=1)
    sys.stdin.reconfigure(encoding='utf-8');sys.stdout.reconfigure(encoding='utf-8')
    folder=Path(folder);folder.mkdir(parents=True,exist_ok=True);requests=queue.Queue()
    def receive():
        try:
            for line in sys.stdin:
                try:requests.put(json.loads(line))
                except ValueError:requests.put({})
        finally:requests.put(None)
    threading.Thread(target=receive,daemon=True).start()
    document=None;metadata=None;identity=None;cache=OrderedDict();snapshot=folder/'source.pdf'
    def clear():
        nonlocal document,metadata,identity
        if document is not None:document.close();document=None
        if metadata is not None:metadata.close();metadata=None
        identity=None
        for path in cache.values():path.unlink(missing_ok=True)
        cache.clear();snapshot.unlink(missing_ok=True)
    def render(index,rotation,edge):
        key=(index,rotation,edge)
        if key in cache:
            cache.move_to_end(key);return cache[key],True
        while len(cache)>=20:cache.popitem(last=False)[1].unlink(missing_ok=True)
        with closing(document[index]) as page:
            w,h=page.get_size()
            with closing(page.render(scale=min(edge/max(w,h),3),rotation=rotation)) as bitmap:
                image=bitmap.to_pil();path=folder/(uuid.uuid4().hex+'.png')
                try:image.save(path)
                finally:image.close()
        cache[key]=path
        return path,False
    try:
        while True:
            job=requests.get()
            if job is None:break
            try:
                source=Path(job['source']);info=source.stat();secret=job.get('password','')
                key=(str(source.resolve()),info.st_mtime_ns,info.st_size,secret)
                if key!=identity:
                    clear();shutil.copyfile(source,snapshot)
                    try:document=pdfium.PdfDocument(snapshot,password=secret or None)
                    except Exception as error:
                        clear();raise ValueError('PDF 无法预览，请检查文件和打开密码。') from error
                    metadata=PdfReader(snapshot)
                    if metadata.is_encrypted:metadata.decrypt(secret)
                    identity=key
                count=len(document);index=int(job['index']);rotation=int(job.get('rotation',0))%360;edge=max(100,min(int(job.get('edge',1300)),2000))
                if not 0<=index<count:raise ValueError('预览页码超出范围。')
                if rotation not in (0,90,180,270):raise ValueError('页面旋转角度无效。')
                path,cached=render(index,rotation,edge)
                with closing(document[index]) as page:width,height=page.get_size()
                unit=float(metadata.pages[index].get('/UserUnit',1));width*=unit;height*=unit
                if rotation in (90,270):width,height=height,width
                answer={'ok':True,'path':str(path),'count':count,'cached':cached,'width':width,'height':height}
            except Exception as error:
                answer={'ok':False,'error':str(error)}
            sys.stdout.write(json.dumps(answer,ensure_ascii=True)+'\n');sys.stdout.flush()
            if answer['ok']:
                # PDFium is used only on this thread. The reader thread just queues JSON.
                # Stop speculative work as soon as a foreground request arrives.
                for adjacent in (index+1,index-1):
                    if not requests.empty():break
                    if 0<=adjacent<count:
                        try:render(adjacent,rotation,edge)
                        except Exception:pass  # Foreground retry reports any actual render failure.
    finally:clear()

