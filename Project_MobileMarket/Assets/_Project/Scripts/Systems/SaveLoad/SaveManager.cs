using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MobileMarket.Systems
{
    /// <summary>One saved item: its id, plus the ids in its grid if it's a backpack ("" = empty cell).</summary>
    [Serializable]
    public class ItemSave
    {
        public string id = "";
        public List<string> contents = new List<string>();
    }

    /// <summary>Everything the player carries: equipment slots and the pockets grid.</summary>
    [Serializable]
    public class InventorySaveData
    {
        public int version;
        public ItemSave head = new ItemSave();
        public ItemSave body = new ItemSave();
        public ItemSave backpack = new ItemSave();
        public ItemSave primary = new ItemSave();
        public List<string> pockets = new List<string>();

        // Version 0 saves were a flat list of item ids.
        public List<string> itemIds = new List<string>();
    }

    /// <summary>Persists the player's inventory to a JSON file. Map-spawned loot is never saved here.</summary>
    public class SaveManager : MonoBehaviour
    {
        public const int InventoryVersion = 1;

        public static SaveManager Instance { get; private set; }

        private string SavePath => Path.Combine(Application.persistentDataPath, "inventory_save.json");

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void SaveInventory(InventorySaveData data)
        {
            data.version = InventoryVersion;
            File.WriteAllText(SavePath, JsonUtility.ToJson(data));
        }

        /// <summary>Returns the saved inventory, or null if there is no save yet.</summary>
        public InventorySaveData LoadInventory()
        {
            if (!File.Exists(SavePath)) return null;
            return JsonUtility.FromJson<InventorySaveData>(File.ReadAllText(SavePath));
        }
    }
}
