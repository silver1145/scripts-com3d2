// #author silver1145
// #name VYM Enhance
// #desc VYM Enhance
// #ref ${BepInExRoot}/../Sybaris/UnityInjector/COM3D2.VibeYourMaid.Plugin.dll

using UnityEngine;
using HarmonyLib;
using System;
using System.Linq;
using System.Reflection.Emit;
using System.Collections.Generic;
using CM3D2.VibeYourMaid.Plugin;

public static class VYM_Enhance
{
    static Harmony instance;
    static Dictionary<int, MaidAheEye> maidsAheEye = new Dictionary<int, MaidAheEye>();
    static bool externalSet = false;
    static Maid currentMaid = null;
    static float currentValue = 0f;

    public static void Main()
    {
        instance = Harmony.CreateAndPatchAll(typeof(VYM_Enhance));
    }

    public static void Unload()
    {
        instance.UnpatchAll(instance.Id);
        instance = null;
    }

    // Switch Show UI
    [HarmonyPatch(typeof(VibeYourMaid), "Update")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> UpdateTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        CodeMatcher codeMatcher = new CodeMatcher(instructions)
            .MatchForward(false, new[] { new CodeMatch(OpCodes.Ldstr, "VibeYourMaid Plugin 有効化") })
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(VibeYourMaid), "cfgw")))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldc_I4_1))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(VibeYourMaid.VibeYourMaidCfgWriting), "mainGuiFlag")));
        return codeMatcher.InstructionEnumeration();
    }

    // Orgasm Modify
    [HarmonyPatch(typeof(VibeYourMaid), "OrgasmProcess")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> OrgasmProcessTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        CodeMatcher codeMatcher = new CodeMatcher(instructions)
            .MatchForward(false, new[] { new CodeMatch(OpCodes.Ldc_R4, 30f) })
            .SetInstructionAndAdvance(new CodeInstruction(OpCodes.Ldc_R4, 3f))
            .MatchForward(false, new[] { new CodeMatch(OpCodes.Ldc_R4, 120f) })
            .SetInstructionAndAdvance(new CodeInstruction(OpCodes.Ldc_R4, 70f));
        return codeMatcher.InstructionEnumeration();
    }

    // Avoid Error
    [HarmonyPatch(typeof(VibeYourMaid), "MaidVoicePlay")]
    [HarmonyFinalizer]
    public static void MaidVoicePlayFinalizer(ref Exception __exception)
    {
        __exception = null;
    }

    // Add Sound
    [HarmonyPatch(typeof(VibeYourMaid), "EffectSio")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> EffectSioTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        CodeMatcher codeMatcher = new CodeMatcher(instructions)
            .MatchForward(false, new[] { new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(Maid), "AddPrefab")) })
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(GameMain), "get_Instance")))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(GameMain), "get_SoundMgr")))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldstr, "se080.ogg"))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldc_I4_0))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(SoundMgr), "PlaySe")))
            .MatchForward(false, new[] { new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(Maid), "AddPrefab")) })
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(GameMain), "get_Instance")))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(GameMain), "get_SoundMgr")))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldstr, "se080.ogg"))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldc_I4_0))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(SoundMgr), "PlaySe")));
        return codeMatcher.InstructionEnumeration();
    }

    // Avoid Error
    [HarmonyPatch(typeof(VibeYourMaid), "EffectSeieki")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> EffectSeiekiTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        Label label;
        CodeMatcher codeMatcher = new CodeMatcher(instructions, generator)
            .MatchForward(false, new[] { new CodeMatch(OpCodes.Ret) })
            .Advance(1)
            .CreateLabel(out label)
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldloc_1))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldnull))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UnityEngine.Object), "op_Equality")))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Brtrue_S, label))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ret));
        return codeMatcher.InstructionEnumeration();
    }

    // Add Ahe Settings
    [HarmonyPatch(typeof(VibeYourMaid), "WindowCallback3_1")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> WindowCallback3_1Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        Label label;
        CodeMatcher codeMatcher = new CodeMatcher(instructions, generator)
            .MatchForward(false, new[] { new CodeMatch(OpCodes.Ldstr, "エロステータス表示") })
            .Advance(-6)
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldc_R4, 420f))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldloc_0))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Conv_R4))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldc_R4, 190f))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldc_R4, 20f))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Newobj, AccessTools.Constructor(typeof(Rect), new [] { typeof(float), typeof(float), typeof(float), typeof(float) })))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldstr, "アヘ 詳細設定"))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(VibeYourMaid), "gsButton")))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(GUI), "Button", new [] { typeof(Rect), typeof(string), typeof(GUIStyle) })))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldc_I4_S, 10))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(VibeYourMaid), "ConfigFlag")))
            .Insert(new CodeInstruction(OpCodes.Ldloc_0))
            .CreateLabel(out label)
            .Advance(1)
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldc_I4_S, 30))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Add))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Stloc_0))
            .MatchBack(false, new[] { new CodeMatch(OpCodes.Call) })
            .Advance(1)
            .InsertAndAdvance(new CodeInstruction(OpCodes.Brfalse_S, label));
        return codeMatcher.InstructionEnumeration();
    }

    // Add Ahe Settings
    [HarmonyPatch(typeof(VibeYourMaid), "WindowCallback3")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> WindowCallback3Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        Label label;
        CodeMatcher codeMatcher = new CodeMatcher(instructions, generator)
            .MatchForward(false, new[] { new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(GUI), "DragWindow")) });
        codeMatcher.InsertAndAdvance(codeMatcher.InstructionAt(-4))
            .Insert(new CodeInstruction(OpCodes.Ldarg_0))
            .CreateLabel(out label)
            .Advance(1)
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(VibeYourMaid), "gsLabel")))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(VYM_Enhance), "WindowCallback3_10")));
        var switches = ((Label[])(codeMatcher.MatchBack(false, new[] { new CodeMatch(OpCodes.Switch) }).Instruction.operand)).ToList();
        switches.Add(label);
        codeMatcher.Instruction.operand = switches.ToArray();
        return codeMatcher.InstructionEnumeration();
    }

    public static void WindowCallback3_10(GUIStyle gsLabel)
    {
        GUI.Label(new Rect(5, 35, 90, 20), "circle_time: " + Math.Round(MaidAheEye.circle_time, 1), gsLabel);
        MaidAheEye.circle_time = (float) Math.Round(GUI.HorizontalSlider(new Rect(95, 40, 100, 20), MaidAheEye.circle_time, 0.1F, 10.0F), 1);
        GUI.Label(new Rect(5, 65, 90, 20), "瞳_offset1: " + Math.Round(MaidAheEye.factor_start, 1), gsLabel);
        MaidAheEye.factor_start = (float) Math.Round(GUI.HorizontalSlider(new Rect(95, 70, 100, 20), MaidAheEye.factor_start, 0.0F, 10.0F), 1);
        GUI.Label(new Rect(5, 95, 90, 20), "瞳_offset2: " + Math.Round(MaidAheEye.factor_end, 1), gsLabel);
        MaidAheEye.factor_end = (float) Math.Round(GUI.HorizontalSlider(new Rect(95, 100, 100, 20), MaidAheEye.factor_end, 0.0F, 10.0F), 1);
        GUI.Label(new Rect(5, 125, 90, 20), "瞳_scale1: " + Math.Round(MaidAheEye.scale_start, 1), gsLabel);
        MaidAheEye.scale_start = (float) Math.Round(GUI.HorizontalSlider(new Rect(95, 130, 100, 20), MaidAheEye.scale_start, 0.0F, 2.0F), 1);
        GUI.Label(new Rect(5, 155, 90, 20), "瞳_scale2: " + Math.Round(MaidAheEye.scale_end, 1), gsLabel);
        MaidAheEye.scale_end = (float) Math.Round(GUI.HorizontalSlider(new Rect(95, 160, 100, 20), MaidAheEye.scale_end, 0.0F, 2.0F), 1);
        GUI.Label(new Rect(5, 185, 90, 20), "off_x: " + Math.Round(MaidAheEye.off_x, 4), gsLabel);
        MaidAheEye.off_x = (float) Math.Round(GUI.HorizontalSlider(new Rect(95, 190, 100, 20), MaidAheEye.off_x, -0.05F, 0.05F), 4);
        GUI.Label(new Rect(5, 215, 90, 20), "off_z: " + Math.Round(MaidAheEye.off_z, 4), gsLabel);
        MaidAheEye.off_z = (float) Math.Round(GUI.HorizontalSlider(new Rect(95, 220, 100, 20), MaidAheEye.off_z, -0.05F, 0.05F), 4);
    }

    // Ahe Enhance
    [HarmonyPatch(typeof(VibeYourMaid), "EffectAhe")]
    [HarmonyPrefix]
    public static bool EffectAhePrefix(ref VibeYourMaid __instance, Maid maid, VibeYourMaid.MaidState maidState, float sp)
    {        
        int maidID = __instance.maidsState.IndexOf(maidState);
        maidID = maidID >= 0 ? maidID : 0;
        float aheValue3 = maidState.aheValue2;
        if (!__instance.cfgw.AheEnabled || maidState.vStateMajor == 10)
        {
            if (maidState.aheResetFlag)
            {
                maid.body0.trsEyeL.localPosition = maidsAheEye[maidID].l_v;
                maid.body0.trsEyeR.localPosition = maidsAheEye[maidID].r_v;
                maid.body0.trsEyeL.localScale = maidsAheEye[maidID].scale_off_orgin_l + new Vector3(1.0f, 1.0f, 1.0f);
                maid.body0.trsEyeR.localScale = maidsAheEye[maidID].scale_off_orgin_r + new Vector3(1.0f, 1.0f, 1.0f);
                maidState.fAheDefEyeL = -9999f;
                maidState.fAheDefEyeR = -9999f;
                maidState.aheResetFlag = false;
                maidsAheEye.Remove(maidID);
            }
            return false;
        }
        if (maidState.fAheDefEyeL < -1000) maidState.fAheDefEyeL = maid.body0.trsEyeL.localPosition.y  * __instance.fEyePosToSliderMul;
        if (maidState.fAheDefEyeR < -1000) maidState.fAheDefEyeR = maid.body0.trsEyeR.localPosition.y  * __instance.fEyePosToSliderMul;
        if (!maidsAheEye.ContainsKey(maidID))
        {
            var eyeLLocalPosition = new Vector3(
                maid.body0.trsEyeL.localPosition.x,
                Math.Max((maidState.fAheDefEyeL + 0f) / __instance.fEyePosToSliderMul, 0f),
                maid.body0.trsEyeL.localPosition.z
            );
            var eyeRLocalPosition = new Vector3(
                maid.body0.trsEyeR.localPosition.x,
                Math.Min((maidState.fAheDefEyeR - 0f) / __instance.fEyePosToSliderMul, 0.0f),
                maid.body0.trsEyeR.localPosition.z
            );
            maidsAheEye.Add(maidID, new MaidAheEye(maid, eyeLLocalPosition, eyeRLocalPosition, maid.body0.trsEyeL.localScale, maid.body0.trsEyeR.localScale));    
        }
        if (maidState.orgasmCmb > 0)
        {
            if (maidState.boostValue - 15 > 0 && maidState.exciteLevel >= 2)
            {
                aheValue3 = maidState.aheValue2 + maidState.boostValue / 3;
                if (maidState.stunFlag) aheValue3 += 25;
            }
            if (aheValue3 > 60) aheValue3 = 60;

            if (maidState.aheValue < aheValue3)
            {
                maidState.aheValue += 0.1f * __instance.timerRate * sp;
            }
            else if (maidState.aheValue > aheValue3)
            {
                maidState.aheValue -= 0.1f * __instance.timerRate * sp;
            }
        }
        else if ((maidState.boostValue - 15 > 0 && maidState.exciteLevel >= 2) || maidState.stunFlag)
        {
            aheValue3 = maidState.boostValue / 2;
            if (maidState.stunFlag) aheValue3 += 25;

            if (maidState.aheValue < aheValue3)
            {
                maidState.aheValue += 0.1f * __instance.timerRate * sp;
            }
            else if (maidState.aheValue > aheValue3)
            {
                maidState.aheValue -= 0.1f * __instance.timerRate * sp;
            }
        }
        else if (maidState.aheValue > 0)
        {
            maidState.aheValue -= 0.05f * __instance.timerRate * sp;
        }

        if (maidState.aheValue < 0f) maidState.aheValue = 0f;

        float v = maidsAheEye[maidID].getCurrentYFactor(maid);
        Vector2 VX = maidsAheEye[maidID].getCurrentX();
        Vector2 VY = maidsAheEye[maidID].getCurrentY(
            Math.Max(v * (maidState.fAheDefEyeL + maidState.aheValue) / __instance.fEyePosToSliderMul, 0f),
            Math.Min(v * (maidState.fAheDefEyeR - maidState.aheValue) / __instance.fEyePosToSliderMul, 0f)
        );
        Vector2 VZ = maidsAheEye[maidID].getCurrentZ();
        maid.body0.trsEyeL.localPosition = new Vector3(VX.x, VY.x, VZ.x);
        maid.body0.trsEyeR.localPosition = new Vector3(VX.y, VY.y, VZ.y);
        if (maidsAheEye[maidID].scaleNeedUpdated())
        {
            maid.body0.trsEyeL.localScale = maidsAheEye[maidID].getCurrentScaleL();
            maid.body0.trsEyeR.localScale = maidsAheEye[maidID].getCurrentScaleR();
        }
        if (!maidState.aheResetFlag) maidState.aheResetFlag = true;
        return false;
    }

    public class MaidAheEye
    {
        public Maid maid;
        public Vector3 l_v;
        public Vector3 r_v;
        public Vector3 scale_off_orgin_l;
        public Vector3 scale_off_orgin_r;
        private float factor_y;
        private float scale = 1.0f;
        private bool circle_flag_factor = true;
        private bool circle_flag_scale = true;
        private bool scale_update_flag = false;
        static Dictionary<float, float> x_offset_map = new Dictionary<float, float> {
            {0.0f, -0.06f}, {0.1f, -0.055f}, {0.2f, -0.05f}, {0.3f, -0.047f}, {0.4f, -0.044f}, {0.5f, -0.035f}, {0.6f, -0.03f}, {0.7f, -0.025f}, {0.8f, -0.02f}, {0.9f, -0.01f},
            {1.0f, 0.0f}, {1.1f, 0.01f}, {1.2f, 0.02f}, {1.3f, 0.03f}, {1.4f, 0.035f}, {1.5f, 0.045f}, {1.6f, 0.05f}, {1.7f, 0.055f}, {1.8f, 0.062f}, {1.9f, 0.068f}, {2.0f, 0.075f}, {2.1f, 0.08f}
        };

        public static float factor_start = 1.0f;
        public static float factor_end = 1.0f;
        public static float scale_start = 1.0f;
        public static float scale_end = 1.0f;
        public static float circle_time = 2.0f;
        public static float off_x = 0.0f;
        public static float off_z = 0.0f;

        public MaidAheEye(Maid m, Vector3 lv, Vector3 rv, Vector3 origin_scale_l, Vector3 origin_scale_r)
        {
            maid = m;
            factor_y = factor_start;
            l_v = lv;
            r_v = rv;
            scale_off_orgin_l = origin_scale_l - new Vector3(1.0f, 1.0f, 1.0f);
            scale_off_orgin_r = origin_scale_r - new Vector3(1.0f, 1.0f, 1.0f);
        }

        public float getCurrentYFactor(Maid maid)
        {
            bool equalFlag = factor_start == factor_end;
            bool externalFlag = externalSet && currentMaid == maid;
            if (equalFlag)
            {
                factor_y = factor_start;
            }
            else if (externalFlag)
            {
                factor_y = Mathf.Lerp(factor_start, factor_end, currentValue);
            }
            else
            {
                float step_factor = 2 * Math.Abs(factor_end - factor_start) / circle_time * Time.deltaTime;
                if (circle_flag_factor)
                {
                    factor_y += step_factor;
                }
                else
                {
                    factor_y -= step_factor;
                }
                if ((factor_end > factor_start && factor_y > factor_end) || (factor_end < factor_start && factor_y > factor_start))
                {
                    circle_flag_factor = false;
                }
                else if ((factor_end > factor_start && factor_y < factor_start) || (factor_end < factor_start && factor_y < factor_end))
                {
                    circle_flag_factor = true;
                }
            }
            
            if (equalFlag)
            {
                scale = scale_start;
            }
            else if (externalFlag)
            {
                scale = Mathf.Lerp(scale_start, scale_end, currentValue);
            }
            else
            {
                float step_scale = 2 * Math.Abs(scale_end - scale_start) / circle_time * Time.deltaTime;
                if (circle_flag_scale)
                {
                    scale += step_scale;
                }
                else
                {
                    scale -= step_scale;
                }
                if ((scale_end > scale_start && scale > scale_end) || (scale_end < scale_start && scale > scale_start))
                {
                    circle_flag_scale = false;
                }
                else if ((scale_end > scale_start && scale < scale_start) || (scale_end < scale_start && scale < scale_end))
                {
                    circle_flag_scale = true;
                }
            }
            return factor_y;
        }
        public Vector2 getCurrentX()
        {
            if (x_offset_map.ContainsKey(scale))
            {
                return new Vector2(l_v.x + x_offset_map[scale] + off_x, r_v.x + x_offset_map[scale] + off_x);
            }
            float floor =  (float) Math.Round(scale - 0.05f, 1);
            float ceil = (float) Math.Round(scale + 0.05f, 1);
            if (x_offset_map.ContainsKey(floor) && x_offset_map.ContainsKey(ceil))
            {
                float temp = x_offset_map[ceil] + (x_offset_map[ceil] - x_offset_map[floor]) * (scale - floor) * 10;
                return new Vector2(l_v.x + temp + off_x, r_v.x + temp + off_x);
            }
            return new Vector2(l_v.x + off_x, r_v.x + off_x);
        }

        public Vector2 getCurrentY(float cur_y_l, float cur_y_r)
        {
            return new Vector2(
                l_v.y + cur_y_l,
                r_v.y + cur_y_r
            );
        }

        public Vector2 getCurrentZ()
        {
            return new Vector2(l_v.z + off_z, r_v.z + off_z);
        }

        public Vector3 getCurrentScaleL()
        {
            return new Vector3(scale, scale, scale) + scale_off_orgin_l;
        }

        public Vector3 getCurrentScaleR()
        {
            return new Vector3(scale, scale, scale) + scale_off_orgin_r;
        }

        public bool scaleNeedUpdated()
        {
            if (scale_start != 1.0f || scale_end != 1.0f)
            {
                scale_update_flag = true;
                return true;
            }
            if (scale_update_flag)
            {
                scale_update_flag = false;
                return true;
            }
            return false;
        }
    }
}
