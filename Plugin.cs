using BepInEx;
using HarmonyLib;
using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using ShipInventoryUpdated.Scripts; 
using ShipInventoryUpdated.Objects; 
using System.Reflection;

[BepInPlugin("com.alpme.quotaaddon", "Ship Inventory Quota Addon", "1.3.0")]
public class QuotaPlugin : BaseUnityPlugin
{
    void Awake()
    {
        var harmony = new Harmony("com.alpme.quotaaddon");
        harmony.PatchAll();
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
                __result = CreateNode("NO ITEMS DETECTED");
                return false;
            }

            int needed = TimeOfDay.Instance.profitQuota - TimeOfDay.Instance.quotaFulfilled;
            if (needed <= 0) {
                __result = CreateNode("QUOTA ALREADY MET");
                return false;
            }

            // Get valid items and their values
            var validItems = allItems
                .Select(item => new { Data = item, Value = GetAnyValue(item) })
                .Where(x => x.Value > 0)
                .ToList();

            // Find the best combination
            var bestCombo = FindBestCombination(validItems.Select(x => x.Value).ToList(), needed);
            
            // Map those values back to the actual ItemData objects
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
                __result = CreateNode($"Quota Retrieval\n----------------\nQuota Required: {needed}\nAmount Taken: {finalSum}\nExtra Payment: {finalSum - needed}\nTotal Items Sold: {toPull.Count}");
            }
            else
            {
                __result = CreateNode("Could not find a valid combination of items.");
            }

            return false;
        }
        return true;
    }

    // Logic: Find the combination of numbers that sums to >= target with minimum overshoot
    private static List<int> FindBestCombination(List<int> values, int target)
    {
        // If the total sum of all items is less than needed, just return everything
        if (values.Sum() <= target) return values;

        int n = values.Count;
        // We limit the search to avoid lag if you have 500+ items
        // Standard DP approach for Subset Sum
        int maxPossible = target + values.Max(); 
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

        // Find the smallest j >= target that is reachable
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

        return values; // Fallback
    }

    private static int GetAnyValue(ItemData item)
    {
        FieldInfo[] fields = typeof(ItemData).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var field in fields)
        {
            if (field.FieldType == typeof(int))
            {
                int val = (int)field.GetValue(item);
                if (val > 1 && val < 2000) return val;
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