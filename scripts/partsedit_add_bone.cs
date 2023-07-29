// #author silver1145
// #name Add Editable Bone For PartsEdit
// #desc Make Mune & Hip Bone Moveable and Scaleable
// #ref ${BepInExRoot}/../Sybaris/UnityInjector/COM3D2.PartsEdit.Plugin.dll

using UnityEngine;
using HarmonyLib;
using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using CM3D2.PartsEdit.Plugin;


public static class PartsEditAddBone {
    static readonly List<string> muneBoneList = new List<string>(){
        "Mune_L",
        "Mune_L_sub",
        "Mune_R",
        "Mune_R_sub"
    };
    static readonly List<string> hipBoneList = new List<string>(){
        "Hip_L",
        "Hip_R"
    };
    static Harmony instance;
    static bool editMune;
    static bool editHip;
    static bool editHipMR;

    public static void Main() {
        instance = Harmony.CreateAndPatchAll(typeof(PartsEditAddBone));
        editMune = false;
        editHip = false;
        editHipMR = false;
    }

    public static void Unload() {
        instance.UnpatchAll(instance.Id);
        instance = null;
    }

    [HarmonyPatch(typeof(BoneEdit), "JudgeVisibleBone")]
    [HarmonyPostfix]
    static void PartsEditVisiblePostfix(BoneRendererAssist bra, ref bool __result) {
        if(muneBoneList.Contains(bra.transform.name) || hipBoneList.Contains(bra.transform.name))
        {
            __result = true;
        }
    }

    [HarmonyPatch(typeof(BoneEdit), "BoneClick")]
    [HarmonyPostfix]
    static void PartsEditModifyBoneClick(Transform bone) {
        if (!editMune && muneBoneList.Contains(bone.name))
        {
            editMune = true;
        }
        else if (editMune && !muneBoneList.Contains(bone.name))
        {
            editMune = false;
        }
        if (!editHip && hipBoneList.Contains(bone.name))
        {
            editHip = true;
            editHipMR = true;
        }
    }

    [HarmonyPatch(typeof(BoneGizmoRenderer), "rotTargetTrs", MethodType.Getter)]
    [HarmonyPostfix]
    static void PartsEditModifyGetter(ref Transform __result) {
        if (__result.name == "Hip_L")
        {
            __result = __result.parent.parent.parent.parent.GetChild(1).Find("Bip01/Bip01 Pelvis/Hip_L");
        }
        else if (__result.name == "Hip_R")
        {
            __result = __result.parent.parent.parent.parent.GetChild(1).Find("Bip01/Bip01 Pelvis/Hip_R");
        }
    }

    [HarmonyPatch(typeof(TBody), "MoveMomoniku")]
    [HarmonyPrefix]
    static bool TBodyModifyPreMoveMomoniku(ref TBody __instance) {
        if (editMune && (__instance.jbMuneL.enabled || __instance.jbMuneR.enabled))
        {
            __instance.maid.GetAnimation().Stop();
            __instance.MuneYureL(0f);
            __instance.jbMuneL.enabled = false;
            __instance.MuneYureR(0f);
            __instance.jbMuneR.enabled = false;
        }
        if (!editHip)
        {
            return true;
        }
        if (!TBody.boMoveMomoniku || __instance.momoniku_L == null || __instance.momoniku_R == null)
        {
        	return false;
        }
        if (editHipMR)
        {
            __instance.Hip_L_MR.localRotation = __instance.Hip_L.localRotation;
            __instance.Hip_R_MR.localRotation = __instance.Hip_R.localRotation;
            __instance.Hip_L_MR.position =__instance.Hip_L.position;
            __instance.Hip_R_MR.position = __instance.Hip_R.position;
            __instance.Hip_L_MR.localPosition =__instance.Hip_L.localPosition;
            __instance.Hip_R_MR.localPosition = __instance.Hip_R.localPosition;
            editHipMR = false;
        }
        float num = Mathf.Clamp(Vector3.Dot(Vector3.up, __instance.Thigh_L.up), 0f, 0.8f);
        float num2 = Mathf.Clamp(Vector3.Dot(Vector3.up, __instance.Thigh_R.up), 0f, 0.8f);
        __instance.momoniku_L.localRotation = __instance.momoniku_L_MR.localRotation;
        __instance.momoniku_R.localRotation = __instance.momoniku_R_MR.localRotation;
        __instance.momoniku_L.Rotate(0f, 0f, num * 10f);
        __instance.momoniku_R.Rotate(0f, 0f, -num2 * 10f);
        __instance.Hip_L.localRotation = __instance.Hip_L_MR.localRotation;
        __instance.Hip_R.localRotation = __instance.Hip_R_MR.localRotation;
        return false;
    }
}