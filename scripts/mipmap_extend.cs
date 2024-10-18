// #author ghorsington
// #name Texture Mipmap Extender
// #desc All texture with mipmap in the name will enable Mipmap

using UnityEngine;
using HarmonyLib;

public static class MipmapExtend
{
    static Harmony instance;

    public static void Main()
    {
        instance = Harmony.CreateAndPatchAll(typeof(MipmapExtend));
    }

    public static void Unload()
    {
        instance.UnpatchSelf();
        instance = null;
    }

    public static Texture2D CreateTexture2D(TextureResource tex_res)
    {
        if (tex_res.format == TextureFormat.DXT1 || tex_res.format == TextureFormat.DXT5)
        {
            Texture2D texture2D = new Texture2D(tex_res.width, tex_res.height, tex_res.format, true);
            texture2D.LoadRawTextureData(tex_res.data);
            texture2D.Apply();
            return texture2D;
        }
        if (tex_res.format == TextureFormat.ARGB32 || tex_res.format == TextureFormat.RGB24)
        {
            Texture2D texture2D2 = new Texture2D(2, 2, tex_res.format, true);
            texture2D2.LoadImage(tex_res.data);
            return texture2D2;
        }
        Debug.LogError("format:" + tex_res.format.ToString() + "は対応していません");
        return null;
    }

    [HarmonyPatch(typeof(ImportCM), "CreateTexture", new [] { typeof(string) })]
    [HarmonyPrefix]
    public static bool CreateTexturePrefix1(ref Texture2D __result, string f_strFileName)
    {
        if (f_strFileName.ToLower().Contains("mipmap"))
        {
            __result = CreateTexture2D(ImportCM.LoadTexture(GameUty.FileSystem, f_strFileName, true));
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(ImportCM), "CreateTexture", new [] { typeof(AFileSystemBase), typeof(string) })]
    [HarmonyPrefix]
    public static bool CreateTexturePrefix2(ref Texture2D __result, AFileSystemBase f_fileSystem, string f_strFileName)
    {
        if (f_strFileName.ToLower().Contains("mipmap"))
        {
            __result = CreateTexture2D(ImportCM.LoadTexture(f_fileSystem, f_strFileName, true));
            return false;
        }
        return true;
    }
}
