"""Exercise the shipped worker, including actual codecs, animation and OCR geometry."""
import hashlib,json,subprocess,sys
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont

root=Path(sys.argv[1]).resolve();root.mkdir(parents=True,exist_ok=True)
exe=Path(sys.argv[2]).resolve();checks=0;sequence=0
def check(ok,label):
    global checks
    assert ok,label
    checks+=1;print('PASS',label)
def run(action,files,expect=True,**values):
    global sequence
    sequence+=1;folder=root/f'job-{sequence}';folder.mkdir();reply=folder/'reply.json';job=folder/'job.json'
    job.write_text(json.dumps(dict(action=action,files=[str(p) for p in files],folder=str(folder),reply=str(reply),**values)),encoding='utf-8')
    command=[sys.executable,str(exe)] if exe.suffix=='.py' else [str(exe)]
    subprocess.run(command+[str(job)],timeout=120,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
    data=json.loads(reply.read_text('utf-8'));assert data['ok']==expect,data
    return data

source=root/'中文 透明样本.png';im=Image.new('RGBA',(800,500),(255,255,255,0));d=ImageDraw.Draw(im);d.rectangle((150,100,650,400),fill=(20,100,230,255));im.save(source);before=hashlib.sha256(source.read_bytes()).hexdigest()
for fmt in ('png','jpg','webp','avif','gif','bmp','tiff','ico','jp2'):
    result=run('image_convert',[source],format=fmt);out=Path(result['outputs'][0])
    with Image.open(out) as decoded:
        decoded.load();check(decoded.size==((256,256) if fmt=='ico' else (800,500)),fmt+' real encoder dimensions')
        if fmt=='jpg':check(min(decoded.convert('RGB').getpixel((0,0)))>245,'JPEG flattens transparent background to white')
    back=run('image_preview',[out]);check(Path(back['outputs'][0]).is_file(),fmt+' reopens with packaged decoder')
frames=[Image.new('RGBA',(80,60),color) for color in ('red','blue')];animated=root/'动画.png';frames[0].save(animated,save_all=True,append_images=frames[1:],duration=[150,250],loop=2)
for fmt in ('gif','webp','avif','png','tiff'):
    data=run('image_convert',[animated],format=fmt)
    with Image.open(data['outputs'][0]) as decoded:check(decoded.n_frames==2,fmt+' preserves multiple frames/pages')
bad=run('image_convert',[animated],expect=False,format='jpg');check('第一帧' in bad['error'],'static output requires explicit flatten choice')
flat=run('image_convert',[animated],format='jpg',firstOnly=True);check(len(flat['outputs'])==1,'explicit first frame generates one static result')
photo=root/'旋转.jpg';exif=Image.Exif();exif[274]=6;Image.new('RGB',(300,100),'red').save(photo,exif=exif)
info=run('image_preview',[photo]);check((info['width'],info['height'])==(100,300),'EXIF orientation normalized once')
small=run('image_convert',[source],format='webp',edge=320,quality=55)
with Image.open(small['outputs'][0]) as decoded:check(decoded.size==(320,200),'compression longest edge preserves aspect')
invalid=root/'损坏.png';invalid.write_bytes(b'not an image');run('image_preview',[invalid],expect=False);check(True,'corrupt input rejected in isolated worker')
ocr=root/'中英文识别.png';im=Image.new('RGB',(1300,420),'white');d=ImageDraw.Draw(im);font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',60);d.text((70,50),'文件工具 图片处理',font=font,fill='black');d.text((70,200),'Local Image Tools 2026',font=font,fill='black');im.save(ocr)
data=run('image_ocr',[ocr],sparse=True);text=''.join(w['text'] for w in data['words']);check('Local' in text and '2026' in text,'offline OCR reads actual English print');check('文件' in text and '图片' in text,'offline OCR reads actual simplified Chinese print');check(all(0<=w['x']<1300 and 0<=w['y']<420 and w['w']>0 and w['h']>0 and w['x']+w['w']<=1300 and w['y']+w['h']<=420 for w in data['words']),'OCR word boxes match original pixel coordinates')
check(any('Local' in line['text'] and '2026' in line['text'] for line in data['words']),'one English sentence stays in one selectable line')
check(any('文件工具图片处理' in line['text'] for line in data['words']),'one Chinese sentence stays in one selectable line without inserted spaces')
card=root/'有色底纹样本.png';im=Image.new('RGB',(1300,420),(150,200,210));d=ImageDraw.Draw(im);d.text((70,50),'文件工具 图片处理',font=font,fill=(15,25,35));im.save(card)
data=run('image_ocr',[card],sparse=True,enhance='auto');check(any('文件工具图片处理' in line['text'] for line in data['words']),'automatic enhancement handles dark print on a tinted background')
dark=root/'深色背景样本.png';im=Image.new('RGB',(1300,420),(20,25,30));d=ImageDraw.Draw(im);d.text((70,100),'Local Image Tools 2026',font=font,fill='white');im.save(dark)
data=run('image_ocr',[dark],sparse=True,enhance='dark');check(any('Local' in line['text'] and '2026' in line['text'] for line in data['words']),'dark background enhancement retains a full white text line')
check(before==hashlib.sha256(source.read_bytes()).hexdigest(),'all worker operations preserve original bytes')
print('ALL',checks,'IMAGE BACKEND CHECKS PASSED')
