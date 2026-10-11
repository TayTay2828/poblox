using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.Networking;

namespace RobloxObbyPeak
{
    /// <summary>Loads the rows of sheets/roblox_assets.json from the player's Roblox folder.</summary>
    internal class RobloxAssets
    {
        static readonly string[] SkyFaces = { "ft", "bk", "lf", "rt", "up", "dn" };

        readonly string root;
        readonly ManualLogSource log;
        public readonly Dictionary<string, AudioClip> Sounds = new Dictionary<string, AudioClip>();
        public readonly Dictionary<string, string> Paths = new Dictionary<string, string>();
        public string FontPath;
        public Texture2D[] SkyTextures;
        public bool Done;

        public RobloxAssets(string root, ManualLogSource log) { this.root = root; this.log = log; }

        public IEnumerator LoadAll()
        {
            foreach (var row in Sheets.RobloxAssets)
            {
                switch (row.Kind)
                {
                    case "sound": yield return LoadSound(row); break;
                    case "font": FontPath = Resolve(row, new[] { ".ttf", ".otf" }); if (FontPath != null) Paths[row.Id] = FontPath; break;
                    case "sky": LoadSky(row); break;
                }
                if (row.Required && !Paths.ContainsKey(row.Id))
                    log.LogError($"Required Roblox file for '{row.Id}' not found in {root}");
            }
            Done = true;
        }

        string Resolve(RobloxAssetRow row, string[] exts)
        {
            foreach (var c in row.Candidates)
            {
                var p = Path.Combine(root, c.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(p)) return p;
            }
            var dir = Path.Combine(root, row.SearchDir.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(dir)) return null;
            var files = Directory.GetFiles(dir).Where(f => exts.Contains(Path.GetExtension(f).ToLowerInvariant())).ToArray();
            foreach (var k in row.Keywords)
            {
                var hit = files.FirstOrDefault(f => Path.GetFileName(f).ToLowerInvariant().Contains(k));
                if (hit != null) return hit;
            }
            return null;
        }

        IEnumerator LoadSound(RobloxAssetRow row)
        {
            var path = Resolve(row, new[] { ".ogg", ".mp3", ".wav" });
            if (path == null) { log.LogWarning($"Roblox sound '{row.Id}' not found"); yield break; }
            var ext = Path.GetExtension(path).ToLowerInvariant();
            var type = ext == ".ogg" ? AudioType.OGGVORBIS : ext == ".mp3" ? AudioType.MPEG : AudioType.WAV;
            using (var req = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, type))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) { log.LogWarning($"Could not read {path}: {req.error}"); yield break; }
                var clip = DownloadHandlerAudioClip.GetContent(req);
                if (clip == null) yield break;
                clip.name = "Roblox_" + Path.GetFileNameWithoutExtension(path);
                Sounds[row.Id] = clip;
                Paths[row.Id] = path;
                log.LogInfo($"Roblox sound '{row.Id}' <- {path} ({clip.length:0.00}s)");
            }
        }

        void LoadSky(RobloxAssetRow row)
        {
            foreach (var cand in row.Candidates)
            {
                var texes = new Texture2D[6];
                var ok = true;
                for (int i = 0; i < 6 && ok; i++)
                {
                    var p = Path.Combine(root, cand.Replace("{face}", SkyFaces[i]).Replace('/', Path.DirectorySeparatorChar));
                    texes[i] = File.Exists(p) ? TextureFile.Load(p, log) : null;
                    ok = texes[i] != null;
                }
                if (!ok) continue;
                SkyTextures = texes;
                Paths[row.Id] = Path.GetDirectoryName(Path.Combine(root, cand));
                log.LogInfo($"Roblox sky <- {Paths[row.Id]}");
                return;
            }
            log.LogWarning("Roblox sky textures not found or in an unreadable format; keeping PEAK's sky");
        }
    }

    /// <summary>Reads DDS and KTX1 textures (Roblox ships its built-in textures as .tex containers of these).</summary>
    internal static class TextureFile
    {
        public static Texture2D Load(string path, ManualLogSource log)
        {
            try
            {
                var b = File.ReadAllBytes(path);
                if (b.Length > 128 && b[0] == 'D' && b[1] == 'D' && b[2] == 'S' && b[3] == ' ') return Dds(b);
                if (b.Length > 64 && b[0] == 0xAB && b[1] == 'K' && b[2] == 'T' && b[3] == 'X') return Ktx(b);
                if (b.Length > 8 && (b[0] == 0x89 || b[0] == 0xFF)) { var t = new Texture2D(2, 2); return t.LoadImage(b) ? t : null; }
                log.LogWarning($"Unknown texture format in {path} (magic {b[0]:X2}{b[1]:X2}{b[2]:X2}{b[3]:X2})");
            }
            catch (Exception e) { log.LogWarning($"Could not read {path}: {e.Message}"); }
            return null;
        }

        static Texture2D Dds(byte[] b)
        {
            int h = BitConverter.ToInt32(b, 12), w = BitConverter.ToInt32(b, 16);
            var four = System.Text.Encoding.ASCII.GetString(b, 84, 4);
            int offset = 128;
            TextureFormat fmt;
            if (four == "DXT1") fmt = TextureFormat.DXT1;
            else if (four == "DXT5") fmt = TextureFormat.DXT5;
            else if (four == "DX10")
            {
                int dxgi = BitConverter.ToInt32(b, 128); offset = 148;
                fmt = dxgi == 71 ? TextureFormat.DXT1 : dxgi == 77 ? TextureFormat.DXT5 : dxgi == 98 ? TextureFormat.BC7 : dxgi == 28 ? TextureFormat.RGBA32 : dxgi == 87 ? TextureFormat.BGRA32 : throw new Exception("DXGI " + dxgi);
            }
            else if (BitConverter.ToInt32(b, 88) == 32) fmt = TextureFormat.BGRA32;
            else throw new Exception("DDS " + four);
            return Raw(b, offset, w, h, fmt);
        }

        static Texture2D Ktx(byte[] b)
        {
            int glFmt = BitConverter.ToInt32(b, 28), w = BitConverter.ToInt32(b, 36), h = BitConverter.ToInt32(b, 40), kv = BitConverter.ToInt32(b, 60);
            int offset = 64 + kv + 4;
            TextureFormat fmt = glFmt == 0x83F1 || glFmt == 0x83F0 ? TextureFormat.DXT1 : glFmt == 0x83F3 ? TextureFormat.DXT5 : glFmt == 0x8E8C ? TextureFormat.BC7 : glFmt == 0x8058 ? TextureFormat.RGBA32 : throw new Exception("KTX gl 0x" + glFmt.ToString("X"));
            return Raw(b, offset, w, h, fmt);
        }

        static Texture2D Raw(byte[] b, int offset, int w, int h, TextureFormat fmt)
        {
            int size = fmt == TextureFormat.DXT1 ? Math.Max(1, (w + 3) / 4) * Math.Max(1, (h + 3) / 4) * 8
                : fmt == TextureFormat.DXT5 || fmt == TextureFormat.BC7 ? Math.Max(1, (w + 3) / 4) * Math.Max(1, (h + 3) / 4) * 16
                : w * h * 4;
            var data = new byte[size];
            Buffer.BlockCopy(b, offset, data, 0, size);
            var t = new Texture2D(w, h, fmt, false) { wrapMode = TextureWrapMode.Clamp };
            t.LoadRawTextureData(data);
            t.Apply(false, true);
            return t;
        }
    }
}
