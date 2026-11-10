using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Threading;

// Autosave/recovery: while dirty, %AppData%\MdEditor\recovery.md (+ recovery.json with the original path) is written every 20 s
// on a background thread (tmp + replace). Deleted on a clean save / close; offered for restore at the next start.
partial class MainWindow
{
    DispatcherTimer recoveryTimer;
    int recWrittenVer = -1;
    static readonly object recLock = new object();
    internal static bool? TestRecoveryAnswer;   // test hook: skips the question

    internal static string RecoveryMd { get { return Path.Combine(Log.Dir, "recovery.md"); } }
    internal static string RecoveryJson { get { return Path.Combine(Log.Dir, "recovery.json"); } }

    void SetupRecovery()
    {
        recoveryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        recoveryTimer.Tick += (s, e) => { try { RecoveryTick(false); } catch (Exception ex) { Log.Error("recovery tick", ex); } };
        recoveryTimer.Start();
    }

    void StopBackground()
    {
        try { if (recoveryTimer != null) recoveryTimer.Stop(); if (previewTimer != null) previewTimer.Stop(); } catch { }
    }

    // dirty -> write (if changed since the last write); clean -> remove
    internal void RecoveryTick(bool wait)
    {
        if (!IsDirtyNow()) { DeleteRecovery(); return; }
        if (recWrittenVer == docVer && File.Exists(RecoveryMd)) return;
        string text = NormText();
        string path = currentPath;
        recWrittenVer = docVer;
        var t = Task.Run(() => WriteRecovery(text, path));
        if (wait) t.Wait(5000);
    }

    static void WriteRecovery(string text, string path)
    {
        lock (recLock)
        {
            try
            {
                Directory.CreateDirectory(Log.Dir);
                AtomicWrite(RecoveryMd, text);
                AtomicWrite(RecoveryJson, new JavaScriptSerializer().Serialize(new RecoveryInfo { Path = path ?? "" }));
            }
            catch (Exception ex) { Log.Error("recovery write", ex); }
        }
    }

    class RecoveryInfo { public string Path; }

    static void AtomicWrite(string path, string content)
    {
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, content, new UTF8Encoding(false));
        if (File.Exists(path)) File.Replace(tmp, path, null); else File.Move(tmp, path);
    }

    internal void DeleteRecovery()
    {
        recWrittenVer = -1;
        lock (recLock)
        {
            try { if (File.Exists(RecoveryMd)) File.Delete(RecoveryMd); } catch (Exception ex) { Log.Error("recovery delete", ex); }
            try { if (File.Exists(RecoveryJson)) File.Delete(RecoveryJson); } catch { }
        }
    }

    // at start (Program wires this to Loaded): ask whether to restore an unsaved document
    internal bool recoveryHandled;   // false = 시작 시 복구 질문 전. 그때까지는 파일을 열어도 복구 파일을 지우지 않는다

    internal void OfferRecovery()
    {
        try
        {
            if (!File.Exists(RecoveryMd)) return;
            string text = File.ReadAllText(RecoveryMd, new UTF8Encoding(false));
            string path = "";
            try
            {
                if (File.Exists(RecoveryJson))
                {
                    var ri = new JavaScriptSerializer().Deserialize<RecoveryInfo>(File.ReadAllText(RecoveryJson, Encoding.UTF8));
                    if (ri != null && ri.Path != null) path = ri.Path;
                }
            }
            catch { }
            if (text.Length == 0 || text == NormText()) { DeleteRecovery(); return; }
            bool yes;
            if (TestRecoveryAnswer.HasValue) yes = TestRecoveryAnswer.Value;
            else
            {
                string what = path.Length > 0 ? Path.GetFileName(path) : "제목 없는 문서";
                string warn = "";
                try { if (path.Length > 0 && File.Exists(path) && File.GetLastWriteTime(path) > File.GetLastWriteTime(RecoveryMd)) warn = "\n\n※ 이 파일은 임시 저장 이후에 다른 곳에서 수정되었습니다. 복구 후 저장하면 그 수정이 덮어써집니다."; } catch { }
                yes = MessageBox.Show(this, "저장하지 않고 종료된 문서가 있습니다 (" + what + ").\n복구할까요?" + warn, AppInfo.Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
            }
            if (!yes) { DeleteRecovery(); return; }
            string baseText = "";
            bool hasFile = path.Length > 0 && File.Exists(path);
            if (hasFile) { try { baseText = File.ReadAllText(path, new UTF8Encoding(false, false)); } catch { } }
            SetEditorTextNoUndo(text);
            savedText = baseText;
            currentPath = hasFile ? path : null;
            fileNameBox.Text = hasFile ? Path.GetFileName(path) : "untitled.md";
            docVer++;
            UpdateDirtyUi(); UpdateFooter();
            RefreshPreviewNow(false);
        }
        catch (Exception ex) { Log.Error("offer recovery", ex); }
        finally { recoveryHandled = true; }
    }
}
