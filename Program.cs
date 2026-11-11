using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using Microsoft.Win32;

class App : Application { }

static class Program
{
    const string PipeName = "MdEditor_Pipe";
    static Mutex mutex;

    // First thing Main does. Must stay in a method/class that references no AvalonEdit/Markdig type (JIT order).
    static class Embedded
    {
        static readonly System.Collections.Generic.Dictionary<string, System.Reflection.Assembly> cache = new System.Collections.Generic.Dictionary<string, System.Reflection.Assembly>();
        public static void Install()
        {
            AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
            {
                try
                {
                    string n = new System.Reflection.AssemblyName(e.Name).Name + ".dll";
                    lock (cache)
                    {
                        System.Reflection.Assembly a;
                        if (cache.TryGetValue(n, out a)) return a;
                        var me = System.Reflection.Assembly.GetExecutingAssembly();
                        using (var st = me.GetManifestResourceStream(n))
                        {
                            if (st == null) return null;
                            var buf = new byte[st.Length];
                            int off = 0;
                            while (off < buf.Length) { int r = st.Read(buf, off, buf.Length - off); if (r <= 0) break; off += r; }
                            a = System.Reflection.Assembly.Load(buf);
                            cache[n] = a;
                            return a;
                        }
                    }
                }
                catch { return null; }
            };
        }
    }

    [STAThread]
    static int Main(string[] args)
    {
        Embedded.Install();
        return RealMain(args);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static int RealMain(string[] args)
    {
        if (args.Length > 0 && args[0] == "--selftest") return SelfTest.Run(args);
        if (args.Length > 1 && (args[0] == "--help-shot" || args[0] == "--shot" || args[0] == "--opt-shot")) SandboxProfile();
        if (args.Length > 1 && args[0] == "--opt-shot") { SetBrowserEmulation(); return OptShot(args); }   // 개발용 캡처는 실제 %AppData%\MdEditor(설정/복구 파일)를 건드리지 않는다
        if (args.Length > 1 && args[0] == "--help-shot") { SetBrowserEmulation(); return HelpWindow.ShotAndExit(args[1], args.Length > 2 && args[2] == "dark"); }
        if (args.Length > 1 && args[0] == "--shot") { SetBrowserEmulation(); return MainShot(args); }
        var sw = Stopwatch.StartNew();
        Log.Init();
        string file = FileArg(args);
        bool created;
        mutex = new Mutex(true, "MdEditor_SingleInstance", out created);
        if (!created)
        {
            if (!SendToFirst(file ?? ""))
                MessageBox.Show("이미 실행 중인 창에 연결하지 못했습니다.", AppInfo.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return 0;
        }
        StartPipeServer();
        SetBrowserEmulation();
        var app = EnsureApp();
        app.ShutdownMode = ShutdownMode.OnMainWindowClose;
        var w = new MainWindow();
        if (file != null) w.OpenFileStartup(file);
        w.Loaded += (s, e) => w.Dispatcher.BeginInvoke(new Action(w.OfferRecovery));
        lock (pipeLock)
        {
            window = w;
            foreach (var p in pending) { var pp = p; w.Dispatcher.BeginInvoke(new Action(() => w.ReceiveExternal(pp))); }
            pending.Clear();
        }
        Log.Write("load ms: " + sw.ElapsedMilliseconds);
        return app.Run(w);
    }

    // Used by SelfTest: builds a window without showing it.
    public static MainWindow CreateForTest()
    {
        EnsureApp();
        var w = new MainWindow();
        w.recoveryHandled = true;   // 테스트 창은 시작 시 복구 질문 단계를 이미 지난 것으로 본다
        return w;
    }

    static void SandboxProfile()
    {
        string prof = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mdeditor_shot_" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("USERPROFILE", prof);
        Environment.SetEnvironmentVariable("APPDATA", System.IO.Path.Combine(prof, "AppData", "Roaming"));
        Environment.SetEnvironmentVariable("LOCALAPPDATA", System.IO.Path.Combine(prof, "AppData", "Local"));
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);

    // 개발용 화면 캡처: `MdEditor.exe --shot <png> [dark] [file.md]` — 단일 인스턴스 잠금과 무관, 자기 창만 PrintWindow(화면 캡처 아님)
    static int MainShot(string[] args)
    {
        string png = args[1];
        bool dark = args.Length > 2 && args[2] == "dark";
        string md = args.Length > 3 ? args[3] : null;
        var app = EnsureApp();
        var w = new MainWindow();
        w.recoveryHandled = true;
        w.Left = 20; w.Top = 20; w.Width = 1280; w.Height = 820;
        if (md != null) w.OpenFileForTest(md);
        w.Show();
        if (dark) { var t0 = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.2) }; t0.Tick += (s, e) => { t0.Stop(); w.SetDark(true); }; t0.Start(); }
        var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        t.Tick += (s, e) =>
        {
            t.Stop();
            try
            {
                var h = new System.Windows.Interop.WindowInteropHelper(w).Handle;
                using (var bmp = new System.Drawing.Bitmap((int)w.ActualWidth, (int)w.ActualHeight))
                using (var g = System.Drawing.Graphics.FromImage(bmp))
                {
                    var dc = g.GetHdc(); PrintWindow(h, dc, 2); g.ReleaseHdc(dc);
                    bmp.Save(png, System.Drawing.Imaging.ImageFormat.Png);
                }
            }
            catch (Exception ex) { Log.Error("shot", ex); }
            app.Shutdown();
        };
        t.Start();
        return app.Run();
    }

    // 개발용: `MdEditor.exe --opt-shot <png> [dark]` — 옵션 창만 캡처 (임시 프로필)
    static int OptShot(string[] args)
    {
        string png = args[1];
        bool dark = args.Length > 2 && args[2] == "dark";
        var app = EnsureApp();
        var w = new MainWindow();
        w.recoveryHandled = true;
        w.Left = 20; w.Top = 20; w.Width = 1280; w.Height = 820;
        w.Show();
        if (dark) w.SetDark(true);
        w.ShowOptionsForTest();
        var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        t.Tick += (s, e) =>
        {
            t.Stop();
            try
            {
                var ow = w.OptWinForTest;
                var h = new System.Windows.Interop.WindowInteropHelper(ow).Handle;
                using (var bmp = new System.Drawing.Bitmap((int)ow.ActualWidth, (int)ow.ActualHeight))
                using (var g = System.Drawing.Graphics.FromImage(bmp))
                {
                    var dc = g.GetHdc(); PrintWindow(h, dc, 2); g.ReleaseHdc(dc);
                    bmp.Save(png, System.Drawing.Imaging.ImageFormat.Png);
                }
            }
            catch (Exception ex) { Log.Error("optshot", ex); }
            app.Shutdown();
        };
        t.Start();
        return app.Run();
    }

    static Application EnsureApp()
    {
        if (Application.Current != null) return Application.Current;
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.DispatcherUnhandledException += (s, e) =>
        {
            Log.Error("dispatcher", e.Exception);
            MessageBox.Show("예기치 않은 오류: " + e.Exception.Message + "\n\n로그: " + Log.FilePath, AppInfo.Title, MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (s, e) => Log.Write("UNHANDLED " + e.ExceptionObject);
        return app;
    }

    static string FileArg(string[] args)
    {
        foreach (var a in args)
        {
            if (a.StartsWith("--")) continue;
            if (Regex.IsMatch(a, @"^[^-].*\.(md|markdown|txt)$", RegexOptions.IgnoreCase))
            {
                try { return Path.GetFullPath(a); } catch { return a; }
            }
        }
        return null;
    }

    static bool SendToFirst(string path)
    {
        var until = DateTime.UtcNow.AddSeconds(3);
        while (true)
        {
            try
            {
                using (var c = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
                {
                    c.Connect(500);
                    var b = Encoding.UTF8.GetBytes(path + "\n");
                    c.Write(b, 0, b.Length);
                    c.Flush();
                }
                return true;
            }
            catch (Exception ex)
            {
                if (DateTime.UtcNow >= until) { Log.Error("send to first", ex); return false; }
                Thread.Sleep(150);
            }
        }
    }

    static readonly object pipeLock = new object();
    static readonly System.Collections.Generic.List<string> pending = new System.Collections.Generic.List<string>();
    static MainWindow window;

    static void Deliver(string line)
    {
        lock (pipeLock)
        {
            if (window == null) { pending.Add(line); return; }
            var w = window;
            w.Dispatcher.BeginInvoke(new Action(() => w.ReceiveExternal(line)));
        }
    }

    static void StartPipeServer()
    {
        var t = new Thread(() =>
        {
            while (true)
            {
                try
                {
                    using (var s = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte))
                    {
                        s.WaitForConnection();
                        using (var r = new StreamReader(s, Encoding.UTF8))
                        {
                            string line = r.ReadLine() ?? "";
                            Deliver(line);
                        }
                    }
                }
                catch (Exception ex) { Log.Error("pipe", ex); Thread.Sleep(500); }
            }
        });
        t.IsBackground = true;
        t.Start();
    }

    static void SetBrowserEmulation()
    {
        try
        {
            using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION"))
                k.SetValue(Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName), 11001, RegistryValueKind.DWord);
        }
        catch { }
    }
}
