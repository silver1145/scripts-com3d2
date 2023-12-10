// #author silver1145
// #name NPRShader Addition
// #desc NPRShader Addition

using UnityEngine;
using HarmonyLib;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Newtonsoft.Json.Linq;
using COM3D2.NPRShader.Plugin;

public static class NPRShaderAdd {
    static Harmony instance;
    public static string[] _BASEFRPROP;
    public static string[] _BASECOLORPROP;
    public static string[] _SHADERKEYWORD;
    public static List<string> _TOGGLEPROP;
    private static FieldInfo HDREnabled;
    private static float k_MaxForOverexposedColor = 191.0f / 255;

    public static void Main()
    {
        // GetPostProcessingStatus
        Type targetType = AccessTools.TypeByName("COM3D2.SceneCaptureAddition.Plugin.SceneCaptureAddition");
        if (targetType != null)
        {
            HDREnabled = AccessTools.Field(targetType, "HDREnabled");
        }
        LoadShaderConfig();
        instance = Harmony.CreateAndPatchAll(typeof(NPRShaderAdd));
    }

    public static void Unload()
    {
        instance.UnpatchAll(instance.Id);
        instance = null;
    }

    public static void LoadShaderConfig()
    {
        HashSet<string> BASEFRPROP = new HashSet<string>();
        HashSet<string> BASECOLORPROP = new HashSet<string>();
        HashSet<string> SHADERKEYWORD = new HashSet<string>();
        HashSet<string> TOGGLEPROP = new HashSet<string>();
        foreach (string prop in (string[]) AccessTools.Field(typeof(ObjectPane), "BASEFRPROP").GetValue(null))
        {
            BASEFRPROP.Add(prop);
        }
        foreach (string prop in (string[]) AccessTools.Field(typeof(ObjectPane), "BASECOLORPROP").GetValue(null))
        {
            BASECOLORPROP.Add(prop);
        }
        foreach (string prop in (string[]) AccessTools.Field(typeof(ObjectPane), "SHADERKEYWORD").GetValue(null))
        {
            SHADERKEYWORD.Add(prop);
        }
        string configDir = (string) AccessTools.Field(typeof(ConstantValues), "ConfigDir").GetValue(null);
        var jsonFiles = Directory.GetFiles(configDir , "*.json", SearchOption.AllDirectories).ToArray();
        for (int i = 0; i < jsonFiles.Count(); i++)
        {
            JObject json = JObject.Parse(File.ReadAllText(jsonFiles[i]));
            JToken value;
            if (json.TryGetValue("BASEFRPROP", out value))
            {
                var bprop = value.ToObject<List<string>>();
                if (bprop != null)
                {
                    BASEFRPROP.UnionWith(bprop);
                }
            }
            if (json.TryGetValue("BASECOLORPROP", out value))
            {
                var bcprop = value.ToObject<List<string>>();
                if (bcprop != null)
                {
                    BASECOLORPROP.UnionWith(bcprop);
                }
            }
            if (json.TryGetValue("SHADERKEYWORD", out value))
            {
                var kwprop = value.ToObject<List<string>>();
                if (kwprop != null)
                {
                    SHADERKEYWORD.UnionWith(kwprop);
                }
            }
            if (json.TryGetValue("TOGGLEPROP", out value))
            {
                var kwdprop = value.ToObject<List<string>>();
                if (kwdprop != null)
                {
                    TOGGLEPROP.UnionWith(kwdprop);
                }
            }
        }
        BASEFRPROP.UnionWith(TOGGLEPROP);
        _BASEFRPROP = BASEFRPROP.ToArray();
        _BASECOLORPROP = BASECOLORPROP.ToArray();
        _SHADERKEYWORD = SHADERKEYWORD.ToArray();
        _TOGGLEPROP = TOGGLEPROP.ToList();
    }

    public static bool IsToggle(string prop)
    {
        return _TOGGLEPROP.Contains(prop);
    }

    public static bool IsKeyword(string prop, Material m)
    {
        if (m.shader == null || m.shader.name.ToLower().Contains("nprtoon"))
        {
            return true;
        }
        if (Array.Exists(_SHADERKEYWORD, element => element == prop))
        {
            return true;
        }
        return false;
    }

    public static Color CorrectHDRColor(Color inColor)
    {
        if (!((bool?)(HDREnabled?.GetValue(null))).GetValueOrDefault())
        {
            float maxColorComponent = inColor.maxColorComponent;
            // Not HDR
            if (maxColorComponent == 0f || (maxColorComponent <= 1f && maxColorComponent >= 0.003921569f))
            {
                return inColor;
            }
            // HDR
            Color retColor = inColor;
            retColor.r = k_MaxForOverexposedColor / maxColorComponent * inColor.r;
            retColor.g = k_MaxForOverexposedColor / maxColorComponent * inColor.g;
            retColor.b = k_MaxForOverexposedColor / maxColorComponent * inColor.b;
            return retColor;
        }
        return inColor;
    }

    [HarmonyPatch(typeof(NPRShader), "ReadMaterial")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> ReadMaterialTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        CodeMatcher codeMatcher = new CodeMatcher(instructions)
            .End()
            .MatchBack(false, new[] { new CodeMatch(OpCodes.Ldstr, "を開けませんでした") })
            .MatchBack(false, new[] { new CodeMatch(OpCodes.Leave) })
            .Advance(1)
            .InsertAndAdvance(new CodeInstruction(OpCodes.Nop))
            .MatchBack(false, new[] { new CodeMatch(OpCodes.Ldstr, "を開けませんでした") })
            .MatchBack(false, new[] { new CodeMatch(OpCodes.Leave) })
            .Advance(1)
            .InsertAndAdvance(new CodeInstruction(OpCodes.Nop));
        
        CodeMatch[] matchSetColor = {
            new CodeMatch(OpCodes.Callvirt, typeof(Material).GetMethod("SetColor", new[] {typeof(string), typeof(Color)}))
        };
        codeMatcher.MatchForward(false, matchSetColor)
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call, typeof(NPRShaderAdd).GetMethod("CorrectHDRColor")));
        codeMatcher.MatchForward(false, new CodeMatch(OpCodes.Ldstr, "Toggle"))
            .Advance(2)
            .InsertAndAdvance(new CodeInstruction(codeMatcher.InstructionAt(-3)))
            .InsertAndAdvance(new CodeInstruction(codeMatcher.InstructionAt(4)))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call, typeof(NPRShaderAdd).GetMethod("IsKeyword")))
            .InsertAndAdvance(new CodeInstruction(OpCodes.And));
        return codeMatcher.InstructionEnumeration();
    }

    [HarmonyPatch(typeof(CustomComboBox), "OnGUI")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> OnGUITranspiler(IEnumerable<CodeInstruction> instructions)
    {
        CodeMatcher codeMatcher = new CodeMatcher(instructions);
        CodeMatch[] matchListRectWidth = {
            new CodeMatch(OpCodes.Call),
            new CodeMatch(OpCodes.Ldloc_S),
            new CodeMatch(OpCodes.Newobj),
            new CodeMatch(OpCodes.Stfld),
            new CodeMatch(OpCodes.Ldstr, "box")
        };
        return codeMatcher.MatchForward(false, matchListRectWidth)
            .Advance(1)
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldc_R4, 2.0f))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Mul))
            .SetInstructionAndAdvance(new CodeInstruction(OpCodes.Ldloc_3)).InstructionEnumeration();
    }

    public static IEnumerable<CodeInstruction> parseMateFileTranspiler(IEnumerable<CodeInstruction> instructions, Type type)
    {
        CodeMatcher codeMatcher = new CodeMatcher(instructions);
        string[] propTypes = new string[] {"BASECOLORPROP", "BASEFRPROP", "SHADERKEYWORD"};
        foreach (string pType in propTypes)
        {
            codeMatcher.Start();
            for(int i = 0; i < 2; i ++)
            {
                codeMatcher.MatchForward(false, new CodeMatch(OpCodes.Ldsfld, AccessTools.Field(type, pType)))
                    .SetInstructionAndAdvance(new CodeInstruction(OpCodes.Ldsfld, typeof(NPRShaderAdd).GetField($"_{pType}")));
            }
        }
        return codeMatcher.InstructionEnumeration();
    }

    [HarmonyPatch(typeof(MaterialPane), "parseMateFile")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> MaterialPaneTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        return parseMateFileTranspiler(instructions, typeof(MaterialPane));
    }

    [HarmonyPatch(typeof(ObjectPane), "parseMateFile")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> ObjectPaneTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        return parseMateFileTranspiler(instructions, typeof(ObjectPane));
    }

    public static IEnumerable<CodeInstruction> ShowPaneTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        CodeMatcher codeMatcher = new CodeMatcher(instructions, generator);
        codeMatcher.MatchForward(false, new[] { new CodeMatch(OpCodes.Ldstr, "culling") });
        codeMatcher.Advance(3);
        foreach (var i in codeMatcher.InstructionsWithOffsets(-6, -1))
        {
            if(i.opcode == OpCodes.Ldstr)
            {
                codeMatcher.InsertAndAdvance(new CodeInstruction(OpCodes.Ldstr, "cullmode"));
            }
            else
            {
                codeMatcher.InsertAndAdvance(i);
            }
        }
        codeMatcher.MatchForward(false, new[] { new CodeMatch(OpCodes.Ldstr, "culling") })
            .CreateLabelAt(codeMatcher.Pos + 3, out Label jumpLabel)
            .InsertAndAdvance(new CodeInstruction(OpCodes.Dup))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldstr, "cullmode"))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Callvirt, typeof(string).GetMethod("Contains")))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Brtrue, jumpLabel));
        codeMatcher.Start()
            .MatchForward(false, new[] { new CodeMatch(OpCodes.Ldstr, "customblend") })
            .MatchForward(false, new[] { new CodeMatch(OpCodes.Ldc_I4_1) })
            .CreateLabel(out Label toggleLabel)
            .MatchBack(false, new[] { new CodeMatch(OpCodes.Ldstr, "customblend") })
            .Advance(-3);
        int blendPos = codeMatcher.Pos;
        codeMatcher.InsertAndAdvance(codeMatcher.InstructionAt(0))
            .InsertAndAdvance(codeMatcher.InstructionAt(1))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call, typeof(NPRShaderAdd).GetMethod("IsToggle")))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Brtrue, toggleLabel));
        return codeMatcher.InstructionEnumeration();
    }

    [HarmonyPatch(typeof(MaterialPane), "ShowPane")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> MaterialShowPaneTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        return ShowPaneTranspiler(instructions, generator);
    }

    [HarmonyPatch(typeof(ObjectPane), "ShowPane")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> ObjectShowPaneTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        return ShowPaneTranspiler(instructions, generator);
    }

    [HarmonyPatch(typeof(TBody), "MulTexSet")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> MulTexSetTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        CodeMatcher codeMatcher = new CodeMatcher(instructions);
        return codeMatcher.MatchForward(false, new CodeMatch(OpCodes.Brfalse))
            .Advance(1)
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ret))
            .InstructionEnumeration();
    }
}
