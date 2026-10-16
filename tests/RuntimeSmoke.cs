using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using SLWarehouse;

// 独立测试 Mod，仅在显式测试参数下运行，不打入发布包。
public class WarehouseSmokeTest : Mod
{
    private readonly List<GameObject> temporaryObjects = new List<GameObject>();
    private readonly List<string> results = new List<string>();

    public override void Ready()
    {
        if (Environment.GetCommandLineArgs().Contains("-slw-smoke-test"))
            StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        yield return null;
        WorldManager world = WorldManager.instance;
        List<GameCard> originalCards = world.AllCards;
        try
        {
            world.AllCards = new List<GameCard>();
            GameBoard mainland = NewObject("mainland").AddComponent<GameBoard>();
            mainland.Id = "main";
            GameBoard island = NewObject("island").AddComponent<GameBoard>();
            island.Id = "island";
            GameBoard cities = NewObject("cities").AddComponent<GameBoard>();
            cities.Id = "cities";
            Check(world.CardCapIncrease(mainland) == 0, "empty board");

            if (Environment.GetCommandLineArgs().Contains("-slw-config-reload"))
            {
                CheckReloadedConfiguration(mainland);
            }
            else
            {
                GameCard vanilla = Card("warehouse", mainland);
                int vanillaCapacity = world.CardCapIncrease(mainland);
                Check(vanillaCapacity > 0, "vanilla warehouse preserved");
                results.Add("INFO vanilla warehouse capacity with installed mods: " + vanillaCapacity);
                results.Add("INFO vanilla warehouse sell value: " + vanilla.CardData.Value);
                GameCard large = Card("slw_large_warehouse", mainland);
                Check(world.CardCapIncrease(mainland) == vanillaCapacity + 30, "large +30");
                GameCard larger = Card("slw_larger_warehouse", mainland);
                Check(world.CardCapIncrease(mainland) == vanillaCapacity + 130, "larger +100 additive");
                GameCard huge = Card("slw_huge_warehouse", mainland);
                Check(huge.CardData.IsFoil, "huge warehouse is always foil on creation");
                Check(!large.CardData.IsFoil && !larger.CardData.IsFoil, "other warehouse tiers retain normal rarity");
                SavedCard foilSave = huge.ToSavedCard();
                Check(foilSave.IsFoil, "huge warehouse saves foil state");
                GameCard foilReload = Card(foilSave.CardPrefabId, mainland);
                foilReload.CardData.SetFoil();
                foilReload.CardData.SetFoil();
                Check(foilReload.CardData.IsFoil && foilReload.CardData.Value == 750, "foil reload does not multiply sell value again");
                SavedCard oldSave = new SavedCard { CardPrefabId = "slw_huge_warehouse", IsFoil = false };
                GameCard oldReload = Card(oldSave.CardPrefabId, mainland);
                Check(oldReload.CardData.IsFoil && oldReload.CardData.Value == 750, "old non-foil save uses guaranteed foil prefab");
                world.AllCards.Remove(foilReload);
                world.AllCards.Remove(oldReload);
                Check(world.CardCapIncrease(mainland) == vanillaCapacity + 930, "huge +800 additive");
                huge.MyBoard = island;
                Check(world.CardCapIncrease(mainland) == vanillaCapacity + 130, "move removes source capacity");
                Check(world.CardCapIncrease(island) == 800, "destination board isolation");
                huge.MyBoard = cities;
                Check(world.CardCapIncrease(cities) == 0, "city capacity unchanged");
                world.AllCards.Remove(larger);
                Check(world.CardCapIncrease(mainland) == vanillaCapacity + 30, "removed warehouse no longer counted");
                Check(large.CardData.Name == "大仓库" || large.CardData.Name == "Large Warehouse", "localized card name");

                string[] inputs = { "warehouse", "slw_large_warehouse", "slw_larger_warehouse" };
                string[] outputs = { "slw_large_warehouse", "slw_larger_warehouse", "slw_huge_warehouse" };
                int[] counts = { 2, 3, 5 };
                int[] capacities = { 30, 100, 800 };
                for (int index = 0; index < outputs.Length; index++)
                {
                    Blueprint blueprint = world.GameDataLoader.GetCardFromId("blueprint_" + outputs[index]) as Blueprint;
                    Check(blueprint != null, outputs[index] + " blueprint loaded");
                    Subprint print = blueprint.Subprints[0];
                    Check(print.ResultCard == outputs[index] && print.CardsToRemove.Length == counts[index]
                        && print.CardsToRemove.All(id => id == inputs[index]), outputs[index] + " consumption and result");
                    Check(print.Time > 0, outputs[index] + " non-instant recipe");
                    SubprintMatchInfo match;
                    Check(print.StackMatchesSubprint(Stack(inputs[index], counts[index], mainland), out match), outputs[index] + " exact stack accepted");
                    Check(!print.StackMatchesSubprint(Stack(inputs[index], counts[index] - 1, mainland), out match), outputs[index] + " insufficient stack rejected");
                    Check(!print.StackMatchesSubprint(Stack(inputs[index], counts[index] + 1, mainland), out match), outputs[index] + " excess stack rejected");
                    GameCard mixed = Stack(inputs[index], counts[index], mainland);
                    mixed.CardData = world.GameDataLoader.GetCardFromId("wood");
                    Check(!print.StackMatchesSubprint(mixed, out match), outputs[index] + " wrong ingredient rejected");

                    GameCard product = Card(outputs[index], mainland);
                    Check(product.CardData.Icon != null && product.CardData.Icon.texture.width > 0, outputs[index] + " custom PNG loaded");
                    SavedCard saved = product.ToSavedCard();
                    Check(saved.CardPrefabId == outputs[index], outputs[index] + " save identifier");
                    GameCard restored = Card(saved.CardPrefabId, mainland);
                    Check(((UpgradedWarehouse)restored.CardData).Capacity == capacities[index], outputs[index] + " prefab reload restores capacity");
                }
                CheckConfiguration();
            }
            results.Add("PASS all runtime smoke checks");
        }
        catch (Exception exception)
        {
            results.Add("FAIL " + exception);
        }
        finally
        {
            world.AllCards = originalCards;
            foreach (GameObject temporary in temporaryObjects)
                UnityEngine.Object.DestroyImmediate(temporary);
            string resultName = Environment.GetCommandLineArgs().Contains("-slw-config-reload") ? "results.config.txt" : "results.txt";
            File.WriteAllLines(System.IO.Path.Combine(Path, resultName), results.ToArray());
        }
        // 由启动脚本结束测试进程，避免触发游戏的退出存档流程。
    }

    private void CheckConfiguration()
    {
        Mod mod;
        Check(ModManager.TryGetMod("sl_warehouse", out mod), "settings mod available");
        Check(mod.Config.Entries.Count == 6, "six native settings registered");
        foreach (ConfigEntryBase entry in mod.Config.Entries)
        {
            Check(entry.ValueType == typeof(int) && !entry.UI.Hidden && entry.UI.RestartAfterChange,
                entry.Name + " native integer input with reload");
            Check(!entry.UI.GetName().Contains("MISSING"), entry.Name + " localized label");
        }

        ConfigEntry<int> count = mod.Config.GetEntry<int>("slw_large_warehouse_count");
        ConfigEntry<int> capacity = mod.Config.GetEntry<int>("slw_large_warehouse_capacity");
        count.Value = -1;
        Check(count.Value == 1, "negative ingredient count clamped");
        count.Value = int.MaxValue;
        Check(count.Value == 100, "excess ingredient count clamped");
        capacity.Value = 0;
        Check(capacity.Value == 1, "zero capacity clamped");
        capacity.Value = int.MaxValue;
        Check(capacity.Value == 1000000, "excess capacity clamped");

        string[] ids = { "slw_large_warehouse", "slw_larger_warehouse", "slw_huge_warehouse" };
        int[] counts = { 4, 6, 8 };
        int[] capacities = { 45, 250, 1600 };
        for (int index = 0; index < ids.Length; index++)
        {
            mod.Config.GetEntry<int>(ids[index] + "_count").Value = counts[index];
            mod.Config.GetEntry<int>(ids[index] + "_capacity").Value = capacities[index];
        }
        ModOptionsScreen screen = ModOptionsScreen.instance;
        Mod previousSelection = ModOptionsScreen.SelectedMod;
        bool wasActive = screen.gameObject.activeSelf;
        try
        {
            ModOptionsScreen.SelectedMod = mod;
            screen.gameObject.SetActive(true);
            TMPro.TMP_InputField[] inputs = screen.ButtonsParent.GetComponentsInChildren<TMPro.TMP_InputField>();
            Check(inputs.Length == 6, "native settings screen creates six input fields");
            inputs[0].text = "7";
            Check(count.Value == 7 && screen.ShouldRestart, "native text input updates config and requests reload");
            inputs[0].text = counts[0].ToString();
        }
        finally
        {
            screen.gameObject.SetActive(wasActive);
            screen.ShouldRestart = false;
            ModOptionsScreen.SelectedMod = previousSelection;
        }
        mod.Config.Save();
        ConfigFile saved = new ConfigFile(mod, System.IO.Path.Combine(mod.Path, "config.json"));
        for (int index = 0; index < ids.Length; index++)
        {
            Check(saved.GetValue<int>(ids[index] + "_count") == counts[index], ids[index] + " ingredient count persisted");
            Check(saved.GetValue<int>(ids[index] + "_capacity") == capacities[index], ids[index] + " capacity persisted");
        }
    }

    private void CheckReloadedConfiguration(GameBoard board)
    {
        string[] ids = { "slw_large_warehouse", "slw_larger_warehouse", "slw_huge_warehouse" };
        string[] inputs = { "warehouse", "slw_large_warehouse", "slw_larger_warehouse" };
        int[] counts = { 4, 6, 8 };
        int[] oldCounts = { 2, 3, 5 };
        int[] capacities = { 45, 250, 1600 };
        WorldManager world = WorldManager.instance;
        for (int index = 0; index < ids.Length; index++)
        {
            world.AllCards.Clear();
            GameCard warehouse = Card(ids[index], board);
            Check(world.CardCapIncrease(board) == capacities[index], ids[index] + " configured capacity after restart");
            Check(warehouse.CardData.Description.Contains(capacities[index].ToString()), ids[index] + " description reflects configured capacity");
            Subprint recipe = ((Blueprint)world.GameDataLoader.GetCardFromId("blueprint_" + ids[index])).Subprints[0];
            Check(recipe.CardsToRemove.Length == counts[index] && recipe.CardsToRemove.All(id => id == inputs[index]), ids[index] + " configured consumption");
            SubprintMatchInfo match;
            Check(recipe.StackMatchesSubprint(Stack(inputs[index], counts[index], board), out match), ids[index] + " configured recipe accepted");
            Check(!recipe.StackMatchesSubprint(Stack(inputs[index], oldCounts[index], board), out match), ids[index] + " previous recipe rejected");
            SavedCard saved = warehouse.ToSavedCard();
            GameCard restored = Card(saved.CardPrefabId, board);
            Check(((UpgradedWarehouse)restored.CardData).Capacity == capacities[index], ids[index] + " saved identifier uses configured capacity");
        }
    }

    private GameObject NewObject(string name)
    {
        GameObject item = new GameObject("slw-test-" + name);
        item.SetActive(false);
        temporaryObjects.Add(item);
        return item;
    }

    private GameCard Card(string id, GameBoard board)
    {
        CardData prefab = WorldManager.instance.GameDataLoader.GetCardFromId(id);
        GameObject item = UnityEngine.Object.Instantiate(prefab.gameObject);
        item.SetActive(false);
        temporaryObjects.Add(item);
        CardData data = item.GetComponent<CardData>();
        GameCard card = item.AddComponent<GameCard>();
        card.CardData = data;
        card.MyBoard = board;
        data.MyGameCard = card;
        WorldManager.instance.AllCards.Add(card);
        return card;
    }

    private GameCard Stack(string id, int count, GameBoard board)
    {
        GameCard root = Card(id, board);
        GameCard tail = root;
        for (int index = 1; index < count; index++)
        {
            GameCard child = Card(id, board);
            child.Parent = tail;
            tail.Child = child;
            tail = child;
        }
        return root;
    }

    private void Check(bool condition, string name)
    {
        if (!condition)
            throw new InvalidOperationException(name);
        results.Add("PASS " + name);
    }
}
