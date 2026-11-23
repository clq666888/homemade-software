from importlib.metadata import distribution
from pathlib import Path
import shutil, sys

root=Path(sys.argv[1])/'licenses'; root.mkdir(parents=True,exist_ok=True)
for name in ['pypdf','pypdfium2','Pillow','cryptography','olefile','cffi','pycparser','pyinstaller','openpyxl','python-pptx','pdfplumber','pdfminer.six','et-xmlfile','lxml','XlsxWriter','typing-extensions','charset-normalizer']:
    package=distribution(name); dest=root/name;dest.mkdir(exist_ok=True)
    for item in package.files or []:
        if any(k in str(item).lower() for k in ['license','copying','notice']):
            source=Path(package.locate_file(item))
            if source.is_file(): shutil.copyfile(source,dest/(str(item).replace('/','_').replace('\\','_')))
    (dest/'VERSION.txt').write_text(f'{name} {package.version}\n',encoding='utf-8')
raw=Path(distribution('pypdfium2').locate_file('pypdfium2_raw'))
python_license=Path(sys.base_prefix)/'LICENSE_PYTHON.txt'
if python_license.exists():shutil.copyfile(python_license,root/'PYTHON_LICENSE.txt')
for source in raw.rglob('*'):
    if source.is_file() and any(k in source.name.lower() for k in ['license','notice']):
        shutil.copyfile(source,root/'pypdfium2'/source.name)
(root/'README.txt').write_text('Offline PDF engine uses pypdf, PDFium/pypdfium2, Pillow, cryptography, olefile, openpyxl, python-pptx and pdfplumber/pdfminer.six.\nPackage licenses and PDFium third-party notices are retained here and in engine/_internal.\nPyInstaller bootloader uses its distribution exception; no Office or browser binaries are redistributed.\n',encoding='utf-8')
