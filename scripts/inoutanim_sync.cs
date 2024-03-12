// #author silver1145
// #name Syasei Sync With Inoutanim
// #desc Vym Syasei Sync With Inoutanim
// #ref ${BepInExRoot}/../Sybaris/UnityInjector/COM3D2.InOutAnimation.Plugin.dll

using UnityEngine;
using HarmonyLib;
using System.Collections.Generic;
using COM3D2.InOutAnimation.Plugin;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;
using System.Reflection.Emit;

public static class InoutAnimSync
{

    static Harmony instance;
    static float shotReadyTime = 0f;
    static float rate;
    // Config
    static public bool bindConfig = false;
    static public ConfigFile configFile = new ConfigFile(Path.Combine(BepInEx.Paths.ConfigPath, "InoutAnimSync.cfg"), false);
    static public ConfigEntry<bool> _enableEmissionAnim = configFile.Bind("InoutAnimSync Setting", "EnableEmissionAnim", false, "Enable Emission Anim");
    static public ConfigEntry<float> _emissionIntensityStart = configFile.Bind("InoutAnimSync Setting", "IntensityStart", 0f, "Intensity Start");
    static public ConfigEntry<float> _emissionIntensityEnd = configFile.Bind("InoutAnimSync Setting", "IntensityEnd", 0f, "Intensity End");
    static public ConfigEntry<int> _smoothWindowSize = configFile.Bind("InoutAnimSync Setting", "SmoothWindowSize", 0, "Smooth Window Size");
    static public ConfigEntry<bool> _enableAheAnim = configFile.Bind("InoutAnimSync Setting", "EnableAheAnim", false, "Enable Ahe Anim");
    // Emission Anim
    static int smoothIndex;
    static float[] smoothBuffer = new float[100];
    static bool enableEmissionAnim = _enableEmissionAnim.Value;
    static int smoothWindowSizeValue = Math.Min(_smoothWindowSize.Value, 100);
    static float emissionIntensityStart = _emissionIntensityStart.Value;
    static float emissionIntensityEnd = _emissionIntensityEnd.Value;
    static int smoothWindowSize { get { return smoothWindowSizeValue; } set { smoothWindowSizeValue = Math.Min(value, 100); } }
    // Ahe Anim
    static bool enableAheAnim = _enableAheAnim.Value;
    static Maid curMaid;

    public static void Main()
    {
        instance = Harmony.CreateAndPatchAll(typeof(InoutAnimSync));
        new TryPatchSceneCapture(instance);
        new TryPatchVibeYourMaid(instance);
        new TryPatchVYMEnhance(instance);
        _enableEmissionAnim.SettingChanged += (s, e) => enableEmissionAnim = _enableEmissionAnim.Value;
        _emissionIntensityStart.SettingChanged += (s, e) => emissionIntensityStart = _emissionIntensityStart.Value;
        _emissionIntensityEnd.SettingChanged += (s, e) => emissionIntensityEnd = _emissionIntensityEnd.Value;
        _smoothWindowSize.SettingChanged += (s, e) => smoothWindowSize = _smoothWindowSize.Value;
        _enableAheAnim.SettingChanged += (s, e) => { enableAheAnim = _enableAheAnim.Value; TryPatchVYMEnhance.UpdateAheAnimState(); };
    }

    public static void Unload()
    {
        instance.UnpatchAll(instance.Id);
        instance = null;
    }

    static float SmoothValue(float inputValue)
    {
        smoothBuffer[smoothIndex] = inputValue;
        float sum = 0;
        for (int i = 0; i < smoothWindowSize; i++)
        {
            sum += smoothBuffer[i];
        }
        float smoothedValue = sum / smoothWindowSize;
        smoothIndex = (smoothIndex + 1) % smoothWindowSize;
        return smoothedValue;
    }

    public static bool IsShootReady()
    {
        return Time.time - shotReadyTime < 10.0f;
    }

    [HarmonyPatch(typeof(InOutAnimation.State), "shotReady", MethodType.Getter)]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> shotReadyGetterTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        CodeMatcher codeMatcher = new CodeMatcher(instructions);
        codeMatcher
            .MatchForward(false, new CodeMatch(OpCodes.Ret))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(InoutAnimSync), nameof(IsShootReady))))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Or));
        return codeMatcher.InstructionEnumeration();
    }

    [HarmonyPatch(typeof(InOutAnimation), "IsShooting")]
    [HarmonyTranspiler]
    static IEnumerable<CodeInstruction> IsShootingTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        CodeMatcher codeMatcher = new CodeMatcher(instructions);
        codeMatcher.MatchForward(false, new CodeMatch(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(UnityEngine.Object), "name")))
            .Advance(1)
            .InsertAndAdvance(new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(string), "ToUpper", new Type[] { })));
        return codeMatcher.InstructionEnumeration();
    }

    // Emission Anim
    [HarmonyPatch(typeof(InOutAnimation.Mediator), "UpdateValues")]
    [HarmonyPostfix]
    static void UpdateValuesPostfix(ref InOutAnimation.Mediator __instance, ref InOutAnimation.State current)
    {
        if (enableEmissionAnim && TryPatchSceneCapture.valid)
        {
            foreach (float r in current.rates.rate)
            {
                if (r >= 0.01f)
                {
                    rate = r;
                    break;
                }
            }
            TryPatchSceneCapture.UpdateRate();
            TryPatchVYMEnhance.UpdateRate();
            if (__instance.TargetMaid != curMaid && TryPatchVYMEnhance.SetMaid(__instance.TargetMaid))
            {
                curMaid = __instance.TargetMaid;
            }
        }
    }

    // Config
    [HarmonyPatch(typeof(InOutAnimation.Controller), MethodType.Constructor, new[] { typeof(InOutAnimation) })]
    [HarmonyPostfix]
    static void ControllerConstructorPostfix(ref InOutAnimation.Controller __instance)
    {
        __instance.containers.Add("emissionanim", new InOutAnimation.Controller.Container("EmissionAnim"));
        __instance.containers["emissionanim"].Add(new InOutAnimation.Controller.ToggleButton("有効", enableEmissionAnim, delegate (bool b)
        {
            _enableEmissionAnim.Value = b;
        }));
        __instance.containers["emissionanim"].Add(new InOutAnimation.Controller.LabelSlider("Intensity Start", emissionIntensityStart, 0f, 30f, 0f, delegate (float f)
        {
            _emissionIntensityStart.Value = f;
        }, false));
        __instance.containers["emissionanim"].Add(new InOutAnimation.Controller.LabelSlider("Intensity End", emissionIntensityEnd, 0f, 30f, 0f, delegate (float f)
        {
            _emissionIntensityEnd.Value = f;
        }, false));
        __instance.containers["emissionanim"].Add(new InOutAnimation.Controller.LabelSlider("Smooth WinSize", (float)smoothWindowSize, 0f, 10f, 0f, delegate (float f)
        {
            _smoothWindowSize.Value = (int)f;
        }, true));
        __instance.containers.Add("aheanim", new InOutAnimation.Controller.Container("AheEyeAnim"));
        __instance.containers["aheanim"].Add(new InOutAnimation.Controller.ToggleButton("有効", enableAheAnim, delegate (bool b)
        {
            _enableAheAnim.Value = b;
        }));
        bindConfig = true;
    }

    [HarmonyPatch(typeof(InOutAnimation), "CheckInput")]
    [HarmonyPostfix]
    static void CheckInputPostfix(ref InOutAnimation __instance)
    {
        if (!bindConfig && __instance.controller.containers.ContainsKey("emissionanim"))
        {
            var contents = __instance.controller.containers["emissionanim"].contents;
            contents[0] = new InOutAnimation.Controller.ToggleButton("有効", enableEmissionAnim, delegate (bool b)
            {
                _enableEmissionAnim.Value = b;
            });
            contents[1] = new InOutAnimation.Controller.LabelSlider("Intensity Start", emissionIntensityStart, 0f, 30f, 0f, delegate (float f)
            {
                _emissionIntensityStart.Value = f;
            }, false);
            contents[2] = new InOutAnimation.Controller.LabelSlider("Intensity End", emissionIntensityEnd, 0f, 30f, 0f, delegate (float f)
            {
                _emissionIntensityEnd.Value = f;
            }, false);
            contents[3] = new InOutAnimation.Controller.LabelSlider("Smooth WinSize", (float)smoothWindowSize, 0f, 10f, 0f, delegate (float f)
            {
                _smoothWindowSize.Value = (int)f;
            }, true);
            contents = __instance.controller.containers["aheanim"].contents;
            contents[0] = new InOutAnimation.Controller.ToggleButton("有効", enableAheAnim, delegate (bool b)
            {
                _enableAheAnim.Value = b;
            });
            bindConfig = true;
        }
        TryPatchVYMEnhance.UpdateAheAnimState();
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
            catch { }
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

    class TryPatchSceneCapture : TryPatch
    {
        public static bool valid = false;
        public static Type cinematicBloomLayerDef;
        public static FieldInfo fieldCinematicBloomLayer;
        public static FieldInfo fieldEmissionIntensity;
        public TryPatchSceneCapture(Harmony harmony, int failLimit = 3) : base(harmony, failLimit) { }

        public override bool Patch()
        {
            Type _cinematicBloomLayer = AccessTools.TypeByName("CM3D2.SceneCapture.Plugin.CinematicBloomLayer");
            Type _cinematicBloomLayerDef = AccessTools.TypeByName("CM3D2.SceneCapture.Plugin.CinematicBloomLayerDef");
            if (_cinematicBloomLayer == null || _cinematicBloomLayerDef == null)
            {
                return false;
            }
            fieldCinematicBloomLayer = AccessTools.Field(_cinematicBloomLayerDef, "cinematicBloomEffect");
            fieldEmissionIntensity = AccessTools.Field(_cinematicBloomLayer, "_intensity");
            cinematicBloomLayerDef = _cinematicBloomLayerDef;
            valid = true;
            return true;
        }

        public static float? GetEmissionIntensity()
        {
            object layer = fieldCinematicBloomLayer.GetValue(null);
            if (layer != null)
            {
                object intensity = fieldEmissionIntensity.GetValue(layer);
                if (intensity != null)
                {
                    return (float)intensity;
                }
            }
            return null;
        }

        public static bool SetEmissionIntensity(float value)
        {
            object layer = fieldCinematicBloomLayer.GetValue(null);
            if (layer != null)
            {
                fieldEmissionIntensity.SetValue(layer, value);
                return true;
            }
            return false;
        }

        public static void UpdateRate()
        {
            if (smoothWindowSize > 0)
            {
                SetEmissionIntensity(Mathf.Lerp(emissionIntensityStart, emissionIntensityEnd, SmoothValue(rate)));
            }
            else
            {
                SetEmissionIntensity(Mathf.Lerp(emissionIntensityStart, emissionIntensityEnd, rate));
            }
        }
    }

    class TryPatchVibeYourMaid : TryPatch
    {
        public TryPatchVibeYourMaid(Harmony harmony, int failLimit = 3) : base(harmony, failLimit) { }

        public override bool Patch()
        {
            Type vibeYourMaid = AccessTools.TypeByName("CM3D2.VibeYourMaid.Plugin.VibeYourMaid");
            if (vibeYourMaid == null)
            {
                return false;
            }
            var manMotionChange = AccessTools.Method(vibeYourMaid, "ManMotionChange", new [] { typeof(string), typeof(int), typeof(bool), typeof(bool), typeof(bool), typeof(float), typeof(float)});
            var manMotionChangePostfix = AccessTools.Method(typeof(TryPatchVibeYourMaid), "ManMotionChangePostfix");
            harmony.Patch(manMotionChange, postfix: new HarmonyMethod(manMotionChangePostfix));
            return true;
        }

        public static void ManMotionChangePostfix(string motion)
        {
            if (motion.Contains("_shasei"))
            {
                shotReadyTime = Time.time;
            }
        }
    }

    class TryPatchVYMEnhance : TryPatch
    {
        static FieldInfo externalSet;
        static FieldInfo currentMaid;
        static FieldInfo currentValue;
        public TryPatchVYMEnhance(Harmony harmony, int failLimit = 3) : base(harmony, failLimit) { }

        public override bool Patch()
        {
            Type vymEnhance = null;
            foreach (var t in AppDomain.CurrentDomain.GetAssemblies().Where(a => a.FullName.StartsWith("Microsoft.VisualStudio") is false).SelectMany(a => a.GetTypes()).Reverse())
            {
                if (t?.FullName == "VYM_Enhance")
                {
                    vymEnhance = t;
                    break;
                }
            }
            if (vymEnhance == null)
            {
                return false;
            }
            var _externalSet = AccessTools.Field(vymEnhance, "externalSet");
            var _currentMaid = AccessTools.Field(vymEnhance, "currentMaid");
            var _currentValue = AccessTools.Field(vymEnhance, "currentValue");
            if (_externalSet == null || _currentMaid == null || _currentValue == null)
            {
                return false;
            }
            externalSet = _externalSet;
            currentMaid = _currentMaid;
            currentValue = _currentValue;
            return true;
        }

        public static void UpdateRate()
        {
            currentValue?.SetValue(null, rate);
        }

        public static void UpdateAheAnimState()
        {
            externalSet?.SetValue(null, enableAheAnim);
        }

        public static bool SetMaid(Maid maid)
        {
            if (currentMaid != null)
            {
                currentMaid.SetValue(null, maid);
                return true;
            }
            return false;
        }
    }
}