using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using ShipInventoryUpdated.Scripts; 
using ShipInventoryUpdated.Objects; 
using System.Reflection;

namespace ShipInventoryQuotaAddon
{
    public static class PluginInfo
    {
        public const string GUID = "Detologist.ShipInventoryQuotaAddon";
        public const string NAME = "Ship Inventory Quota Addon";
        public const string VERSION = "1.3.0";
    }

    [BepInPlugin(PluginInfo.GUID, PluginInfo.NAME, PluginInfo.VERSION)]
    public class QuotaPlugin : BaseUnityPlugin
    {
        public static ManualLogSource Log;

        void Awake()
        {
            Log = Logger;
            
            var harmony = new Harmony(PluginInfo.GUID);
            harmony.PatchAll();
            
            Log.LogInfo($"{PluginInfo.NAME} v{PluginInfo.VERSION} loaded successfully.");
        }
    }

    [HarmonyPatch(typeof(Terminal))]
    public class TerminalPatch
    {
        [HarmonyPatch("ParsePlayerSentence")]
        [HarmonyPrefix]
        static bool Prefix(Terminal __instance, ref TerminalNode __result)
        {
            string input = __instance.screenText.text.Substring(__instance.screenText.text.Length - __instance.textAdded).ToLower().Trim();

            if (input == "rquota")
            {
                ItemData[] allItems = Inventory.Items;
                
                if (allItems == null || allItems.Length == 0)
                {
                    __result = CreateNode("INVENTORY EMPTY");
                    return false;
                }

                int needed = TimeOfDay.Instance.profitQuota - TimeOfDay.Instance.quotaFulfilled;
                if (needed <= 0) 
                {
                    __result = CreateNode("QUOTA ALREADY MET");
                    return false;
                }

                var validItems = allItems
                    .Select(item => new { Data = item, Value = GetAnyValue(item) })
                    .Where(x => x.Value > 0)
                    .ToList();

                var bestCombo = FindBestCombination(validItems.Select(x => x.Value).ToList(), needed);
                
                List<ItemData> toPull = new List<ItemData>();
                List<int> remainingValues = new List<int>(bestCombo);
                int finalSum = 0;

                foreach (var item in validItems)
                {
                    if (remainingValues.Contains(item.Value))
                    {
                        toPull.Add(item.Data);
                        finalSum += item.Value;
                        remainingValues.Remove(item.Value);
                    }
                }

                if (toPull.Count > 0)
                {
                    Inventory.Remove(toPull.ToArray());
                    __result = CreateNode($"QUOTA RETRIEVAL\n----------------\nQuota Required: {needed}\nAmount Taken: {finalSum}\nExtra Profit: {finalSum - (needed > 0 ? needed : 0)}\nItems Retrieved: {toPull.Count}");
                }
                else
                {
                    __result = CreateNode("NO VALID COMBINATION FOUND");
                }

                return false; 
            }
            return true;
        }

        private static List<int> FindBestCombination(List<int> values, int target)
        {
            if (values.Sum() <= target) return values;

            int n = values.Count;
            int maxPossible = target + (values.Count > 0 ? values.Max() : 0); 
            bool[] dp = new bool[maxPossible + 1];
            int[] parent = new int[maxPossible + 1];
            int[] itemUsed = new int[maxPossible + 1];
            
            dp[0] = true;

            for (int i = 0; i < n; i++)
            {
                int v = values[i];
                for (int j = maxPossible; j >= v; j--)
                {
                    if (!dp[j] && dp[j - v])
                    {
                        dp[j] = true;
                        parent[j] = j - v;
                        itemUsed[j] = v;
                    }
                }
            }

            for (int j = target; j <= maxPossible; j++)
            {
                if (dp[j])
                {
                    List<int> result = new List<int>();
                    int curr = j;
                    while (curr > 0)
                    {
                        result.Add(itemUsed[curr]);
                        curr = parent[curr];
                    }
                    return result;
                }
            }
            return values;
        }

        private static int GetAnyValue(ItemData item)
        {
            FieldInfo[] fields = typeof(ItemData).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (field.FieldType == typeof(int))
                {
                    int val = (int)field.GetValue(item);
                    if (val > 1 && val < 5000) return val;
                }
            }
            return 0;
        }

        private static TerminalNode CreateNode(string message)
        {
            TerminalNode node = ScriptableObject.CreateInstance<TerminalNode>();
            node.displayText = message + "\n\n";
            node.clearPreviousText = true;
            return node;
        }
    }
}
