using System;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using UnityEngine;

namespace RobloxObbyPeak
{
    /// <summary>
    /// The obby banner. Draws with TextMeshPro using a font asset built from Roblox's own font file
    /// (TMP is reached by reflection so the mod needs no TMP reference); falls back to IMGUI text.
    /// </summary>
    internal class Banner : MonoBehaviour
    {
        public static ManualLogSource Log;
        string title, sub;
        float until;
        Component tmpTitle, tmpSub;
        PropertyInfo textProp;
        GUIStyle big, small;
        public string FontSource = "PEAK default";

        public void Setup(string robloxFontPath)
        {
            if (robloxFontPath == null) return;
            try
            {
                var tmpAsm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Unity.TextMeshPro");
                var fontAssetT = tmpAsm?.GetType("TMPro.TMP_FontAsset");
                var uguiT = tmpAsm?.GetType("TMPro.TextMeshProUGUI");
                if (fontAssetT == null || uguiT == null) { Log.LogWarning("TextMeshPro not found; banner uses IMGUI"); return; }
                var create = fontAssetT.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(m => m.Name == "CreateFontAsset")
                    .Select(m => (m, p: m.GetParameters()))
                    .FirstOrDefault(x => x.p.Length >= 7 && x.p[0].ParameterType == typeof(string) && x.p[1].ParameterType == typeof(int));
                if (create.m == null) { Log.LogWarning("TMP_FontAsset.CreateFontAsset(path) unavailable; banner uses IMGUI"); return; }
                var args = new object[create.p.Length];
                args[0] = robloxFontPath; args[1] = 0; args[2] = 90; args[3] = 9;
                args[4] = Enum.ToObject(create.p[4].ParameterType, 4165); // GlyphRenderMode.SDFAA
                args[5] = 1024; args[6] = 1024;
                for (int i = 7; i < args.Length; i++) args[i] = create.p[i].HasDefaultValue ? create.p[i].DefaultValue : null;
                var fontAsset = create.m.Invoke(null, args);
                if (fontAsset == null) { Log.LogWarning("Roblox font could not be turned into a TMP font"); return; }

                var canvasGo = new GameObject("RobloxObbyPeak_Canvas");
                canvasGo.transform.SetParent(transform, false);
                var canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 32000;
                textProp = uguiT.GetProperty("text");
                tmpTitle = MakeText(canvasGo.transform, uguiT, fontAsset, 96f, new Vector2(0.5f, 0.78f));
                tmpSub = MakeText(canvasGo.transform, uguiT, fontAsset, 40f, new Vector2(0.5f, 0.70f));
                FontSource = robloxFontPath;
                Log.LogInfo("Banner font <- " + robloxFontPath);
            }
            catch (Exception e) { Log.LogWarning("Roblox font setup failed, using IMGUI: " + (e.InnerException ?? e).Message); tmpTitle = tmpSub = null; }
        }

        static Component MakeText(Transform parent, Type uguiT, object font, float size, Vector2 anchor)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.sizeDelta = new Vector2(1600, size * 1.4f);
            var t = go.AddComponent(uguiT);
            uguiT.GetProperty("font").SetValue(t, font);
            uguiT.GetProperty("fontSize").SetValue(t, size);
            var align = uguiT.GetProperty("alignment");
            align.SetValue(t, Enum.ToObject(align.PropertyType, 514)); // TextAlignmentOptions.Center
            uguiT.GetProperty("color").SetValue(t, Color.white);
            uguiT.GetProperty("outlineWidth")?.SetValue(t, 0.25f);
            uguiT.GetProperty("outlineColor")?.SetValue(t, (Color32)Color.black);
            go.SetActive(false);
            return t;
        }

        public void Show(string t, string s, float seconds = 3f)
        {
            title = t; sub = s; until = Time.unscaledTime + seconds;
            if (tmpTitle == null) return;
            textProp.SetValue(tmpTitle, t); textProp.SetValue(tmpSub, s ?? "");
            tmpTitle.gameObject.SetActive(true); tmpSub.gameObject.SetActive(!string.IsNullOrEmpty(s));
        }

        void Update()
        {
            if (tmpTitle != null && tmpTitle.gameObject.activeSelf && Time.unscaledTime > until)
            { tmpTitle.gameObject.SetActive(false); tmpSub.gameObject.SetActive(false); }
        }

        void OnGUI()
        {
            if (tmpTitle != null || Time.unscaledTime > until || title == null) return;
            if (big == null)
            {
                big = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = Screen.height / 11 };
                small = new GUIStyle(big) { fontSize = Screen.height / 26, fontStyle = FontStyle.Normal };
            }
            Shadowed(new Rect(0, Screen.height * 0.14f, Screen.width, Screen.height * 0.12f), title, big);
            if (!string.IsNullOrEmpty(sub)) Shadowed(new Rect(0, Screen.height * 0.25f, Screen.width, Screen.height * 0.06f), sub, small);
        }

        static void Shadowed(Rect r, string text, GUIStyle st)
        {
            var c = GUI.color; GUI.color = Color.black;
            GUI.Label(new Rect(r.x + 3, r.y + 3, r.width, r.height), text, st);
            GUI.color = Color.white; GUI.Label(r, text, st); GUI.color = c;
        }
    }
}
