"""Transparent image layers; preserve original page contents, rotation and form tree."""
from pathlib import Path
from pypdf import PdfWriter
from pypdf.generic import DictionaryObject, NameObject, NumberObject, DecodedStreamObject
from PIL import Image
from pdf_access import password_for


def geometry(page):
    media, crop = page.mediabox, page.cropbox
    x, y = max(float(media.left), float(crop.left)), max(float(media.bottom), float(crop.bottom))
    w = min(float(media.right), float(crop.right)) - x
    h = min(float(media.top), float(crop.top)) - y
    rotation = page.rotation % 360
    if w <= 0 or h <= 0 or rotation not in (0, 90, 180, 270):
        raise ValueError('PDF 页面尺寸或旋转角度无效。')
    # Map the visible, upright image back into the unchanged PDF coordinate space.
    matrix = {0:(w,0,0,h,x,y),90:(0,h,-w,0,x+w,y),180:(-w,0,0,-h,x+w,y+h),270:(0,-h,w,0,x,y+h)}[rotation]
    width,height = (h,w) if rotation in (90,270) else (w,h)
    unit = float(page.get('/UserUnit',1))
    width *= unit; height *= unit
    key = f'{width:.4f}x{height:.4f}'
    return key,width,height,matrix


def selected(job,number):
    scope = int(job.get('scope',0))
    return scope == 0 or scope == 1 and number%2 == 1 or scope == 2 and number%2 == 0 or scope == 3 and int(job.get('first',1)) <= number <= int(job.get('last',1))


def plan(job,read_pdf):
    sizes={}
    if int(job.get('scope',0)) not in (0,1,2,3):raise ValueError('水印页面范围无效。')
    for source in job['files']:
        reader=read_pdf(source,password_for(job,source))
        try:
            if int(job.get('scope',0))==3 and not 1<=int(job.get('first',1))<=int(job.get('last',1))<=len(reader.pages):
                raise ValueError(f'{Path(source).name}：水印页码范围超出文档页数。')
            found=False
            for number,page in enumerate(reader.pages,1):
                if not selected(job,number):continue
                found=True;key,width,height,_=geometry(page)
                sizes[key]={'key':key,'width':width,'height':height}
            if not found:raise ValueError(f'{Path(source).name}：所选范围没有可添加水印的页面。')
        finally:reader.close()
    return {'sizes':list(sizes.values())}


def image_object(writer,path):
    with Image.open(path) as image:
        rgba=image.convert('RGBA');width,height=rgba.size
        if width*height>11000000:raise ValueError('水印图层过大。')
        mask=DecodedStreamObject();mask.set_data(rgba.getchannel('A').tobytes())
        mask.update({NameObject('/Type'):NameObject('/XObject'),NameObject('/Subtype'):NameObject('/Image'),NameObject('/Width'):NumberObject(width),NameObject('/Height'):NumberObject(height),NameObject('/ColorSpace'):NameObject('/DeviceGray'),NameObject('/BitsPerComponent'):NumberObject(8)})
        rgb=DecodedStreamObject();rgb.set_data(rgba.convert('RGB').tobytes());rgba.close()
        rgb.update({NameObject('/Type'):NameObject('/XObject'),NameObject('/Subtype'):NameObject('/Image'),NameObject('/Width'):NumberObject(width),NameObject('/Height'):NumberObject(height),NameObject('/ColorSpace'):NameObject('/DeviceRGB'),NameObject('/BitsPerComponent'):NumberObject(8),NameObject('/SMask'):writer._add_object(mask.flate_encode())})
        return writer._add_object(rgb.flate_encode())


def execute(job,read_pdf,progress,write_pdf):
    required=plan(job,read_pdf)['sizes'];paths={x['key']:x['path'] for x in job.get('layers',[])}
    for size in required:
        if size['key'] not in paths:raise ValueError('水印图层不完整，请重试。')
    outputs=[];files=job['files'];password=job.get('password','')
    for file_index,source in enumerate(files):
        password=password_for(job,source)
        reader=read_pdf(source,password);writer=PdfWriter(clone_from=reader)
        try:
            images={}
            for page_index,page in enumerate(writer.pages):
                if selected(job,page_index+1):
                    key,_,_,matrix=geometry(page)
                    if key not in images:images[key]=image_object(writer,paths[key])
                    # Copy resource dictionaries before modifying: pages can share inherited resources.
                    resources=DictionaryObject(page.get('/Resources',DictionaryObject()).get_object())
                    xobjects=DictionaryObject(resources.get('/XObject',DictionaryObject()).get_object())
                    name='/LocalWatermark';suffix=0
                    while name in xobjects:suffix+=1;name=f'/LocalWatermark{suffix}'
                    xobjects[NameObject(name)]=images[key];resources[NameObject('/XObject')]=xobjects;page[NameObject('/Resources')]=resources
                    original=page.get_contents()
                    if original is not None:
                        original.isolate_graphics_state();page.replace_contents(original)
                    stream=DecodedStreamObject();stream.set_data(('q '+' '.join(f'{value:.8f}' for value in matrix)+' cm '+name+' Do Q\n').encode('ascii'))
                    from pypdf.generic import ArrayObject
                    old=page.get('/Contents')
                    content=ArrayObject(list(old.get_object()) if old is not None and isinstance(old.get_object(),ArrayObject) else [old] if old is not None else [])
                    content.append(writer._add_object(stream));page[NameObject('/Contents')]=content
                progress(job,int(95*(file_index+(page_index+1)/max(1,len(writer.pages)))/len(files)),f'正在添加水印：第 {page_index+1}/{len(writer.pages)} 页')
            if reader.is_encrypted:writer.encrypt(password,algorithm='AES-256-R5')
            dest=Path(job['folder'])/f'{file_index+1}_{Path(source).stem}_水印.pdf';write_pdf(writer,dest);outputs.append(str(dest))
        finally:writer.close();reader.close()
    return {'outputs':outputs,'note':'已添加水印，原文件保留。'}
