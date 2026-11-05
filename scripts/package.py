"""Create user-facing release assets from a built executable and clean source tree."""
from pathlib import Path
import argparse
import hashlib
import zipfile

root=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--dist',type=Path,default=root/'dist')
parser.add_argument('--version',default='1.1.0')
args=parser.parse_args();dist=args.dist.resolve()
exe=dist/'VideoDrop-Portable.exe'
if not exe.is_file():raise SystemExit('Build the executable first.')
version=args.version
portable=dist/('VideoDrop-'+version+'-Windows-x64.zip')
with zipfile.ZipFile(portable,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
    z.write(exe,exe.name)
    for name in ['README.md','LICENSE','THIRD_PARTY.md','CHANGELOG.md']:z.write(root/name,name)
    for folder in ['licenses','docs']:
        for path in sorted((root/folder).rglob('*')):
            if path.is_file():z.write(path,path.relative_to(root))
source=dist/('VideoDrop-Source-'+version+'.zip')
with zipfile.ZipFile(source,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
    for path in sorted(root.rglob('*')):
        rel=path.relative_to(root)
        if path.is_file() and not any(part in {'.git','build','dist','VideoDrop-data','Downloads','__pycache__'} for part in rel.parts):
            z.write(path,Path('VideoDrop-'+version)/rel)
assets=[exe,portable,source]
(dist/'SHA256SUMS.txt').write_text(''.join(hashlib.sha256(path.read_bytes()).hexdigest()+'  '+path.name+'\n' for path in assets),'utf-8')
for path in assets:print(path.name,path.stat().st_size)
