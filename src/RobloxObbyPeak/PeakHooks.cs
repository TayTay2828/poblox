using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using UnityEngine;

namespace RobloxObbyPeak
{
    /// <summary>Resolves the rows of sheets/hooks.json against PEAK's Assembly-CSharp by name.</summary>
    internal class PeakHooks
    {
        const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        readonly Dictionary<string, MemberInfo> members = new Dictionary<string, MemberInfo>();
        readonly ManualLogSource log;
        public Type CharacterType;

        public PeakHooks(ManualLogSource log)
        {
            this.log = log;
            var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
            foreach (var row in Sheets.Hooks)
            {
                var type = asm?.GetType(row.TypeName) ?? asm?.GetTypes().FirstOrDefault(t => t.Name == row.TypeName);
                MemberInfo m = null;
                if (type != null)
                    m = row.MemberKind == "field" ? (MemberInfo)type.GetField(row.Member, All)
                      : row.MemberKind == "property" ? type.GetProperty(row.Member, All)
                      : type.GetMethods(All).FirstOrDefault(x => x.Name == row.Member);
                if (row.TypeName == "Character") CharacterType = CharacterType ?? type;
                if (m != null) { members[row.Id] = m; log.LogInfo($"hook {row.Id}: {row.TypeName}.{row.Member} ok"); }
                else log.LogWarning($"hook {row.Id}: {row.TypeName}.{row.Member} not found; fallback = {row.Fallback}");
            }
        }

        public bool Has(string id) => members.ContainsKey(id);
        public MethodInfo Method(string id) => members.TryGetValue(id, out var m) ? m as MethodInfo : null;

        public object Get(string id, object target)
        {
            if (!members.TryGetValue(id, out var m)) return null;
            try
            {
                if (m is FieldInfo f) return f.GetValue(f.IsStatic ? null : target);
                if (m is PropertyInfo p) return p.GetValue(p.GetGetMethod(true).IsStatic ? null : target);
            }
            catch (Exception) { }
            return null;
        }

        public List<Component> Characters()
        {
            var list = new List<Component>();
            if (Get("all_characters", null) is IEnumerable e)
                foreach (var c in e) if (c is Component comp && comp) list.Add(comp);
            if (list.Count == 0 && CharacterType != null)
                foreach (var o in UnityEngine.Object.FindObjectsOfType(CharacterType)) if (o is Component comp) list.Add(comp);
            return list;
        }

        public Vector3 Position(Component c) => Get("char_center", c) is Vector3 v ? v : c.transform.position;

        public string Name(Component c) => Get("char_name", c) as string is string s && s.Length > 0 ? s : "Scout";

        public bool IsLocal(Component c) => ReferenceEquals(Get("local_character", null), c);

        /// <summary>True while the scout is ragdoll-falling, passed out or dead.</summary>
        public bool IsDown(Component c)
        {
            var data = Get("char_data", c);
            if (data == null) return false;
            return Get("data_passed_out", data) is bool po && po
                || Get("data_dead", data) is bool d && d
                || Get("data_fall", data) is float fs && fs > 0.25f;
        }
    }
}
