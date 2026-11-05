"""Build the portable Windows x64 executable with the built-in .NET compiler."""
from pathlib import Path
import argparse
import hashlib
import os
import subprocess
import zipfile

root=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--deps-dir',type=Path,default=root/'build'/'deps')
parser.add_argument('--output-dir',type=Path,default=root/'dist')
parser.add_argument('--tools-archive',type=Path)
args=parser.parse_args()
args.output_dir.mkdir(parents=True,exist_ok=True)
build=root/'build';build.mkdir(exist_ok=True)
deps=args.deps_dir.resolve()
for name in ['yt-dlp.exe','deno.exe','ffmpeg.exe','ffprobe.exe']:
    if not (deps/name).is_file(): raise SystemExit('Missing '+name+'. Run python scripts/fetch_deps.py first.')
archive=args.tools_archive
if archive is None:
    archive=build/'tools.zip'
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
        for p in sorted(deps.glob('*.exe')):z.write(p,p.name)
        for p in sorted((root/'licenses').iterdir()):
            if p.is_file():z.write(p,p.name)
framework=Path(os.environ.get('WINDIR','C:/Windows'))/'Microsoft.NET'/'Framework64'/'v4.0.30319'
refs=['System.dll','System.Core.dll','System.Xaml.dll','System.Windows.Forms.dll',
      'System.IO.Compression.dll','WPF/WindowsBase.dll','WPF/PresentationCore.dll','WPF/PresentationFramework.dll']
out=args.output_dir/'VideoDrop-Portable.exe'
command=[str(framework/'csc.exe'),'/nologo','/target:winexe','/platform:x64','/optimize+',
    '/out:'+str(out),'/resource:'+str(archive.resolve())+',tools.zip',
    '/resource:'+str(root/'src/MainWindow.xaml')+',MainWindow.xaml',
    '/resource:'+str(root/'src/app.ico')+',app.ico',
    '/win32manifest:'+str(root/'src/app.manifest'),'/win32icon:'+str(root/'src/app.ico')]
command+=['/reference:'+str(framework/ref) for ref in refs]
command+=[str(root/'src/Tools.cs'),str(root/'src/App.cs')]
subprocess.run(command,check=True)
(args.output_dir/'SHA256SUMS.txt').write_text(hashlib.sha256(out.read_bytes()).hexdigest()+'  '+out.name+'\n','utf-8')
print('Built',out,'('+str(out.stat().st_size)+' bytes)')
