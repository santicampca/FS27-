using System;
using System.IO;
using FS27.Client;

// The same match as Client/test/smoke.mjs runs in the browser, played natively through the same bridge: the two final checksums must be equal (determinism across runtimes).
public static class Program
{
    public static void Main(string[] args)
    {
        string authoring = args.Length > 0 ? args[0] : Path.GetFullPath("Docs/authoring");
        foreach (string f in DataFiles.AllFiles()) GameBridge.AddFile(f, File.ReadAllText(Path.Combine(authoring, f)));
        string problems = GameBridge.LoadData();
        if (problems != "") { Console.Error.WriteLine("database problems: " + problems); Environment.Exit(2); }
        string roster = GameBridge.NewMatch(2, 3, 11, "Classic", "Professional");
        if (roster.StartsWith("error")) { Console.Error.WriteLine(roster); Environment.Exit(3); }
        double[] s = null; long steps = 0; bool finished = false;
        for (int i = 0; i < 400000 && !finished; i++) { steps += GameBridge.Advance(8.0 / 60); s = GameBridge.Snapshot(); finished = s[11] == 1; }
        Console.WriteLine("finished=" + finished + " matchSeconds=" + s[0].ToString("0.0") + " score=" + s[2] + "-" + s[3] + " steps=" + steps);
        Console.WriteLine("checksum=" + GameBridge.Checksum());
    }
}
