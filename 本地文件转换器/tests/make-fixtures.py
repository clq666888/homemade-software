from pathlib import Path
import sys
from PIL import Image, ImageDraw
from docx import Document
from docx.shared import Pt

root = Path(sys.argv[1]); root.mkdir(parents=True, exist_ok=True)
im = Image.new('RGB', (900, 1200), '#eaf1fc')
d = ImageDraw.Draw(im)
d.rectangle((60, 60, 840, 1140), outline='#2360d7', width=8)
d.text((100, 100), 'PORTRAIT   PAGE 1', fill='#1c283a', font_size=45)
d.rectangle((100, 250, 800, 380), fill='#2360d7')
im.save(root / '竖版 图片.jpg')
wide = Image.new('RGBA', (1200, 700), (0, 0, 0, 0))
d = ImageDraw.Draw(wide)
d.rectangle((50, 50, 1150, 650), outline='#238660', width=12)
d.text((120, 160), 'TRANSPARENT   PAGE 2', fill='#238660', font_size=48)
wide.save(root / '透明 横图.png')
im.save(root / '多页 扫描.tiff', save_all=True, append_images=[wide.convert('RGB')])
im.save(root / '普通.bmp')
(root / '损坏.png').write_bytes(b'not an image')
(root / '损坏.docx').write_bytes(b'not an office file')
doc = Document()
doc.styles['Normal'].font.name = 'Microsoft YaHei'
doc.styles['Normal'].font.size = Pt(12)
doc.add_paragraph('本地转换验收文档', 'Title')
doc.add_paragraph('用于验证中文、表格、分页与本地 Word 转 PDF。')
doc.add_paragraph('Offline conversion sample with spaces in the path.')
table = doc.add_table(rows=1, cols=2)
table.style = 'Table Grid'
table.rows[0].cells[0].text = '项目'; table.rows[0].cells[1].text = '结果'
row = table.add_row(); row.cells[0].text = '中文内容'; row.cells[1].text = '应完整保留'
doc.add_page_break()
doc.add_paragraph('第二页', 'Heading 1')
doc.add_paragraph('分页标记应保留。Second page verification.')
doc.save(root / '中文 测试.docx')
print(root)
