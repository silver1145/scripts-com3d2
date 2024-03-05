// #author silver1145
// #name ModelExtend
// #desc Model Extend

using UnityEngine;
using HarmonyLib;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System;
using System.Text;

public static class ModelExtend
{
    static Harmony instance;

    public static void Main()
    {
        instance = Harmony.CreateAndPatchAll(typeof(ModelExtend));
    }

    public static void Unload()
    {
        instance.UnpatchAll(instance.Id);
        instance = null;
    }

    public class ModelExtendData
    {
        public string baseBoneName;
        public string vertexColorFilename;
        public string uv2Filename;
        public string uv3Filename;
        public string uv4Filename;
        public bool? receiveShadows;
        public UnityEngine.Rendering.ShadowCastingMode? shadowCastingMode;

        public static ModelExtendData ParseXML(byte[] xmlData)
        {
            ModelExtendData data = new ModelExtendData();
            try
            {
                XmlDocument xmlDoc = new XmlDocument();
                string xml = Encoding.UTF8.GetString(xmlData);
                xmlDoc.LoadXml(xml);
                XmlNode pluginNode = xmlDoc.SelectSingleNode("plugins/plugin[@name='ModelExtend']");
                if (pluginNode != null)
                {
                    data.baseBoneName = pluginNode.SelectSingleNode("BaseBoneName")?.InnerText;
                    data.vertexColorFilename = pluginNode.SelectSingleNode("VertexColorFilename")?.InnerText;
                    data.uv2Filename = pluginNode.SelectSingleNode("UV2Filename")?.InnerText;
                    data.uv3Filename = pluginNode.SelectSingleNode("UV3Filename")?.InnerText;
                    data.uv4Filename = pluginNode.SelectSingleNode("UV4Filename")?.InnerText;
                    if (bool.TryParse(pluginNode.SelectSingleNode("ReceiveShadows")?.InnerText, out var _receiveShadows))
                    {
                        data.receiveShadows = _receiveShadows;
                    }
                    try
                    {
                        data.shadowCastingMode = (UnityEngine.Rendering.ShadowCastingMode)Enum.Parse(typeof(UnityEngine.Rendering.ShadowCastingMode), pluginNode.SelectSingleNode("ShadowCastingMode")?.InnerText, true);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(ex);
            }
            return data;
        }
    }

    static Color[] ReadVertexColor(string filename, int vertexCount)
    {
        Color[] ret = null;
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
                    if (binaryReader.ReadInt32() == vertexCount)
                    {
                        Color[] colors = new Color[vertexCount];
                        for (int i = 0; i < vertexCount; i++)
                        {
                            colors[i] = new Color(
                                binaryReader.ReadSingle(),
                                binaryReader.ReadSingle(),
                                binaryReader.ReadSingle(),
                                binaryReader.ReadSingle()
                            );
                        }
                        ret = colors;
                    }
                    else
                    {
                        Debug.LogWarning($"Wrong VertexCount: {filename}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to Read VertexColor: {filename}");
                Debug.LogError(ex);
            }
            
        }
        return ret;
    }

    static Vector2[] ReadUV(string filename, int vertexCount)
    {
        Vector2[] ret = null;
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
                    if (binaryReader.ReadInt32() == vertexCount)
                    {
                        Vector2[] uvs = new Vector2[vertexCount];
                        for (int i = 0; i < vertexCount; i++)
                        {
                            uvs[i] = new Vector2(
                                binaryReader.ReadSingle(),
                                binaryReader.ReadSingle()
                            );
                        }
                        ret = uvs;
                    }
                    else
                    {
                        Debug.LogWarning($"Wrong VertexCount: {filename}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to Read UV: {filename}");
                Debug.LogError(ex);
            }
            
        }
        return ret;
    }

    [HarmonyPatch(typeof(TBodySkin), "Load", new[] {typeof(MPN), typeof(Transform), typeof(Transform), typeof(Dictionary<string, Transform>), typeof(string), typeof(string), typeof(string), typeof(string), typeof(int), typeof(bool), typeof(int)})]
    [HarmonyPrefix]
    public static void LoadPrefix(string filename, ref string bonename, ref ModelExtendData __state)
    {
        if (filename.ToLower().EndsWith(".bodybone.model"))
        {
            bonename = "_ROOT_";
        }
        string extendFilename = Path.ChangeExtension(filename, ".exmodel.xml");
        if (GameUty.FileSystem.IsExistentFile(extendFilename))
        {
            using (var f = GameUty.FileOpen(extendFilename))
            {
                __state = ModelExtendData.ParseXML(f.ReadAll());
            }
            if (__state.baseBoneName != null)
            {
                bonename = __state.baseBoneName;
            }
        }
    }

    [HarmonyPatch(typeof(TBodySkin), "Load", new[] {typeof(MPN), typeof(Transform), typeof(Transform), typeof(Dictionary<string, Transform>), typeof(string), typeof(string), typeof(string), typeof(string), typeof(int), typeof(bool), typeof(int)})]
    [HarmonyPostfix]
    public static void LoadPostfix(ref TBodySkin __instance, ref ModelExtendData __state)
    {
        foreach (Transform transform in __instance.obj.GetComponentsInChildren<Transform>(true))
        {
            Renderer renderer = transform.GetComponent<Renderer>();
            if (renderer != null && __state != null)
            {
                if (renderer is SkinnedMeshRenderer skinnedMeshRenderer && skinnedMeshRenderer.sharedMesh != null)
                {
                    int vertexCount = skinnedMeshRenderer.sharedMesh.vertexCount;
                    if (__state.vertexColorFilename != null)
                    {
                        var colors = ReadVertexColor(__state.vertexColorFilename, vertexCount);
                        if (colors != null)
                        {
                            skinnedMeshRenderer.sharedMesh.colors = colors;
                        }
                    }
                    if (__state.uv2Filename != null)
                    {
                        var uv2 = ReadUV(__state.uv2Filename, vertexCount);
                        if (uv2 != null)
                        {
                            skinnedMeshRenderer.sharedMesh.uv2 = uv2;
                        }
                    }
                    if (__state.uv3Filename != null)
                    {
                        var uv3 = ReadUV(__state.uv3Filename, vertexCount);
                        if (uv3 != null)
                        {
                            skinnedMeshRenderer.sharedMesh.uv3 = uv3;
                        }
                    }
                    if (__state.uv4Filename != null)
                    {
                        var uv4 = ReadUV(__state.uv4Filename, vertexCount);
                        if (uv4 != null)
                        {
                            skinnedMeshRenderer.sharedMesh.uv4 = uv4;
                        }
                    }
                }
                if (__state.receiveShadows.HasValue)
                {
                    renderer.receiveShadows = __state.receiveShadows.Value;
                }
                if (__state.shadowCastingMode.HasValue)
                {
                    renderer.shadowCastingMode = __state.shadowCastingMode.Value;
                }
            }
        }
    }
}