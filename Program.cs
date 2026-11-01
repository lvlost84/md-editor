using System;
static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--selftest") return SelfTest.Run(args);
        return 0; // UI implementer replaces
    }
}
