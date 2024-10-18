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
    static HashSet<AnimInfo> animInfos;
    static HashSet<AnimInfo> animInfosToClean;
    static float lastTime;

    class AnimInfo
    {
        public string name;
        public Animation anim;
        public AnimationState state;

        public AnimInfo(string name, Animation anim, AnimationState state)
        {
            this.name = name;
            this.anim = anim;
            this.state = state;
        }
    }

    public static void Main()
    {
        animInfos = new HashSet<AnimInfo>();
        animInfosToClean = new HashSet<AnimInfo>();
        instance = Harmony.CreateAndPatchAll(typeof(DCMSyncAnm));
        new TryPatchMovieTexture(instance);
        new TryPatchSceneCapture(instance);
    }

    public static void Unload()
    {
        instance.UnpatchSelf();
        instance = null;
        animInfos.Clear();
        animInfos = null;
        animInfosToClean.Clear();
        animInfosToClean = null;
    }

    public static void CleanAnm()
    {
        foreach (var i in animInfos)
        {
            if (i.state == null || i.anim == null)
            {
                animInfosToClean.Add(i);
            }
        }
        foreach (var i in animInfosToClean)
        {
            animInfos.Remove(i);
        }
        animInfosToClean.Clear();
    }

    public static void ResetAnm()
    {
        CleanAnm();
        foreach (var i in animInfos)
        {
            var anim = i.anim;
            var state = i.state;
            if (state != null)
            {
                state.time = 0f;
                state.speed = 1f;
                state.enabled = true;
                anim.Play(i.name);
            }
        }
    }

    public static void SetAnmPlaying(bool isPlaying)
    {
        foreach (var i in animInfos)
        {
            var anim = i.anim;
            var state = i.state;
            if (state != null)
            {
                state.speed = 1f;
                state.enabled = isPlaying;
                if (!(state?.clip.isLooping ?? true) && isPlaying)
                {
                    if (state.time != 0f && state.time <= state.clip.length)
                    {
                        anim.Play(i.name);
                    }
                }
            }
        }
    }

    public static void SeekAnm(float time, bool paused = true)
    {
        foreach (var i in animInfos)
        {
            var anim = i.anim;
            var state = i.state;
            if (state != null)
            {
                if (paused && !state.enabled)
                {
                    state.enabled = true;
                    state.speed = 0f;
                }
                state.time = time;
                if (!(state?.clip.isLooping ?? true))
                {
                    if (time <= state.clip.length)
                    {
                        anim.Play(i.name);
                    }
                }
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
            animInfos.Add(new AnimInfo(f_strAnimName, __instance.m_animItem, __instance.m_animItem[f_strAnimName]));
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
        ResetAnm();
        TryPatchMovieTexture.Reset();
    }

    [HarmonyPatch(typeof(DanceCameraMotion), "StopFreeDance")]
    [HarmonyPostfix]
    public static void StopFreeDancePostfix(ref DanceCameraMotion __instance, bool isFreeStoped)
    {
        if (__instance.isFreeDance)
        {
            SetAnmPlaying(!isFreeStoped);
            TryPatchMovieTexture.SetPlaying(!isFreeStoped);
        }
    }

    [HarmonyPatch(typeof(DanceCameraMotion), "SetAnimationAndBgmTime")]
    [HarmonyPostfix]
    public static void SetFreeDanceAreaDanceTimePostfix(float bgmTime)
    {
        if (bgmTime != lastTime)
        {
            SeekAnm(bgmTime);
            TryPatchMovieTexture.Seek(bgmTime);
            lastTime = bgmTime;
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

    class TryPatchMovieTexture : TryPatch
    {
        static MethodInfo resetMethod;
        static MethodInfo playMethod;
        static MethodInfo pauseMethod;
        static MethodInfo seekMethod;
        public TryPatchMovieTexture(Harmony harmony, int failLimit = 3) : base(harmony, failLimit) {}

        public override bool Patch()
        {
            Type t = AccessTools.TypeByName("COM3D2.MovieTexture.Plugin.MovieTextureManager");
            if (t != null)
            {
                resetMethod = t.GetMethod("ResetMovie");
                playMethod = t.GetMethod("PlayMovie");
                pauseMethod = t.GetMethod("PauseMovie");
                seekMethod = t.GetMethod("SeekMovie");
                return true;
            }
            return false;
        }

        public static void Reset()
        {
            resetMethod?.Invoke(null, null);
            playMethod?.Invoke(null, null);
        }

        public static void SetPlaying(bool isPlaying)
        {
            if (isPlaying)
            {
                playMethod?.Invoke(null, null);
            }
            else
            {
                pauseMethod?.Invoke(null, null);
            }
        }

        public static void Seek(float time)
        {
            seekMethod?.Invoke(null, new object[] { time });
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
                    animInfos.Add(new AnimInfo(f_strAnimName, animItemValue, animItemValue[f_strAnimName]));
                }
                
            }
        }
    }
}
