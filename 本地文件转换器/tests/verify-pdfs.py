from pathlib import Path
import sys
from pypdf import PdfReader

root = Path(sys.argv[1]) / 'outputs'
expected = {'合并验收.pdf': 2, '合并验收 (1).pdf': 2, '多页 扫描.pdf': 2,
            '旋转验收.pdf': 1, '强制纵向.pdf': 1, '中文 测试.pdf': 2}
for name, count in expected.items():
    reader = PdfReader(root / name, strict=True)
    assert len(reader.pages) == count, name
    if name == '中文 测试.pdf':
        assert '本地转换验收文档' in reader.pages[0].extract_text()
        assert '第二页' in reader.pages[1].extract_text()
    print(f'PASS pages and structure: {name}')
rotated = PdfReader(root / '旋转验收.pdf').pages[0].mediabox
assert rotated.width > rotated.height
portrait = PdfReader(root / '强制纵向.pdf').pages[0].mediabox
assert portrait.width < portrait.height
assert not list(root.glob('*.tmp'))
print('ALL PDF CHECKS PASSED')
