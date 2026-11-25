"""Normalize OCR into visual text lines without joining separate columns."""
import re

def clean_text(text):
    text=' '.join(text.split())
    return re.sub(r'(?<=[\u3400-\u9fff])\s+(?=[\u3400-\u9fff])','',text)

def merge_lines(items):
    # Sparse-text detection can split one visual line into several blocks. Join only
    # near-baseline pieces with comparable glyph height and a small horizontal gap.
    groups=[]
    for item in sorted(items,key=lambda p:(p['y']+p['h']/2,p['x'])):
        item=dict(item);item['text']=clean_text(item['text'])
        if not item['text'] or item['h']<4 or item['w']<2 or not any(c.isalnum() for c in item['text']):continue
        matches=[]
        for group in groups:
            ratio=max(group['h'],item['h'])/min(group['h'],item['h'])
            overlap=min(group['y']+group['h'],item['y']+item['h'])-max(group['y'],item['y'])
            # Center-height sorting may visit the right fragment first. Measure
            # the gap in either direction, rather than requiring left-to-right input.
            gap=max(0,max(item['x'],group['x'])-min(item['x']+item['w'],group['x']+group['w']))
            baseline=abs(group['y']+group['h']/2-item['y']-item['h']/2)
            if ratio<=1.6 and overlap>=.65*min(group['h'],item['h']) and baseline<=.24*max(group['h'],item['h']) and gap<=1.5*max(group['h'],item['h']):
                matches.append(group)
        if not matches:
            item['pieces']=[dict(item)];groups.append(item)
        else:
            match=matches[0];pieces=[item]
            for group in matches:
                pieces.extend(group['pieces'])
                if group is not match:groups.remove(group)
            left=min(p['x'] for p in pieces);right=max(p['x']+p['w'] for p in pieces);top=min(p['y'] for p in pieces);bottom=max(p['y']+p['h'] for p in pieces)
            match.update(pieces=pieces,x=left,y=top,w=right-left,h=bottom-top)
    result=[]
    for group in sorted(groups,key=lambda p:(p['y'],p['x'])):
        pieces=sorted(group.pop('pieces'),key=lambda p:p['x']);text=''
        for piece in pieces:
            value=piece['text']
            join='' if not text or '\u3400'<=text[-1]<='\u9fff' and '\u3400'<=value[0]<='\u9fff' else ' '
            text+=join+value
        group.update(text=clean_text(text),newline=True);result.append(group)
    return result
