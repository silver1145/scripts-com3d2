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
using System.Text;

public static class NPRShaderAdd
{
    static Harmony instance;
    public static string[] _BASEFRPROP;
    public static string[] _BASECOLORPROP;
    public static string[] _SHADERKEYWORD;
    public static List<string> _TOGGLEPROP;
    public static Dictionary<string, string> shaderMatNames;
    private static FieldInfo HDREnabled;
    private static float k_MaxForOverexposedColor = 191.0f / 255;

    public static void Main()
    {
        LoadShaderConfig();
        LoadShaderMatNames();
        instance = Harmony.CreateAndPatchAll(typeof(NPRShaderAdd));
        new TryPatchSetTexture(instance);
        new TryPatchSceneCaptureAddition(instance);
    }

    public static void Unload()
    {
        instance.UnpatchSelf();
        instance = null;
        _BASEFRPROP = null;
        _BASECOLORPROP = null;
        _SHADERKEYWORD = null;
        _TOGGLEPROP?.Clear();
        _TOGGLEPROP = null;
        shaderMatNames?.Clear();
        shaderMatNames = null;
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

    public static bool IsKeyword(string prop, float value, Material mat)
    {
        if (mat.shader == null || mat.shader.name.ToLower().Contains("nprtoon"))
        {
            return true;
        }
        if (prop.EndsWith("_SSKEYWORD", StringComparison.OrdinalIgnoreCase))
        {
            string keyword = prop.ToUpper().Replace("_SSKEYWORD", string.Empty);
            if (value == 1f)
            {
                mat.EnableKeyword(keyword);
            }
            else
            {
                mat.DisableKeyword(keyword);
            }
            return false;
        }
        if (Array.Exists(_SHADERKEYWORD, element => element == prop))
        {
            if (value == 1f)
            {
                mat.EnableKeyword(prop);
            }
            else
            {
                mat.DisableKeyword(prop);
            }
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
        if (name.ToLower().Contains("cubetex"))
        {
            mat.SetTexture(name, CubemapConverter.ByTexture2D(tex));
        }
        else
        {
            mat.SetTexture(name, tex);
        }
    }

    public static bool TrySetCubeMap(string texType, string prop, BinaryReader br, Material mat, TBodySkin bodyskin)
    {
        if (texType != "cube")
        {
            return false;
        }
        string texName = br.ReadString();
        br.ReadString();    // Tex Path
        Texture2D tex = ImportCM.CreateTexture(texName + ".tex");
        Cubemap cube = CubemapConverter.ByTexture2D(tex);
        UnityEngine.Object.Destroy(tex);
        mat.SetTexture(prop, cube);
        if (cube != null)
        {
            cube.name = texName;
            if (bodyskin != null)
            {
                bodyskin.listDEL.Add(cube);
            }
        }
        
        br.ReadSingle();    // Offset.x
        br.ReadSingle();    // Offset.y
        br.ReadSingle();    // Scale.x
        br.ReadSingle();    // Scale.x
        return true;
    }

    public static void LoadShaderMatNames()
    {
        if (shaderMatNames == null)
        {
            shaderMatNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        foreach (var p in AssetLoader.m_dicCacheMaterial)
        {
            if (p.Value != null)
            {
                shaderMatNames[p.Value.shader.name] = p.Key;
            }
        }
    }

    public static void AddCacheMaterial(Material material)
    {
        string filename = material.name;
        if (filename.ToLower().StartsWith("com3d2mod_"))
        {
            filename = filename.Substring(10);
        }
        AssetLoader.m_dicCacheMaterial[filename] = material;
    }

    public static bool IsSSMaterial(string filename)
    {
        if (GameUty.FileSystem.IsExistentFile(filename))
        {
            try
            {
                byte[] data;
                using (var f = GameUty.FileOpen(filename))
                {
                    data = f.ReadAll();
                }
                using (BinaryReader binaryReader = new BinaryReader(new MemoryStream(data), Encoding.UTF8))
                {
                    binaryReader.ReadString();  // CM3D2_MATERIAL
                    binaryReader.ReadInt32();   // Version
                    binaryReader.ReadString();  // Mate Name
                    binaryReader.ReadString();  // Pmat
                    string shaderName = binaryReader.ReadString();
                    return shaderMatNames.ContainsKey(shaderName);
                }
            }
            catch {}
        }
        return false;
    }

    public static bool IsInfinityColorSlot(string slotname)
    {
        slotname = slotname.ToLower();
        return slotname.StartsWith("hair") || slotname == "head" || slotname == "body";
    }

    public static string GetFileName(string filePath)
    {
        if (filePath != null)
        {
            return Path.GetFileName(filePath);
        }
        return null;
    }

    public static IEnumerable<CodeInstruction> FixLeaveTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        CodeMatcher codeMatcher = new CodeMatcher(instructions);
        codeMatcher.Start();
        while (codeMatcher.IsValid)
        {
            codeMatcher.MatchForward(false, new CodeMatch(OpCodes.Leave))
                .Advance(1)
                .InsertAndAdvance(new CodeInstruction(OpCodes.Nop));
        }
        return codeMatcher.InstructionEnumeration();
    }

    [HarmonyPatch(typeof(COM3D2.NPRShader.Plugin.Util), "LoadALLShaders")]
    [HarmonyPrefix]
    public static void LoadALLShadersPrefix()
    {
        AssetLoader.m_dicCacheMaterial = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
    }

    [HarmonyPatch(typeof(COM3D2.NPRShader.Plugin.Util), "LoadALLShaders")]
    [HarmonyPostfix]
    public static void LoadALLShadersPostfix()
    {
        LoadShaderMatNames();
    }

    [HarmonyPatch(typeof(COM3D2.NPRShader.Plugin.Util), "LoadALLShaders")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> LoadALLShadersTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        CodeMatcher codeMatcher = new CodeMatcher(instructions)
            .MatchForward(false, new CodeMatch(OpCodes.Ldc_I4_1))
            .Advance(-1)
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldloc_S, 4))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(NPRShaderAdd), nameof(AddCacheMaterial))));
        return codeMatcher.InstructionEnumeration();
    }

    [HarmonyPatch(typeof(NPRShader), "ReadMaterial")]
    [HarmonyPatch(typeof(AssetLoader), "ReadMaterialWithSetShader")]
    [HarmonyPrefix]
    public static void ReadMaterialPrefix(BinaryReader r, ref string shaderMatName)
    {
        if (AssetLoader.m_dicCacheMaterial.ContainsKey(shaderMatName))
        {
            return;
        }
        long seekPos = r.BaseStream.Position;
        using (BinaryReader br = new BinaryReader(r.BaseStream))
        {
            try
            {
                br.ReadString(); // Pmat
                string shaderName = br.ReadString();
                if (shaderMatNames.TryGetValue(shaderName, out var name))
                {
                    shaderMatName = name;
                }
            }
            catch {}
            br.m_stream = null;
        }
        r.BaseStream.Seek(seekPos, SeekOrigin.Begin);
    }

    [HarmonyPatch(typeof(NPRShader), "ReadMaterial")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> ReadMaterialTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        CodeMatcher codeMatcher = new CodeMatcher(FixLeaveTranspiler(instructions));
        codeMatcher.MatchForward(false, new CodeMatch(OpCodes.Ldstr, "null")).Advance(-1);
        object endLabel = codeMatcher.InstructionAt(8).operand;
        codeMatcher.InsertAndAdvance(codeMatcher.Instruction)
            .InsertAndAdvance(codeMatcher.InstructionAt(5))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
            .InsertAndAdvance(codeMatcher.InstructionAt(4))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_2))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(NPRShaderAdd), nameof(TrySetCubeMap))))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Brtrue_S, endLabel));
        CodeMatch[] matchSetColor = {
            new CodeMatch(OpCodes.Callvirt, typeof(Material).GetMethod("SetColor", new[] {typeof(string), typeof(Color)}))
        };
        codeMatcher.MatchForward(false, matchSetColor)
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call, typeof(NPRShaderAdd).GetMethod(nameof(CorrectHDRColor))));
        codeMatcher.MatchForward(false, new CodeMatch(OpCodes.Ldstr, "Toggle"))
            .Advance(2)
            .InsertAndAdvance(new CodeInstruction(codeMatcher.InstructionAt(-3)))
            .InsertAndAdvance(new CodeInstruction(codeMatcher.InstructionAt(1)))
            .InsertAndAdvance(new CodeInstruction(codeMatcher.InstructionAt(4)))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call, typeof(NPRShaderAdd).GetMethod(nameof(IsKeyword))))
            .InsertAndAdvance(new CodeInstruction(OpCodes.And));
        return codeMatcher.InstructionEnumeration();
    }

    [HarmonyPatch(typeof(AssetLoader), "ReadMaterialWithSetShader")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> ReadMaterialWithSetShaderTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        CodeMatcher codeMatcher = new CodeMatcher(FixLeaveTranspiler(instructions));
        codeMatcher.MatchForward(false, new CodeMatch(OpCodes.Ldstr, "null")).Advance(-1);
        object endLabel = codeMatcher.InstructionAt(8).operand;
        codeMatcher.InsertAndAdvance(codeMatcher.Instruction)
            .InsertAndAdvance(codeMatcher.InstructionAt(5))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
            .InsertAndAdvance(codeMatcher.InstructionAt(4))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldnull))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(NPRShaderAdd), nameof(TrySetCubeMap))))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Brtrue_S, endLabel));
        CodeMatch[] matchSetColor = {
            new CodeMatch(OpCodes.Callvirt, typeof(Material).GetMethod("SetColor", new[] {typeof(string), typeof(Color)}))
        };
        codeMatcher.MatchForward(false, matchSetColor)
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call, typeof(NPRShaderAdd).GetMethod(nameof(CorrectHDRColor))));
        codeMatcher.MatchForward(false, new CodeMatch(OpCodes.Ldstr, "Toggle"))
            .Advance(2)
            .InsertAndAdvance(new CodeInstruction(codeMatcher.InstructionAt(-3)))
            .InsertAndAdvance(new CodeInstruction(codeMatcher.InstructionAt(1)))
            .InsertAndAdvance(new CodeInstruction(codeMatcher.InstructionAt(4)))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call, typeof(NPRShaderAdd).GetMethod(nameof(IsKeyword))))
            .InsertAndAdvance(new CodeInstruction(OpCodes.And));
        return codeMatcher.InstructionEnumeration();
    }

    [HarmonyPatch(typeof(NPRShader), "ReadMaterial")]
    [HarmonyPatch(typeof(AssetLoader), "ReadMaterial")]
    [HarmonyPatch(typeof(AssetLoader), "ReadMaterialWithSetShader")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> ReadMaterialPmatFix(IEnumerable<CodeInstruction> instructions)
    {
        CodeMatcher codeMatcher = new CodeMatcher(FixLeaveTranspiler(instructions));
        codeMatcher.MatchForward(false, new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(GameUty), "FileOpen"))).Advance(-1)
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(NPRShaderAdd), nameof(GetFileName))));
        codeMatcher.MatchForward(false, new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(AFileBase), "IsValid")))
            .InsertAndAdvance(codeMatcher.InstructionAt(-1))
            .InsertAndAdvance(codeMatcher.InstructionAt(1));
        codeMatcher.MatchForward(false, new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(GameUty), "FileOpen"))).Advance(-1)
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(NPRShaderAdd), nameof(GetFileName))));
        codeMatcher.MatchForward(false, new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(AFileBase), "IsValid")))
            .InsertAndAdvance(codeMatcher.InstructionAt(-1))
            .InsertAndAdvance(codeMatcher.InstructionAt(1));
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
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call, typeof(NPRShaderAdd).GetMethod(nameof(IsToggle))))
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
    public static IEnumerable<CodeInstruction> ChangeNPRSMaterialTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        CodeMatcher codeMatcher = new CodeMatcher(instructions, generator);
        Label label;
        codeMatcher.MatchForward(false, new CodeMatch(OpCodes.Brtrue))
            .InsertAndAdvance(codeMatcher.Instruction)
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_3))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(NPRShaderAdd), nameof(IsSSMaterial))));
        codeMatcher.MatchForward(false, new CodeMatch(OpCodes.Newobj))
            .RemoveInstruction();
        codeMatcher.MatchForward(false, new CodeMatch(OpCodes.Blt))
            .Advance(5)
            .CreateLabel(out label)
            .Advance(-4)
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_1))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(NPRShaderAdd), nameof(IsInfinityColorSlot))))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Brtrue, label));
        codeMatcher.MatchForward(false, new CodeMatch(OpCodes.Callvirt, AccessTools.PropertySetter(typeof(UnityEngine.Object), "name")))
            .RemoveInstructionsWithOffsets(1, 18);
        return codeMatcher.InstructionEnumeration();
    }

    [HarmonyPatch(typeof(ObjectWindow), "UpdaateMaterial")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> UpdaateMaterialTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        CodeMatcher codeMatcher = new CodeMatcher(FixLeaveTranspiler(instructions));
        codeMatcher.MatchForward(false, new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(string), "Contains")))
        .Advance(1)
            .Insert(new CodeInstruction(OpCodes.Or))
            .Insert(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(NPRShaderAdd), nameof(IsSSMaterial))))
            .Insert(codeMatcher.InstructionAt(-5))
            .Insert(codeMatcher.InstructionAt(-6));
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
        private static readonly Vector3[][] FaceDirections = new Vector3[][]
        {
            new[] { new Vector3(-1f, 1f, -1f), new Vector3(1f, 1f, -1f), new Vector3(-1f, -1f, -1f), new Vector3(1f, -1f, -1f) },
            new[] { new Vector3(1f, 1f, 1f), new Vector3(-1f, 1f, 1f), new Vector3(1f, -1f, 1f), new Vector3(-1f, -1f, 1f) },
            new[] { new Vector3(1f, 1f, -1f), new Vector3(1f, 1f, 1f), new Vector3(1f, -1f, -1f), new Vector3(1f, -1f, 1f) },
            new[] { new Vector3(-1f, 1f, 1f), new Vector3(-1f, 1f, -1f), new Vector3(-1f, -1f, 1f), new Vector3(-1f, -1f, -1f) },
            new[] { new Vector3(-1f, 1f, -1f), new Vector3(-1f, 1f, 1f), new Vector3(1f, 1f, -1f), new Vector3(1f, 1f, 1f) },
            new[] { new Vector3(1f, -1f, -1f), new Vector3(1f, -1f, 1f), new Vector3(-1f, -1f, -1f), new Vector3(-1f, -1f, 1f) },
        };

        public static Cubemap ByTexture2D(Texture2D originTex)
        {
            float ratio = (float)originTex.width / originTex.height;
            if (Math.Abs(ratio - 2) < 0.05)
            {
                return ByLatLongTexture2D(originTex);
            }
            if (Math.Abs(ratio - 1) < 0.05)
            {
                return BySphereTexture2D(originTex);
            }
            return ByCubeTexture2D(originTex);
        }

        public static Cubemap ByCubeTexture2D(Texture2D originTex)
        {
            int w = originTex.width;
            int h = originTex.height;
            Color[] pixels = originTex.GetPixels();
            FlipPixelsY(pixels, w, h);
            Cubemap cubemap = null;
            float ratio = (float)w / h;
            if (Math.Round(ratio) == 6)
            {
                int everyW = (int)(w / 6f);
                int cubeMapSize = Mathf.Min(everyW, h);
                cubemap = new Cubemap(cubeMapSize, TextureFormat.RGBA32, false);
                cubemap.SetPixels(GetPixelBlock(pixels, w, 0, 0, cubeMapSize, cubeMapSize), CubemapFace.PositiveX);
                cubemap.SetPixels(GetPixelBlock(pixels, w, cubeMapSize, 0, cubeMapSize, cubeMapSize), CubemapFace.NegativeX);
                cubemap.SetPixels(GetPixelBlock(pixels, w, 2 * cubeMapSize, 0, cubeMapSize, cubeMapSize), CubemapFace.PositiveY);
                cubemap.SetPixels(GetPixelBlock(pixels, w, 3 * cubeMapSize, 0, cubeMapSize, cubeMapSize), CubemapFace.NegativeY);
                cubemap.SetPixels(GetPixelBlock(pixels, w, 4 * cubeMapSize, 0, cubeMapSize, cubeMapSize), CubemapFace.PositiveZ);
                cubemap.SetPixels(GetPixelBlock(pixels, w, 5 * cubeMapSize, 0, cubeMapSize, cubeMapSize), CubemapFace.NegativeZ);
                cubemap.Apply();
            }
            else if (Math.Abs(ratio - 1.0 / 6.0) < 0.05)
            {
                int everyH = (int)(h / 6f);
                int cubeMapSize = Mathf.Min(w, everyH);
                cubemap = new Cubemap(cubeMapSize, TextureFormat.RGBA32, false);
                cubemap.SetPixels(GetPixelBlock(pixels, w, 0, 0, cubeMapSize, cubeMapSize), CubemapFace.PositiveX);
                cubemap.SetPixels(GetPixelBlock(pixels, w, 0, cubeMapSize, cubeMapSize, cubeMapSize), CubemapFace.NegativeX);
                cubemap.SetPixels(GetPixelBlock(pixels, w, 0, 2 * cubeMapSize, cubeMapSize, cubeMapSize), CubemapFace.PositiveY);
                cubemap.SetPixels(GetPixelBlock(pixels, w, 0, 3 * cubeMapSize, cubeMapSize, cubeMapSize), CubemapFace.NegativeY);
                cubemap.SetPixels(GetPixelBlock(pixels, w, 0, 4 * cubeMapSize, cubeMapSize, cubeMapSize), CubemapFace.PositiveZ);
                cubemap.SetPixels(GetPixelBlock(pixels, w, 0, 5 * cubeMapSize, cubeMapSize, cubeMapSize), CubemapFace.NegativeZ);
                cubemap.Apply();
            }
            else if (Math.Abs(ratio - 4.0 / 3.0) < 0.05)
            {
                int everyW = (int)(w / 4f);
                int everyH = (int)(h / 3f);
                int cubeMapSize = Mathf.Min(everyW, everyH);
                cubemap = new Cubemap(cubeMapSize, TextureFormat.RGBA32, false);
                cubemap.SetPixels(GetPixelBlock(pixels, w, cubeMapSize, 0, cubeMapSize, cubeMapSize), CubemapFace.PositiveY);
                cubemap.SetPixels(GetPixelBlock(pixels, w, 0, cubeMapSize, cubeMapSize, cubeMapSize), CubemapFace.NegativeX);
                cubemap.SetPixels(GetPixelBlock(pixels, w, cubeMapSize, cubeMapSize, cubeMapSize, cubeMapSize), CubemapFace.PositiveZ);
                cubemap.SetPixels(GetPixelBlock(pixels, w, 2 * cubeMapSize, cubeMapSize, cubeMapSize, cubeMapSize), CubemapFace.PositiveX);
                cubemap.SetPixels(GetPixelBlock(pixels, w, 3 * cubeMapSize, cubeMapSize, cubeMapSize, cubeMapSize), CubemapFace.NegativeZ);
                cubemap.SetPixels(GetPixelBlock(pixels, w, cubeMapSize, 2 * cubeMapSize, cubeMapSize, cubeMapSize), CubemapFace.NegativeY);
                cubemap.Apply();
            }
            else if (Math.Abs(ratio - 3.0 / 4.0) < 0.05)
            {
                int everyW = (int)(w / 3f);
                int everyH = (int)(h / 4f);
                int cubeMapSize = Mathf.Min(everyW, everyH);
                cubemap = new Cubemap(cubeMapSize, TextureFormat.RGBA32, false);
                cubemap.SetPixels(GetPixelBlock(pixels, w, cubeMapSize, 0, cubeMapSize, cubeMapSize), CubemapFace.PositiveY);
                cubemap.SetPixels(GetPixelBlock(pixels, w, 0, cubeMapSize, cubeMapSize, cubeMapSize), CubemapFace.NegativeX);
                cubemap.SetPixels(GetPixelBlock(pixels, w, cubeMapSize, cubeMapSize, cubeMapSize, cubeMapSize), CubemapFace.PositiveZ);
                cubemap.SetPixels(GetPixelBlock(pixels, w, 2 * cubeMapSize, cubeMapSize, cubeMapSize, cubeMapSize), CubemapFace.PositiveX);
                cubemap.SetPixels(GetPixelBlock(pixels, w, cubeMapSize, 2 * cubeMapSize, cubeMapSize, cubeMapSize), CubemapFace.NegativeY);
                cubemap.SetPixels(GetPixelBlock(pixels, w, cubeMapSize, 3 * cubeMapSize, cubeMapSize, cubeMapSize), CubemapFace.NegativeZ);
                cubemap.Apply();
            }
            else
            {
                Debug.LogWarning($"Cannot be converted to Cubemap: {originTex} ({w}x{h})");
            }
            return cubemap;
        }

        static Vector3[] GetFaceDirections(int faceIndex)
        {
            return FaceDirections[faceIndex];
        }

        // from https://assetstore.unity.com/packages/tools/utilities/panorama-to-cubemap-13616
        public static Cubemap ByLatLongTexture2D(Texture2D originTex)
        {
            int everyW = (int)(originTex.width / 4f);
            int everyH = (int)(originTex.height / 3f);
            int cubeMapSize = Mathf.Min(everyW, everyH);
            Color[] srcPixels = originTex.GetPixels();
            int srcWidth = originTex.width;
            int srcHeight = originTex.height;
            Cubemap cubemap = new Cubemap(cubeMapSize, TextureFormat.RGBA32, false);
            cubemap.SetPixels(CreateFacePixelsLatLong(srcPixels, srcWidth, srcHeight, cubeMapSize, 0), CubemapFace.NegativeX);
            cubemap.SetPixels(CreateFacePixelsLatLong(srcPixels, srcWidth, srcHeight, cubeMapSize, 1), CubemapFace.PositiveX);
            cubemap.SetPixels(CreateFacePixelsLatLong(srcPixels, srcWidth, srcHeight, cubeMapSize, 2), CubemapFace.PositiveZ);
            cubemap.SetPixels(CreateFacePixelsLatLong(srcPixels, srcWidth, srcHeight, cubeMapSize, 3), CubemapFace.NegativeZ);
            cubemap.SetPixels(CreateFacePixelsLatLong(srcPixels, srcWidth, srcHeight, cubeMapSize, 4), CubemapFace.PositiveY);
            cubemap.SetPixels(CreateFacePixelsLatLong(srcPixels, srcWidth, srcHeight, cubeMapSize, 5), CubemapFace.NegativeY);
            cubemap.Apply();
            return cubemap;
        }

        static Color[] CreateFacePixelsLatLong(Color[] srcPixels, int srcWidth, int srcHeight, int texSize, int faceIndex)
        {
            Vector3[] vDirA = GetFaceDirections(faceIndex);
            Vector3 rotDX1 = (vDirA[1] - vDirA[0]) / texSize;
            Vector3 rotDX2 = (vDirA[3] - vDirA[2]) / texSize;
            float dy = 1.0f / texSize;
            float fy = 0.0f;
            Color[] facePixels = new Color[texSize * texSize];
            for (int y = 0; y < texSize; y++)
            {
                Vector3 xv1 = vDirA[0];
                Vector3 xv2 = vDirA[2];
                for (int x = 0; x < texSize; x++)
                {
                    Vector3 v = ((xv2 - xv1) * fy) + xv1;
                    v.Normalize();
                    facePixels[y * texSize + x] = CalcProjectionLatLong(srcPixels, srcWidth, srcHeight, v);
                    xv1 += rotDX1;
                    xv2 += rotDX2;
                }
                fy += dy;
            }
            return facePixels;
        }

        static Color CalcProjectionLatLong(Color[] srcPixels, int srcWidth, int srcHeight, Vector3 vDir)
        {
            float theta = Mathf.Atan2(vDir.z, vDir.x);  // -π ～ +π
            float phi = Mathf.Acos(vDir.y);             //  0  ～ +π

            while (theta < -Mathf.PI) theta += Mathf.PI + Mathf.PI;
            while (theta > Mathf.PI) theta -= Mathf.PI + Mathf.PI;

            float dx = theta / Mathf.PI;        // -1.0 ～ +1.0
            float dy = phi / Mathf.PI;          //  0.0 ～ +1.0

            dx = dx * 0.5f + 0.5f;
            int px = (int)(dx * srcWidth);
            if (px < 0) px = 0;
            if (px >= srcWidth) px = srcWidth - 1;
            int py = (int)(dy * srcHeight);
            if (py < 0) py = 0;
            if (py >= srcHeight) py = srcHeight - 1;
            return srcPixels[(srcHeight - py - 1) * srcWidth + px];
        }

        public static Cubemap BySphereTexture2D(Texture2D originTex)
        {
            int cubeMapSize = originTex.width / 2;
            Color[] srcPixels = originTex.GetPixels();
            int srcWidth = originTex.width;
            int srcHeight = originTex.height;
            Cubemap cubemap = new Cubemap(cubeMapSize, TextureFormat.RGBA32, false);
            cubemap.SetPixels(CreateFacePixelsSphere(srcPixels, srcWidth, srcHeight, cubeMapSize, 0), CubemapFace.NegativeX);
            cubemap.SetPixels(CreateFacePixelsSphere(srcPixels, srcWidth, srcHeight, cubeMapSize, 1), CubemapFace.PositiveX);
            cubemap.SetPixels(CreateFacePixelsSphere(srcPixels, srcWidth, srcHeight, cubeMapSize, 2), CubemapFace.PositiveZ);
            cubemap.SetPixels(CreateFacePixelsSphere(srcPixels, srcWidth, srcHeight, cubeMapSize, 3), CubemapFace.NegativeZ);
            cubemap.SetPixels(CreateFacePixelsSphere(srcPixels, srcWidth, srcHeight, cubeMapSize, 4), CubemapFace.PositiveY);
            cubemap.SetPixels(CreateFacePixelsSphere(srcPixels, srcWidth, srcHeight, cubeMapSize, 5), CubemapFace.NegativeY);
            cubemap.Apply();
            return cubemap;
        }

        static Color[] CreateFacePixelsSphere(Color[] srcPixels, int srcWidth, int srcHeight, int texSize, int faceIndex)
        {
            Vector3[] vDirA = GetFaceDirections(faceIndex);
            Vector3 rotDX1 = (vDirA[1] - vDirA[0]) / texSize;
            Vector3 rotDX2 = (vDirA[3] - vDirA[2]) / texSize;
            float dy = 1.0f / texSize;
            float fy = 0.0f;
            Color[] facePixels = new Color[texSize * texSize];
            for (int y = 0; y < texSize; y++)
            {
                Vector3 xv1 = vDirA[0];
                Vector3 xv2 = vDirA[2];
                for (int x = 0; x < texSize; x++)
                {
                    Vector3 v = ((xv2 - xv1) * fy) + xv1;
                    v.Normalize();
                    facePixels[y * texSize + x] = CalcProjectionSphere(srcPixels, srcWidth, srcHeight, v);
                    xv1 += rotDX1;
                    xv2 += rotDX2;
                }
                fy += dy;
            }
            return facePixels;
        }

        static Color CalcProjectionSphere(Color[] srcPixels, int srcWidth, int srcHeight, Vector3 vDir)
        {
            float sx = vDir.z;
            float sy = vDir.y;
            float sz = vDir.x;
            float m = 2.0f * Mathf.Sqrt(sx * sx + sy * sy + (sz + 1.0f) * (sz + 1.0f));
            float u, v;
            if (m > 0.001f)
            {
                u = sx / m + 0.5f;
                v = sy / m + 0.5f;
            }
            else
            {
                u = 0.5f;
                v = 0.5f;
            }
            int px = (int)(u * srcWidth);
            if (px < 0) px = 0;
            if (px >= srcWidth) px = srcWidth - 1;
            int py = (int)(v * srcHeight);
            if (py < 0) py = 0;
            if (py >= srcHeight) py = srcHeight - 1;
            return srcPixels[py * srcWidth + px];
        }

        static void FlipPixelsX(Color[] pixels, int w, int h)
        {
            for (int j = 0; j < h; j++)
            {
                int row = j * w;
                for (int i = 0; i < w / 2; i++)
                {
                    int left = row + i;
                    int right = row + (w - 1 - i);

                    Color temp = pixels[left];
                    pixels[left] = pixels[right];
                    pixels[right] = temp;
                }
            }
        }

        static void FlipPixelsY(Color[] pixels, int w, int h)
        {
            for (int j = 0; j < h / 2; j++)
            {
                int up = j * w;
                int down = (h - 1 - j) * w;
                for (int i = 0; i < w; i++)
                {
                    Color temp = pixels[up + i];
                    pixels[up + i] = pixels[down + i];
                    pixels[down + i] = temp;
                }
            }
        }

        static Color[] GetPixelBlock(Color[] pixels, int texWidth, int bx, int by, int bw, int bh)
        {
            Color[] block = new Color[bw * bh];
            for (int j = 0; j < bh; j++)
            {
                Array.Copy(pixels, (by + j) * texWidth + bx, block, j * bw, bw);
            }
            return block;
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
            var transpiler = AccessTools.Method(typeof(TryPatchSetTexture), nameof(ReadMaterialTranspiler));
            Type t = AccessTools.TypeByName("COM3D2.MovieTexture.Plugin.MovieTexturePatcher");
            if (t != null)
            {
                MethodInfo targetMethod = t.GetMethod("SetTexture");
                harmony.Patch(targetMethod, transpiler: new HarmonyMethod(transpiler));
            }
            else
            {
                MethodInfo targetMethod1 = AccessTools.Method(typeof(NPRShader), "ReadMaterial");
                MethodInfo targetMethod2 = AccessTools.Method(typeof(AssetLoader), "ReadMaterial");
                MethodInfo targetMethod3 = AccessTools.Method(typeof(AssetLoader), "ReadMaterialWithSetShader");
                harmony.Patch(targetMethod1, transpiler: new HarmonyMethod(transpiler));
                harmony.Patch(targetMethod2, transpiler: new HarmonyMethod(transpiler));
                harmony.Patch(targetMethod3, transpiler: new HarmonyMethod(transpiler));
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
                .SetInstruction(new CodeInstruction(OpCodes.Call, typeof(NPRShaderAdd).GetMethod(nameof(SetTexture))));
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
