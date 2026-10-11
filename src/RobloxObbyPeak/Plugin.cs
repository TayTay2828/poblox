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
        public const string Version = "0.1.0";
        const string TestEnv = "ROBLOXOBBYPEAK_TEST";

        internal static Plugin Instance;
        internal static ManualLogSource Log;
        readonly Dictionary<string, ConfigEntry<bool>> systems = new Dictionary<string, ConfigEntry<bool>>();
        ConfigEntry<string> robloxFolder;
        RobloxAssets assets;
        PeakHooks hooks;
        Banner banner;
        AudioSource audio2d;
        Material skyMaterial;

        // obby run state (reset per scene)
        float? baseHeight;
        float firstSeen = -1f;
        int teamStage;
        readonly Dictionary<int, bool> wasDown = new Dictionary<int, bool>();
        readonly Dictionary<int, (float y, float t)> lastPos = new Dictionary<int, (float, float)>();
        readonly Dictionary<int, float> lastOof = new Dictionary<int, float>();
        bool testMode, testDone;

        void Awake()
        {
            Instance = this; Log = Logger; Banner.Log = Logger;
            robloxFolder = Config.Bind("Roblox", "RobloxFolder", "", "Roblox install or version folder. Empty = use the folder Melty passes, else %LOCALAPPDATA%\\Roblox.");
            foreach (var s in Sheets.Systems)
                systems[s.Id] = Config.Bind("Systems", s.ConfigKey, s.Default, s.Description);
            testMode = Environment.GetEnvironmentVariable(TestEnv) == "1";

            var root = RobloxLocator.Find(robloxFolder.Value, m => Log.LogInfo(m));
            if (root == null)
            {
                Log.LogError("Roblox install not found. Install Roblox (it only needs to be installed, not running) or set RobloxFolder in the config.");
                return;
            }
            Log.LogInfo("Roblox content <- " + root);
            hooks = new PeakHooks(Log);
            assets = new RobloxAssets(root, Log);

            var go = new GameObject("RobloxObbyPeak");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            audio2d = go.AddComponent<AudioSource>();
            audio2d.playOnAwake = false; audio2d.spatialBlend = 0f;
            banner = go.AddComponent<Banner>();
            StartCoroutine(Boot());

            SceneManager.sceneLoaded += (s, _) => ResetRun(s.name);
            var light = hooks.Method("campfire_light");
            if (light != null && On("campfire_checkpoint"))
                new Harmony(Guid).Patch(light, postfix: new HarmonyMethod(typeof(Plugin), nameof(CampfireLit)));
        }

        IEnumerator Boot()
        {
            yield return assets.LoadAll();
            banner.Setup(assets.FontPath);
            if (assets.SkyTextures != null && On("roblox_sky")) BuildSky();
            Log.LogInfo($"RobloxObbyPeak ready: sounds={string.Join(",", assets.Sounds.Keys)} font={banner.FontSource} sky={(skyMaterial != null ? "roblox" : "peak")}");
        }

        bool On(string system) => systems.TryGetValue(system, out var c) && c.Value;

        static bool IsGameplayScene(string name) =>
            !(name.IndexOf("Airport", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Title", StringComparison.OrdinalIgnoreCase) >= 0);

        void ResetRun(string scene)
        {
            baseHeight = null; firstSeen = -1f; teamStage = 0;
            wasDown.Clear(); lastPos.Clear(); lastOof.Clear();
            Log.LogInfo("Scene " + scene + (IsGameplayScene(scene) ? " (obby on)" : " (lobby)"));
            ApplySky();
        }

        void Update()
        {
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

            if (testMode && !testDone && Time.time - firstSeen > 6f) RunTest(chars[0]);
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
            var shader = Shader.Find("Skybox/6 Sided");
            if (shader == null) { Log.LogWarning("Skybox shader not in this build; keeping PEAK's sky"); return; }
            skyMaterial = new Material(shader);
            string[] props = { "_FrontTex", "_BackTex", "_LeftTex", "_RightTex", "_UpTex", "_DownTex" };
            for (int i = 0; i < 6; i++) skyMaterial.SetTexture(props[i], assets.SkyTextures[i]);
            ApplySky();
        }

        void ApplySky()
        {
            if (skyMaterial == null || !IsGameplayScene(SceneManager.GetActiveScene().name)) return;
            RenderSettings.skybox = skyMaterial;
            DynamicGI.UpdateEnvironment();
        }

        void RunTest(Component c)
        {
            testDone = true;
            banner.Show("Stage 1", "Roblox Obby Peak test", 5f);
            Play("snd_stage", null);
            Play("snd_oof", null);
            Log.LogInfo($"RobloxObbyPeak TEST: characters={hooks.Characters().Count} oofSound={assets.Sounds.ContainsKey("snd_oof")} font={banner.FontSource} stage={teamStage}");
        }
    }
}
