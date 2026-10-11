using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RobloxObbyPeak
{
    [BepInPlugin(Guid, "Roblox Obby Peak", Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "melty.robloxobbypeak";
        public const string Version = "0.1.1";
        const string TestEnv = "ROBLOXOBBYPEAK_TEST";

        internal static Plugin Instance;
        internal static ManualLogSource Log;
        readonly Dictionary<string, ConfigEntry<bool>> systems = new Dictionary<string, ConfigEntry<bool>>();
        ConfigEntry<string> robloxFolder;
        RobloxAssets assets;
        PeakHooks hooks;
        Banner banner;
        AudioSource audio2d;
        RobloxSky sky;
        float nextHeartbeat;

        // obby run state (reset per scene)
        float? baseHeight;
        float firstSeen = -1f;
        int teamStage;
        readonly Dictionary<int, bool> wasDown = new Dictionary<int, bool>();
        readonly Dictionary<int, (float y, float t)> lastPos = new Dictionary<int, (float, float)>();
        readonly Dictionary<int, float> lastOof = new Dictionary<int, float>();
        bool testMode;
        string checksFile;
        Driver driver;

        internal class Driver : MonoBehaviour { void Update() => Instance?.Tick(); }

        void Awake()
        {
            Instance = this; Log = Logger; Banner.Log = Logger;
            Breadcrumbs.Begin();
            robloxFolder = Config.Bind("Roblox", "RobloxFolder", "", "Roblox install or version folder. Empty = use the folder Melty passes, else %LOCALAPPDATA%\\Roblox.");
            foreach (var s in Sheets.Systems)
                systems[s.Id] = Config.Bind("Systems", s.ConfigKey, s.Default, s.Description);
            testMode = RobloxLocator.Setting(TestEnv) == "1";

            checksFile = MeltyTest.ChecksFile();
            testMode |= checksFile != null;
            var root = RobloxLocator.Find(robloxFolder.Value, m => Log.LogInfo(m));
            if (root == null)
            {
                Log.LogError("Roblox install not found. Install Roblox (it only needs to be installed, not running) or set RobloxFolder in the config.");
                if (testMode)
                    MeltyTest.Report(checksFile, Sheets.Tests.ToDictionary(t => t.Id, t => "Roblox install not found"));
                return;
            }
            Log.LogInfo("Roblox content <- " + root);
            hooks = new PeakHooks(Log);
            assets = new RobloxAssets(root, Log);

            var go = new GameObject("RobloxObbyPeak");
            DontDestroyOnLoad(go);
            audio2d = go.AddComponent<AudioSource>();
            audio2d.playOnAwake = false; audio2d.spatialBlend = 0f;
            banner = go.AddComponent<Banner>();
            // our own object drives the mod, in case the game destroys BepInEx's manager object
            driver = go.AddComponent<Driver>();
            driver.StartCoroutine(Boot());

            SceneManager.sceneLoaded += (s, _) => ResetRun(s.name);
            var light = hooks.Method("campfire_light");
            if (light != null && On("campfire_checkpoint"))
                new Harmony(Guid).Patch(light, postfix: new HarmonyMethod(typeof(Plugin), nameof(CampfireLit)));
        }

        IEnumerator Boot()
        {
            Breadcrumbs.Write("loading Roblox files");
            yield return assets.LoadAll();
            Breadcrumbs.Write("Roblox files loaded; building banner font");
            banner.Setup(assets.FontPath);
            Breadcrumbs.Write("banner ready (" + banner.FontSource + ")");
            if (assets.SkyTextures != null && On("roblox_sky")) BuildSky();
            Log.LogInfo($"RobloxObbyPeak ready: sounds={string.Join(",", assets.Sounds.Keys)} font={banner.FontSource} sky={(sky != null ? "roblox" : "peak")}");
            Breadcrumbs.Write("ready");
            if (testMode) RunTest();
        }

        bool On(string system) => systems.TryGetValue(system, out var c) && c.Value;

        static bool IsGameplayScene(string name) =>
            !(name.IndexOf("Airport", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Title", StringComparison.OrdinalIgnoreCase) >= 0);

        void ResetRun(string scene)
        {
            baseHeight = null; firstSeen = -1f; teamStage = 0;
            wasDown.Clear(); lastPos.Clear(); lastOof.Clear();
            Breadcrumbs.Write("scene " + scene);
            Log.LogInfo("Scene " + scene + (IsGameplayScene(scene) ? " (obby on)" : " (lobby)"));
            ApplySky();
        }

        void Tick()
        {
            if (Time.unscaledTime >= nextHeartbeat)
            {
                nextHeartbeat = Time.unscaledTime + 5f;
                Breadcrumbs.Write("alive in " + SceneManager.GetActiveScene().name);
            }
            if (hooks == null || assets == null || !assets.Done) return;
            if (!IsGameplayScene(SceneManager.GetActiveScene().name)) return;
            var chars = hooks.Characters();
            if (chars.Count == 0) return;
            if (firstSeen < 0) firstSeen = Time.time;
            if (baseHeight == null)
            {
                if (Time.time - firstSeen < 3f) return; // let scouts land after spawning
                baseHeight = chars.Min(c => hooks.Position(c).y);
                Log.LogInfo($"Run start height {baseHeight:0.0}");
            }

            foreach (var c in chars)
            {
                var pos = hooks.Position(c);
                var id = c.GetInstanceID();
                if (On("stage_banner")) CheckStage(c, pos.y - baseHeight.Value);
                if (On("fall_oof")) CheckOof(c, id, pos);
            }

        }

        void CheckStage(Component c, float height)
        {
            var row = Sheets.Stages.LastOrDefault(s => height >= s.MinHeight);
            if (row.BannerText == null || row.Stage <= teamStage) return;
            teamStage = row.Stage;
            var who = row.Stage == 1 ? "Climb to the next stage!" : hooks.Name(c) + " reached it";
            Log.LogInfo($"Stage {row.Stage} reached by {hooks.Name(c)} at {height:0}m");
            banner.Show(row.BannerText, who);
            Play(row.SoundId, null);
        }

        void CheckOof(Component c, int id, Vector3 pos)
        {
            bool down = hooks.IsDown(c);
            // fallback when the game's state fields can't be read: a fast drop
            if (!hooks.Has("char_data") && lastPos.TryGetValue(id, out var lp) && Time.time > lp.t)
                down = (pos.y - lp.y) / (Time.time - lp.t) < -18f;
            lastPos[id] = (pos.y, Time.time);
            wasDown.TryGetValue(id, out var before);
            wasDown[id] = down;
            if (!down || before) return;
            if (lastOof.TryGetValue(id, out var t) && Time.time - t < 2f) return;
            lastOof[id] = Time.time;
            Log.LogInfo("Oof: " + hooks.Name(c));
            Play("snd_oof", hooks.IsLocal(c) ? (Vector3?)null : pos);
        }

        static void CampfireLit(Component __instance)
        {
            var p = Instance;
            if (p == null || p.banner == null) return;
            Log.LogInfo("Campfire lit -> checkpoint");
            p.banner.Show("Checkpoint!", "Campfire lit", 3f);
            p.Play("snd_checkpoint", null);
        }

        void Play(string soundId, Vector3? at)
        {
            if (!assets.Sounds.TryGetValue(soundId, out var clip)) return;
            if (at.HasValue) AudioSource.PlayClipAtPoint(clip, at.Value, 1f);
            else audio2d.PlayOneShot(clip);
        }

        void BuildSky()
        {
            sky = driver.gameObject.AddComponent<RobloxSky>();
            if (!sky.Build(assets.SkyTextures)) { Log.LogWarning("No unlit shader found for the Roblox sky; keeping PEAK's sky"); Destroy(sky); sky = null; return; }
            Log.LogInfo("Roblox sky box built with " + sky.ShaderName);
            ApplySky();
        }

        void ApplySky()
        {
            if (sky != null) sky.Active = IsGameplayScene(SceneManager.GetActiveScene().name);
        }

        void RunTest()
        {
            banner.Show("Stage 1", "Roblox Obby Peak test", 5f);
            Play("snd_stage", null);
            Play("snd_oof", null);
            var fail = new Dictionary<string, string>
            {
                ["roblox-found"] = null,
                ["oof-sound"] = assets.Sounds.ContainsKey("snd_oof") ? null : "ouch.ogg not found or not decodable",
                ["peak-hooks"] = hooks.CharacterType == null ? "PEAK Character type not found"
                    : !hooks.Has("all_characters") ? "Character.AllCharacters not found"
                    : !hooks.Has("char_data") ? "Character.data not found" : null,
                ["banner-shown"] = null,
            };
            Log.LogInfo($"RobloxObbyPeak TEST: font={banner.FontSource} sounds={assets.Sounds.Count}");
            MeltyTest.Report(checksFile, fail);
            Breadcrumbs.Write("test checks written; banner on screen");
        }
    }
}
