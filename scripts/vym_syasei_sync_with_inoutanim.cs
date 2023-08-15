// #author silver1145
// #name Syasei Sync With Inoutanim
// #desc Vym Syasei Sync With Inoutanim
// #ref ${BepInExRoot}/../Sybaris/UnityInjector/COM3D2.VibeYourMaid.Plugin.dll
// #ref ${BepInExRoot}/../Sybaris/UnityInjector/COM3D2.InOutAnimation.Plugin.dll

using UnityEngine;
using HarmonyLib;
using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using COM3D2.InOutAnimation.Plugin;
using CM3D2.VibeYourMaid.Plugin;

public static class SyaseiSync
{

    static Harmony instance;
    static Dictionary<Maid, bool> isSyasei = new Dictionary<Maid, bool>();
    static bool isShooting = false;

    public static void Main()
    {
        instance = Harmony.CreateAndPatchAll(typeof(SyaseiSync));
    }

    public static void Unload()
    {
        instance.UnpatchAll(instance.Id);
        instance = null;
    }

    [HarmonyPatch(typeof(VibeYourMaid), "SyaseiCheck")]
    [HarmonyPostfix]
    public static void SyaseiCheckPostfix(int maidID, ref VibeYourMaid __instance, ref bool __result)
    {
        isSyasei[__instance.stockMaids[maidID].mem] = __result;
    }
    
    [HarmonyPatch(typeof(InOutAnimation), "IsShooting")]
    [HarmonyPostfix]
    public static void IsShootingPostfix(ref InOutAnimation __instance, ref bool __result)
    {
        if (!__result)
        {
            if (isSyasei.TryGetValue(__instance.mediator.TargetMaid, out bool value))
            {
                isShooting = value;
            }
        }
        else
        {
            isShooting = true;
        }
    }

    [HarmonyPatch(typeof(InOutAnimation.FlipAnim), "GetCurrentTex")]
    [HarmonyPostfix]
    public static void GetCurrentTexPostfix(ref InOutAnimation.FlipAnim __instance, ref Texture2D __result)
    {
        if (isShooting)
        {
            __result = !__instance.TextureLoadedEx ? Texture2D.blackTexture : __instance.texturesEx[Mathf.RoundToInt((__instance.texturesEx.Length - 1) * Mathf.InverseLerp(0, __instance.textures.Length - 1, __instance.CurrentFrame))];
        }
    }

}