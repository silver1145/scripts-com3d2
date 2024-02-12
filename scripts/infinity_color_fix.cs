// #author silver1145
// #name InfinityColor Fix
// #desc Fix InfinityColor on Alpha Channel and Add InfinityColor Mask

using UnityEngine;
using HarmonyLib;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

public static class InfinityColorFix
{
    static Harmony instance;
    static AssetBundle assetBundle;
    static string shaderFile = "BepinEx/config/InfinityColor_Fix/infinitycolor_fix";
    static Dictionary<string, Texture2D> baseTexDict;
    static Dictionary<string, Texture2D> maskTexDict;
    static HashSet<Material> materialToMask;
    static Material maskMaterial;

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
        instance.UnpatchAll(instance.Id);
        instance = null;
        // GameUty.m_matSystem[(int)GameUty.SystemMaterial.InfinityColor] = null;
        GameUty.m_matSystem[(int)GameUty.SystemMaterial.TexTo8bitTex] = null;
        assetBundle.Unload(false);
        assetBundle = null;
        Object.Destroy(maskMaterial);
        baseTexDict.Clear();
        baseTexDict = null;
        maskTexDict.Clear();
        maskTexDict = null;
        materialToMask.Clear();
        materialToMask = null;
    }

    public static bool LoadAssetBundle()
    {
        if (File.Exists(shaderFile))
        {
            try
            {
                assetBundle = AssetBundle.LoadFromFile(shaderFile);
                // Material material1 = assetBundle.LoadAsset("InfinityColor", typeof(Material)) as Material;
                // GameUty.m_matSystem[(int)GameUty.SystemMaterial.InfinityColor] = material1;
                Material material2 = assetBundle.LoadAsset("TexTo8bitTex", typeof(Material)) as Material;
                GameUty.m_matSystem[(int)GameUty.SystemMaterial.TexTo8bitTex] = material2;
                maskMaterial = assetBundle.LoadAsset("InfinityColorMask", typeof(Material)) as Material;
                return true;
            }
            catch(Exception e)
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
        if (baseTexDict == null)
        {
            baseTexDict = new Dictionary<string, Texture2D>();
        }
        if (maskTexDict == null)
        {
            maskTexDict = new Dictionary<string, Texture2D>();
        }
        if (materialToMask == null)
        {
            materialToMask = new HashSet<Material>();
        }
        baseTexDict.Clear();
        maskTexDict.Clear();
        materialToMask.Clear();
        
        var Files = Directory.GetFiles(BepInEx.Paths.GameRootPath + "\\Mod", "*.*", SearchOption.AllDirectories).Where(t => t.ToLower().EndsWith(".infinity_mask.tex")).ToArray();
        for (int i = 0; i < Files.Count(); i++)
        {
            string mask_path = Files[i].ToLower();
            string mask_name = Path.GetFileName(mask_path);
            string base_name = mask_name.Substring(0, mask_name.LastIndexOf(".infinity_mask.tex")) + ".tex";
            if (File.Exists(mask_path.Substring(0, mask_path.LastIndexOf(".infinity_mask.tex")) + ".tex"))
            {
                baseTexDict[base_name] = new Texture2D(2, 2);
                maskTexDict[base_name] = new Texture2D(2, 2);
            }
            else
            {
                Debug.LogWarning($"Mask without Base Texture: {mask_name}");
            }
        }
        if (countNum)
        {
            Debug.Log($"Load InfinityColor Mask Textures: {baseTexDict.Count}");
        }
    }

    [HarmonyPatch(typeof(RenderTextureCache), "GetTexture")]
    [HarmonyPrefix]
    public static void GetTexturePrefix(ref RenderTextureFormat tex_format)
    {
        tex_format = RenderTextureFormat.ARGB32;
    }

    [HarmonyPatch(typeof(InfinityColorTextureCache), "UpdateTexture", new[] {typeof(Texture), typeof(MaidParts.PARTS_COLOR), typeof(RenderTexture)})]
    [HarmonyPrefix]
    public static void UpdateTexturePrefix(Texture base_tex, RenderTexture target_tex)
    {
        if (base_tex == null || target_tex == null)
        {
            return;
        }
        RenderTexture active = RenderTexture.active;
        RenderTexture.active = target_tex;
        GL.Clear(true, true, Color.clear);
        RenderTexture.active = active;
    }

    [HarmonyPatch(typeof(InfinityColorTextureCache), "UpdateTexture", new[] {typeof(Texture), typeof(MaidParts.PARTS_COLOR), typeof(RenderTexture)})]
    [HarmonyPostfix]
    public static void UpdateTexturePostfix(Texture base_tex, RenderTexture target_tex)
    {
        if (base_tex == null || target_tex == null || baseTexDict == null || maskTexDict == null)
        {
            return;
        }
        string base_name = base_tex.name.ToLower();
        if (baseTexDict.TryGetValue(base_name, out Texture2D _base_tex) && maskTexDict.TryGetValue(base_name, out Texture2D mask_tex))
        {
            if (_base_tex.width == 2)
            {
                if (!GameUty.FileSystem.IsExistentFile(base_name))
                {
                    Debug.LogWarning($"InfinityColor Base Texture not Found: {base_name}");
                    return;
                }
                Texture2D tex1 = ImportCM.CreateTexture(base_name);
                Object.Destroy(baseTexDict[base_name]);
                baseTexDict[base_name] = tex1;
                _base_tex = tex1;
            }
            if (mask_tex.width == 2)
            {
                string mask_name = Path.GetFileNameWithoutExtension(base_name) + ".infinity_mask.tex";
                if (!GameUty.FileSystem.IsExistentFile(mask_name))
                {
                    Debug.LogWarning($"InfinityColor Mask Texture not Found: {mask_name}");
                    return;
                }
                Texture2D tex2 = ImportCM.CreateTexture(mask_name);
                Object.Destroy(maskTexDict[base_name]);
                maskTexDict[base_name] = tex2;
                mask_tex = tex2;
            }
            RenderTexture active = RenderTexture.active;
            maskMaterial.SetTexture("_Mask", mask_tex);
            Graphics.Blit(_base_tex, target_tex, maskMaterial);
            RenderTexture.active = active;
        }
    }

    [HarmonyPatch(typeof(TBody), "ChangeTex")]
    [HarmonyPostfix]
    public static void ChangeTexPostfix(ref TBody __instance, string slotname, int matno, string prop_name)
    {
        int num = (int)TBody.hashSlotName[slotname];
        TBodySkin tbodySkin = __instance.goSlot[num];

        if (tbodySkin.obj != null)
        {
            SkinnedMeshRenderer componentInChildren = tbodySkin.obj.GetComponentInChildren<SkinnedMeshRenderer>();
            if (componentInChildren != null)
            {
                foreach (var m in componentInChildren.sharedMaterials)
                {
                    Texture tex = m.GetTexture(prop_name);
                    if (tex != null)
                    {
                        if (baseTexDict.ContainsKey(tex.name))
                        {
                            materialToMask.Add(m);
                            return;
                        }
                        if (materialToMask.Contains(m) && prop_name == "_ToonRamp" && GameUty.FileSystem.IsExistentFile(tex.name))
                        {
                            Texture2D tex2d = ImportCM.CreateTexture(tex.name);
                            m.SetTexture(prop_name, tex2d);
                        }
                    }
                }
            }
        }
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
        public TryPatchMaidLoader(Harmony harmony, int failLimit = 3) : base(harmony, failLimit) {}

        public override bool Patch()
        {
            var mOriginal = AccessTools.Method(AccessTools.TypeByName("COM3D2.MaidLoader.RefreshMod"), "RefreshCo");
            var mPrefix = SymbolExtensions.GetMethodInfo(() => RefreshCoPrefix());
            harmony.Patch(mOriginal, prefix: new HarmonyMethod(mPrefix));
            return true;
        }

        public static void RefreshCoPrefix()
        {
            LoadMaskTex(true);
        }
    }
}
