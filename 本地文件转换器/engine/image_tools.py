"""Bounded raster conversion and offline OCR. Original files are never overwritten."""
from __future__ import annotations
import ctypes as C
import io
import os
import shutil
import sys
import tempfile
from pathlib import Path
from PIL import Image, ImageOps, ImageCms

Image.MAX_IMAGE_PIXELS = 40_000_000
FORMATS = {'jpg':'JPEG','png':'PNG','webp':'WEBP','avif':'AVIF','gif':'GIF',
           'bmp':'BMP','tiff':'TIFF','ico':'ICO','jp2':'JPEG2000'}
MULTI = {'PNG','WEBP','AVIF','GIF','TIFF'}

def opened(path):
    im = Image.open(path)
    if im.width * im.height > 40_000_000:
        im.close()
        raise ValueError('图片超过 4000 万像素，请先缩小。')
    if im.format not in set(FORMATS.values()):
        im.close()
        raise ValueError('本版支持 JPG、PNG、WebP、AVIF、GIF、BMP、TIFF、ICO、JPEG 2000。')
    return im

def normalized(im):
    out = ImageOps.exif_transpose(im).convert('RGBA')
    icc = im.info.get('icc_profile')
    if icc and len(icc) < 4_000_000:
        try:
            rgb = ImageCms.profileToProfile(out.convert('RGB'), ImageCms.ImageCmsProfile(io.BytesIO(icc)), ImageCms.createProfile('sRGB'), outputMode='RGB')
            rgb.putalpha(out.getchannel('A')); out.close(); out = rgb
        except (ValueError, OSError, ImageCms.PyCMSError):
            pass
    return out

def ocr(job):
    # Loading inside the isolated worker permits prompt cancellation without unsafe native thread aborts.
    import ctypes
    runtime = Path(sys.executable).parent.parent / 'ocr' if getattr(sys, 'frozen', False) else Path(__file__).resolve().parents[1]/'.work/release/ocr'
    if not (runtime/'tesseract50.dll').exists():
        raise ValueError('缺少离线文字识别组件，请保留完整发行文件夹。')
    handles = [os.add_dll_directory(str(runtime))]
    lib = ctypes.CDLL(str(runtime/'tesseract50.dll'))
    def fn(name, ret, *args):
        f = getattr(lib, name); f.restype=ret; f.argtypes=list(args); return f
    ptr=C.c_void_p; integer=C.c_int
    create=fn('TessBaseAPICreate',ptr)
    init=fn('TessBaseAPIInit3',integer,ptr,C.c_char_p,C.c_char_p)
    set_image=fn('TessBaseAPISetImage',None,ptr,C.c_char_p,integer,integer,integer,integer)
    set_mode=fn('TessBaseAPISetPageSegMode',None,ptr,integer)
    set_dpi=fn('TessBaseAPISetSourceResolution',None,ptr,integer)
    recognize=fn('TessBaseAPIRecognize',integer,ptr,ptr)
    get_iter=fn('TessBaseAPIGetIterator',ptr,ptr)
    get_page=fn('TessResultIteratorGetPageIterator',ptr,ptr)
    get_text=fn('TessResultIteratorGetUTF8Text',ptr,ptr,integer)
    next_item=fn('TessResultIteratorNext',integer,ptr,integer)
    bounds=fn('TessPageIteratorBoundingBox',integer,ptr,integer,*([C.POINTER(integer)]*4))
    begin_line=fn('TessPageIteratorIsAtBeginningOf',integer,ptr,integer)
    delete_text=fn('TessDeleteText',None,ptr)
    delete_iter=fn('TessResultIteratorDelete',None,ptr)
    destroy=fn('TessBaseAPIDelete',None,ptr)
    api=create(); iterator=None
    try:
        # Tesseract's old Windows file APIs do not reliably support Unicode model paths.
        # A private ASCII temp directory is used only for this job, never a global cache.
        with tempfile.TemporaryDirectory(prefix='FileToolsOCR-') as data:
            for name in ('chi_sim','eng'): shutil.copyfile(runtime/'tessdata'/f'{name}.traineddata',Path(data)/f'{name}.traineddata')
            language=job.get('language','chi_sim+eng')
            if language not in ('eng','chi_sim','chi_sim+eng'):raise ValueError('不支持的识别语言。')
            if init(api,os.fsencode(data),language.encode('ascii')):
                raise ValueError('无法加载离线中英文识别模型。')
            with opened(job['files'][0]) as source:
                im=normalized(source); width,height=im.size
            from ocr_preprocess import prepare
            im=prepare(im,job.get('enhance','auto'));w,h=im.size;pixels=im.tobytes();im.close()
            set_image(api,pixels,w,h,3,w*3);set_mode(api,11 if job.get('sparse',False) else 3);set_dpi(api,300)
            if recognize(api,None): raise ValueError('未能完成文字识别，请检查图片清晰度。')
            iterator=get_iter(api); words=[]
            if iterator:
                page=get_page(iterator)
                while True:
                    text=get_text(iterator,2)
                    if text:
                        try: value=C.string_at(text).decode('utf-8').strip()
                        finally: delete_text(text)
                        box=[integer() for _ in range(4)]
                        if value and bounds(page,2,*[C.byref(x) for x in box]):
                            x,y,r,b=[v.value for v in box]
                            words.append({'text':value,'x':x*width/w,'y':y*height/h,'w':(r-x)*width/w,'h':(b-y)*height/h,'newline':bool(begin_line(page,2))})
                    if not next_item(iterator,2):break
            from ocr_lines import merge_lines
            return {'words':merge_lines(words),'width':width,'height':height}
    finally:
        if iterator:delete_iter(iterator)
        destroy(api)
        for h in handles:h.close()

def execute(job, progress, atomic):
    action=job['action']
    if action=='image_ocr':return ocr(job)
    files=job['files']; folder=Path(job['folder']);folder.mkdir(parents=True,exist_ok=True)
    if action in ('image_info','image_preview'):
        with opened(files[0]) as im:
            frames=getattr(im,'n_frames',1);fmt=im.format
            out=normalized(im)
            result={'width':out.width,'height':out.height,'frames':frames,'format':fmt}
            if action=='image_preview':
                dest=folder/'preview.png';atomic(dest,lambda p:out.save(p,format='PNG'));result['outputs']=[str(dest)]
            out.close();return result
    fmt=FORMATS.get(job.get('format','png'))
    if not fmt:raise ValueError('不支持的图片输出格式。')
    outputs=[];notes=[]
    for number,path in enumerate(files):
        progress(job,int(95*number/max(1,len(files))),f'正在处理图片 {number+1}/{len(files)}')
        with opened(path) as source:
            count=getattr(source,'n_frames',1);first=bool(job.get('firstOnly',False))
            if count>1 and not first and fmt not in MULTI:
                raise ValueError(f'{Path(path).name} 包含多帧/多页。请选择支持多帧的格式，或勾选“仅取第一帧”。')
            count=1 if first else count
            if count>500 or count*source.width*source.height>120_000_000:
                raise ValueError('多帧图片超过本版处理预算（500 帧或合计 1.2 亿像素），可选择仅取第一帧。')
            frames=[];durations=[]
            try:
                for i in range(count):
                    source.seek(i);im=normalized(source)
                    edge=int(job.get('edge',0))
                    if edge:im.thumbnail((edge,edge),Image.Resampling.LANCZOS)
                    if fmt in ('JPEG','BMP','JPEG2000'):
                        white=Image.new('RGB',im.size,'white');white.paste(im,mask=im.getchannel('A'));im.close();im=white
                    if fmt=='ICO':
                        im.thumbnail((256,256),Image.Resampling.LANCZOS)
                        square=Image.new('RGBA',(256,256));square.alpha_composite(im,((256-im.width)//2,(256-im.height)//2));im.close();im=square
                    frames.append(im);durations.append(max(1,int(round(source.info.get('duration',100)))))
                options={}
                quality=int(job.get('quality',85))
                if not 1<=quality<=100:raise ValueError('图片质量必须在 1–100 之间。')
                if fmt in ('JPEG','WEBP','AVIF'):options.update(quality=quality)
                if fmt=='JPEG':options.update(optimize=True,progressive=True)
                if fmt=='WEBP':options.update(lossless=bool(job.get('lossless',False)),method=4)
                if fmt=='PNG':options.update(optimize=True)
                if fmt=='TIFF':options.update(compression='tiff_deflate')
                if fmt=='ICO':options.update(sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])
                if count>1:
                    options.update(save_all=True,append_images=frames[1:])
                    if fmt in ('GIF','WEBP','PNG','AVIF'):options.update(duration=durations,loop=int(source.info.get('loop',0)))
                ext=job.get('format','png');dest=folder/f'{number+1}_{Path(path).stem}.{ext}'
                atomic(dest,lambda p:frames[0].save(p,format=fmt,**options));outputs.append(str(dest))
                if first and getattr(source,'n_frames',1)>1:notes.append('已按选择仅导出第一帧/页。')
                if fmt=='ICO':notes.append('ICO 最大 256×256 像素。')
                if fmt=='GIF':notes.append('GIF 使用最多 256 色，半透明会量化。')
            finally:
                for im in frames:im.close()
    return {'outputs':outputs,'note':'\n'.join(dict.fromkeys(notes))}
