using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using Forms = System.Windows.Forms;

[assembly: AssemblyTitle("VideoDrop Portable")]
[assembly: AssemblyDescription("Portable YouTube downloader for Windows")]
[assembly: AssemblyVersion("1.1.0.0")]
namespace VideoDrop {
static class Program {
    [STAThread] static int Main(string[] args) {
        if(args.Length==2 && args[0]=="--self-test") {
            try { Tools.Ensure(); File.WriteAllText(args[1],"OK\r\n"+Tools.PathFor("yt-dlp.exe")); return 0; }
            catch(Exception ex) { File.WriteAllText(args[1],ex.ToString()); return 1; }
        }
        var app=new Application();
        try {
            var main=new MainWindow(args.Length==2&&args[0]=="--screenshot");
            if(args.Length==2&&args[0]=="--screenshot") {
                main.Window.ContentRendered+=async delegate {
                    await main.Ready.Task;
                    main.Get<TextBox>("Folder").Text="D:\\Videos";
                    main.Window.UpdateLayout();
                    var visual=(FrameworkElement)main.Window.Content;
                    var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(visual.ActualWidth),(int)Math.Ceiling(visual.ActualHeight),96,96,PixelFormats.Pbgra32);
                    bitmap.Render(visual);
                    var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                    using(var output=File.Create(args[1]))encoder.Save(output);
                    main.Window.Close();
                };
            }
            return app.Run(main.Window);
        }
        catch(Exception ex) { MessageBox.Show("Не удалось запустить VideoDrop.\n"+ex.Message,"VideoDrop",MessageBoxButton.OK,MessageBoxImage.Error); return 1; }
    }
}
class MainWindow {
    public readonly Window Window;
    public readonly TaskCompletionSource<bool> Ready=new TaskCompletionSource<bool>();
    readonly TextBox urls,folder,log;
    readonly TextBlock placeholder,status,detail,hint;
    readonly RadioButton video,audio;
    readonly ComboBox quality;
    readonly CheckBox playlist;
    readonly Button start,cancel,paste,browse,update;
    readonly ProgressBar progress;
    readonly Expander logPanel;
    readonly bool preview;
    Process active;
    readonly object processLock=new object();
    volatile bool cancelled;
    bool busy,ready;
    int completed;
    string lastFile="",lastError="";
    readonly string[] heights={"480","720","1080","1440","2160","best"};
    public MainWindow(bool preview=false) {
        this.preview=preview;
        using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("MainWindow.xaml")) Window=(Window)XamlReader.Load(stream);
        double availableHeight=Math.Max(520,SystemParameters.WorkArea.Height-24);
        double availableWidth=Math.Max(760,SystemParameters.WorkArea.Width-24);
        Window.MinHeight=Math.Min(Window.MinHeight,availableHeight);Window.Height=Math.Min(Window.Height,availableHeight);
        Window.MinWidth=Math.Min(Window.MinWidth,availableWidth);Window.Width=Math.Min(Window.Width,availableWidth);
        using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("app.ico")) Window.Icon=System.Windows.Media.Imaging.BitmapFrame.Create(stream);
        urls=Get<TextBox>("Urls");folder=Get<TextBox>("Folder");log=Get<TextBox>("Log");
        placeholder=Get<TextBlock>("Placeholder");status=Get<TextBlock>("Status");detail=Get<TextBlock>("Detail");hint=Get<TextBlock>("QualityHint");
        video=Get<RadioButton>("Video");audio=Get<RadioButton>("Audio");quality=Get<ComboBox>("Quality");playlist=Get<CheckBox>("Playlist");
        start=Get<Button>("Start");cancel=Get<Button>("Cancel");paste=Get<Button>("Paste");browse=Get<Button>("Browse");update=Get<Button>("UpdateEngine");
        progress=Get<ProgressBar>("Progress");logPanel=Get<Expander>("LogPanel");
        folder.Text=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Downloads");
        if(!preview)LoadSettings(); RefreshHint();
        urls.TextChanged+=delegate { placeholder.Visibility=urls.Text.Length==0?Visibility.Visible:Visibility.Collapsed; };
        video.Checked+=delegate { RefreshHint(); }; audio.Checked+=delegate { RefreshHint(); }; quality.SelectionChanged+=delegate { RefreshHint(); };
        paste.Click+=delegate { try { if(Clipboard.ContainsText()) {urls.Text=Clipboard.GetText().Trim();urls.CaretIndex=urls.Text.Length;urls.Focus();} else Status("В буфере нет ссылки","Скопируй адрес видео и попробуй ещё раз.",false); } catch(Exception ex){Status("Не удалось прочитать буфер",ex.Message,true);} };
        browse.Click+=delegate { using(var picker=new Forms.FolderBrowserDialog()) {picker.Description="Куда сохранять видео и музыку";if(Directory.Exists(folder.Text))picker.SelectedPath=folder.Text;if(picker.ShowDialog()==Forms.DialogResult.OK)folder.Text=picker.SelectedPath;} };
        Get<Button>("OpenFolder").Click+=delegate { try { Directory.CreateDirectory(folder.Text);Process.Start(new ProcessStartInfo(folder.Text){UseShellExecute=true}); } catch(Exception ex) {Status("Не удалось открыть папку",ex.Message,true);} };
        Get<Button>("Help").Click+=delegate { MessageBox.Show(Window,"VideoDrop 1.1.0 · Portable\n\nВидео и музыка с YouTube без установки.\nВ комплекте: yt-dlp, FFmpeg, ffprobe и Deno.\n\nКачество — верхний предел: если выбранное разрешение недоступно, будет использовано меньшее.\n\nИсходный код и обновления:\ngithub.com/dm-tupikov/VideoDrop\n\nСохраняйте материалы, на скачивание которых у вас есть разрешение.","О программе",MessageBoxButton.OK,MessageBoxImage.Information); };
        start.Click+=async delegate {await Download();};cancel.Click+=async delegate {await Cancel();};update.Click+=async delegate {await Update();};
        Window.PreviewKeyDown+=async delegate(object sender,KeyEventArgs e) {if(e.Key==Key.Escape&&busy){e.Handled=true;await Cancel();}};
        Window.Closing+=delegate(object sender,System.ComponentModel.CancelEventArgs e) {if(busy){e.Cancel=true;Status("Загрузка ещё выполняется","Нажми «Отменить», затем закрой программу.",false);}else SaveSettings();};
        Window.Loaded+=async delegate {
            try { await Task.Run((Action)Tools.Ensure);ready=true;SetBusy(false);progress.IsIndeterminate=false;Status("Готово к загрузке","Вставь ссылку — всё остальное уже настроено.",false);urls.Focus();Ready.TrySetResult(true); }
            catch(Exception ex){progress.IsIndeterminate=false;Status("Не удалось подготовить программу","Перемести EXE в папку с правом записи.",true);AddLog(ex.ToString());logPanel.IsExpanded=true;Ready.TrySetResult(false);}
        };
    }
    public T Get<T>(string name) where T:class {return Window.FindName(name) as T;}
    void RefreshHint() {
        bool mp3=audio.IsChecked==true;quality.IsEnabled=!mp3&&!busy;
        string h=quality.SelectedIndex>=0?heights[quality.SelectedIndex]:"720";
        hint.Text=mp3?"MP3 · наилучшее качество звука":h=="best"?"Лучшее качество · MP4 со звуком":"До "+h+"p · MP4 со звуком";
    }
    void SetBusy(bool value) {
        busy=value;start.IsEnabled=update.IsEnabled=ready&&!value;
        urls.IsEnabled=folder.IsEnabled=browse.IsEnabled=paste.IsEnabled=video.IsEnabled=audio.IsEnabled=playlist.IsEnabled=!value;
        cancel.Visibility=value?Visibility.Visible:Visibility.Collapsed;cancel.IsEnabled=value;
        start.Content=value?"Выполняется…":"↓   Скачать";RefreshHint();
    }
    void Status(string title,string subtitle,bool error) {
        status.Text=title;detail.Text=subtitle;
        status.Foreground=new SolidColorBrush((Color)ColorConverter.ConvertFromString(error?"#C14954":"#182338"));
        detail.ToolTip=subtitle;
    }
    void AddLog(string s) {
        if(log.Text.Length>160000)log.Text=log.Text.Substring(log.Text.Length-100000);
        log.AppendText(s+Environment.NewLine);log.ScrollToEnd();
    }
    void Receive(string line) {
        if(line==null)return;
        Window.Dispatcher.Invoke(new Action(delegate {
            if(line.StartsWith("APP_PROGRESS|")) {
                var p=line.Split('|');double n;
                if(p.Length>=4&&double.TryParse(p[1].Trim().TrimEnd('%'),NumberStyles.Any,CultureInfo.InvariantCulture,out n)) {
                    progress.IsIndeterminate=false;progress.Value=Math.Max(0,Math.Min(100,n));
                    Status("Скачиваем · "+p[1].Trim(),p[2].Trim()+" · осталось "+p[3].Trim(),false);
                }
            }else if(line.StartsWith("APP_FILE|")){
                completed++;lastFile=line.Substring(9);AddLog("Сохранено: "+lastFile);
            }else {
                AddLog(line);if(line.Contains("ERROR:"))lastError=line;
                if(line.StartsWith("[Merger]")||line.StartsWith("[ExtractAudio]")||line.StartsWith("[VideoRemuxer]")){
                    Status("Готовим файл","Объединяем видео и звук. Ещё немного…",false);progress.IsIndeterminate=true;
                }
            }
        }));
    }
    async Task<int> Run(List<string> args) {
        return await Task.Run(delegate {
            var info=new ProcessStartInfo(Tools.PathFor("yt-dlp.exe"),Tools.Arguments(args)) {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8,WorkingDirectory=Tools.Root};
            info.EnvironmentVariables["DENO_DIR"]=Path.Combine(Tools.Root,"deno-cache");
            using(var p=new Process {StartInfo=info}) {
                p.OutputDataReceived+=delegate(object s,DataReceivedEventArgs e){Receive(e.Data);};p.ErrorDataReceived+=delegate(object s,DataReceivedEventArgs e){Receive(e.Data);};
                lock(processLock){p.Start();active=p;}
                p.BeginOutputReadLine();p.BeginErrorReadLine();if(cancelled)StopTree(p);
                p.WaitForExit();int code=p.ExitCode;lock(processLock){active=null;}return code;
            }
        });
    }
    static void StopTree(Process p) {
        try {if(p.HasExited)return;using(var killer=Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"taskkill.exe"),"/PID "+p.Id+" /T /F"){UseShellExecute=false,CreateNoWindow=true}))killer.WaitForExit();}
        catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){try{p.Kill();}catch{}}
    }
    async Task Cancel(){cancelled=true;cancel.IsEnabled=false;Status("Останавливаем загрузку","Частичные файлы останутся для продолжения.",false);await Task.Run(delegate {lock(processLock){if(active!=null)StopTree(active);}});}
    async Task Download() {
        string[] links=urls.Text.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).Select(s=>s.Trim()).Where(s=>s.Length>0).ToArray();
        if(links.Length==0){Status("Сначала добавь ссылку","Подойдёт адрес видео, Shorts или плейлиста YouTube.",false);urls.Focus();return;}
        foreach(string link in links){Uri uri;if(!Uri.TryCreate(link,UriKind.Absolute,out uri)||(uri.Scheme!="https"&&uri.Scheme!="http")||!(uri.Host=="youtu.be"||uri.Host=="youtube.com"||uri.Host.EndsWith(".youtube.com",StringComparison.OrdinalIgnoreCase))){Status("Проверь ссылку","Нужен полный адрес youtube.com или youtu.be.",true);urls.Focus();return;}}
        string target;
        try {if(string.IsNullOrWhiteSpace(folder.Text))throw new IOException("Выбери папку сохранения.");target=Path.GetFullPath(folder.Text.Trim());Directory.CreateDirectory(target);using(var test=File.Create(Path.Combine(target,".videodrop-"+Guid.NewGuid().ToString("N")),1,FileOptions.DeleteOnClose)){}folder.Text=target;}
        catch(Exception ex){Status("В эту папку нельзя сохранить файл",ex.Message,true);return;}
        var args=Tools.DownloadArgs(target,heights[quality.SelectedIndex],audio.IsChecked==true,playlist.IsChecked==true,links);
        SaveSettings();SetBusy(true);cancelled=false;completed=0;lastFile="";lastError="";log.Clear();progress.Value=0;progress.IsIndeterminate=true;
        Status("Получаем информацию о видео","Это может занять несколько секунд.",false);
        try {
            int code=await Run(args);
            if(cancelled)Status("Загрузка отменена","Повтори загрузку, чтобы продолжить с того же места.",false);
            else if(code==0){progress.Value=100;Status(completed==1?"Готово! Файл сохранён":"Готово! Файлов: "+completed,completed==1?Path.GetFileName(lastFile):"Открой «Мои файлы», чтобы посмотреть результат.",false);}
            else {Status(completed>0?"Часть файлов сохранена":"Не удалось скачать видео","Проверь сеть или обнови yt-dlp. Подробности — ниже.",true);AddLog(lastError.Length>0?lastError:"Код завершения: "+code);logPanel.IsExpanded=true;}
        }catch(Exception ex){Status("Не удалось запустить загрузку",ex.Message,true);AddLog(ex.ToString());logPanel.IsExpanded=true;}
        finally{progress.IsIndeterminate=false;SetBusy(false);}
    }
    async Task Update(){SetBusy(true);cancelled=false;log.Clear();progress.Value=0;progress.IsIndeterminate=true;Status("Проверяем обновление","Официальный выпуск yt-dlp на GitHub.",false);
        try{int code=await Run(new List<string>{"--ignore-config","--no-plugin-dirs","--update"});Status(cancelled?"Обновление отменено":code==0?"yt-dlp актуален":"Не удалось обновить yt-dlp",code==0?"Можно скачивать видео.":"Подробности — в журнале загрузки.",code!=0&&!cancelled);if(code!=0&&!cancelled)logPanel.IsExpanded=true;}
        catch(Exception ex){Status("Не удалось обновить yt-dlp",ex.Message,true);AddLog(ex.ToString());logPanel.IsExpanded=true;}
        finally{progress.IsIndeterminate=false;SetBusy(false);}
    }
    void SaveSettings(){if(preview)return;try{Directory.CreateDirectory(Tools.Root);File.WriteAllLines(Path.Combine(Tools.Root,"settings.txt"),new[]{folder.Text,quality.SelectedIndex.ToString(),audio.IsChecked==true?"1":"0"},Encoding.UTF8);}catch{}}
    void LoadSettings(){try{var s=File.ReadAllLines(Path.Combine(Tools.Root,"settings.txt"),Encoding.UTF8);int n;if(s.Length>0&&!string.IsNullOrWhiteSpace(s[0]))folder.Text=s[0];if(s.Length>1&&int.TryParse(s[1],out n)&&n>=0&&n<quality.Items.Count)quality.SelectedIndex=n;if(s.Length>2&&s[2]=="1")audio.IsChecked=true;}catch{}}
}
}
