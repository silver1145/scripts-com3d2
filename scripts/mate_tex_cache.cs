// #author silver1145
// #name Mate Tex Cache
// #desc Mate & Tex Cache

// #define MATETEXCACHE_DEBUG
using UnityEngine;
using HarmonyLib;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using BepInEx.Configuration;
using COM3D2.NPRShader.Plugin;

public static class MateTexCache
{
    static public Harmony instance;
    // consts
    readonly static List<string> MateCacheTypes = new List<string> { "None", "NPR_Only", "All" };
    readonly static List<string> TexCacheTypes = new List<string> { "ByMate", "All" };
    const int MateCacheType_None = 0, MateCacheType_NPR_Only = 1, MateCacheType_All = 2;
    const int TexCacheType_ByMate = 0, TexCacheType_All = 1;
    static byte[] cachePattern = new byte[] { 0x66, 0x06, 0x5F, 0x43, 0x61, 0x63, 0x68, 0x65 }; // f6_Cache
    static byte[] floatOnePattern = new byte[] { 0x00, 0x00, 0x80, 0x3F };                      // 1.0f
    // config
    static public ConfigFile configFile = new ConfigFile(Path.Combine(BepInEx.Paths.ConfigPath, "MateTexCache.cfg"), false);
    static public ConfigEntry<bool> _golbalEnable = configFile.Bind("MateTexCache Setting", "GolbalEnable", true, "Global Switch");
    static public ConfigEntry<int> _tempCacheCapacity = configFile.Bind("MateTexCache Setting", "TempCacheCapacity", 10, new ConfigDescription("Capacity for Temp Cache (will Destroy)", new AcceptableValueRange<int>(0, 100)));
    static public ConfigEntry<string> _mateCacheType = configFile.Bind("MateTexCache Setting", "MateCacheType", "NPR_Only", new ConfigDescription("MateCache Type", new AcceptableValueList<string>(MateCacheTypes.ToArray())));
    static public ConfigEntry<string> _texCacheType = configFile.Bind("MateTexCache Setting", "TexCacheType", "ByMate", new ConfigDescription("TexCache Type", new AcceptableValueList<string>(TexCacheTypes.ToArray())));
    static bool golbalEnable = _golbalEnable.Value;
    static int tempCacheCapacity = _tempCacheCapacity.Value;
    static int mateCacheType = MateCacheTypes.IndexOf(_mateCacheType.Value);
    static int texCacheType = TexCacheTypes.IndexOf(_texCacheType.Value);
    // try patch MaidLoader flag
    static bool patched = false;
    // cache
    static public UObjectCache<Material> mateCache;
    static public UObjectCache<Texture2D> texCache;
    static public Dictionary<BinaryReader, List<Texture2D>> texTempManage;
    public delegate void RemoveCacheDelegate<T>(T item);
    public delegate byte[] GetDataDelegate(string name);

    public class CacheItem<T> : IDisposable where T : UnityEngine.Object
    {
        public T item;
        public string md5;
        public int length;
        public int refCount;
        public bool dirty;
        public List<UnityEngine.Object> depObjects;
        private bool isDisposed = false;

        public CacheItem(T item, string md5, int len)
        {
            this.item = item;
            this.md5 = md5;
            this.length = len;
            this.refCount = 1;
            this.dirty = false;
        }

        ~CacheItem()
        {
            Dispose(false);
        }

        public bool CheckValid(byte[] data)
        {
            if (!dirty)
            {
                return true;
            }
            if (length == data.Length && md5 == GetMD5checksum(data))
            {
                dirty = false;
                return true;
            }
            return false;
        }

        public void AddDependent(UnityEngine.Object obj)
        {
            if (depObjects == null)
            {
                depObjects = new List<UnityEngine.Object>();
            }
            depObjects.Add(obj);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                if (disposing)
                {
                    UnityEngine.Object.DestroyImmediate(item);
                    if (depObjects != null)
                    {
                        foreach (var o in depObjects)
                        {
                            DestroyImmediate(o);
                        }
                        depObjects.Clear();
                        depObjects = null;
                    }
                }
                isDisposed = true;
            }
        }
    }

    public class LRUCacheItem<K, V>
    {
        public K Key { get; }
        public V Value { get; set; }

        public LRUCacheItem(K key, V value)
        {
            Key = key;
            Value = value;
        }
    }

    public class LRUCache<K, V> : IEnumerable<LRUCacheItem<K, V>> where V : IDisposable
    {
        public LinkedList<LRUCacheItem<K, V>> cacheList;
        private Dictionary<K, LinkedListNode<LRUCacheItem<K, V>>> cacheDict;

        public LRUCache()
        {
            cacheList = new LinkedList<LRUCacheItem<K, V>>();
            cacheDict = new Dictionary<K, LinkedListNode<LRUCacheItem<K, V>>>();
        }

        public void Add(K key, V value, RemoveCacheDelegate<V> removeRevCacheAction = null, bool addFirst = true)
        {
            if (cacheDict.TryGetValue(key, out var node))
            {
                cacheList.Remove(node);
                cacheList.AddFirst(node);
            }
            var newItem = new LRUCacheItem<K, V>(key, value);
            var newNode = new LinkedListNode<LRUCacheItem<K, V>>(newItem);
            if (addFirst)
            {
                cacheList.AddFirst(newNode);
            }
            else
            {
                cacheList.AddLast(newNode);
            }
            cacheDict[key] = newNode;
            while (tempCacheCapacity >= 0 && cacheList.Count > tempCacheCapacity)
            {
                var lastNode = cacheList.Last;
                cacheList.RemoveLast();
                cacheDict.Remove(lastNode.Value.Key);
                removeRevCacheAction?.Invoke(lastNode.Value.Value);
                lastNode.Value.Value?.Dispose();
            }
        }

        public void Remove(K key, bool destroy = false)
        {
            if (cacheDict.TryGetValue(key, out var node))
            {
                cacheList.Remove(node);
                cacheDict.Remove(key);
                if (destroy)
                {
                    node.Value.Value?.Dispose();
                }
            }
        }

        public bool TryGetValue(K key, out V value)
        {
            if (cacheDict.TryGetValue(key, out var node))
            {
                cacheList.Remove(node);
                cacheList.AddFirst(node);
                value = node.Value.Value;
                return true;
            }
            value = default(V);
            return false;
        }

        public void Clear()
        {
            cacheList.Clear();
            cacheDict.Clear();
        }

        public IEnumerator<LRUCacheItem<K, V>> GetEnumerator()
        {
            foreach (var item in cacheList)
            {
                yield return item;
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public class UObjectCache<T> : IDisposable where T : UnityEngine.Object
    {
        public Dictionary<string, CacheItem<T>> cache;
        public Dictionary<T, CacheItem<T>> reverseCache;
        public LRUCache<string, CacheItem<T>> tempCache;
        private RemoveCacheDelegate<CacheItem<T>> removeCacheDelegate;
        private GetDataDelegate getDataDelegate;
        private bool isDisposed = false;

        public UObjectCache(GetDataDelegate getData = null)
        {
            cache = new Dictionary<string, CacheItem<T>>();
            reverseCache = new Dictionary<T, CacheItem<T>>();
            tempCache = new LRUCache<string, CacheItem<T>>();
            removeCacheDelegate = RemoveRevCache;
            getDataDelegate = getData;
        }

        ~UObjectCache()
        {
            Dispose(false);
        }

        private void CacheToTemp(CacheItem<T> item)
        {
            string key= "";
            foreach (var p in cache)
            {
                if (p.Value == item)
                {
                    key = p.Key;
                    cache.Remove(key);
                    break;
                }
            }
            if (!string.IsNullOrEmpty(key))
            {
                tempCache.Add(key, item, removeCacheDelegate);
            }
        }

        private void TempToCache(string key, CacheItem<T> item)
        {
            tempCache.Remove(key);
            cache[key] = item;
        }

        private void MarkOutdated(string key, CacheItem<T> item)
        {
            item.refCount = 0;
            reverseCache.Remove(item.item);
            tempCache.Remove(key);
            tempCache.Add(Path.ChangeExtension(key, ".oldmate"), item, removeCacheDelegate);
        }

        public void RemoveRevCache(CacheItem<T> item)
        {
            reverseCache.Remove(item.item);
        }

        public void Add(string name, T obj, byte[] data)
        {
            CacheItem<T> item = new CacheItem<T>(obj, GetMD5checksum(data), data.Length);
            cache[name] = item;
            reverseCache[obj] = item;
        }

        public void UpdateRefCount(T obj, int op = -1)
        {
            reverseCache[obj].refCount += op;
            if (op < 0 && reverseCache[obj].refCount == 0)
            {
                CacheToTemp(reverseCache[obj]);
            }
        }

        public bool Contains(T obj)
        {
            return reverseCache.ContainsKey(obj);
        }

        public bool TryGetValue(string key, out T value, byte[] data = null)
        {
            CacheItem<T> cItem;
            bool isTemp = false;
            if (cache.TryGetValue(key, out cItem))
            {
                cItem.refCount++;
            }
            else if (tempCache.TryGetValue(key, out cItem))
            {
                isTemp = true;
                cItem.refCount++;
            }
            if (cItem != null)
            {
                bool valid = !cItem.dirty;
                if (getDataDelegate != null && !valid)
                {
                    data = getDataDelegate?.Invoke(key);
                }
                if (data != null && !valid)
                {
                    valid = cItem.CheckValid(data);
                }
                if (valid)
                {
                    if (isTemp)
                    {
                        TempToCache(key, cItem);
                    }
                    value = cItem.item;
                    return true;
                }
                if (!isTemp)
                {
                    CacheToTemp(cItem);
                }
                MarkOutdated(key, cItem);
            }
            value = default(T);
            return false;
        }

        public void SetAllDirt()
        {
            foreach (var p in reverseCache)
            {
                p.Value.dirty = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void LeakDispose()
        {
            Dispose(true, true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing, bool leak = false)
        {
            if (!isDisposed)
            {
                if (disposing)
                {
                    reverseCache.Clear();
                    reverseCache = null;
                    if (!leak)
                    {
                        foreach (var kvp in cache)
                        {
                            kvp.Value?.Dispose();
                        }
                    }
                    cache.Clear();
                    cache = null;
                    if (!leak)
                    {
                        foreach (var kvp in tempCache.cacheList)
                        {
                            kvp.Value?.Dispose();
                        }
                    }
                    tempCache.Clear();
                    tempCache = null;
                }
                isDisposed = true;
            }
        }
    }

    public class BoyerMoore
    {
        private static int ALPHABET_SIZE = 256;

        private byte[] text;
        private byte[] pattern;

        private int[] last;
        private int[] match;
        private int[] suffix;

        public BoyerMoore(byte[] pattern, byte[] text)
        {
            this.text = text;
            this.pattern = pattern;
            last = new int[ALPHABET_SIZE];
            match = new int[pattern.Length];
            suffix = new int[pattern.Length];
        }

        public int Match()
        {
            // Preprocessing
            ComputeLast();
            ComputeMatch();
            // Searching
            int i = pattern.Length - 1;
            int j = pattern.Length - 1;    
            while (i < text.Length)
            {
                if (pattern[j] == text[i])
                {
                    if (j == 0)
                    { 
                        return i;
                    }
                    j--;
                    i--;
                } 
                else
                {
                    i += pattern.Length - j - 1 + Math.Max(j - last[text[i]], match[j]);
                    j = pattern.Length - 1;
                }
            }
            return -1;    
        }  

        private void ComputeLast()
        {
            for (int k = 0; k < last.Length; k++)
            { 
                last[k] = -1;
            }
            for (int j = pattern.Length-1; j >= 0; j--)
            {
                if (last[pattern[j]] < 0)
                {
                    last[pattern[j]] = j;
                }
            }
        }

        private void ComputeMatch()
        {
            for (int j = 0; j < match.Length; j++)
            { 
                match[j] = match.Length;
            }
            ComputeSuffix();
            for (int i = 0; i < match.Length - 1; i++)
            {
                int j = suffix[i + 1] - 1;
                if (suffix[i] > j)
                {
                    match[j] = j - i;
                } 
                else
                {
                    match[j] = Math.Min(j - i + match[i], match[j]);
                }
            }
            if (suffix[0] < pattern.Length)
            {
                for (int j = suffix[0] - 1; j >= 0; j--)
                {
                    if (suffix[0] < match[j])
                    {
                        match[j] = suffix[0];
                    }
                }
                {
                    int j = suffix[0];
                    for (int k = suffix[j]; k < pattern.Length; k = suffix[k])
                    {
                        while (j < k)
                        {
                            if (match[j] > k)
                            {
                                match[j] = k;
                            }
                            j++;
                        }
                    }
                }
            }
        }

        private void ComputeSuffix()
        {        
            suffix[suffix.Length-1] = suffix.Length;            
            int j = suffix.Length - 1;
            for (int i = suffix.Length - 2; i >= 0; i--)
            {  
                while (j < suffix.Length - 1 && !pattern[j].Equals(pattern[i]))
                {
                    j = suffix[j + 1] - 1;
                }
                if (pattern[j] == pattern[i])
                { 
                    j--; 
                }
                suffix[i] = j + 1;
            }
        }

    }

    public static void Main()
    {
        Init();
        instance = Harmony.CreateAndPatchAll(typeof(MateTexCache));
        TryPatch();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += SceneLoaded;
    }

    public static void Unload()
    {
        instance.UnpatchAll(instance.Id);
        instance = null;
        UnInit();
    }

    public static void Init()
    {
        mateCache = new UObjectCache<Material>();
        texCache = new UObjectCache<Texture2D>(GetFileContent);
        texTempManage = new Dictionary<BinaryReader, List<Texture2D>>();
        _golbalEnable.SettingChanged += (s, e) => golbalEnable = _golbalEnable.Value;
        _tempCacheCapacity.SettingChanged += (s, e) => tempCacheCapacity = _tempCacheCapacity.Value;
        _mateCacheType.SettingChanged += (s, e) => mateCacheType = MateCacheTypes.IndexOf(_mateCacheType.Value);
        _texCacheType.SettingChanged += (s, e) => texCacheType = TexCacheTypes.IndexOf(_texCacheType.Value);
    }

    public static void UnInit()
    {
        mateCache.LeakDispose();
        texCache.LeakDispose();
        texTempManage.Clear();
        mateCache = null;
        texCache = null;
        texTempManage = null;
    }

    public static void TryPatch()
    {
        try
        {
            var mOriginal = AccessTools.Method(Type.GetType("COM3D2.MaidLoader.RefreshMod, COM3D2.MaidLoader"), "RefreshCo");
            var mPrefix = SymbolExtensions.GetMethodInfo(() => RefreshCoPrefix());
            instance.Patch(mOriginal, new HarmonyMethod(mPrefix));
            patched = true;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= SceneLoaded;
        }
        finally
        {
        }
    }

    private static void SceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode)
    {
        if (!patched)
        {
            patched = true;
            TryPatch();
        }
    }

    public static void RefreshCoPrefix()
    {
        mateCache.SetAllDirt();
        texCache.SetAllDirt();
    }

    // `NPRShader.LoadMaterial` & `ImportCM.LoadMaterial`
    /*
    public static Material LoadMaterial(string f_strFileName, TBodySkin bodyskin, Material existmat = null)
    {
        byte[] array;       // or ImportCM.m_matTempFile for `ImportCM.LoadMaterial`
+       bool isNPR = True   // False for `ImportCM.LoadMaterial`
        try
        {
            using (AFileBase afileBase = ...)
            {
                ...
+               fileSize = afileBase.GetSize()
            }
        }
        BinaryReader binaryReader = ...;
        ...
+       Material material = MateTexCache.GetMaterial(f_strFileName, binaryReader, array, fileSize, isNPR);
+       if (material != null)
+       {
+           if (bodyskin != null)
+           {
+               bodyskin.listDEL.Add(material);
+           }
+       }
+       else
+       {
            material = NPRShader.ReadMaterial(binaryReader, f_strFileName, bodyskin, existmat);
+           material = MateTexCache.AddMaterial(material, f_strFileName, binaryReader, array, fileSize, isNPR);
+       }
        binaryReader.Close();
        return material;
    }
    */
    [HarmonyPatch(typeof(NPRShader), "LoadMaterial")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> NPRShaderLoadMaterialTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        LocalBuilder fileSize = generator.DeclareLocal(typeof(int));
        CodeMatcher codeMatcher = new CodeMatcher(instructions, generator);
        Label matLabel;
        Label jmpLabel;
        codeMatcher.End()
            .MatchBack(false, new[] { new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(BinaryReader), "Close")) })
            .Advance(-1)
            .CreateLabel(out matLabel)
            .Advance(-1)
            .MatchBack(false, new[] { new CodeMatch(OpCodes.Ldloc_1) });
        var setMaterialCI = codeMatcher.InstructionAt(9);
        var loadMaterialCI = codeMatcher.InstructionAt(12);
        codeMatcher.InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldloc_1))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldloc_0))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldloc, fileSize))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldc_I4_1))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MateTexCache), "GetMaterial")))
            .InsertAndAdvance(setMaterialCI)
            .InsertAndAdvance(loadMaterialCI)
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldnull))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UnityEngine.Object), "op_Inequality")))
            .Insert(new CodeInstruction(OpCodes.Brtrue_S, matLabel))
            .CreateLabel(out jmpLabel)
            .InsertAndAdvance(new CodeInstruction(OpCodes.Dup))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Brfalse_S, jmpLabel))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_1))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Brfalse_S, jmpLabel))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_1))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(TBodySkin), "listDEL")))
            .InsertAndAdvance(loadMaterialCI)
            .InsertAndAdvance(new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(List<UnityEngine.Object>), "Add")))
            .MatchForward(false, new[] { new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(NPRShader), "ReadMaterial")) })
            .Advance(1)
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldloc_1))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldloc_0))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldloc_S, fileSize))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldc_I4_1))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MateTexCache), "AddMaterial")));
        codeMatcher.Start()
            .MatchForward(false, new[] { new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(AFileBase), "Read")) })
            .Advance(1)
            .InsertAndAdvance(codeMatcher.InstructionAt(-3))
            .InsertAndAdvance(codeMatcher.InstructionAt(-3))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Stloc_S, fileSize));
        return codeMatcher.InstructionEnumeration();
    }

    [HarmonyPatch(typeof(ImportCM), "LoadMaterial")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> ImportCMLoadMaterialTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        LocalBuilder fileSize = generator.DeclareLocal(typeof(int));
        CodeMatcher codeMatcher = new CodeMatcher(instructions, generator);
        Label matLabel;
        codeMatcher.End()
            .MatchBack(false, new[] { new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(BinaryReader), "Close")) })
            .Advance(-1)
            .CreateLabel(out matLabel)
            .MatchBack(false, new[] { new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(BinaryReader), "ReadString")) })
            .Advance(2)
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldloc_3))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(ImportCM), "m_matTempFile")))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldloc, fileSize))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldc_I4_0))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MateTexCache), "GetMaterial")))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Stloc_0))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldloc_0))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldnull))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UnityEngine.Object), "op_Inequality")))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Brtrue_S, matLabel))
            .MatchForward(false, new[] { new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(ImportCM), "ReadMaterial")) })
            .Advance(1)
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldloc_3))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(ImportCM), "m_matTempFile")))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldloc_S, fileSize))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldc_I4_0))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MateTexCache), "AddMaterial")));
        codeMatcher.Start()
            .MatchForward(false, new[] { new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(AFileBase), "Read")) })
            .Advance(1)
            .InsertAndAdvance(codeMatcher.InstructionAt(-3))
            .InsertAndAdvance(codeMatcher.InstructionAt(-3))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Stloc_S, fileSize));
        return codeMatcher.InstructionEnumeration();
    }

    // `NPRShader.ReadMaterial` & `ImportCM.ReadMaterial`
    /*
    public static Material ReadMaterial(BinaryReader r, TBodySkin bodyskin = null, Material existmat = null)
    {
        ...
+       bool moveToDep = MateTexCache.ContainsTempDependent(r);
        for (;;)
        {
            ...
            Texture2D texture2D = ...;
+           if (moveToDep)
+           {
+               MateTexCache.AddTempDependent(r, texture2D);
+           }
            if (bodyskin != null)
            {
                bodyskin.listDEL.Add(texture2D);

            }
            ...
        }
        ...
    }
    */
    public static IEnumerable<CodeInstruction> ReadMaterialTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var locals = Traverse.Create(Traverse.Create(generator).Field("Target").GetValue<MonoMod.Utils.Cil.CecilILGenerator>()).Field("_Variables").GetValue<Dictionary<LocalBuilder, Mono.Cecil.Cil.VariableDefinition>>().Keys.ToArray();
        LocalBuilder moveToDep = generator.DeclareLocal(typeof(bool));
        object texture2D = null;
        foreach (LocalBuilder local in locals)
        {
            if (local.LocalType == typeof(Texture2D))
            {
                texture2D = local;
                break;
            }
        }
        if (texture2D == null)
        {
            throw new Exception("Local Variables not Found");
        }
        CodeMatcher codeMatcher = new CodeMatcher(instructions, generator);
        Label label;
        codeMatcher.End()
            .MatchBack(false, new[] { new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(object), "GetHashCode")) })
            .Advance(-2)
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MateTexCache), "ContainsTempDependent")))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Stloc_S, moveToDep))
            .MatchForward(false, new[] { new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(TBodySkin), "listDEL")) })
            .Advance(-3)
            .CreateLabel(out label)
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldloc_S, moveToDep))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Brfalse_S, label))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldloc_S, texture2D))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MateTexCache), "AddTempDependent")))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldc_I4_0))
            .InsertAndAdvance(codeMatcher.InstructionAt(1));
        return codeMatcher.InstructionEnumeration();
    }

    [HarmonyPatch(typeof(NPRShader), "ReadMaterial")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> NPRShaderReadMaterialTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        return ReadMaterialTranspiler(instructions, generator);
    }

    [HarmonyPatch(typeof(ImportCM), "ReadMaterial")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> ImportCMReadMaterialTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        return ReadMaterialTranspiler(instructions, generator);
    }

    // Add the instances generated by accessing Render.Materials to the listDEL
    [HarmonyPatch(typeof(ImportCM), "ReadMaterial")]
    [HarmonyPrefix]
    public static void ImportCMReadMaterialPrefix(TBodySkin bodyskin, Material existmat)
    {
        if (bodyskin != null && existmat != null)
        {
            bodyskin.listDEL.Add(existmat);
        }
    }

    // Update Render.materials with cached material
    [HarmonyPatch(typeof(TBody), "ChangeMaterial")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> ChangeMaterialTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        CodeMatcher codeMatcher = new CodeMatcher(instructions);
        codeMatcher.MatchForward(false, new[] { new CodeMatch(OpCodes.Pop) })
            .SetInstructionAndAdvance(codeMatcher.InstructionAt(-5))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_2))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MateTexCache), "RefreshMaterial")));
        return codeMatcher.InstructionEnumeration();
    }

    // `ImportCM.CreateTexture`
    /*
    public static Texture2D CreateTexture(string f_strFileName)
	{
+       Texture2D tex = MateTexCache.GetTexture2D(f_strFileName);
+       if (tex != null)
+       {
+           return tex    
+       }
		tex = ImportCM.LoadTexture(GameUty.FileSystem, f_strFileName, true).CreateTexture2D();
+       tex = MateTexCache.AddTexture2D(tex, f_strFileName);
        return tex;
	}
    */
    [HarmonyPatch(typeof(ImportCM), "CreateTexture", new[] { typeof(string) })]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> CreateTextureTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        LocalBuilder tex = generator.DeclareLocal(typeof(Texture2D));
        Label label;
        CodeMatcher codeMatcher = new CodeMatcher(instructions, generator);
        codeMatcher.Start()
            .CreateLabel(out label)
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MateTexCache), "GetTexture2D")))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Stloc_S, tex))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldloc_S, tex))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldnull))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UnityEngine.Object), "op_Equality")))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Brtrue_S, label))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldloc_S, tex))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ret))
            .End()
            .MatchBack(false, new[] { new CodeMatch(OpCodes.Ret) })
            .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
            .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MateTexCache), "AddTexture2D")));
        return codeMatcher.InstructionEnumeration();
    }

    /*
-   UnityEngine.Object.DestroyImmediate(...);
+   MateTexCache.DestroyImmediate(...);
    */
    public static IEnumerable<CodeInstruction> ReplaceDestroyImmediate(IEnumerable<CodeInstruction> instructions)
    {
        CodeMatcher codeMatcher = new CodeMatcher(instructions);
        codeMatcher.MatchForward(false, new[] { new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(UnityEngine.Object), "DestroyImmediate", new[] { typeof(UnityEngine.Object) })) })
            .SetInstruction(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MateTexCache), "DestroyImmediate")));
        return codeMatcher.InstructionEnumeration();
    }

    [HarmonyPatch(typeof(TBodySkin), "DeleteObj")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> DeleteObjTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        return ReplaceDestroyImmediate(instructions);
    }

    /*
-   UnityEngine.Object.Destroy(...);
+   MateTexCache.DestroyImmediate(...);
    */
    public static IEnumerable<CodeInstruction> ReplaceDestroy(IEnumerable<CodeInstruction> instructions)
    {
        CodeMatcher codeMatcher = new CodeMatcher(instructions);
        codeMatcher.MatchForward(false, new[] { new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(UnityEngine.Object), "Destroy", new[] { typeof(UnityEngine.Object) })) })
            .SetInstruction(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MateTexCache), "DestroyImmediate")));
        return codeMatcher.InstructionEnumeration();
    }

    [HarmonyPatch(typeof(TBody), "MulTexSet")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> MulTexSetTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        return ReplaceDestroy(instructions);
    }

    [HarmonyPatch(typeof(TBody.TexLay.LaySet), "Remove")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> TexLayRemoveTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        return ReplaceDestroy(instructions);
    }

    [HarmonyPatch(typeof(TBody.TexLay.OrderTex), "Remove")]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> TexLayLaySetRemoveTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        return ReplaceDestroy(instructions);
    }

    public static Material GetMaterial(string f_strFileName, BinaryReader r, byte[] untruncatedData, int size, bool isNPR = false)
    {
        bool flag = mateCacheType == MateCacheType_All;
        flag |= mateCacheType == MateCacheType_NPR_Only && isNPR;
        if (untruncatedData.Length > size)
        {
            untruncatedData = untruncatedData.Take(size).ToArray();
        }
        BoyerMoore boyerMoore = new BoyerMoore(cachePattern, untruncatedData);
        int index = boyerMoore.Match();
        if (index != -1 && index <= size - 16)
        {
            flag = untruncatedData.Skip(index + cachePattern.Length).Take(4).ToArray().SequenceEqual(floatOnePattern);
        }
        flag &= golbalEnable;
        if (flag)
        {
            if (GameUty.FileSystem.IsExistentFile(f_strFileName))
            {
                if (mateCache.TryGetValue(f_strFileName.ToLower(), out Material mat, untruncatedData))
                {
                    return mat;
                }
                texTempManage.Clear();
                texTempManage.Add(r, new List<Texture2D>());
            }
        }
        return null;
    }

    public static Texture2D GetTexture2D(string f_strFileName)
    {
        bool flag = texCacheType == TexCacheType_All;
        flag &= golbalEnable;
        if (flag)
        {
            if (GameUty.FileSystem.IsExistentFile(f_strFileName))
            {
                byte[] data;
                using (var f = GameUty.FileOpen(f_strFileName))
                {
                    data = f.ReadAll();
                }
                if (texCache.TryGetValue(f_strFileName.ToLower(), out Texture2D tex))
                {
                    return tex;
                }
            }
        }
        return null;
    }

    public static Material AddMaterial(Material mat, string f_strFileName, BinaryReader r, byte[] untruncatedData, int size, bool isNPR = false)
    {
        bool flag = mateCacheType == MateCacheType_All;
        flag |= mateCacheType == MateCacheType_NPR_Only && isNPR;
        flag &= mat != null;
        if (untruncatedData.Length > size)
        {
            untruncatedData = untruncatedData.Take(size).ToArray();
        }
        BoyerMoore boyerMoore = new BoyerMoore(cachePattern, untruncatedData);
        int index = boyerMoore.Match();
        if (index != -1 && index <= size - 16)
        {
            flag = untruncatedData.Skip(index + cachePattern.Length).Take(4).ToArray().SequenceEqual(floatOnePattern);
        }
        flag &= golbalEnable;
        if (flag)
        {
            if (GameUty.FileSystem.IsExistentFile(f_strFileName))
            {
                mateCache.Add(f_strFileName.ToLower(), mat, untruncatedData);
                if (texTempManage != null && texTempManage.TryGetValue(r, out var textures))
                {
                    foreach (var tex in textures)
                    {
                        AddDependent(mat, tex);
                    }
                    textures.Clear();
                    texTempManage.Clear();
                }
            }
        }
        return mat;
    }

    public static Texture2D AddTexture2D(Texture2D tex, string f_strFileName)
    {
        bool flag = texCacheType == TexCacheType_All && tex != null;
        flag &= golbalEnable;
        if (flag)
        {
            if (GameUty.FileSystem.IsExistentFile(f_strFileName))
            {
                byte[] data;
                using (var f = GameUty.FileOpen(f_strFileName))
                {
                    data = f.ReadAll();
                }
                texCache.Add(f_strFileName.ToLower(), tex, data);
            }
        }
        return tex;
    }

    public static string GetMD5checksum(byte[] inputData)
    {
        System.IO.MemoryStream stream = new System.IO.MemoryStream();
        stream.Write(inputData, 0, inputData.Length);
        stream.Seek(0, System.IO.SeekOrigin.Begin);
        using (var md5Instance = System.Security.Cryptography.MD5.Create())
        {
            var hashResult = md5Instance.ComputeHash(stream);
            return BitConverter.ToString(hashResult).Replace("-", "").ToLowerInvariant();
        }
    }

    public static byte[] GetFileContent(string f_strFileName)
    {
        byte[] data = null;
        if (GameUty.FileSystem.IsExistentFile(f_strFileName))
        {
            using (var f = GameUty.FileOpen(f_strFileName))
            {
                data = f.ReadAll();
            }
        }
        return data;
    }

    public static bool ContainsTempDependent(BinaryReader r)
    {
        return texTempManage.ContainsKey(r);
    }

    public static void AddTempDependent(BinaryReader r, Texture2D tex)
    {
        if (texTempManage.TryGetValue(r, out var textures))
        {
            textures.Add(tex);
        }
    }

    public static void AddDependent(Material mat, Texture2D tex)
    {
        if (mateCache.reverseCache.TryGetValue(mat, out var item))
        {
            item.AddDependent(tex);
        }
    }

    public static void RefreshMaterial(Material mat, Renderer component, int f_nMatNo)
    {
        if (mat.GetInstanceID() != component.materials[f_nMatNo].GetInstanceID())
        {
            Material[] materials = (Material[])component.materials.Clone();
            materials[f_nMatNo] = mat;
            component.materials = materials;
        }
    }

    public static void DestroyMaterial(Material mat)
    {
        if (mateCache.Contains(mat))
        {
            mateCache.UpdateRefCount(mat);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(mat);
        }
    }

    public static void DestroyTexture2D(Texture2D tex)
    {
        if (texCache.Contains(tex))
        {
            texCache.UpdateRefCount(tex);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }

    public static void DestroyImmediate(UnityEngine.Object obj)
    {
        if (obj is Texture2D tex)
        {
            DestroyTexture2D(tex);
        }
        else if (obj is Material mat)
        {
            DestroyMaterial(mat);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(obj);
        }
    }

}

#if MATETEXCACHE_DEBUG
static class MateTexCacheDebug
{
    static GameObject gameObject;

    public static void Main()
    {
        gameObject = new GameObject();
        gameObject.AddComponent<MateTexCacheDebugGUI>();
    }

    public static void Unload()
    {
        GameObject.Destroy(gameObject);
        gameObject = null;
    }

    class MateTexCacheDebugGUI : MonoBehaviour
    {
        private static bool showGUI = false;
        private GUIStyle grayCS;
        private GUIStyle whiteCS;
        private KeyCode ENABLE_KEYCODE = KeyCode.D;
        private Vector2 scrollViewPos = Vector2.zero;
        private const int WIDTH = 1080;
        private const int HEIGHT = 720;
        private const int MARGIN_X = 5;
        private const int MARGIN_TOP = 20;
        private const int MARGIN_BOTTOM = 5;

        void Awake()
        {
            DontDestroyOnLoad(this);
            grayCS = new GUIStyle();
            grayCS.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
            grayCS.font = Font.CreateDynamicFontFromOSFont("Consolas", 16);
            whiteCS = new GUIStyle();
            whiteCS.normal.textColor = Color.white;
            whiteCS.font = Font.CreateDynamicFontFromOSFont("Consolas", 16);
        }

        void Update()
        {
            if (Input.GetKeyDown(ENABLE_KEYCODE) && Input.GetKey(KeyCode.LeftControl))
            {
                showGUI = !showGUI;
            }
        }

        void Window(int id)
        {
            GUILayout.BeginArea(new Rect(MARGIN_X, MARGIN_TOP, WIDTH - MARGIN_X * 2, HEIGHT - MARGIN_TOP - MARGIN_BOTTOM));
            {
                scrollViewPos = GUILayout.BeginScrollView(scrollViewPos);
                {
                    GUILayout.BeginVertical();
                    {
                        GUILayout.Label("MateCache", whiteCS);
                        GUILayout.Label("refCount".PadRight(10) + "matName".PadRight(80) + "ID".PadRight(10) + "isDirt".PadRight(10) + "isTemp", whiteCS);
                        int i = 0;
                        foreach (var p in MateTexCache.mateCache.cache)
                        {
                            GUILayout.Label(p.Value.refCount.ToString().PadRight(10) + p.Key.PadRight(80) + p.Value.item.GetInstanceID().ToString().PadRight(10) + p.Value.dirty.ToString().PadRight(10), (i++ % 2 == 0) ? grayCS : whiteCS);
                        }
                        foreach (var p in MateTexCache.mateCache.tempCache.cacheList)
                        {
                            GUILayout.Label(p.Value.refCount.ToString().PadRight(10) + p.Key.PadRight(80) + p.Value.item.GetInstanceID().ToString().PadRight(10) + p.Value.dirty.ToString().PadRight(10) + "True", (i++ % 2 == 0) ? grayCS : whiteCS);
                        }
                        GUILayout.Label("");
                        GUILayout.Label("TexCache", whiteCS);
                        GUILayout.Label("refCount".PadRight(10) + "texName".PadRight(80) + "ID".PadRight(10) + "isDirt".PadRight(10) + "isTemp", whiteCS);
                        i = 0;
                        foreach (var p in MateTexCache.texCache.cache)
                        {
                            if (p.Key.EndsWith("_i_.tex") || p.Key.StartsWith("_i_") || p.Key.Contains("_icon_"))
                            {
                                continue;
                            }
                            GUILayout.Label(p.Value.refCount.ToString().PadRight(10) + p.Key.PadRight(80) + p.Value.item.GetInstanceID().ToString().PadRight(10) + p.Value.dirty.ToString().PadRight(10), (i++ % 2 == 0) ? grayCS : whiteCS);
                        }
                        foreach (var p in MateTexCache.texCache.tempCache.cacheList)
                        {
                            GUILayout.Label(p.Value.refCount.ToString().PadRight(10) + p.Key.PadRight(80) + p.Value.item.GetInstanceID().ToString().PadRight(10) + p.Value.dirty.ToString().PadRight(10) + "True", (i++ % 2 == 0) ? grayCS : whiteCS);
                        }
                        GUI.enabled = true;
                    }
                    GUILayout.EndVertical();
                }
                GUILayout.EndScrollView();
            }
            GUILayout.EndArea();
        }

        void OnGUI()
        {
            if (!showGUI)
            {
                return;
            }
            GUI.Window(11452, new Rect(0, (Screen.height - HEIGHT) / 2f, WIDTH, HEIGHT), Window, "MateTexCache Debug");
        }
    }
}
#endif
