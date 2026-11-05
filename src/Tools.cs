using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
namespace VideoDrop {
static class Tools {
    public static readonly string Root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VideoDrop-data");
    public static readonly string Bin = Path.Combine(Root, "tools-v1");
    public static string PathFor(string name) { return Path.Combine(Bin, name); }
    public static void Ensure() {
        Directory.CreateDirectory(Bin);
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("tools.zip"))
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Read)) {
            foreach (var entry in zip.Entries) {
                // The embedded archive is built locally and contains flat filenames only.
                if (entry.Name != entry.FullName) throw new IOException("Invalid embedded filename");
                string target = PathFor(entry.Name);
                if (File.Exists(target) && new FileInfo(target).Length > 0) continue;
                string temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try {
                    using(var input = entry.Open()) using(var output = File.Create(temp)) input.CopyTo(output);
                    try { File.Move(temp, target); }
                    catch(IOException) { if(!File.Exists(target)) throw; }
                } finally { if(File.Exists(temp)) File.Delete(temp); }
            }
        }
    }
    // Windows CommandLineToArgvW quoting; no command shell is used.
    public static string Quote(string value) {
        var result = new StringBuilder("\""); int slashes = 0;
        foreach(char c in value) {
            if(c == '\\') { slashes++; continue; }
            if(c == '"') { result.Append('\\', slashes * 2 + 1); result.Append(c); }
            else { result.Append('\\', slashes); result.Append(c); }
            slashes = 0;
        }
        result.Append('\\', slashes * 2); return result.Append('"').ToString();
    }
    public static string Arguments(IEnumerable<string> args) { return string.Join(" ", args.Select(Quote)); }
    public static List<string> DownloadArgs(string folder, string height, bool audio, bool playlist, IEnumerable<string> urls) {
        var a = new List<string> { "--ignore-config", "--no-plugin-dirs", "--no-remote-components", "--no-quiet", "--newline", "--no-colors",
            "--encoding", "utf-8", "--windows-filenames", "--no-overwrites", "--socket-timeout", "30", "--retries", "3",
            "--ffmpeg-location", Bin, "--js-runtimes", "deno:" + PathFor("deno.exe"),
            "--cache-dir", Path.Combine(Root,"cache"), "--progress", "--progress-delta", "0.5",
            "--progress-template", "download:APP_PROGRESS|%(progress._percent_str)s|%(progress._speed_str)s|%(progress._eta_str)s",
            "--print", "after_move:APP_FILE|%(filepath)s", "-P", folder, "-o", "%(title).150B [%(id)s].%(ext)s",
            playlist ? "--yes-playlist" : "--no-playlist" };
        if(audio) a.AddRange(new[]{"-f","bestaudio/best","-x","--audio-format","mp3","--audio-quality","0"});
        else {
            string limit = height == "best" ? "" : "[height<=" + height + "]";
            a.AddRange(new[]{"-f","bv*"+limit+"+ba/b"+limit,"-S","res,vcodec:h264,acodec:aac", "--merge-output-format","mp4","--remux-video","mp4"});
        }
        a.Add("--"); a.AddRange(urls); return a;
    }
}
}
