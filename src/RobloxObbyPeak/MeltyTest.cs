using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace RobloxObbyPeak
{
    /// <summary>
    /// Melty test runs: while %LOCALAPPDATA%/Melty/test/roblox-obby-peak.json exists, report each row of
    /// sheets/tests.json as "pass id" or "fail id: why" to the checks file it names.
    /// </summary>
    internal static class MeltyTest
    {
        const string Slug = "roblox-obby-peak";

        public static string ChecksFile()
        {
            try
            {
                var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var f = Path.Combine(local, "Melty", "test", Slug + ".json");
                if (!File.Exists(f)) return null;
                var m = Regex.Match(File.ReadAllText(f), "\"checks\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
                return m.Success ? Regex.Unescape(m.Groups[1].Value) : null;
            }
            catch (Exception) { return null; }
        }

        public static void Report(string checksFile, Dictionary<string, string> failures)
        {
            var lines = new List<string>();
            foreach (var t in Sheets.Tests)
                lines.Add(failures.TryGetValue(t.Id, out var why) && why != null ? $"fail {t.Id}: {why}" : $"pass {t.Id}");
            foreach (var l in lines) Plugin.Log.LogInfo("Melty test: " + l);
            if (checksFile == null) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(checksFile));
                File.AppendAllText(checksFile, string.Join("\n", lines) + "\n");
            }
            catch (Exception e) { Plugin.Log.LogWarning("Could not write test checks: " + e.Message); }
        }
    }
}
