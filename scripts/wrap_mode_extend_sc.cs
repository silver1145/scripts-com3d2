// #author ghorsington
// #name Texture Wrap Extender For SceneCapture
// #desc Make textures repeated for SceneCapture
// #ref ${BepInExRoot}/../Sybaris/UnityInjector/COM3D2.SceneCapture.Plugin.dll

using UnityEngine;
using HarmonyLib;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Reflection;
using CM3D2.SceneCapture.Plugin;

public static class WrapModeExtendSC {
    //const string TWR_INFIX = "";
    //const string TWR_POSTFIX = "";

    static Harmony instance;

    public static void Main() {
        instance = Harmony.CreateAndPatchAll(typeof(WrapModeExtendSC));
    }

    public static void Unload() {
        instance.UnpatchAll(instance.Id);
        instance = null;
    }

    public static TextureWrapMode FixWrapMode(Texture2D tex, TextureWrapMode twm) {
        return TextureWrapMode.Repeat;
    }
	
    [HarmonyPatch(typeof(AssetLoader), "ReadMaterial")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> SCReadMaterialTranspiler(IEnumerable<CodeInstruction> instrs, ILGenerator il) {
        var loc = il.DeclareLocal(typeof(TextureWrapMode));
        var target = AccessTools.PropertySetter(typeof(Texture), "wrapMode");
        foreach(var ins in instrs) {
            if(ins.opcode == OpCodes.Callvirt && ((MethodInfo) ins.operand == target)){
                yield return new CodeInstruction(OpCodes.Stloc, loc);
                yield return new CodeInstruction(OpCodes.Dup);
                yield return new CodeInstruction(OpCodes.Ldloc, loc);
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(WrapModeExtendSC), "FixWrapMode"));
            }
            yield return ins;
        }
    }
}