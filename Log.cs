using System;
using System.IO;
using System.Text;
static class Log
{
    static readonly object gate = new object();
    public static string Dir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MdEditor"); } }
    public static string FilePath { get { return Path.Combine(Dir, "error.log"); } }
    public static void Init()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var fi = new FileInfo(FilePath);
            if (fi.Exists && fi.Length > 1000000) File.Delete(FilePath);
        }
        catch { }
        Write("start v" + AppInfo.Version);
    }
    public static void Write(string msg)
    {
        try
        {
            lock (gate)
            {
                Directory.CreateDirectory(Dir);
                File.AppendAllText(FilePath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + msg + "\r\n", new UTF8Encoding(false));
            }
        }
        catch { }
    }
    public static void Error(string where, Exception ex) { Write("ERROR " + where + ": " + ex); }
}
