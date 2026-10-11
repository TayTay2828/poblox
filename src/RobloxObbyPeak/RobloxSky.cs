using System;
using UnityEngine;

namespace RobloxObbyPeak
{
    /// <summary>Roblox's six sky faces on an inward-facing box that follows the camera (PEAK's build has no built-in skybox shader).</summary>
    internal class RobloxSky : MonoBehaviour
    {
        static readonly string[] Shaders = { "Universal Render Pipeline/Unlit", "HDRP/Unlit", "Unlit/Texture", "Sprites/Default", "UI/Default" };
        // ft, bk, lf, rt, up, dn: position direction and rotation of each quad (a Unity quad faces -Z)
        static readonly Vector3[] Dirs = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right, Vector3.up, Vector3.down };
        static readonly Vector3[] Rots = { Vector3.zero, new Vector3(0, 180, 0), new Vector3(0, -90, 0), new Vector3(0, 90, 0), new Vector3(-90, 0, 0), new Vector3(90, 0, 0) };

        Transform root;
        Transform[] faces;
        public bool Active;
        public string ShaderName;

        public bool Build(Texture2D[] tex)
        {
            Shader shader = null;
            foreach (var n in Shaders) { shader = Shader.Find(n); if (shader != null) { ShaderName = n; break; } }
            if (shader == null) return false;
            root = new GameObject("RobloxObbyPeak_Sky").transform;
            DontDestroyOnLoad(root.gameObject);
            faces = new Transform[6];
            for (int i = 0; i < 6; i++)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(q.GetComponent<Collider>());
                q.name = "face" + i;
                q.transform.SetParent(root, false);
                q.transform.localEulerAngles = Rots[i];
                var m = new Material(shader) { renderQueue = 1000 };
                foreach (var p in new[] { "_BaseMap", "_MainTex", "_UnlitColorMap" }) if (m.HasProperty(p)) m.SetTexture(p, tex[i]);
                var r = q.GetComponent<MeshRenderer>();
                r.sharedMaterial = m;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                faces[i] = q.transform;
            }
            root.gameObject.SetActive(false);
            return true;
        }

        void LateUpdate()
        {
            if (root == null) return;
            var cam = Camera.main;
            bool show = Active && cam != null;
            if (root.gameObject.activeSelf != show) root.gameObject.SetActive(show);
            if (!show) return;
            float d = cam.farClipPlane * 0.57f; // box corners stay inside the far plane
            root.position = cam.transform.position;
            for (int i = 0; i < 6; i++) { faces[i].localPosition = Dirs[i] * d; faces[i].localScale = Vector3.one * (2f * d + 1f); }
        }
    }
}
