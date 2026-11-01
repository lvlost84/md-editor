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

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--selftest") return SelfTest.Run(args);
        if (args.Length > 1 && args[0] == "--help-shot") { SetBrowserEmulation(); return HelpWindow.ShotAndExit(args[1], args.Length > 2 && args[2] == "dark"); }
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
        return new MainWindow();
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
