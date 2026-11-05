using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;
using VideoDrop;
class Checks {
    static int Main(string[] args) {
        if(args.Length>0 && args[0]=="echo") { File.WriteAllLines(args[1],args.Skip(2).Select(s=>Convert.ToBase64String(Encoding.UTF8.GetBytes(s)))); return 0; }
        string[] samples={"", "plain", "C:\\Папка с пробелами\\", "quote\"inside", "one\\\\\"two", "https://youtu.be/id?x=1&y=2", "$(secret)`test", "line\r\nbreak"};
        string report=Path.GetFullPath("build/argv-test.txt");
        var child=new ProcessStartInfo(System.Reflection.Assembly.GetExecutingAssembly().Location,Tools.Arguments(new[]{"echo",report}.Concat(samples))) {UseShellExecute=false,CreateNoWindow=true};
        using(var p=Process.Start(child)) { p.WaitForExit(); if(p.ExitCode!=0) throw new Exception("child failed"); }
        string[] actual=File.ReadAllLines(report).Select(s=>Encoding.UTF8.GetString(Convert.FromBase64String(s))).ToArray();
        if(!samples.SequenceEqual(actual)) throw new Exception("Argument escaping changed arguments");
        var a=Tools.DownloadArgs("C:\\Video with spaces\\","720",false,false,new[]{"https://youtu.be/id"});
        if(a[a.IndexOf("-f")+1]!="bv*[height<=720]+ba/b[height<=720]" || !a.Contains("--no-playlist")) throw new Exception("720p selector");
        var best=Tools.DownloadArgs("D:\\","best",false,true,new[]{"https://youtu.be/id"});
        if(best[best.IndexOf("-f")+1]!="bv*+ba/b" || !best.Contains("--yes-playlist")) throw new Exception("best/playlist selector");
        var audio=Tools.DownloadArgs("D:\\","720",true,false,new[]{"https://youtu.be/id"});
        if(!audio.Contains("mp3") || audio.Contains("--remux-video")) throw new Exception("MP3 selector");
        Console.WriteLine("PASS: real process argument roundtrip (8 cases), 720p/best/MP3/playlist arguments.");
        return 0;
    }
}
