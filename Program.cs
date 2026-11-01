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
        var sw = Stopwatch.StartNew();
        Log.Init();
        string file = FileArg(args);
        bool created;
        mutex = new Mutex(true, "MdEditor_SingleInstance", out created);
        if (!created)
        {
            SendToFirst(file ?? "");
            return 0;
        }
        SetBrowserEmulation();
        var app = EnsureApp();
        app.ShutdownMode = ShutdownMode.OnMainWindowClose;
        var w = new MainWindow();
        if (file != null) w.OpenFileStartup(file);
        StartPipeServer(w);
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
            if (Regex.IsMatch(a, @"\.(md|markdown|txt)$", RegexOptions.IgnoreCase))
            {
                try { return Path.GetFullPath(a); } catch { return a; }
            }
            break;
        }
        return null;
    }

    static void SendToFirst(string path)
    {
        try
        {
            using (var c = new NamedPipeClientStream(".", PipeName, PipeDirection.Out))
            {
                c.Connect(3000);
                var b = Encoding.UTF8.GetBytes(path + "\n");
                c.Write(b, 0, b.Length);
                c.Flush();
            }
        }
        catch (Exception ex) { Log.Error("send to first", ex); }
    }

    static void StartPipeServer(MainWindow w)
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
                            w.Dispatcher.BeginInvoke(new Action(() => w.ReceiveExternal(line)));
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
