"""Small, optional OCR-only contrast preparation; never changes the displayed original."""
from PIL import Image, ImageOps, ImageFilter, ImageStat

def prepare(im,mode):
    if mode not in ('auto','original','paper','dark','card'):raise ValueError('不支持的识别增强方式。')
    original=im
    if im.mode=='RGBA':
        background=Image.new('RGB',im.size,'white');background.paste(im,mask=im.getchannel('A'));im=background
    else:im=im.convert('RGB')
    original.close()
    if mode!='original':
        sample=im.copy();sample.thumbnail((128,128));stats=ImageStat.Stat(sample);mean=sum(stats.mean)/3
        pixels=list(sample.getdata());colored=sum(max(p)-min(p)>35 for p in pixels)/max(1,len(pixels));sample.close()
        gray=ImageOps.grayscale(im)
        selected=mode
        if selected=='auto':selected='dark' if mean<105 else 'card' if mean>125 and colored>.35 else 'paper'
        if selected=='dark':gray=ImageOps.invert(gray)
        if selected=='card':gray=gray.filter(ImageFilter.MedianFilter(3)).point(lambda p:255 if p>110 else 0)
        else:gray=ImageOps.autocontrast(gray,cutoff=.5)
        im.close();im=gray.convert('RGB');gray.close()
    factor=min(2,1800/max(im.size))
    if factor>1:
        bigger=im.resize((round(im.width*factor),round(im.height*factor)),Image.Resampling.LANCZOS);im.close();im=bigger
    im.thumbnail((4800,4800));return im
