using HarmonyLib;
using HMLLibrary;
using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.UI;
using RaftModLoader;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;


namespace BatterySlotFixer
{
    public class Main : Mod
    {
        public Transform prefabHolder;
        public List<(GameObject, GameObject)> prefabs = new List<(GameObject, GameObject)>();
        public static string dataFolder = HLib.path_modsFolder + "\\ModData\\BatterySlotFixer\\";
        public static string fileName_Index = "itemIndecies.txt";
        public static string fileName_Name = "itemNames.txt";
        public List<int> itemIndecies = new List<int>();
        public List<string> itemNames = new List<string>();
        public List<Item_Base> items = new List<Item_Base>();
        public Battery prefab;
        public static Main instance;
        public void Start()
        {
            instance = this;
            prefabHolder = new GameObject("prefabHolder").transform;
            prefabHolder.gameObject.SetActive(false);
            DontDestroyOnLoad(prefabHolder.gameObject);

            prefab = Instantiate(ItemManager.GetItemByIndex(293).settings_buildable.GetBlockPrefab(0).GetComponentInChildren<Battery>(), prefabHolder, false);
            prefab.name = "BatterySlot";

            if (!Directory.Exists(dataFolder))
            {
                Directory.CreateDirectory(dataFolder);
                CreateDefaultIndexFile();
                CreateDefaultNameFile();
            }
            else
            {
                if (!File.Exists(dataFolder + fileName_Index))
                    CreateDefaultIndexFile();
                if (!File.Exists(dataFolder + fileName_Name))
                    CreateDefaultNameFile();
            }
            LoadConfig();
            Log("Mod has been loaded!");
        }

        float wait = 1;
        void Update()
        {
            wait += Time.deltaTime;
            if (wait >= 1) {
                wait %= 1;
                var missing = new List<(int?, string)>();
                foreach (var i in itemIndecies)
                    missing.Add((i, null));
                foreach (var i in itemNames)
                    missing.Add((null, i));
                items.RemoveAll(x => !x);
                foreach (var i in items)
                    missing.RemoveAll(x => x.Item1 == i.UniqueIndex || x.Item2 == i.UniqueName);
                foreach (var i in missing)
                {
                    var item = i.Item1 == null ? ItemManager.GetItemByName(i.Item2) : ItemManager.GetItemByIndex(i.Item1.Value);
                    if (item)
                    {
                        try
                        {
                            var p = item.settings_buildable.GetBlockPrefabs();
                            for (int j = 0; j < p.Length; j++)
                                p[j] = EditPrefab(p[j]);
                        } catch (Exception e) { Debug.LogError(e); }
                        items.Add(item);
                    }
                }

                for (int i = prefabs.Count - 1; i >= 0; i--)
                    if (!prefabs[i].Item1)
                    {
                        Destroy(prefabs[i].Item2);
                        prefabs.RemoveAt(i);
                    }
            }
        }

        static T EditPrefab<T>(T original) where T : Object
        {
            if (!original)
                return null;
            var t = (original as GameObject)?.transform ?? (original as Component)?.transform;
            if (!t)
                return null;
            var n = Instantiate(t, instance.prefabHolder);
            n.name = t.name;
            instance.prefabs.Add((t.gameObject, n.gameObject));
            var bats = n.GetComponentsInChildren<Battery>();
            var i = 0;
            foreach (var b in bats)
                if (b)
                {
                    var nb = Instantiate(instance.prefab, b.transform.parent, false);
                    nb.name = $"BatterySlot{(i++ > 0?$" ({i})":"")}";
                    var p = b.transform.parent.InverseTransformPoint(b.transform.TransformPoint(b.GetComponent<BoxCollider>().center)) - b.transform.parent.InverseTransformPoint(b.transform.TransformPoint(nb.GetComponent<BoxCollider>().center));

                    nb.transform.localPosition = b.transform.localPosition + p;
                    nb.transform.localRotation = b.transform.localRotation;
                    var a = nb.gameObject.AddComponent<BatteryAccess>();
                    a.batteryIndex = b.BatteryIndex;
                    a.networkBehaviourID = b.NetworkMessageReciever;
                    DestroyImmediate(a);
                    foreach (var c in nb.GetComponents<Component>())
                    {
                        var oc = b.GetComponent(c.GetType());
                        if (oc)
                            n.ReplaceValues(oc, c);
                    }
                    n.ReplaceValues(b.gameObject, nb.gameObject);
                    DestroyImmediate(b.gameObject);
                }
            return n.GetComponent<T>();
        }

        public void OnModUnload()
        {
            Log("Mod has been unloaded!");
        }
        
        [ConsoleCommand(name: "reloadBatteryFixConfig", docs: "Reloads the mod's config files to edit any additional items added to the item lists")]
        public static string MyCommand(string[] args)
        {
            try
            {
                instance.LoadConfig();
                return "Config successfully loaded";
            }
            catch (Exception e)
            {
                return $"Failed to load the config\n{e}";
            }
        }

        void CreateDefaultIndexFile()
        {
            File.WriteAllBytes(dataFolder + fileName_Index, GetEmbeddedFileBytes("defaultIndecies.txt"));
        }
        void CreateDefaultNameFile()
        {
            File.WriteAllBytes(dataFolder + fileName_Name, GetEmbeddedFileBytes("defaultNames.txt"));
        }

        void LoadConfig()
        {
            itemIndecies.Clear();
            itemNames.Clear();
            try
            {
                foreach (var l in File.ReadAllLines(dataFolder + fileName_Index))
                    if (int.TryParse(l, out var i))
                        itemIndecies.Add(i);
            }
            catch (Exception e) { Debug.LogError($"An error occured while reading item indecies\n{e}"); }
            try
            { 
                foreach (var l in File.ReadAllLines(dataFolder + fileName_Name))
                    itemNames.Add(l);
            }
            catch (Exception e) { Debug.LogError($"An error occured while reading item names\n{e}"); }
        }
    }
    class BatteryAccess : MonoBehaviour
    {
        static FieldInfo _networkBehaviourID = typeof(Battery).GetField("networkBehaviourID", ~BindingFlags.Default);
        static FieldInfo _batteryIndex = typeof(Battery).GetField("batteryIndex", ~BindingFlags.Default);
        public MonoBehaviour_ID_Network networkBehaviourID
        {
            get => (MonoBehaviour_ID_Network)_networkBehaviourID.GetValue(GetComponent<Battery>());
            set => _networkBehaviourID.SetValue(GetComponent<Battery>(), value);
        }
        public int batteryIndex
        {
            get => (int)_batteryIndex.GetValue(GetComponent<Battery>());
            set => _batteryIndex.SetValue(GetComponent<Battery>(), value);
        }
    }

    static class ExtentionMethods
    {
        public static void CopyFieldsOf(this object value, object source)
        {
            var t1 = value.GetType();
            var t2 = source.GetType();
            while (!t1.IsAssignableFrom(t2))
                t1 = t1.BaseType;
            while (t1 != typeof(Object) && t1 != typeof(object))
            {
                foreach (var f in t1.GetFields(~BindingFlags.Default))
                    if (!f.IsStatic)
                        f.SetValue(value, f.GetValue(source));
                t1 = t1.BaseType;
            }
        }

        public static void ReplaceValues(this Component value, object original, object replacement, int serializableLayers = 0)
        {
            foreach (var c in value.GetComponentsInChildren<Component>())
                (c as object).ReplaceValues(original, replacement, serializableLayers);
        }
        public static void ReplaceValues(this GameObject value, object original, object replacement, int serializableLayers = 0)
        {
            foreach (var c in value.GetComponentsInChildren<Component>())
                (c as object).ReplaceValues(original, replacement, serializableLayers);
        }

        public static void ReplaceValues(this object value, object original, object replacement, int serializableLayers = 0)
        {
            if (value == null)
                return;
            var t = value.GetType();
            while (t != typeof(Object) && t != typeof(object))
            {
                foreach (var f in t.GetFields(~BindingFlags.Default))
                    if (!f.IsStatic)
                    {
                        if (f.GetValue(value) == original || (f.GetValue(value)?.Equals(original) ?? false))
                            f.SetValue(value, replacement);
                        else if (f.GetValue(value) is IList)
                        {
                            var l = f.GetValue(value) as IList;
                            for (int i = 0; i < l.Count; i++)
                                if (l[i] == original || (l[i]?.Equals(original) ?? false))
                                    l[i] = replacement;
                        }
                        else if (serializableLayers > 0 && (f.GetValue(value)?.GetType()?.IsSerializable ?? false))
                            f.GetValue(value).ReplaceValues(original, replacement, serializableLayers - 1);
                    }
                t = t.BaseType;
            }
        }
    }
}