"""Download pinned upstream executables and verify SHA-256 before extraction."""
from pathlib import Path
import hashlib
import json
import shutil
import urllib.request
import zipfile

root=Path(__file__).resolve().parents[1]
downloads=root/'build'/'downloads';deps=root/'build'/'deps'
downloads.mkdir(parents=True,exist_ok=True);deps.mkdir(parents=True,exist_ok=True)
for item in json.loads((root/'scripts/deps.lock.json').read_text('utf-8')):
    path=downloads/item['filename']
    if not path.exists():
        print('Downloading',item['url'],flush=True)
        temp=path.with_suffix(path.suffix+'.part')
        with urllib.request.urlopen(item['url'],timeout=180) as response,temp.open('wb') as output:
            shutil.copyfileobj(response,output)
        temp.replace(path)
    if hashlib.sha256(path.read_bytes()).hexdigest()!=item['sha256']:
        raise SystemExit('SHA-256 mismatch: '+str(path)+'. Remove the archive and retry.')
    if path.suffix=='.exe':shutil.copy2(path,deps/path.name)
    else:
        with zipfile.ZipFile(path) as archive:
            for name in archive.namelist():
                if name=='deno.exe' or name.endswith(('/ffmpeg.exe','/ffprobe.exe')):
                    (deps/Path(name).name).write_bytes(archive.read(name))
print('All dependencies verified.')
