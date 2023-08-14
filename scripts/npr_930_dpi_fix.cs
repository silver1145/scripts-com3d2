// #author silver1145
// #name NPRShader DPI Fix
// #desc DPI Fix on 2K for NPRShader v930

using UnityEngine;
using HarmonyLib;
using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using COM3D2.NPRShader.Plugin;

public static class NPRShaderDpiFix
{

    static Harmony instance;

    public static void Main()
    {
        instance = Harmony.CreateAndPatchAll(typeof(NPRShaderDpiFix));
    }

    public static void Unload()
    {
        instance.UnpatchAll(instance.Id);
        instance = null;
    }

    [HarmonyPatch(typeof(ControlBase), "FixPx")]
    [HarmonyPostfix]
    public static void NPRFixPxPostfix(int px, ref int __result)
    {
        __result = (int)((Math.Pow(Screen.dpi / 96f, 0.2)) * (float)px);
        //__result = (int)(0.95f * (float)px);
    }


}