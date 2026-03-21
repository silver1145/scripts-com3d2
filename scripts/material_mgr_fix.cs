// #author silver1145
// #name Material Mgr Fix
// #desc Material Mgr Fix for FixSkinMaskCutout

using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

public static class MaterialMgrFix
{
    static Harmony instance;

    public static void Main()
    {
        instance = Harmony.CreateAndPatchAll(typeof(MaterialMgrFix));
        new TryPatchMaterialMgr(instance);
    }

    public static void Unload()
    {
        instance.UnpatchSelf();
        instance = null;
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

    class TryPatchMaterialMgr : TryPatch
    {
        public TryPatchMaterialMgr(Harmony harmony, int failLimit = 1) : base(harmony, failLimit) {}

        public override bool Patch()
        {
            var materialMgr = AccessTools.TypeByName("MaterialMgr");
            if (materialMgr == null)
            {
                return false;
            }
            var mOriginal = AccessTools.Method(materialMgr, "FixSkinMaskCutout");
            harmony.Patch(mOriginal, prefix: new HarmonyMethod(AccessTools.Method(typeof(TryPatchMaterialMgr), nameof(FixSkinMaskCutoutPrefix))));
            return true;
        }

        public static void FixSkinMaskCutoutPrefix(object __instance)
        {
            var type = __instance.GetType();
            TBodySkin m_tbSkin = (AccessTools.Field(type, "m_tbSkin")?.GetValue(__instance)) as TBodySkin;
            if (m_tbSkin == null) return;
            if (m_tbSkin.SlotId != TBody.SlotID.body)
            {
                return;
            }

            var materialsField = AccessTools.Field(type, "m_materials");
            var materials = materialsField?.GetValue(__instance) as UnityEngine.Material[];
            if (materials == null || materials.Length == 0 || materials[0] == null)
            {
                foreach (Transform transform in m_tbSkin.obj.transform.GetComponentsInChildren<Transform>(true))
                {
                    Renderer render = transform.GetComponent<Renderer>();
                    if (render != null && render.material != null)
                    {
                        materialsField.SetValue(__instance, render.materials);
                        return;
                    }
                }
            }
        }
    }
}
