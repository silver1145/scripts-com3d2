// #author silver1145
// #name InoutAnimSync
// #desc InoutAnim Sync
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
    static readonly int BUFFERSIZE = 100;
    static float shotReadyTime = 0f;
    static float rate;
    static float originRate;
    static float smoothRate;
    static float relativeRate;
    static int rateBufferIndex = 0;
    static float[] rateBuffer = new float[BUFFERSIZE];
    static LinkedList<float> rateMinQueue = new LinkedList<float>();
    static LinkedList<float> rateMaxQueue = new LinkedList<float>();
    static float rateMin = 0f;
    static float rateMax = 0f;
    static float relativeRateMin = 0f;
    static float relativeRateMax = 0f;
    static float relativeRateFactor = 1f;
    // Config
    static public bool bindConfig = false;
    static public ConfigFile configFile = new ConfigFile(Path.Combine(BepInEx.Paths.ConfigPath, "InoutAnimSync.cfg"), false);
    static public ConfigEntry<bool> _enableEmissionAnim = configFile.Bind("InoutAnimSync Setting", "EnableEmissionAnim", false, "Enable Emission Anim");
    static public ConfigEntry<float> _emissionIntensityStart = configFile.Bind("InoutAnimSync Setting", "IntensityStart", 0f, "Intensity Start");
    static public ConfigEntry<float> _emissionIntensityEnd = configFile.Bind("InoutAnimSync Setting", "IntensityEnd", 0f, "Intensity End");
    static public ConfigEntry<float> _emissionThresholdStart = configFile.Bind("InoutAnimSync Setting", "ThresholdStart", 0f, "Threshold Start");
    static public ConfigEntry<float> _emissionThresholdEnd = configFile.Bind("InoutAnimSync Setting", "ThresholdEnd", 0f, "Threshold End");
    static public ConfigEntry<int> _smoothWindowSize = configFile.Bind("InoutAnimSync Setting", "SmoothWindowSize", 0, "Smooth Window Size");
    static public ConfigEntry<bool> _useRelativeInterval = configFile.Bind("InoutAnimSync Setting", "UseRelativeInterval", false, "Use Relative Interval");
    static public ConfigEntry<float> _relativeExponent = configFile.Bind("InoutAnimSync Setting", "RelativeExponent", 0.3f, "Relative Exponent");
    static public ConfigEntry<bool> _enableAheAnim = configFile.Bind("InoutAnimSync Setting", "EnableAheAnim", false, "Enable Ahe Anim");
    // Emission Anim
    static bool enableEmissionAnim = _enableEmissionAnim.Value;
    static int smoothWindowSizeValue = Math.Min(_smoothWindowSize.Value, BUFFERSIZE);
    static float emissionIntensityStart = _emissionIntensityStart.Value;
    static float emissionIntensityEnd = _emissionIntensityEnd.Value;
    static float emissionThresholdStart = _emissionThresholdStart.Value;
    static float emissionThresholdEnd = _emissionThresholdEnd.Value;
    static int smoothWindowSize { get { return smoothWindowSizeValue; } set { smoothWindowSizeValue = Math.Min(value, BUFFERSIZE); } }
    static bool useRelativeInterval = _useRelativeInterval.Value;
    static float relativeExponent = _relativeExponent.Value;
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
        _emissionThresholdStart.SettingChanged += (s, e) => emissionThresholdStart = _emissionThresholdStart.Value;
        _emissionThresholdEnd.SettingChanged += (s, e) => emissionThresholdEnd = _emissionThresholdEnd.Value;
        _smoothWindowSize.SettingChanged += (s, e) => smoothWindowSize = _smoothWindowSize.Value;
        _useRelativeInterval.SettingChanged += (s, e) => useRelativeInterval = _useRelativeInterval.Value;
        _relativeExponent.SettingChanged += (s, e) => relativeExponent = _relativeExponent.Value;
        _enableAheAnim.SettingChanged += (s, e) => { enableAheAnim = _enableAheAnim.Value; TryPatchVYMEnhance.UpdateAheAnimState(); };
    }

    public static void Unload()
    {
        instance.UnpatchAll(instance.Id);
        instance = null;
        curMaid = null;
        rateBuffer = null;
        rateMinQueue = null;
        rateMaxQueue = null;
        configFile = null;
        _enableEmissionAnim = null;
        _emissionIntensityStart = null;
        _emissionIntensityEnd = null;
        _emissionThresholdStart = null;
        _emissionThresholdEnd = null;
        _smoothWindowSize = null;
        _useRelativeInterval = null;
        _relativeExponent = null;
        _enableAheAnim = null;
    }

    static void SetRate(float value)
    {
        rate = originRate = value;
        if (smoothWindowSize > 0)
        {
            float sum = value;
            for (int i = 0, j = (rateBufferIndex - smoothWindowSize + BUFFERSIZE) % BUFFERSIZE; i < smoothWindowSize - 1; i++, j = (j + 1) % smoothWindowSize)
            {
                sum += rateBuffer[j];
            }
            smoothRate = sum / smoothWindowSize;
            rate = smoothRate;
        }
        if (useRelativeInterval)
        {
            bool needUpdate = false;
            float popValue = rateBuffer[rateBufferIndex];
            if (popValue != 0f)
            {
                // Queue Full
                if (popValue == rateMin)
                {
                    needUpdate = true;
                    rateMinQueue.RemoveFirst();
                }
                if (popValue == rateMax)
                {
                    needUpdate = true;
                    rateMaxQueue.RemoveFirst();
                }
            }
            while (rateMinQueue.Count != 0 && rateMinQueue.Last.Value > value)
            {
                rateMinQueue.RemoveLast();
            }
            rateMinQueue.AddLast(value);
            needUpdate |= rateMinQueue.First.Value != rateMin;
            while (rateMaxQueue.Count != 0 && rateMaxQueue.Last.Value < value)
            {
                rateMaxQueue.RemoveLast();
            }
            rateMaxQueue.AddLast(value);
            needUpdate |= rateMaxQueue.First.Value != rateMax;
            if (needUpdate)
            {
                rateMin = rateMinQueue.First.Value;
                rateMax = rateMaxQueue.First.Value;
                // [rateMin, rateMax] => [relativeRateMin, relativeRateMax]
                // length => length^relativeExponent
                // relativeRateMin = (1 - length^relativeExponent)/(1 - length) * rateMin
                // relativeRateMax = 1 - (1 - length^relativeExponent)/(1 - length) * (1 - rateMax)
                float length = Math.Max(rateMax - rateMin, 0.05f);
                if (length >= 0.95f)
                {
                    relativeRateMin = rateMin;
                    relativeRateMax = rateMax;
                }
                else
                {
                    float factor = (1f - (float)Math.Pow(length, relativeExponent)) / (1f - length);
                    relativeRateMin = factor * Math.Min(rateMin, 0.95f);
                    relativeRateMax = 1 - factor * (1f - Math.Max(rateMax, 0.05f));
                }
                relativeRateFactor = (relativeRateMax - relativeRateMin) / length;
            }
            relativeRate = ((smoothWindowSize > 0 ? smoothRate : value) - rateMin) * relativeRateFactor + relativeRateMin;
            rate = relativeRate;
        }
        rateBuffer[rateBufferIndex] = value;
        rateBufferIndex = (rateBufferIndex + 1) % BUFFERSIZE;
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
                    SetRate(r);
                    TryPatchSceneCapture.UpdateRate();
                    TryPatchVYMEnhance.UpdateRate();
                    break;
                }
            }
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
        __instance.containers["emissionanim"].Add(new InOutAnimation.Controller.LabelSlider("Threshold Start", emissionThresholdStart, 0f, 3f, 0f, delegate (float f)
        {
            _emissionThresholdStart.Value = f;
        }, false));
        __instance.containers["emissionanim"].Add(new InOutAnimation.Controller.LabelSlider("Threshold End", emissionThresholdEnd, 0f, 3f, 0f, delegate (float f)
        {
            _emissionThresholdEnd.Value = f;
        }, false));
        __instance.containers["emissionanim"].Add(new InOutAnimation.Controller.LabelSlider("Smooth WinSize", smoothWindowSize, 0f, 20f, 0f, delegate (float f)
        {
            _smoothWindowSize.Value = (int)f;
        }, true));
        __instance.containers["emissionanim"].Add(new InOutAnimation.Controller.ToggleButton("Relative Interval", useRelativeInterval, delegate (bool b)
        {
            _useRelativeInterval.Value = b;
        }));
        __instance.containers["emissionanim"].Add(new InOutAnimation.Controller.LabelSlider("Relative Exponent", relativeExponent, 0.1f, 0.9f, 0.2f, delegate (float f)
        {
            _relativeExponent.Value = f;
        }, false));
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
            contents[3] = new InOutAnimation.Controller.LabelSlider("Threshold Start", emissionThresholdStart, 0f, 3f, 0f, delegate (float f)
            {
                _emissionThresholdStart.Value = f;
            }, false);
            contents[4] = new InOutAnimation.Controller.LabelSlider("Threshold End", emissionThresholdEnd, 0f, 3f, 0f, delegate (float f)
            {
                _emissionThresholdEnd.Value = f;
            }, false);
            contents[5] = new InOutAnimation.Controller.LabelSlider("Smooth WinSize", smoothWindowSize, 0f, 20f, 0f, delegate (float f)
            {
                _smoothWindowSize.Value = (int)f;
            }, true);
            contents[6] = new InOutAnimation.Controller.ToggleButton("Relative Interval", useRelativeInterval, delegate (bool b)
            {
                _useRelativeInterval.Value = b;
            });
            contents[7] = new InOutAnimation.Controller.LabelSlider("Relative Exponent", relativeExponent, 0.1f, 0.9f, 0.2f, delegate (float f)
            {
                _relativeExponent.Value = f;
            }, false);
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
        public static object cinematicBloomLayer;
        public static Type cinematicBloomLayerDef;
        public static FieldInfo fieldCinematicBloomLayer;
        public static FieldInfo fieldEmissionIntensity;
        public static FieldInfo fieldEmissionThreshold;
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
            fieldEmissionThreshold = AccessTools.Field(_cinematicBloomLayer, "_threshold");
            cinematicBloomLayerDef = _cinematicBloomLayerDef;
            valid = true;
            return true;
        }

        public static float? GetEmissionIntensity()
        {
            if (cinematicBloomLayer == null)
            {
                cinematicBloomLayer = fieldCinematicBloomLayer.GetValue(null);
            }
            if (cinematicBloomLayer != null)
            {
                object intensity = fieldEmissionIntensity.GetValue(cinematicBloomLayer);
                if (intensity != null)
                {
                    return (float)intensity;
                }
            }
            return null;
        }

        public static bool SetEmissionIntensity(float value)
        {
            if (cinematicBloomLayer == null)
            {
                cinematicBloomLayer = fieldCinematicBloomLayer.GetValue(null);
            }
            if (cinematicBloomLayer != null)
            {
                fieldEmissionIntensity.SetValue(cinematicBloomLayer, value);
                return true;
            }
            return false;
        }

        public static float? GetEmissionThreshold()
        {
            if (cinematicBloomLayer == null)
            {
                cinematicBloomLayer = fieldCinematicBloomLayer.GetValue(null);
            }
            if (cinematicBloomLayer != null)
            {
                object threshold = fieldEmissionThreshold.GetValue(cinematicBloomLayer);
                if (threshold != null)
                {
                    return (float)threshold;
                }
            }
            return null;
        }

        public static bool SetEmissionThreshold(float value)
        {
            if (cinematicBloomLayer == null)
            {
                cinematicBloomLayer = fieldCinematicBloomLayer.GetValue(null);
            }
            if (cinematicBloomLayer != null)
            {
                fieldEmissionThreshold.SetValue(cinematicBloomLayer, value);
                return true;
            }
            return false;
        }

        public static void UpdateRate()
        {
            SetEmissionIntensity(Mathf.Lerp(emissionIntensityStart, emissionIntensityEnd, rate));
            SetEmissionThreshold(Mathf.Lerp(emissionThresholdStart, emissionThresholdEnd, rate));
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