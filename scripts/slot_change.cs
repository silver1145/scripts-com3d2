// #author silver1145
// #name Slot Change
// #desc Allow All Slots to Use Body Bone

using UnityEngine;
using HarmonyLib;
using System;
using System.Collections.Generic;

public static class SlotChange {
    static Harmony instance;

    public static void Main()
    {
        instance = Harmony.CreateAndPatchAll(typeof(SlotChange));
    }

    public static void Unload()
    {
        instance.UnpatchAll(instance.Id);
        instance = null;
    }

    [HarmonyPatch(typeof(TBodySkin), "Load", new[] {typeof(MPN), typeof(Transform), typeof(Transform), typeof(Dictionary<string, Transform>), typeof(string), typeof(string), typeof(string), typeof(string), typeof(int), typeof(bool), typeof(int)})]
    [HarmonyPrefix]
    public static void LoadPrefix(string filename, ref string bonename)
    {
        if (filename.ToLower().EndsWith(".bodybone.model"))
        {
            bonename = "_ROOT_";
        }
    }
}