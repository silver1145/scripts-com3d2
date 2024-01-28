// #author silver1145
// #name DCM Sync Anm
// #desc DCM Sync With Item Anm and MovieTexture
// #ref ${BepInExRoot}/../Sybaris/UnityInjector/COM3D2.DanceCameraMotion.Plugin.dll

using UnityEngine;
using HarmonyLib;
using System;
using System.Reflection;
using System.Collections.Generic;
using COM3D2.DanceCameraMotion.Plugin;

public static class DCMSyncAnm
{
    static Harmony instance;
    static KeyCode PLAY_KEYCODE = KeyCode.BackQuote;
    static HashSet<AnimationState> anmStates;
    static HashSet<AnimationState> anmStatesToClean;

    public static void Main()
    {
        anmStates = new HashSet<AnimationState>();
        anmStatesToClean = new HashSet<AnimationState>();
        instance = Harmony.CreateAndPatchAll(typeof(DCMSyncAnm));
        new TryPatchMovieTexture(instance);
        new TryPatchSceneCapture(instance);
    }

    public static void Unload()
    {
        instance.UnpatchAll(instance.Id);
        instance = null;
        anmStates.Clear();
        anmStates = null;
        anmStatesToClean.Clear();
        anmStatesToClean = null;
    }

    public static void CleanAnm()
    {
        foreach (var anms in anmStates)
        {
            if (anms == null)
            {
                anmStatesToClean.Add(anms);
            }
        }
        foreach (var anms in anmStatesToClean)
        {
            anmStates.Remove(anms);
        }
        anmStatesToClean.Clear();
    }

    public static void ResetAnm()
    {
        CleanAnm();
        foreach (var anms in anmStates)
        {
            if (anms != null)
            {
                anms.time = 0f;
            }
        }
    }

    [HarmonyPatch(typeof(TBodySkin), "AnimationPlay")]
    [HarmonyPostfix]
    public static void AnimationPlayPostfix(ref TBodySkin __instance, ref bool __result, string f_strAnimName)
    {
        if (__result)
        {
            CleanAnm();
            anmStates.Add(__instance.m_animItem[f_strAnimName]);
        }
    }

    [HarmonyPatch(typeof(DanceCameraMotion), "Update")]
    [HarmonyPrefix]
    public static void UpdatePrefix(ref DanceCameraMotion __instance)
    {
        if (Input.GetKeyDown(PLAY_KEYCODE))
        {
            __instance.StartOrStopDance();
        }
    }

    [HarmonyPatch(typeof(DanceCameraMotion), "StartOrStopDance")]
    [HarmonyPostfix]
    public static void StartOrStopDancePostfix(ref DanceCameraMotion __instance)
    {
        if (__instance.isFreeDance)
        {
            ResetAnm();
            TryPatchMovieTexture.Reset();
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

    class TryPatchMovieTexture : TryPatch
    {
        static MethodInfo resetMethod;
        public TryPatchMovieTexture(Harmony harmony, int failLimit = 3) : base(harmony, failLimit) {}

        public override bool Patch()
        {
            Type t = AccessTools.TypeByName("COM3D2.MovieTexture.Plugin.MovieTextureManager");
            if (t != null)
            {
                resetMethod = t.GetMethod("ResetMovie");
                return true;
            }
            return false;
        }

        public static void Reset()
        {
            resetMethod?.Invoke(null, null);
        }
    }

    class TryPatchSceneCapture : TryPatch
    {
        public TryPatchSceneCapture(Harmony harmony, int failLimit = 3) : base(harmony, failLimit) {}

        public override bool Patch()
        {
            Type typeItemAnimation = AccessTools.TypeByName("CM3D2.SceneCapture.Plugin.ItemAnimation");
            var mOriginal = AccessTools.Method(typeItemAnimation, "AnimationPlay");
            MethodInfo mPostfix = AccessTools.Method(typeof(TryPatchSceneCapture), nameof(AnimationPlayPostfix));
            harmony.Patch(mOriginal, postfix: new HarmonyMethod(mPostfix));
            return true;
        }

        public static void AnimationPlayPostfix(ref object __instance, string f_strAnimName, ref bool __result)
        {
            if (__result)
            {
                CleanAnm();
                Type objectType = __instance.GetType();
                FieldInfo fieldInfo = objectType.GetField("m_animItem", BindingFlags.Instance | BindingFlags.NonPublic);
                if (fieldInfo != null)
                {
                    Animation animItemValue = (Animation)fieldInfo.GetValue(__instance);
                    anmStates.Add(animItemValue[f_strAnimName]);
                }
                
            }
        }
    }
}
