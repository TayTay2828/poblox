using System;
using System.IO;
using System.Linq;

namespace RobloxObbyPeak
{
    /// <summary>Finds the player's own Roblox version folder (the one with content/ and RobloxPlayerBeta.exe). Read-only.</summary>
    internal static class RobloxLocator
    {
        public const string EnvVar = "ROBLOXOBBYPEAK_ROBLOX_DIR";

        public static string Find(string configured, Action<string> log)
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var tries = new[]
            {
                Environment.GetEnvironmentVariable(EnvVar),
                configured,
                string.IsNullOrEmpty(local) ? null : Path.Combine(local, "Roblox"),
                string.IsNullOrEmpty(local) ? null : Path.Combine(local, "Bloxstrap"),
                string.IsNullOrEmpty(local) ? null : Path.Combine(local, "Fishstrap"),
            };
            foreach (var t in tries)
            {
                if (string.IsNullOrWhiteSpace(t)) continue;
                var found = Resolve(t.Trim().Trim('"'));
                if (found != null) return found;
                log($"No Roblox content under '{t}'");
            }
            return null;
        }

        static string Resolve(string dir)
        {
            try
            {
                if (!Directory.Exists(dir)) return null;
                if (IsVersionDir(dir)) return dir;
                foreach (var parent in new[] { Path.Combine(dir, "Versions"), dir })
                {
                    if (!Directory.Exists(parent)) continue;
                    var best = Directory.GetDirectories(parent)
                        .Where(IsVersionDir)
                        .OrderByDescending(d => File.Exists(Path.Combine(d, "RobloxPlayerBeta.exe")))
                        .ThenByDescending(d => Directory.GetLastWriteTimeUtc(d))
                        .FirstOrDefault();
                    if (best != null) return best;
                }
            }
            catch (Exception) { }
            return null;
        }

        static bool IsVersionDir(string d) => Directory.Exists(Path.Combine(d, "content", "sounds"));
    }
}
