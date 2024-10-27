// #author silver1145
// #name Pmat Extend
// #desc Allow Mate set RenderQueue (pmat)

using UnityEngine;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;

public static class PmatExtend
{
    static Harmony instance;

    public static void Main()
    {
        instance = Harmony.CreateAndPatchAll(typeof(PmatExtend));
        new TryPatchNPRShader(instance);
        new TryPatchSceneCapture(instance);
    }

    public static void Unload()
    {
        instance.UnpatchSelf();
        instance = null;
    }

    public static bool TrySetRenderQueue(Material m)
    {
        if (int.TryParse(m.name, out int renderQueue) && renderQueue >= -1 && renderQueue <= 5000)
        {
            if (renderQueue != -1) 
            {
                m.SetFloat("_SetManualRenderQueue", renderQueue);
            }
            m.renderQueue = renderQueue;
            return false;
        }
        return true;
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

    public static IEnumerable<CodeInstruction> ReadMaterialRQTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        CodeMatcher codeMatcher = new CodeMatcher(instructions);
        codeMatcher.MatchForward(false, new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(UnityEngine.Object), "set_name")))
            .MatchForward(false, new CodeMatch(OpCodes.Brfalse));
        var brfalse = codeMatcher.Instruction;
        codeMatcher.MatchBack(false, new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(UnityEngine.Object), "set_name")))
            .Advance(1)
            .InsertAndAdvance(codeMatcher.InstructionAt(-3))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PmatExtend), nameof(TrySetRenderQueue))))
            .InsertAndAdvance(brfalse);
        return codeMatcher.InstructionEnumeration();
    }

    [HarmonyPatch(typeof(ImportCM), "ReadMaterial")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> ReadMaterialTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        return ReadMaterialRQTranspiler(instructions);
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
            catch { }
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

    class TryPatchNPRShader : TryPatch
    {
        public TryPatchNPRShader(Harmony harmony, int failLimit = 1) : base(harmony, failLimit) { }

        public override bool Patch()
        {
            if (AccessTools.TypeByName("COM3D2.NPRShader.Plugin.NPRShader") == null)
            {
                return false;
            }
            var nprShaderReadMaterial = AccessTools.Method(AccessTools.TypeByName("COM3D2.NPRShader.Plugin.NPRShader"), "ReadMaterial");
            var assetLoaderReadMaterial = AccessTools.Method(AccessTools.TypeByName("COM3D2.NPRShader.Plugin.AssetLoader"), "ReadMaterial");
            var assetLoaderReadMaterialWithSetShader = AccessTools.Method(AccessTools.TypeByName("COM3D2.NPRShader.Plugin.AssetLoader"), "ReadMaterialWithSetShader");
            var nprShaderReadMaterialTranspiler = AccessTools.Method(typeof(TryPatchNPRShader), "NPRShaderReadMaterialTranspiler");
            harmony.Patch(nprShaderReadMaterial, transpiler: new HarmonyMethod(nprShaderReadMaterialTranspiler));
            harmony.Patch(assetLoaderReadMaterial, transpiler: new HarmonyMethod(nprShaderReadMaterialTranspiler));
            harmony.Patch(assetLoaderReadMaterialWithSetShader, transpiler: new HarmonyMethod(nprShaderReadMaterialTranspiler));
            return true;
        }

        public static IEnumerable<CodeInstruction> NPRShaderReadMaterialTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            return ReadMaterialTranspiler(FixLeaveTranspiler(instructions));
        }
    }

    class TryPatchSceneCapture : TryPatch
    {
        public TryPatchSceneCapture(Harmony harmony, int failLimit = 3) : base(harmony, failLimit) { }

        public override bool Patch()
        {
            if (AccessTools.TypeByName("CM3D2.SceneCapture.Plugin.AssetLoader") == null)
            {
                return false;
            }
            var sceneCaptureReadMaterial = AccessTools.Method(AccessTools.TypeByName("CM3D2.SceneCapture.Plugin.AssetLoader"), "ReadMaterial");
            var nprShaderReadMaterialTranspiler = AccessTools.Method(typeof(TryPatchSceneCapture), "SceneCaptureReadMaterialTranspiler");
            harmony.Patch(sceneCaptureReadMaterial, transpiler: new HarmonyMethod(nprShaderReadMaterialTranspiler));
            return true;
        }

        public static IEnumerable<CodeInstruction> SceneCaptureReadMaterialTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            return ReadMaterialTranspiler(instructions);
        }
    }
}