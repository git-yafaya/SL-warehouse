using System;
using System.Linq;
using HarmonyLib;

namespace SLWarehouse
{
    public class WarehouseMod : Mod
    {
        private const int MaxIngredientCount = 100;
        private const int MaxCapacity = 1000000;

        public override void Ready()
        {
            Harmony.PatchAll(typeof(WarehouseMod).Assembly);
            foreach (string cardId in new[] { "slw_large_warehouse", "slw_larger_warehouse", "slw_huge_warehouse" })
            {
                ConfigureWarehouse(cardId);
                WorldManager.instance.GameDataLoader.AddCardToSetCardBag(
                    SetCardBagType.AdvancedBuildingIdea, "blueprint_" + cardId, 1);
            }
            Config.Save();
            Logger.Log("Warehouse upgrades and in-game settings ready.");
        }

        private void ConfigureWarehouse(string cardId)
        {
            GameDataLoader loader = WorldManager.instance.GameDataLoader;
            UpgradedWarehouse warehouse = (UpgradedWarehouse)loader.GetCardFromId(cardId);
            Subprint recipe = ((Blueprint)loader.GetCardFromId("blueprint_" + cardId)).Subprints[0];
            int count = ReadSetting(cardId + "_count", recipe.RequiredCards.Length, MaxIngredientCount, "slw_config_count_help");
            warehouse.Capacity = ReadSetting(cardId + "_capacity", warehouse.Capacity, MaxCapacity, "slw_config_capacity_help");
            recipe.RequiredCards = Enumerable.Repeat(recipe.RequiredCards[0], count).ToArray();
            recipe.CardsToRemove = (string[])recipe.RequiredCards.Clone();
        }

        private int ReadSetting(string key, int defaultValue, int maximum, string tooltipTerm)
        {
            ConfigEntry<int> entry = Config.GetEntry<int>(key, defaultValue, new ConfigUI
            {
                NameTerm = key,
                TooltipTerm = tooltipTerm,
                RestartAfterChange = true
            });
            // 配方会按数量分配数组，限制输入范围并将修正值保存回配置。
            entry.OnChanged += value =>
            {
                int validValue = Math.Max(1, Math.Min(maximum, value));
                if (value != validValue)
                    entry.Value = validValue;
            };
            entry.Value = Math.Max(1, Math.Min(maximum, entry.Value));
            return entry.Value;
        }
    }

    public class UpgradedWarehouse : Building
    {
        public int Capacity;

        public override void SetFoil()
        {
            // 超大仓库预制体已是闪卡，读档时不能再次叠加原版五倍售价。
            if (Id == "slw_huge_warehouse" && IsFoil)
                return;
            base.SetFoil();
        }
    }

    [HarmonyPatch(typeof(CardData), "get_Description")]
    internal static class WarehouseDescriptionPatch
    {
        [HarmonyPostfix]
        private static void Postfix(CardData __instance, ref string __result)
        {
            UpgradedWarehouse warehouse = __instance as UpgradedWarehouse;
            if (warehouse != null)
                __result = SokLoc.Translate("slw_warehouse_description", LocParam.Create("capacity", warehouse.Capacity.ToString()));
        }
    }

    [HarmonyPatch(typeof(WorldManager), "CardCapIncrease")]
    internal static class WarehouseCapacityPatch
    {
        [HarmonyPostfix]
        internal static void Postfix(WorldManager __instance, GameBoard __0, ref int __result)
        {
            // 城市使用市政厅规则，仓库只为普通棋盘追加容量。
            if (__0.Id == "cities")
                return;

            foreach (GameCard card in __instance.AllCards)
            {
                if (card.MyBoard != __0)
                    continue;

                UpgradedWarehouse warehouse = card.CardData as UpgradedWarehouse;
                if (warehouse != null)
                    __result += warehouse.Capacity;
            }
        }
    }
}
