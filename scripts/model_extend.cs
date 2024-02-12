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