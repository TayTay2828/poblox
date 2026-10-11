// PalCraft extractor: reads the player's own Palworld install and writes, for each Pal in sheets/pals.json,
// its skeletal mesh (bind pose, LOD0) and base-colour textures into PalCraft's folder in the Minecraft instance.
// Nothing is copied out of Palworld into the mod's download; this runs on the player's PC.
//
// usage: PalCraftExtractor --palworld <Palworld folder> --out <folder> --mappings <Mappings.usmap>
using System.Reflection;
using System.Text;
using System.Text.Json;
using CUE4Parse.FileProvider;
using CUE4Parse.MappingsProvider.Usmap;
using CUE4Parse.UE4.Assets.Exports;
using CUE4Parse.UE4.Assets.Exports.Material;
using CUE4Parse.UE4.Assets.Exports.SkeletalMesh;
using CUE4Parse.UE4.Assets.Exports.Texture;
using CUE4Parse.UE4.Versions;
using CUE4Parse_Conversion.Meshes;
using CUE4Parse_Conversion.Textures;
using Newtonsoft.Json.Linq;

static string Arg(string[] a, string name) { var i = Array.IndexOf(a, name); return i >= 0 && i + 1 < a.Length ? a[i + 1] : null; }

var palworld = Arg(args, "--palworld");
var outDir = Arg(args, "--out");
var mappings = Arg(args, "--mappings") ?? Path.Combine(AppContext.BaseDirectory, "Mappings.usmap");
if (palworld == null || outDir == null) { Console.Error.WriteLine("usage: --palworld <dir> --out <dir> [--mappings <usmap>]"); return 2; }
Directory.CreateDirectory(outDir);
var logPath = Path.Combine(outDir, "extract.log");
var log = new StreamWriter(logPath, false, Encoding.UTF8) { AutoFlush = true };
void Log(string s) { Console.WriteLine(s); log.WriteLine(s); }

JsonElement palsSheet;
using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("pals.json"))
    palsSheet = JsonDocument.Parse(s).RootElement.GetProperty("rows").Clone();

var paks = Path.Combine(palworld, "Pal", "Content", "Paks");
if (!Directory.Exists(paks)) { Log("FAIL no Palworld paks at " + paks); return 3; }
Log("Palworld paks: " + paks);

var provider = new DefaultFileProvider(paks, SearchOption.TopDirectoryOnly, new VersionContainer(EGame.GAME_UE5_1));
provider.MappingsContainer = new FileUsmapTypeMappingsProvider(mappings, StringComparer.OrdinalIgnoreCase);
provider.Initialize();
provider.Mount();
Log($"Mounted {provider.Files.Count} files");

// Pal names from Palworld's own text table (optional: the sheet's fallback name is used if it can't be read).
var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
try
{
    var tbl = provider.LoadPackageObjects("Pal/Content/L10N/en/Pal/DataTable/Text/DT_PalNameText_Common").First(e => (e.Class?.Name.ToString() ?? "").Contains("DataTable"));
    var rows = (JObject)JObject.Parse(Newtonsoft.Json.JsonConvert.SerializeObject(tbl))["Rows"];
    foreach (var p in rows.Properties())
    {
        var text = p.Value.SelectToken("TextData.LocalizedString")?.ToString() ?? p.Value.SelectToken("TextData.SourceString")?.ToString();
        if (!string.IsNullOrEmpty(text)) names[p.Name] = text;
    }
    Log($"names: {names.Count} rows");
}
catch (Exception e) { Log("names table unreadable, using fallback names: " + e.Message); }

var manifest = new List<object>();
int ok = 0;
foreach (var row in palsSheet.EnumerateArray())
{
    var id = row.GetProperty("id").GetString();
    var tribe = row.GetProperty("tribe").GetString();
    try
    {
        var palDir = Path.Combine(outDir, "pals", id);
        Directory.CreateDirectory(palDir);
        var meshPath = FindMesh(tribe);
        if (meshPath == null) throw new Exception("no SK_" + tribe + " mesh in Palworld");
        var mesh = provider.LoadPackageObjects(meshPath).OfType<USkeletalMesh>().First();
        if (!MeshConverter.TryConvert(mesh, out var converted) || converted.LODs.Count == 0) throw new Exception("mesh conversion failed");
        var lod = converted.LODs[0];
        var indices = lod.Indices.Value;
        var sections = lod.Sections.Value;

        // textures: one per material slot, the base colour parameter
        var texFiles = new List<string>();
        for (int m = 0; m < mesh.Materials.Length; m++)
        {
            string file = null;
            try
            {
                var mat = mesh.Materials[m]?.Load<UMaterialInterface>();
                var tex = mat == null ? null : BaseColor(mat);
                if (tex != null)
                {
                    var decoded = tex.Decode(1024, ETexturePlatform.DesktopMobile);
                    var png = TextureEncoder.Encode(decoded, ETextureFormat.Png, false, out _);
                    file = $"tex_{m}.png";
                    File.WriteAllBytes(Path.Combine(palDir, file), png);
                }
            }
            catch (Exception e) { Log($"  {id}: material {m} texture skipped: {e.Message}"); }
            texFiles.Add(file);
        }

        // mesh.bin: "PCM1", vertexCount, then per vertex x y z u v (UE units converted: Z-up cm -> Y-up blocks),
        // sectionCount, per section: textureSlot, indexCount, indices
        using (var bw = new BinaryWriter(File.Create(Path.Combine(palDir, "mesh.bin"))))
        {
            bw.Write(Encoding.ASCII.GetBytes("PCM1"));
            bw.Write(lod.Verts.Length);
            foreach (var v in lod.Verts)
            {
                bw.Write(v.Position.Y / 100f); // UE +Y (right) -> MC +X
                bw.Write(v.Position.Z / 100f); // UE +Z (up) -> MC +Y
                bw.Write(v.Position.X / 100f); // UE +X (forward) -> MC +Z (forward)
                bw.Write(v.UV.U);
                bw.Write(v.UV.V);
            }
            var valid = sections.Where(s => s.NumFaces > 0).ToArray();
            bw.Write(valid.Length);
            foreach (var s in valid)
            {
                bw.Write(s.MaterialIndex);
                bw.Write(s.NumFaces * 3);
                for (int i = 0; i < s.NumFaces * 3; i++) bw.Write((int)indices[s.FirstIndex + i]);
            }
        }

        var name = names.TryGetValue("PAL_NAME_" + tribe, out var n) ? n : row.GetProperty("fallbackName").GetString();
        manifest.Add(new { id, tribe, name, mesh = meshPath, vertices = lod.Verts.Length, textures = texFiles });
        Log($"OK {id} ({name}): {lod.Verts.Length} vertices, {sections.Length} sections, {texFiles.Count(f => f != null)} textures from {meshPath}");
        ok++;
    }
    catch (Exception e) { Log($"FAIL {id} ({tribe}): {e.Message}"); }
}

// Palworld's own Pal Sphere icon for the item
try
{
    var icon = provider.Files.Keys.Where(k => k.Contains("/Texture/", StringComparison.OrdinalIgnoreCase) && k.Contains("PalSphere", StringComparison.OrdinalIgnoreCase) && k.EndsWith(".uasset"))
        .OrderBy(k => k.Contains("Icon", StringComparison.OrdinalIgnoreCase) ? 0 : 1).ThenBy(k => k.Length).FirstOrDefault();
    if (icon != null)
    {
        var tex = provider.LoadPackageObjects(icon[..^".uasset".Length]).OfType<UTexture2D>().First();
        File.WriteAllBytes(Path.Combine(outDir, "pal_sphere.png"), TextureEncoder.Encode(tex.Decode(256, ETexturePlatform.DesktopMobile), ETextureFormat.Png, false, out _));
        Log("OK sphere icon from " + icon);
    }
    else Log("sphere icon not found; the mod's own icon stays");
}
catch (Exception e) { Log("sphere icon skipped: " + e.Message); }

File.WriteAllText(Path.Combine(outDir, "pals.json"), JsonSerializer.Serialize(new { version = 1, pals = manifest }, new JsonSerializerOptions { WriteIndented = true }));
Log(ok == palsSheet.GetArrayLength() ? "DONE all pals" : $"DONE {ok}/{palsSheet.GetArrayLength()} pals");
return ok > 0 ? 0 : 1;

string FindMesh(string tribe)
{
    var want = $"/SK_{tribe}.uasset";
    var hit = provider.Files.Keys.FirstOrDefault(k => k.EndsWith(want, StringComparison.OrdinalIgnoreCase) && k.Contains("/Monster/", StringComparison.OrdinalIgnoreCase))
           ?? provider.Files.Keys.FirstOrDefault(k => k.EndsWith(want, StringComparison.OrdinalIgnoreCase));
    return hit?[..^".uasset".Length];
}

static UTexture2D BaseColor(UMaterialInterface mat)
{
    var p = new CMaterialParams2();
    mat.GetParams(p, EMaterialFormat.AllLayers);
    if (p.TryGetTexture2d(out var t, CMaterialParams2.Diffuse.SelectMany(x => x).Concat(new[] { "BaseColor", "Base Color", "BaseColorTexture", "Albedo", "Diffuse", "MainTex", "Color", "T_D", "Tex_D" }).ToArray()) && t is UTexture2D t2) return t2;
    if (mat is UMaterialInstanceConstant mic)
        foreach (var tp in mic.TextureParameterValues ?? Array.Empty<FTextureParameterValue>())
        {
            var nm = tp.Name ?? "";
            if (nm.Contains("Normal", StringComparison.OrdinalIgnoreCase) || nm.Contains("Mask", StringComparison.OrdinalIgnoreCase)) continue;
            if (tp.ParameterValue.TryLoad(out UTexture2D tt)) return tt;
        }
    return p.TryGetFirstTexture2d(out var f) && f is UTexture2D f2 ? f2 : null;
}
