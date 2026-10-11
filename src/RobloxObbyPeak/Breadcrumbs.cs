using System;
using System.IO;

namespace RobloxObbyPeak
{
    /// <summary>A short timeline in {localappdata}/RobloxObbyPeak/session.log, so a sudden close shows the last step reached.</summary>
    internal static class Breadcrumbs
    {
        static string file;
        static DateTime start;

        public static void Begin()
        {
            try
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RobloxObbyPeak");
                Directory.CreateDirectory(dir);
                file = Path.Combine(dir, "session.log");
                start = DateTime.UtcNow;
                File.WriteAllText(file, $"Roblox Obby Peak {Plugin.Version} session {start:u}\n");
            }
            catch (Exception) { file = null; }
        }

        public static void Write(string what)
        {
            if (file == null) return;
            try { File.AppendAllText(file, $"{(DateTime.UtcNow - start).TotalSeconds,7:0.0}s {what}\n"); } catch (Exception) { }
        }
    }
}
