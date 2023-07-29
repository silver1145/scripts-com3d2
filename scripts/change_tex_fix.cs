// #author silver1145
// #name ChangeTex Fix
// #desc Avoid TBody.ChangeTex when file does not exist

using UnityEngine;
using HarmonyLib;
using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;


public static class LoadTestMaterial {
    static Harmony instance;

    public static void Main() {
        instance = Harmony.CreateAndPatchAll(typeof(LoadTestMaterial));
    }

    public static void Unload() {
        instance.UnpatchAll(instance.Id);
        instance = null;
    }

    [HarmonyPatch(typeof(TBody), "ChangeTex")]
    [HarmonyPrefix]
    public static bool TBodyChangeTexPrefix(ref TBody __instance, string slotname, string filename) {
        int num = (int)TBody.hashSlotName[slotname];
        TBodySkin tbodySkin = __instance.goSlot[num];
        string file_name = filename.Replace("*", Path.GetFileNameWithoutExtension(tbodySkin.m_strModelFileName));
        return GameUty.FileSystem.IsExistentFile(file_name);
    }
}