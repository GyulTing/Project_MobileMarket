using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using MobileMarket.Data;

namespace MobileMarket.Systems
{
    /// <summary>Persists the player's inventory to a JSON file. Map-spawned loot is never saved here.</summary>
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        [Serializable]
        private class InventorySaveData
        {
            public List<string> itemIds = new List<string>();
        }

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

        public void SaveInventory(IReadOnlyList<ItemData> items)
        {
            var data = new InventorySaveData();
            foreach (var item in items)
            {
                if (item != null) data.itemIds.Add(item.id);
            }
            File.WriteAllText(SavePath, JsonUtility.ToJson(data));
        }

        public List<ItemData> LoadInventory()
        {
            var result = new List<ItemData>();
            if (!File.Exists(SavePath)) return result;

            var data = JsonUtility.FromJson<InventorySaveData>(File.ReadAllText(SavePath));
            if (data == null) return result;

            foreach (var id in data.itemIds)
            {
                var item = ItemDatabase.GetById(id);
                if (item != null) result.Add(item);
            }
            return result;
        }
    }
}
