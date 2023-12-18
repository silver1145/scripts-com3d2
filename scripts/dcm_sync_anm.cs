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
            MovieTextureReset.Reset();
        }
    }
}

public static class MovieTextureReset {
    static MethodInfo resetMethod;
    static bool hasGet = false;

    public static void Main()
    {
        GetMethod();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += SceneLoaded;
    }

    public static void Unload()
    {
        resetMethod = null;
    }

    public static void GetMethod()
    {
        Type t = Type.GetType("COM3D2.MovieTexture.MovieTextureManager, COM3D2.MovieTexture");
        if (t != null)
        {
            resetMethod = t.GetMethod("ResetMovie");
            hasGet = true;
        }
    }

    private static void SceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode)
    {
        if (!hasGet)
        {
            hasGet = true;
            GetMethod();
        }
    }

    public static void Reset()
    {
        resetMethod?.Invoke(null, null);
    }
}

public static class SceneCapturePatch {
    static Harmony instance;
    static bool patched = false;

    public static void Main()
    {
        DoPatch();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += SceneLoaded;
    }

    public static void Unload()
    {
        instance.UnpatchAll(instance.Id);
        instance = null;
    }

    public static void DoPatch()
    {
        try
        {
            instance = new Harmony("SceneCapturePatch");
            Type typeItemAnimation = Type.GetType("CM3D2.SceneCapture.Plugin.ItemAnimation, COM3D2.SceneCapture.Plugin");
            var mOriginal = AccessTools.Method(typeItemAnimation, "AnimationPlay");
            MethodInfo mPostfix = typeof(SceneCapturePatch).GetMethod("AnimationPlayPostfix");
            instance.Patch(mOriginal, postfix: new HarmonyMethod(mPostfix));
            patched = true;
        }
        finally
        {
        }
    }

    private static void SceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode)
    {
        if (!patched)
        {
            patched = true;
            DoPatch();
        }
    }

    public static void AnimationPlayPostfix(ref object __instance, string f_strAnimName, ref bool __result)
    {
        if (__result)
        {
            DCMSyncAnm.CleanAnm();
            Type objectType = __instance.GetType();
            FieldInfo fieldInfo = objectType.GetField("m_animItem", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fieldInfo != null)
            {
                Animation animItemValue = (Animation)fieldInfo.GetValue(__instance);
                DCMSyncAnm.anmStates.Add(animItemValue[f_strAnimName]);
            }
            
        }
    }
}
