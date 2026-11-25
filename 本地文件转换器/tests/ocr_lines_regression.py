"""Geometry checks for visual lines, independent of recognition accuracy."""
import sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'engine'))
from ocr_lines import merge_lines,clean_text

checks=0
def check(value,name):
    global checks
    assert value,name
    checks+=1;print('PASS',name)
def piece(text,x,y,w,h):return dict(text=text,x=x,y=y,w=w,h=h)

joined=merge_lines([piece('中华',10,10,50,30),piece('人民共和国',70,11,150,29)])
check(len(joined)==1 and joined[0]['text']=='中华人民共和国','Chinese fragments on the same baseline become one selectable line')
check(joined[0]['x']==10 and joined[0]['w']==210,'merged line bounds cover all original fragments')
check(len(merge_lines([piece('left column',0,10,80,20),piece('right column',400,10,80,20)]))==2,'large inter-column gap stays separate')
check(len(merge_lines([piece('top',0,10,60,20),piece('bottom',0,45,80,20)]))==2,'separate baselines remain separate rows')
check(clean_text('中 华 人 民 Local Image 2026')=='中华人民 Local Image 2026','Chinese spacing is normalized without removing English word spaces')
check(len(merge_lines([piece('noise',0,0,1,2),piece('真实文字',10,20,100,25)]))==1,'tiny background artifacts do not create selectable boxes')
check(len(merge_lines([piece('large',0,10,100,40),piece('small',110,20,40,10)]))==2,'different font-height fragments are not blindly joined')
reverse=merge_lines([piece('power',10,12,55,20),piece('generation',74,10,100,20),piece('exceeded',184,11,88,20)])
check(len(reverse)==1 and reverse[0]['text']=='power generation exceeded','right fragment visited first still merges left and right pieces')
bridge=merge_lines([piece('left',0,10,40,20),piece('right',95,10,45,20),piece('middle',48,12,40,20)])
check(len(bridge)==1 and bridge[0]['text']=='left middle right','a later middle fragment bridges two existing partial groups')
check(len(merge_lines([piece('left',0,12,80,20),piece('column',400,10,80,20)]))==2,'reverse visitation still preserves widely separated columns')
print('ALL',checks,'OCR LINE CHECKS PASSED')
