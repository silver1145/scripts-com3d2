// #author silver1145
// #name Extract Ks Scripts
// #desc Extract *.ks scripts from game

using UnityEngine;
using HarmonyLib;
using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;

public static class ExtractKsScripts
{
    static string KS_PATH = "_ks";
    static bool _lock = false;
    static KeyCode TOGGLE_KEYCODE = KeyCode.E;
    static GameObject gameObject;

    public static void Main()
    {
        gameObject = new GameObject();
        gameObject.AddComponent<ExtractScripts>();
    }

    public static void Unload()
    {
        GameObject.Destroy(gameObject);
        gameObject = null;
    }

    class ExtractScripts : MonoBehaviour
    {
        void Awake()
        {
            DontDestroyOnLoad(this);
        }

        void Update()
        {
            if (GameMain.Instance.GetNowSceneName() == "SceneTitle" && !GameMain.Instance.MainCamera.IsFadeProc() && Input.GetKeyDown(TOGGLE_KEYCODE) && !_lock)
            {
                GameMain.Instance.SysDlg.Show("Export *.ks File?", SystemDialog.TYPE.YES_NO, delegate
                {
                    _lock = true;
                    Debug.Log($"Extracting scripts...");
                    var scripts = GameUty.FileSystem.GetFileListAtExtension(".ks");
                    Debug.Log($"Found {scripts.Length} scripts");
                    int progress = 0;
                    int totalCount = scripts.Length;
                    int percent = -1;
                    foreach (var scriptFile in scripts)
                    {
                        progress++;
                        int percentage100 = (int)((progress / (float) totalCount) * 10000);
                        int percentage = percentage100 / 100;
                        if (percentage100 % 100 == 0 && percentage != percent)
                        {
                            percent = percentage;
                            Console.WriteLine("exporting: {0}%", percentage);
                        }
                        
                        var dir = Path.Combine(KS_PATH, Path.GetDirectoryName(scriptFile));
                        var name = Path.GetFileNameWithoutExtension(scriptFile);
                        Directory.CreateDirectory(dir);
                        
                        var f = GameUty.FileOpen(scriptFile);
                        using (FileStream fileStream = new FileStream(Path.Combine(dir, $"{name}.ks"), FileMode.Create))
                        {
                            using (BinaryWriter writer = new BinaryWriter(fileStream))
                            {
                                writer.Write(f.ReadAll());
                            }
                        }
                    }
                    GameMain.Instance.SysDlg.Close();
                    Debug.Log($"Export Success!");
                    _lock = false;
                    GameMain.Instance.SysDlg.Show("*.ks Export Success", SystemDialog.TYPE.OK, new SystemDialog.OnClick(GameMain.Instance.SysDlg.Close));
                }, new SystemDialog.OnClick(GameMain.Instance.SysDlg.Close));
            }
        }
    }
}