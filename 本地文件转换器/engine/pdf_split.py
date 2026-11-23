"""Page/group splitting produces a ZIP; proportional cropping retains each document."""
import math
import re
import tempfile
import zipfile
from pathlib import Path
from pypdf import PdfWriter
from pypdf.generic import RectangleObject


def page_range(value, count):
    text=str(value).strip().replace('，',',').replace('–','-').replace('—','-')
    if not text: raise ValueError('每一份都必须填写页码，例如 1-3,5。')
    result=[]
    for item in text.split(','):
        match=re.fullmatch(r'\s*(\d+)\s*(?:-\s*(\d+)\s*)?',item)
        if not match: raise ValueError(f'页码格式错误：{item}；请使用 1-3,5 这样的格式。')
        first=int(match[1]);last=int(match[2] or first)
        if first<1 or last<first or last>count:
            raise ValueError(f'页码范围 {item} 超出有效范围 1-{count}，或起止顺序错误。')
        result.extend(range(first-1,last))
        if len(result)>count*2: raise ValueError('同一份中请勿重复填写页码。')
    if len(set(result))!=len(result): raise ValueError('同一份中请勿重复填写页码。')
    return result


def execute_split(job, read_pdf, progress, atomic_path):
    files=job.get('files',[]);password=job.get('password','');method=job.get('splitMode','each')
    if not files: raise ValueError('请先添加 PDF。')
    if method not in ('each','ranges','ratio'): raise ValueError('拆分方式无效。')
    ratio=float(job.get('ratio',50));axis=job.get('axis','horizontal')
    if method=='ratio' and (not math.isfinite(ratio) or not 0<ratio<100 or axis not in ('horizontal','vertical')):
        raise ValueError('切割比例必须大于 0% 且小于 100%，方向必须选择上下或左右。')
    keep=job.get('keep','first')
    if method=='ratio' and keep not in ('first','second'):raise ValueError('请选择要保留的区域。')
    ranges=job.get('ranges',[])
    if method=='ranges' and (not isinstance(ranges,list) or not 1<=len(ranges)<=100):
        raise ValueError('请设置 1-100 份，并填写每份的页码。')
    # Validate every input before opening the final archive. A bad batch yields no partial download.
    plans=[];total=0
    for source in files:
        reader=read_pdf(source,password);count=len(reader.pages)
        if count==0: raise ValueError(f'{Path(source).name} 没有可拆分页面。')
        if method=='ranges':
            try:groups=[page_range(value,count) for value in ranges]
            except ValueError as error:raise ValueError(f'{Path(source).name}：{error}') from error
        else:groups=None
        plans.append((source,count,groups))
        total+=len(groups) if groups is not None else count
    if method=='ratio':
        outputs=[];done=0
        for number,(source,count,_) in enumerate(plans):
            reader=read_pdf(source,password);writer=PdfWriter()
            try:
                writer.append(reader)
                for page in writer.pages:
                    if page.rotation:page.transfer_rotation_to_content()
                    box=page.cropbox;left,bottom,right,top=map(float,[box.left,box.bottom,box.right,box.top]);fraction=ratio/100
                    if axis=='horizontal':
                        bounds=(left,top-(top-bottom)*fraction,right,top) if keep=='first' else (left,bottom,right,bottom+(top-bottom)*fraction)
                    else:
                        bounds=(left,bottom,left+(right-left)*fraction,top) if keep=='first' else (right-(right-left)*fraction,bottom,right,top)
                    for attr in ('mediabox','cropbox','trimbox','bleedbox','artbox'):setattr(page,attr,RectangleObject(bounds))
                    done+=1;progress(job,int(95*done/max(1,total)),f'正在裁剪第 {done}/{total} 页')
                if reader.is_encrypted:writer.encrypt(password,algorithm='AES-256-R5')
                dest=Path(job['folder'])/f'{number+1}_{Path(source).stem}_裁剪.pdf'
                atomic_path(dest,lambda temporary:writer.write(temporary));outputs.append(str(dest))
            finally:writer.close()
        return {'outputs':outputs,'note':'每个原文件生成一份裁剪后的 PDF，保留全部页数与顺序。'}
    dest=Path(job['folder'])/'拆分结果.zip';done=0
    def build(archive_path):
        nonlocal done
        with tempfile.TemporaryDirectory(prefix='split-',dir=Path(job['reply']).parent) as work:
            with zipfile.ZipFile(archive_path,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=6,allowZip64=True) as archive:
                for number,(source,count,groups) in enumerate(plans):
                    reader=read_pdf(source,password)
                    prefix=f'{number+1:03d}_{Path(source).stem}'
                    if groups is None: groups=[[i] for i in range(count)]
                    for group_index,indices in enumerate(groups):
                        writer=PdfWriter()
                        try:
                            writer.append(reader,pages=indices)
                            if method=='ranges':name=f'第{group_index+1:03d}份.pdf'
                            else:name=f'第{group_index+1:04d}页.pdf'
                            if reader.is_encrypted:writer.encrypt(password,algorithm='AES-256-R5')
                            target=Path(work)/'part.pdf';writer.write(target)
                            archive.write(target,prefix+'/'+name)
                            done+=1;progress(job,int(95*done/max(1,total)),f'已打包 {done}/{total} 份 PDF')
                        finally:writer.close()
    atomic_path(dest,build)
    note=f'已将 {total} 份 PDF 打包为一个 ZIP，原文件保留。'
    return {'outputs':[str(dest)],'note':note}
