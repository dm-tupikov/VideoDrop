"""Verify argument roundtrips in a real child process and format selection."""
from pathlib import Path
import os
import subprocess

root=Path(__file__).resolve().parents[1]
build=root/'build';build.mkdir(exist_ok=True)
framework=Path(os.environ.get('WINDIR','C:/Windows'))/'Microsoft.NET/Framework64/v4.0.30319'
out=build/'Checks.exe'
subprocess.run([str(framework/'csc.exe'),'/nologo','/target:exe','/main:Checks',
    '/out:'+str(out),'/reference:System.IO.Compression.dll',str(root/'src/Tools.cs'),str(root/'src/Checks.cs')],check=True)
subprocess.run([str(out)],cwd=root,check=True)
