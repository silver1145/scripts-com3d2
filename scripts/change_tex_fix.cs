// #author silver1145
// #name ChangeTex Fix
// #desc Fix TBody.ChangeTex when file does not exist

using UnityEngine;
using HarmonyLib;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security;
using System.Security.Permissions;

public static class ChangeTexFix
{
    static Harmony instance;

    public static void Main()
    {
        instance = Harmony.CreateAndPatchAll(typeof(ChangeTexFix));
    }

    public static void Unload()
    {
        instance.UnpatchAll(instance.Id);
        instance = null;
    }

    [HarmonyPatch(typeof(TBody), "ChangeTex")]
    [HarmonyPrefix]
    public static void ChangeTexPrefix(ref TBody __instance, string slotname, Dictionary<string, byte[]> dicModTexData, ref string filename, string prop_name)
    {
        int num = (int)TBody.hashSlotName[slotname];
        TBodySkin tbodySkin = __instance.goSlot[num];
        string file_name = filename.Replace("*", Path.GetFileNameWithoutExtension(tbodySkin.m_strModelFileName));
        if (dicModTexData != null && dicModTexData.ContainsKey(filename))
        {
            return;
        }
        if (GameUty.FileSystem.IsExistentFile(file_name))
        {
            return;
        }
        if (tbodySkin.obj != null)
        {
            SkinnedMeshRenderer componentInChildren = tbodySkin.obj.GetComponentInChildren<SkinnedMeshRenderer>();
            if (componentInChildren != null)
            {
                foreach (var m in componentInChildren.materials)
                {
                    Texture tex = m.GetTexture(prop_name);
                    if (tex != null)
                    {
                        file_name = Path.GetFileNameWithoutExtension(tex.name) + ".tex";
                        if (GameUty.FileSystem.IsExistentFile(file_name))
                        {
                            filename = file_name;
                        }
                        return;
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(InfinityColorTextureCache), "UpdateTexture", new[] {typeof(Texture), typeof(MaidParts.PARTS_COLOR), typeof(RenderTexture)})]
    [HarmonyPrefix]
    public static void UpdateTexturePrefix(Texture base_tex, RenderTexture target_tex)
    {
        if (base_tex == null || target_tex == null)
        {
            return;
        }
        target_tex.name = base_tex.name;
    }
}