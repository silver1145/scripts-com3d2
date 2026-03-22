// #author silver1145
// #name InfinityColor Fix
// #desc Fix InfinityColor on Alpha Channel and Add InfinityColor Mask

using UnityEngine;
using HarmonyLib;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection.Emit;

public static class InfinityColorFix
{
    static Harmony instance;
    static string shaderFile = "BepinEx/config/InfinityColor_Fix/infinitycolor_fix";
    static HashSet<string> maskedTexNames = new HashSet<string>();
    static Dictionary<InfinityColorTextureCache, Dictionary<string, Texture2D>> cacheMaskTextures = new Dictionary<InfinityColorTextureCache, Dictionary<string, Texture2D>>();

    public static void Main()
    {
        instance = Harmony.CreateAndPatchAll(typeof(InfinityColorFix));
        new TryPatchMaidLoader(instance);
        if (LoadAssetBundle())
        {
            LoadMaskTex();
        }
    }

    public static void Unload()
    {
        instance.UnpatchSelf();
        instance = null;
        GameUty.m_matSystem[(int)GameUty.SystemMaterial.InfinityColor] = null;
        GameUty.m_matSystem[(int)GameUty.SystemMaterial.TexTo8bitTex] = null;
        maskedTexNames.Clear();
        maskedTexNames = null;
        cacheMaskTextures.Clear();
        cacheMaskTextures = null;
    }

    public static bool LoadAssetBundle()
    {
        if (File.Exists(shaderFile))
        {
            try
            {
                AssetBundle assetBundle = AssetBundle.LoadFromFile(shaderFile);
                Material material1 = assetBundle.LoadAsset("InfinityColor", typeof(Material)) as Material;
                GameUty.m_matSystem[(int)GameUty.SystemMaterial.InfinityColor] = material1;
                Material material2 = assetBundle.LoadAsset("TexTo8bitTex", typeof(Material)) as Material;
                GameUty.m_matSystem[(int)GameUty.SystemMaterial.TexTo8bitTex] = material2;
                assetBundle.Unload(false);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Load infinitycolor_fix Error: " + e.ToString());
            }
        }
        else
        {
            Debug.LogWarning("infinitycolor_fix File not Exist");
        }
        return false;
    }

    public static void LoadMaskTex(bool countNum = false)
    {
        maskedTexNames.Clear();
        var Files = Directory.GetFiles(BepInEx.Paths.GameRootPath + "\\Mod", "*.*", SearchOption.AllDirectories).Where(t => t.ToLower().EndsWith(".infinity_mask.tex")).ToArray();
        for (int i = 0; i < Files.Count(); i++)
        {
            string mask_path = Files[i].ToLower();
            string mask_name = Path.GetFileName(mask_path);
            string base_name = mask_path.Substring(0, mask_path.LastIndexOf(".infinity_mask.tex")) + ".tex";
            if (File.Exists(base_name))
            {
                maskedTexNames.Add(Path.GetFileName(base_name));
            }
            else
            {
                Debug.LogWarning($"Mask without Base Texture: {mask_name}");
            }
        }
        if (countNum)
        {
            Debug.Log($"Load InfinityColor Mask Textures: {maskedTexNames.Count}");
        }
    }

    public static void PrepareMask(TBodySkin tbodySkin, Renderer renderer, int mat_no, string prop_name, Texture base_tex, MaidParts.PARTS_COLOR parts_color)
    {
        if (parts_color <= MaidParts.PARTS_COLOR.NONE || MaidParts.PARTS_COLOR.MAX <= parts_color)
        {
            return;
        }
        if (tbodySkin != null)
        {
            InfinityColorTextureCache cache = tbodySkin.TextureCache;
            string base_tex_name = base_tex.name?.ToLower();
            if (maskedTexNames.Contains(base_tex_name))
            {
                string mask_name = Path.GetFileNameWithoutExtension(base_tex_name) + ".infinity_mask.tex";
                if (GameUty.FileSystem.IsExistentFile(mask_name))
                {
                    if (!cacheMaskTextures.ContainsKey(cache))
                    {
                        cacheMaskTextures[cache] = new Dictionary<string, Texture2D>();
                    }
                    Texture2D mask_tex = ImportCM.CreateTexture(mask_name);
                    mask_tex.name = mask_name;
                    tbodySkin.listDEL.Add(mask_tex);
                    cacheMaskTextures[cache][base_tex_name] = mask_tex;
                    // TODO: Patch to implement _ToonRampMask
                    // Material material = renderer.sharedMaterials[mat_no];
                    // if (prop_name == "_MainTex" && toonRampMaskShaders.Contains(material.shader))
                    // {
                    //     material.SetTexture("_ToonRampMask", mask_tex);
                    // }
                }
            }
        }
    }

    public static Material BlendMask(Material mat, Texture base_tex, RenderTexture target_tex, InfinityColorTextureCache cache)
    {
        RenderTexture active = RenderTexture.active;
        RenderTexture.active = target_tex;
        GL.Clear(true, true, Color.clear);
        RenderTexture.active = active;
        string base_tex_name = base_tex.name?.ToLower();
        bool hasMask = false;
        if (maskedTexNames.Contains(base_tex_name))
        {
            if (cacheMaskTextures.TryGetValue(cache, out Dictionary<string, Texture2D> maskTexs) && maskTexs != null)
            {
                if (maskTexs.TryGetValue(base_tex_name, out Texture2D mask) && mask != null)
                {
                    mat.SetTexture("_Mask", mask);
                    hasMask = true;
                }
            }
        }
        if (!hasMask)
        {
            mat.SetTexture("_Mask", Texture2D.whiteTexture);
        }
        return mat;
    }

    [HarmonyPatch(typeof(RenderTextureCache), "GetTexture")]
    [HarmonyPrefix]
    public static void GetTexturePrefix(ref RenderTextureFormat tex_format)
    {
        tex_format = RenderTextureFormat.ARGB32;
    }

    // `InfinityColorTextureCache.UpdateTexture`
    /*
    Material systemMaterial = GameUty.GetSystemMaterial(GameUty.SystemMaterial.InfinityColor);
+   InfinityColorFix.BlendMask(systemMaterial, base_tex, target_tex, this);
    systemMaterial.SetTexture("_MultiColTex", this.maid_.Parts.GetPartsColorTableTex(parts_color));
    */
    [HarmonyPatch(typeof(InfinityColorTextureCache), "UpdateTexture", new[] { typeof(Texture), typeof(MaidParts.PARTS_COLOR), typeof(RenderTexture) })]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> UpdateTextureTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        CodeMatcher codeMatcher = new CodeMatcher(instructions)
            .MatchForward(false, new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(GameUty), "GetSystemMaterial")))
            .Advance(3)
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_1))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_3))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(InfinityColorFix), nameof(BlendMask))));
        return codeMatcher.InstructionEnumeration();
    }

    [HarmonyPatch(typeof(InfinityColorTextureCache), "RemoveTexture", new Type[] {})]
    [HarmonyPostfix]
    public static void RemoveTexturePostfix(InfinityColorTextureCache __instance)
    {
        if (cacheMaskTextures.TryGetValue(__instance, out Dictionary<string, Texture2D> maskTexs) && maskTexs != null)
        {
            maskTexs.Clear();
        }
    }

    // TODO: Remove after implement _ToonRampMask
    [HarmonyPatch(typeof(TBody), "ChangeTex")]
    [HarmonyPostfix]
    public static void ChangeTexPostfix(ref TBody __instance, string slotname, int matno, string prop_name, string filename)
    {
        if (prop_name != "_MainTex" && prop_name != "_ToonRamp")
        {
            return;
        }
        if (prop_name == "_ToonRamp")
        {
            int num = (int)TBody.hashSlotName[slotname];
            TBodySkin tbodySkin = __instance.goSlot[num];
            if (tbodySkin.obj != null)
            {
                SkinnedMeshRenderer componentInChildren = tbodySkin.obj.GetComponentInChildren<SkinnedMeshRenderer>();
                if (componentInChildren != null && componentInChildren.sharedMaterials.Length > matno)
                {
                    Material m = componentInChildren.sharedMaterials[matno];
                    if (m != null)
                    {
                        if (maskedTexNames.Contains(m.GetTexture("_MainTex").name?.ToLower()))
                        {
                            Texture2D tex2d = null;
                            for (int i = tbodySkin.listDEL.Count - 1; i >= 0; i--)
                            {
                                var toDel = tbodySkin.listDEL[i];
                                if (toDel is Texture2D toDelTex && toDelTex != null && toDelTex.name == filename)
                                {
                                    tex2d = toDelTex;
                                    break;
                                }
                            }
                            if (tex2d == null && GameUty.FileSystem.IsExistentFile(filename))
                            {
                                tex2d = ImportCM.CreateTexture(filename);
                                tbodySkin.listDEL.Add(tex2d);
                            }
                            if (tex2d != null)
                            {
                                m.SetTexture(prop_name, tex2d);
                            }
                        }
                    }
                }
            }
        }
    }

    // `TBody.ChangeTex`
    /*
+   InfinityColorFix.PrepareMask(tbodySkin, renderer, matno, prop_name, texture, f_ePartsColorId);
    tbodySkin.TextureCache.AddTexture(matno, prop_name, texture, f_ePartsColorId);
    */
    [HarmonyPatch(typeof(TBody), "ChangeTex")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> ChangeTexTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var locals = Traverse.Create(Traverse.Create(generator).Field("Target").GetValue<MonoMod.Utils.Cil.CecilILGenerator>()).Field("_Variables").GetValue<Dictionary<LocalBuilder, Mono.Cecil.Cil.VariableDefinition>>().Keys.ToArray();
        LocalBuilder moveToDep = generator.DeclareLocal(typeof(bool));
        object renderer = null;
        foreach (LocalBuilder local in locals)
        {
            if (local.LocalType == typeof(Renderer))
            {
                renderer = local;
                break;
            }
        }
        if (renderer == null)
        {
            throw new Exception("Local Variables not Found");
        }
        CodeMatcher codeMatcher = new CodeMatcher(instructions).MatchForward(false, new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(InfinityColorTextureCache), "AddTexture", new[] { typeof(int), typeof(string), typeof(Texture), typeof(MaidParts.PARTS_COLOR) })));
        codeMatcher.InsertAndAdvance(codeMatcher.InstructionAt(-6))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldloc_S, renderer))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_2))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_3))
            .InsertAndAdvance(codeMatcher.InstructionsWithOffsets(-6, -5))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(InfinityColorFix), nameof(PrepareMask))));
        return codeMatcher.InstructionEnumeration();
    }

    abstract class TryPatch
    {
        public static List<TryPatch> tryPatches = new List<TryPatch>();
        private bool patched = false;
        private int failCount = 0;
        private int failLimit;
        public Harmony harmony;

        public TryPatch(Harmony harmony, int failLimit = 3)
        {
            this.harmony = harmony;
            this.failLimit = failLimit;
            tryPatches.Add(this);
            DoPatch();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += SceneLoaded;
        }

        void DoPatch()
        {
            try
            {
                patched = Patch();
            }
            catch {}
            finally
            {
                if (patched || (failLimit > 0 && ++failCount >= failLimit))
                {
                    RemovePatch();
                }
            }
        }

        void SceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode)
        {
            if (!patched)
            {
                DoPatch();
            }
        }

        void RemovePatch()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= SceneLoaded;
            tryPatches.Remove(this);
        }

        public abstract bool Patch();
    }

    class TryPatchMaidLoader : TryPatch
    {
        public TryPatchMaidLoader(Harmony harmony, int failLimit = 3) : base(harmony, failLimit) { }

        public override bool Patch()
        {
            var mOriginal = AccessTools.Method(AccessTools.TypeByName("COM3D2.MaidLoader.RefreshMod"), "RefreshCo");
            harmony.Patch(mOriginal, prefix: new HarmonyMethod(AccessTools.Method(typeof(TryPatchMaidLoader), nameof(RefreshCoPrefix))));
            return true;
        }

        public static void RefreshCoPrefix()
        {
            LoadMaskTex(true);
        }
    }
}
