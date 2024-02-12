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
using COM3D2.NPRShader.Managed;

public static class NPRShaderAdd
{
    static Harmony instance;
    public static string[] _BASEFRPROP;
    public static string[] _BASECOLORPROP;
    public static string[] _SHADERKEYWORD;
    public static List<string> _TOGGLEPROP;
    private static FieldInfo HDREnabled;
    private static float k_MaxForOverexposedColor = 191.0f / 255;

    public static void Main()
    {
        LoadShaderConfig();
        instance = Harmony.CreateAndPatchAll(typeof(NPRShaderAdd));
        new TryPatchSetTexture(instance);
        new TryPatchSceneCaptureAddition(instance);
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

    public static void SetTexture(Material mat, string name, Texture2D tex)
    {
        mat.SetTexture(name, tex);
        if (name.ToLower().Contains("cube") && mat.GetTexture(name) == null)
        {
            mat.SetTexture(name, CubemapConverter.ByTexture2D(tex));
        }
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

    [HarmonyPatch(typeof(AssetLoader), "ReadMaterialWithSetShader")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> ReadMaterialWithSetShaderTranspiler(IEnumerable<CodeInstruction> instructions)
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

    [HarmonyPatch(typeof(NPRShaderManaged), "ChangeNPRSMaterial")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> ChangeNPRSMaterialTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        CodeMatcher codeMatcher = new CodeMatcher(instructions);
        codeMatcher.MatchForward(false, new CodeMatch(OpCodes.Newobj));
        codeMatcher.RemoveInstruction()
            .MatchForward(false, new CodeMatch(OpCodes.Callvirt, AccessTools.PropertySetter(typeof(UnityEngine.Object), "name")))
            .RemoveInstructionsWithOffsets(1, 18);
        return codeMatcher.InstructionEnumeration();
    }

    [HarmonyPatch(typeof(MaterialPane), "getMaterial")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> MaterialGetMaterialTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        CodeMatcher codeMatcher = new CodeMatcher(instructions);
        codeMatcher.MatchForward(false, new CodeMatch(OpCodes.Newobj));
        var newObj = codeMatcher.Instruction;
        codeMatcher.RemoveInstruction()
            .MatchForward(false, new CodeMatch(OpCodes.Ldelem_Ref))
            .Advance(1)
            .Insert(newObj);
        return codeMatcher.InstructionEnumeration();
    }

    [HarmonyPatch(typeof(ObjectPane), "getMaterial")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> ObjectGetMaterialTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        CodeMatcher codeMatcher = new CodeMatcher(instructions);
        codeMatcher.MatchForward(false, new CodeMatch(OpCodes.Newobj));
        var newObj = codeMatcher.Instruction;
        codeMatcher.RemoveInstruction()
            .MatchForward(false, new CodeMatch(OpCodes.Ldarg_0))
            .RemoveInstructionsWithOffsets(0, 2)
            .MatchForward(false, new CodeMatch(OpCodes.Ldfld))
            .MatchForward(false, new CodeMatch(OpCodes.Ldelem_Ref))
            .Advance(1)
            .Insert(newObj);
        return codeMatcher.InstructionEnumeration();
    }

    [HarmonyPatch(typeof(MaterialPane), "setMaterial")]
    [HarmonyPatch(typeof(ObjectPane), "setMaterial")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> SetMaterialTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        CodeMatcher codeMatcher = new CodeMatcher(instructions);
        codeMatcher.MatchForward(false, new CodeMatch(OpCodes.Newobj)).RemoveInstruction();
        return codeMatcher.InstructionEnumeration();
    }

    [HarmonyPatch(typeof(MaterialPane), "resetMaterial")]
    [HarmonyPatch(typeof(ObjectPane), "resetMaterial")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> ResetMaterialTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        CodeMatcher codeMatcher = new CodeMatcher(instructions);
        codeMatcher.MatchForward(false, new CodeMatch(OpCodes.Newobj)).RemoveInstruction();
        return codeMatcher.InstructionEnumeration();
    }

    public class CubemapConverter
    {
        public static Cubemap ByTexture2D(Texture2D tempTex)
        {
            if (Math.Abs((float)tempTex.width / tempTex.height - 2) < 0.05)
            {
                return ByLatLongTexture2D(tempTex);
            }
            return ByCubeTexture2D(tempTex);
        }

        public static Cubemap ByCubeTexture2D(Texture2D tempTex)
        {
            tempTex = FlipPixels(tempTex, false, true);
            if (Math.Round((float)tempTex.width / tempTex.height) == 6)
            {
                int everyW = (int)(tempTex.width / 6f);
                int cubeMapSize = Mathf.Min(everyW, tempTex.height);
                Cubemap cubemap = new Cubemap(cubeMapSize, TextureFormat.RGBA32, false);
                cubemap.SetPixels(tempTex.GetPixels(0, 0, cubeMapSize, cubeMapSize), CubemapFace.PositiveX);
                cubemap.SetPixels(tempTex.GetPixels(cubeMapSize, 0, cubeMapSize, cubeMapSize), CubemapFace.NegativeX);
                cubemap.SetPixels(tempTex.GetPixels(2 * cubeMapSize, 0, cubeMapSize, cubeMapSize), CubemapFace.PositiveY);
                cubemap.SetPixels(tempTex.GetPixels(3 * cubeMapSize, 0, cubeMapSize, cubeMapSize), CubemapFace.NegativeY);
                cubemap.SetPixels(tempTex.GetPixels(4 * cubeMapSize, 0, cubeMapSize, cubeMapSize), CubemapFace.PositiveZ);
                cubemap.SetPixels(tempTex.GetPixels(5 * cubeMapSize, 0, cubeMapSize, cubeMapSize), CubemapFace.NegativeZ);
                cubemap.Apply();
                return cubemap;
            }
            else if (Math.Round((float)tempTex.height / tempTex.width) == 6)
            {
                int everyH = (int)(tempTex.height / 6f);
                int cubeMapSize = Mathf.Min(tempTex.width, everyH);
                Cubemap cubemap = new Cubemap(cubeMapSize, TextureFormat.RGBA32, false);
                cubemap.SetPixels(tempTex.GetPixels(0, 0, cubeMapSize, cubeMapSize), CubemapFace.PositiveX);
                cubemap.SetPixels(tempTex.GetPixels(0, cubeMapSize, cubeMapSize, cubeMapSize), CubemapFace.NegativeX);
                cubemap.SetPixels(tempTex.GetPixels(0, 2 * cubeMapSize, cubeMapSize, cubeMapSize), CubemapFace.PositiveY);
                cubemap.SetPixels(tempTex.GetPixels(0, 3 * cubeMapSize, cubeMapSize, cubeMapSize), CubemapFace.NegativeY);
                cubemap.SetPixels(tempTex.GetPixels(0, 4 * cubeMapSize, cubeMapSize, cubeMapSize), CubemapFace.PositiveZ);
                cubemap.SetPixels(tempTex.GetPixels(0, 5 * cubeMapSize, cubeMapSize, cubeMapSize), CubemapFace.NegativeZ);
                cubemap.Apply();
                return cubemap;
            }
            else if (Math.Abs((float)tempTex.width / tempTex.height - 4.0 / 3.0) < 0.05)
            {
                int everyW = (int)(tempTex.width / 4f);
                int everyH = (int)(tempTex.height / 3f);
                int cubeMapSize = Mathf.Min(everyW, everyH);
                Cubemap cubemap = new Cubemap(cubeMapSize, TextureFormat.RGBA32, false);
                cubemap.SetPixels(tempTex.GetPixels(cubeMapSize, 0, cubeMapSize, cubeMapSize), CubemapFace.PositiveY);
                cubemap.SetPixels(tempTex.GetPixels(0, cubeMapSize, cubeMapSize, cubeMapSize), CubemapFace.NegativeX);
                cubemap.SetPixels(tempTex.GetPixels(cubeMapSize, cubeMapSize, cubeMapSize, cubeMapSize), CubemapFace.PositiveZ);
                cubemap.SetPixels(tempTex.GetPixels(2 * cubeMapSize, cubeMapSize, cubeMapSize, cubeMapSize), CubemapFace.PositiveX);
                cubemap.SetPixels(tempTex.GetPixels(3 * cubeMapSize, cubeMapSize, cubeMapSize, cubeMapSize), CubemapFace.NegativeZ);
                cubemap.SetPixels(tempTex.GetPixels(cubeMapSize, 2 * cubeMapSize, cubeMapSize, cubeMapSize), CubemapFace.NegativeY);
                cubemap.Apply();
                return cubemap;
            }
            else if (Math.Abs((float)tempTex.height / tempTex.width - 4.0 / 3.0) < 0.05)
            {
                int everyW = (int)(tempTex.width / 3f);
                int everyH = (int)(tempTex.height / 4f);
                int cubeMapSize = Mathf.Min(everyW, everyH);
                Cubemap cubemap = new Cubemap(cubeMapSize, TextureFormat.RGBA32, false);
                cubemap.SetPixels(tempTex.GetPixels(cubeMapSize, 0, cubeMapSize, cubeMapSize), CubemapFace.PositiveY);
                cubemap.SetPixels(tempTex.GetPixels(0, cubeMapSize, cubeMapSize, cubeMapSize), CubemapFace.NegativeX);
                cubemap.SetPixels(tempTex.GetPixels(cubeMapSize, cubeMapSize, cubeMapSize, cubeMapSize), CubemapFace.PositiveZ);
                cubemap.SetPixels(tempTex.GetPixels(2 * cubeMapSize, cubeMapSize, cubeMapSize, cubeMapSize), CubemapFace.PositiveX);
                cubemap.SetPixels(tempTex.GetPixels(cubeMapSize, 2 * cubeMapSize, cubeMapSize, cubeMapSize), CubemapFace.NegativeY);
                cubemap.SetPixels(tempTex.GetPixels(cubeMapSize, 3 * cubeMapSize, cubeMapSize, cubeMapSize), CubemapFace.NegativeZ);
                cubemap.Apply();
                return cubemap;
            }
            Debug.LogWarning($"Cannot be converted to Cubemap: {tempTex} ({tempTex.width}x{tempTex.height})");
            return null;
        }

        // from https://assetstore.unity.com/packages/tools/utilities/panorama-to-cubemap-13616
        public static Cubemap ByLatLongTexture2D(Texture2D tempTex)
        {
            int everyW = (int)(tempTex.width / 4f);
            int everyH = (int)(tempTex.height / 3f);
            int cubeMapSize = Mathf.Min(everyW, everyH);
            Cubemap cubemap = new Cubemap(cubeMapSize, TextureFormat.RGBA32, false);
            cubemap.SetPixels(CreateCubemapTexture(tempTex, cubeMapSize, 0).GetPixels(), CubemapFace.NegativeX);
            cubemap.SetPixels(CreateCubemapTexture(tempTex, cubeMapSize, 1).GetPixels(), CubemapFace.PositiveX);
            cubemap.SetPixels(CreateCubemapTexture(tempTex, cubeMapSize, 2).GetPixels(), CubemapFace.PositiveZ);
            cubemap.SetPixels(CreateCubemapTexture(tempTex, cubeMapSize, 3).GetPixels(), CubemapFace.NegativeZ);
            cubemap.SetPixels(CreateCubemapTexture(tempTex, cubeMapSize, 4).GetPixels(), CubemapFace.PositiveY);
            cubemap.SetPixels(CreateCubemapTexture(tempTex, cubeMapSize, 5).GetPixels(), CubemapFace.NegativeY);
            cubemap.Apply();
            return cubemap;
        }

        static Texture2D CreateCubemapTexture(Texture2D m_srcTexture, int texSize, int faceIndex)
        {
            Texture2D tex = new Texture2D(texSize, texSize, TextureFormat.RGB24, false);

            Vector3[] vDirA = new Vector3[4];
            if (faceIndex == 0)
            {
                vDirA[0] = new Vector3(-1.0f, 1.0f, -1.0f);
                vDirA[1] = new Vector3(1.0f, 1.0f, -1.0f);
                vDirA[2] = new Vector3(-1.0f, -1.0f, -1.0f);
                vDirA[3] = new Vector3(1.0f, -1.0f, -1.0f);
            }
            if (faceIndex == 1)
            {
                vDirA[0] = new Vector3(1.0f, 1.0f, 1.0f);
                vDirA[1] = new Vector3(-1.0f, 1.0f, 1.0f);
                vDirA[2] = new Vector3(1.0f, -1.0f, 1.0f);
                vDirA[3] = new Vector3(-1.0f, -1.0f, 1.0f);
            }
            if (faceIndex == 2)
            {
                vDirA[0] = new Vector3(1.0f, 1.0f, -1.0f);
                vDirA[1] = new Vector3(1.0f, 1.0f, 1.0f);
                vDirA[2] = new Vector3(1.0f, -1.0f, -1.0f);
                vDirA[3] = new Vector3(1.0f, -1.0f, 1.0f);
            }
            if (faceIndex == 3)
            {
                vDirA[0] = new Vector3(-1.0f, 1.0f, 1.0f);
                vDirA[1] = new Vector3(-1.0f, 1.0f, -1.0f);
                vDirA[2] = new Vector3(-1.0f, -1.0f, 1.0f);
                vDirA[3] = new Vector3(-1.0f, -1.0f, -1.0f);
            }
            if (faceIndex == 4)
            {
                vDirA[0] = new Vector3(-1.0f, 1.0f, -1.0f);
                vDirA[1] = new Vector3(-1.0f, 1.0f, 1.0f);
                vDirA[2] = new Vector3(1.0f, 1.0f, -1.0f);
                vDirA[3] = new Vector3(1.0f, 1.0f, 1.0f);
            }
            if (faceIndex == 5)
            {
                vDirA[0] = new Vector3(1.0f, -1.0f, -1.0f);
                vDirA[1] = new Vector3(1.0f, -1.0f, 1.0f);
                vDirA[2] = new Vector3(-1.0f, -1.0f, -1.0f);
                vDirA[3] = new Vector3(-1.0f, -1.0f, 1.0f);
            }
            Vector3 rotDX1 = (vDirA[1] - vDirA[0]) / (float)texSize;
            Vector3 rotDX2 = (vDirA[3] - vDirA[2]) / (float)texSize;
            float dy = 1.0f / (float)texSize;
            float fy = 0.0f;
            Color[] cols = new Color[texSize];
            for (int y = 0; y < texSize; y++)
            {
                Vector3 xv1 = vDirA[0];
                Vector3 xv2 = vDirA[2];
                for (int x = 0; x < texSize; x++)
                {
                    Vector3 v = ((xv2 - xv1) * fy) + xv1;
                    v.Normalize();
                    cols[x] = CalcProjectionSpherical(m_srcTexture, v);
                    xv1 += rotDX1;
                    xv2 += rotDX2;
                }
                tex.SetPixels(0, y, texSize, 1, cols);
                fy += dy;
            }
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();
            return tex;
        }

        static Color CalcProjectionSpherical(Texture2D m_srcTexture, Vector3 vDir)
        {
            float theta = Mathf.Atan2(vDir.z, vDir.x);  // -π ～ +π (vertical rotation)
            float phi = Mathf.Acos(vDir.y);             //  0  ～ +π (horizontal rotation)

            while (theta < -Mathf.PI) theta += Mathf.PI + Mathf.PI;
            while (theta > Mathf.PI) theta -= Mathf.PI + Mathf.PI;

            float dx = theta / Mathf.PI;        // -1.0 ～ +1.0.
            float dy = phi / Mathf.PI;          //  0.0 ～ +1.0.

            dx = dx * 0.5f + 0.5f;
            int px = (int)(dx * (float)m_srcTexture.width);
            if (px < 0)
            {
                px = 0;
            }
            if (px >= m_srcTexture.width)
            {
                px = m_srcTexture.width - 1;
            }
            int py = (int)(dy * (float)m_srcTexture.height);
            if (py < 0)
            {
                py = 0;
            }
            if (py >= m_srcTexture.height)
            {
                py = m_srcTexture.height - 1;
            }
            Color col = m_srcTexture.GetPixel(px, m_srcTexture.height - py - 1);
            return col;
        }

        static Texture2D FlipPixels(Texture2D texture, bool flipX, bool flipY)
        {
            if (!flipX && !flipY)
            {
                return texture;
            }
            if (flipX)
            {
                for (int i = 0; i < texture.width / 2; i++)
                {
                    for (int j = 0; j < texture.height; j++)
                    {
                        Color tempC = texture.GetPixel(i, j);
                        texture.SetPixel(i, j, texture.GetPixel(texture.width - 1 - i, j));
                        texture.SetPixel(texture.width - 1 - i, j, tempC);
                    }
                }
            }
            if (flipY)
            {
                for (int i = 0; i < texture.width; i++)
                {
                    for (int j = 0; j < texture.height / 2; j++)
                    {
                        Color tempC = texture.GetPixel(i, j);
                        texture.SetPixel(i, j, texture.GetPixel(i, texture.height - 1 - j));
                        texture.SetPixel(i, texture.height - 1 - j, tempC);
                    }
                }
            }
            texture.Apply();
            return texture;
        }
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
            catch {}
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

    class TryPatchSetTexture : TryPatch
    {
        public TryPatchSetTexture(Harmony harmony, int failLimit = 3) : base(harmony, failLimit) {}

        public override bool Patch()
        {
            var transpiler = SymbolExtensions.GetMethodInfo((IEnumerable<CodeInstruction> instructions) => ReadMaterialTranspiler(instructions));
            Type t = AccessTools.TypeByName("COM3D2.MovieTexture.Plugin.MovieTexturePatcher");
            if (t != null)
            {
                MethodInfo targetMethod = t.GetMethod("SetTexture");
                harmony.Patch(targetMethod, transpiler: new HarmonyMethod(transpiler));
            }
            else
            {
                MethodInfo targetMethod1 = AccessTools.Method(typeof(NPRShader), "ReadMaterial");
                MethodInfo targetMethod2 = AccessTools.Method(typeof(AssetLoader), "ReadMaterialWithSetShader");
                harmony.Patch(targetMethod1, transpiler: new HarmonyMethod(transpiler));
                harmony.Patch(targetMethod2, transpiler: new HarmonyMethod(transpiler));
            }
            return true;
        }

        public static IEnumerable<CodeInstruction> ReadMaterialTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            CodeMatcher codeMatcher = new CodeMatcher(instructions);
            CodeMatch[] matchSetTexture = {
                new CodeMatch(OpCodes.Callvirt, typeof(Material).GetMethod("SetTexture", new[] {typeof(string), typeof(Texture)}))
            };
            codeMatcher.End();
            codeMatcher.MatchBack(false, matchSetTexture)
                .SetInstruction(new CodeInstruction(OpCodes.Call, typeof(NPRShaderAdd).GetMethod("SetTexture")));
            return codeMatcher.InstructionEnumeration();
        }
    }

    class TryPatchSceneCaptureAddition : TryPatch
    {
        public TryPatchSceneCaptureAddition(Harmony harmony, int failLimit = 3) : base(harmony, failLimit) {}

        public override bool Patch()
        {
            Type t = AccessTools.TypeByName("COM3D2.SceneCaptureAddition.Plugin.SceneCaptureAddition");
            if (t != null)
            {
                HDREnabled = AccessTools.Field(t, "HDREnabled");
                return true;
            }
            return false;
        }

    }
}
