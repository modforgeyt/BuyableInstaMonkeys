global using BTD_Mod_Helper.Extensions;
using HarmonyLib;
using BTD_Mod_Helper.Api;
using Il2CppAssets.Scripts.Models.Profile;
using Il2CppAssets.Scripts.Unity;
using Il2CppAssets.Scripts.Unity.UI_New.Main.PowersSelect;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppInterop.Runtime;
using Il2CppTMPro;
using MelonLoader;
using BTD_Mod_Helper;
using BuyableInstaMonkeys;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[assembly: MelonInfo(typeof(BuyableInstaMonkeys.BuyableInstaMonkeys), ModHelperData.Name, ModHelperData.Version, ModHelperData.RepoOwner)]
[assembly: MelonGame("Ninja Kiwi", "BloonsTD6")]
[assembly: MelonGame("Ninja Kiwi", "BloonsTD6-Epic")]

namespace BuyableInstaMonkeys;

public class BuyableInstaMonkeys : BloonsTD6Mod
{
    internal const int RandomTierZeroPrice = 125;

    internal static readonly string[] TowerIds =
    {
        "DartMonkey", "BoomerangMonkey", "BombShooter", "TackShooter", "IceMonkey", "GlueGunner",
        "SniperMonkey", "MonkeySub", "MonkeyBuccaneer", "MonkeyAce", "HeliPilot", "MortarMonkey",
        "DartlingGunner", "WizardMonkey", "SuperMonkey", "NinjaMonkey", "Alchemist", "Druid",
        "Mermonkey", "Skywarden", "BananaFarm", "SpikeFactory", "MonkeyVillage", "EngineerMonkey"
    };

    private static readonly InstaOption[] InstaOptions = CreateInstaOptions();

    internal static readonly StoreOffer[] StoreOffers =
    {
        new("RANDOM 250 MM", 250),
        new("0-0-0 125 MM", 125, new[] { 0, 0, 0 }),
        new("T1 250 MM", 250, tier: 1),
        new("T2 500 MM", 500, tier: 2),
        new("T3 1250 MM", 1250, tier: 3),
        new("T4 2500 MM", 2500, tier: 4),
        new("T5 5000 MM", 5000, tier: 5)
    };

    private sealed class InstaOption
    {
        internal readonly int[] Tiers;
        internal readonly int Weight;

        internal InstaOption(int[] tiers)
        {
            Tiers = tiers;

            Weight = 1 << (8 - (tiers[0] + tiers[1] + tiers[2]));
        }
    }

    internal sealed class StoreOffer
    {
        internal readonly string Label;
        internal readonly int Price;
        internal readonly int[]? ExactTiers;
        internal readonly int? MainTier;

        internal StoreOffer(string label, int price, int[]? exactTiers = null, int? tier = null)
        {
            Label = label;
            Price = price;
            ExactTiers = exactTiers;
            MainTier = tier;
        }
    }

    public override void OnApplicationStart()
    {
        ModHelper.Msg<BuyableInstaMonkeys>("BuyableInstaMonkeys loaded!");
    }

    internal static void BuyInsta(InstaTowerScreen screen, StoreOffer offer, string? towerId = null)
    {
        var player = Game.Player;
        if (player == null)
            return;

        if (player.GetMonkeyMoney() < offer.Price)
        {
            Game.instance.audioFactory.PlaySoundFromUnity(screen.click1Sound, "BuyableInstaMonkeysNoMoney");
            ModHelper.Msg<BuyableInstaMonkeys>("Not enough Monkey Money to buy an Insta Monkey.");
            return;
        }

        towerId ??= TowerIds[Random.Range(0, TowerIds.Length)];
        var tiers = offer.ExactTiers ?? GetRandomUpgradeTiers(offer.MainTier);
        player.SpendMonkeyMoney(offer.Price, "Buyable Insta Monkey");
        player.AddInstaTower(towerId, new Il2CppStructArray<int>(tiers), 1);
        player.SaveNow();
        ModContent.GetAudioClip<BuyableInstaMonkeys>("UIGetGold").Play("BuyableInstaMonkeysPurchase");

        screen.UpdateTypesCounts();
        ModHelper.Msg<BuyableInstaMonkeys>($"Bought a {towerId} {tiers[0]}-{tiers[1]}-{tiers[2]} Insta Monkey for {offer.Price} Monkey Money.");
    }

    private static InstaOption[] CreateInstaOptions()
    {
        var options = new List<InstaOption>();
        var seen = new HashSet<string>();

        void AddOption(int[] tiers)
        {
            if (seen.Add($"{tiers[0]}-{tiers[1]}-{tiers[2]}"))
                options.Add(new InstaOption(tiers));
        }

        AddOption(new[] { 0, 0, 0 });

        for (var mainPath = 0; mainPath < 3; mainPath++)
        {
            for (var mainTier = 1; mainTier <= 5; mainTier++)
            {
                AddOption(CreateTiers(mainPath, mainTier, -1, 0));

                for (var crossPath = 0; crossPath < 3; crossPath++)
                {
                    if (crossPath == mainPath)
                        continue;

                    for (var crossTier = 1; crossTier <= 2; crossTier++)
                        AddOption(CreateTiers(mainPath, mainTier, crossPath, crossTier));
                }
            }
        }

        return options.ToArray();
    }

    private static int[] CreateTiers(int mainPath, int mainTier, int crossPath, int crossTier)
    {
        var tiers = new[] { 0, 0, 0 };
        tiers[mainPath] = mainTier;
        if (crossPath >= 0)
            tiers[crossPath] = crossTier;
        return tiers;
    }

    private static int[] GetRandomUpgradeTiers(int? requiredMainTier = null)
    {
        var totalWeight = 0;
        foreach (var option in InstaOptions)
        {
            if (!requiredMainTier.HasValue || GetMainTier(option.Tiers) == requiredMainTier.Value)
                totalWeight += option.Weight;
        }

        var roll = Random.Range(0, totalWeight);
        foreach (var option in InstaOptions)
        {
            if (requiredMainTier.HasValue && GetMainTier(option.Tiers) != requiredMainTier.Value)
                continue;

            roll -= option.Weight;
            if (roll < 0)
                return option.Tiers;
        }

        return new[] { 0, 0, 0 };
    }

    internal static List<int[]> GetPathsForOffer(StoreOffer offer)
    {
        if (offer.ExactTiers != null)
            return new List<int[]> { offer.ExactTiers };

        var paths = new List<int[]>();
        foreach (var option in InstaOptions)
        {
            if (!offer.MainTier.HasValue || GetMainTier(option.Tiers) == offer.MainTier.Value)
                paths.Add(option.Tiers);
        }

        return paths;
    }

    private static int GetMainTier(int[] tiers) => Mathf.Max(tiers[0], Mathf.Max(tiers[1], tiers[2]));

}

[HarmonyPatch(typeof(InstaTowerScreen), nameof(InstaTowerScreen.Awake))]
internal static class InstaTowerScreenAwakePatch
{
    [HarmonyPostfix]
    private static void Postfix(InstaTowerScreen __instance)
    {
        if (__instance.collectionViewToggle.transform.parent.Find("BuyableInstaMonkeysButton") != null)
            return;

        var labels = new List<Transform>();
        var offerButtons = new List<GameObject>();
        var pickerButtons = new List<GameObject>();
        var pickerPages = new[]
        {
            new List<GameObject>(), new List<GameObject>(), new List<GameObject>()
        };
        var pathPages = new List<List<GameObject>>();
        var pathObjects = new List<GameObject>();
        BuyableInstaMonkeys.StoreOffer? selectedOffer = null;
        string? selectedTowerId = null;
        var pickerPage = 0;
        var pathPage = 0;
        var showingPaths = false;

        var pickerPanel = new GameObject("BuyableInstaMonkeysPickerMenu", Il2CppType.Of<RectTransform>(), Il2CppType.Of<Image>());
        pickerPanel.transform.SetParent(__instance.transform, false);
        var pickerPanelRect = pickerPanel.GetComponent<RectTransform>();
        pickerPanelRect.anchorMin = Vector2.zero;
        pickerPanelRect.anchorMax = Vector2.one;
        pickerPanelRect.offsetMin = Vector2.zero;
        pickerPanelRect.offsetMax = Vector2.zero;
        pickerPanel.GetComponent<Image>().color = new Color(0f, 0.08f, 0.16f, 0.96f);

        void SetPickerVisible(bool visible)
        {
            pickerPanel.SetActive(visible);
        }

        void ShowPickerPage(int page)
        {
            pickerPage = Mathf.Clamp(page, 0, pickerPages.Length - 1);
            for (var index = 0; index < pickerPages.Length; index++)
            {
                foreach (var pickerObject in pickerPages[index])
                    pickerObject.SetActive(index == pickerPage);
            }
        }

        void ShowPathPage(int page)
        {
            if (pathPages.Count == 0)
                return;

            pathPage = Mathf.Clamp(page, 0, pathPages.Count - 1);
            for (var index = 0; index < pathPages.Count; index++)
            {
                foreach (var pathObject in pathPages[index])
                    pathObject.SetActive(index == pathPage);
            }
        }

        void ShowTowerPicker()
        {
            showingPaths = false;
            foreach (var pathObject in pathObjects)
                pathObject.SetActive(false);
            ShowPickerPage(0);
        }

        void ShowPathPicker(BuyableInstaMonkeys.StoreOffer offer)
        {
            showingPaths = true;
            foreach (var page in pickerPages)
            {
                foreach (var pickerObject in page)
                    pickerObject.SetActive(false);
            }

            foreach (var pathObject in pathObjects)
                Object.Destroy(pathObject);
            pathObjects.Clear();
            pathPages.Clear();

            var paths = BuyableInstaMonkeys.GetPathsForOffer(offer);
            for (var page = 0; page < Mathf.CeilToInt(paths.Count / 9f); page++)
                pathPages.Add(new List<GameObject>());

            for (var index = 0; index < paths.Count; index++)
            {
                var tiers = paths[index];
                var page = index / 9;
                var buttonObject = Object.Instantiate(__instance.collectionViewToggle.gameObject, pickerPanel.transform);
                pathObjects.Add(buttonObject);
                pathPages[page].Add(buttonObject);

                var button = buttonObject.GetComponent<Toggle>();
                button.onValueChanged.RemoveAllListeners();
                button.isOn = false;
                var rect = buttonObject.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.localScale = new Vector3(0.08f, 0.08f, 1f);
                rect.anchoredPosition = new Vector2(-450f + 450f * (index % 3), 240f - 420f * ((index % 9) / 3));

                var label = buttonObject.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                {
                    label.text = $"{tiers[0]}-{tiers[1]}-{tiers[2]}";
                    label.transform.SetParent(pickerPanel.transform, false);
                    var labelRect = label.GetComponent<RectTransform>();
                    labelRect.anchorMin = rect.anchorMin;
                    labelRect.anchorMax = rect.anchorMax;
                    labelRect.pivot = new Vector2(0.5f, 0.5f);
                    labelRect.anchoredPosition = rect.anchoredPosition + new Vector2(0f, -95f);
                    labelRect.sizeDelta = new Vector2(220f, 65f);
                    labelRect.localScale = new Vector3(0.52f, 0.52f, 1f);
                    label.enableWordWrapping = false;
                    label.fontSize = 45f;
                    label.transform.SetAsLastSibling();
                    pathObjects.Add(label.gameObject);
                    pathPages[page].Add(label.gameObject);
                }

                button.onValueChanged.AddListener((UnityAction<bool>)(isOn =>
                {
                    if (!isOn || selectedTowerId == null)
                        return;

                    button.isOn = false;
                    BuyableInstaMonkeys.BuyInsta(
                        __instance,
                        new BuyableInstaMonkeys.StoreOffer(offer.Label, offer.Price, tiers),
                        selectedTowerId);
                }));
            }

            ShowPathPage(0);
        }

        for (var index = 0; index < BuyableInstaMonkeys.StoreOffers.Length; index++)
        {
            var offer = BuyableInstaMonkeys.StoreOffers[index];
            var buttonObject = Object.Instantiate(
                __instance.collectionViewToggle.gameObject,
                __instance.collectionViewToggle.transform.parent);
            buttonObject.name = index == 0 ? "BuyableInstaMonkeysButton" : $"BuyableInstaMonkeysButton{index}";
            offerButtons.Add(buttonObject);

            var button = buttonObject.GetComponent<Toggle>();
            button.onValueChanged.RemoveAllListeners();
            button.isOn = false;

            var label = buttonObject.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
                label.text = offer.Label;

            var rect = buttonObject.GetComponent<RectTransform>();
            rect.localScale = new Vector3(0.35f, 0.35f, 1f);
            var column = index % 2;
            var row = index / 2;
            var xOffset = column == 0 ? -240f : 40f;
            rect.anchoredPosition += new Vector2(xOffset, -460f - 420f * row);

            if (label != null)
            {
                var labelRect = label.GetComponent<RectTransform>();
                label.transform.SetParent(__instance.collectionViewToggle.transform.parent, false);
                labelRect.anchorMin = rect.anchorMin;
                labelRect.anchorMax = rect.anchorMax;
                labelRect.pivot = new Vector2(0.5f, 0.5f);
                labelRect.anchoredPosition = rect.anchoredPosition + new Vector2(0f, -130f);
                labelRect.sizeDelta = new Vector2(300f, 90f);
                labelRect.localScale = new Vector3(0.75f, 0.75f, 1f);
                label.enableWordWrapping = false;
                label.fontSize = 60f;
                labels.Add(label.transform);
            }

            button.onValueChanged.AddListener((UnityAction<bool>)(isOn =>
            {
                if (!isOn)
                    return;

                if (offer.ExactTiers == null && offer.MainTier == null)
                {
                    BuyableInstaMonkeys.BuyInsta(__instance, offer);
                    button.isOn = false;
                    return;
                }

                selectedOffer = offer;
                selectedTowerId = null;
                ShowTowerPicker();
                SetPickerVisible(true);
                button.isOn = false;
            }));
        }

        for (var index = 0; index < BuyableInstaMonkeys.TowerIds.Length; index++)
        {
            var towerId = BuyableInstaMonkeys.TowerIds[index];
            var buttonObject = Object.Instantiate(
                __instance.collectionViewToggle.gameObject,
                pickerPanel.transform);
            buttonObject.name = $"BuyableInstaMonkeysPicker{towerId}";
            pickerButtons.Add(buttonObject);
            pickerPages[index / 9].Add(buttonObject);

            var button = buttonObject.GetComponent<Toggle>();
            button.onValueChanged.RemoveAllListeners();
            button.isOn = false;

            var label = buttonObject.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
                label.text = towerId.Replace("Monkey", " Monkey").Replace("Shooter", " Shooter").Replace("Gunner", " Gunner").Replace("Factory", " Factory").Replace("Village", " Village").Replace("Buccaneer", " Buccaneer").Replace("Mermonkey", "Mermonkey").ToUpperInvariant();

            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = new Vector3(0.08f, 0.08f, 1f);
            var column = index % 3;
            var row = (index % 9) / 3;
            rect.anchoredPosition = new Vector2(-450f + 450f * column, 240f - 420f * row);

            if (label != null)
            {
                var labelRect = label.GetComponent<RectTransform>();
                label.transform.SetParent(pickerPanel.transform, false);
                labelRect.anchorMin = rect.anchorMin;
                labelRect.anchorMax = rect.anchorMax;
                labelRect.pivot = new Vector2(0.5f, 0.5f);
                labelRect.anchoredPosition = rect.anchoredPosition + new Vector2(0f, -95f);
                labelRect.sizeDelta = new Vector2(220f, 65f);
                labelRect.localScale = new Vector3(0.52f, 0.52f, 1f);
                label.enableWordWrapping = true;
                label.fontSize = 34f;
                labels.Add(label.transform);
                pickerPages[index / 9].Add(label.gameObject);
            }

            button.onValueChanged.AddListener((UnityAction<bool>)(isOn =>
            {
                if (!isOn || selectedOffer == null)
                    return;

                button.isOn = false;
                selectedTowerId = towerId;
                ShowPathPicker(selectedOffer);
            }));
        }

        var backObject = Object.Instantiate(__instance.collectionViewToggle.gameObject, pickerPanel.transform);
        backObject.name = "BuyableInstaMonkeysPickerBack";
        var backButton = backObject.GetComponent<Toggle>();
        backButton.onValueChanged.RemoveAllListeners();
        backButton.isOn = false;
        var backRect = backObject.GetComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0.5f, 0.5f);
        backRect.anchorMax = new Vector2(0.5f, 0.5f);
        backRect.pivot = new Vector2(0.5f, 0.5f);
        backRect.localScale = new Vector3(0.18f, 0.18f, 1f);
        backRect.anchoredPosition = new Vector2(-500f, 480f);
        var backLabel = backObject.GetComponentInChildren<TextMeshProUGUI>();
        if (backLabel != null)
        {
            backLabel.text = "BACK";
            backLabel.transform.SetParent(pickerPanel.transform, false);
            var backLabelRect = backLabel.GetComponent<RectTransform>();
            backLabelRect.anchorMin = backRect.anchorMin;
            backLabelRect.anchorMax = backRect.anchorMax;
            backLabelRect.pivot = new Vector2(0.5f, 0.5f);
            backLabelRect.anchoredPosition = backRect.anchoredPosition + new Vector2(0f, -85f);
            backLabelRect.sizeDelta = new Vector2(180f, 55f);
            backLabelRect.localScale = new Vector3(0.55f, 0.55f, 1f);
            backLabel.enableWordWrapping = false;
            backLabel.fontSize = 60f;
        }
        backButton.onValueChanged.AddListener((UnityAction<bool>)(isOn =>
        {
            if (!isOn)
                return;

            backButton.isOn = false;
            if (showingPaths)
                ShowTowerPicker();
            else
                SetPickerVisible(false);
        }));

        var previousObject = Object.Instantiate(__instance.collectionViewToggle.gameObject, pickerPanel.transform);
        var previousButton = previousObject.GetComponent<Toggle>();
        previousButton.onValueChanged.RemoveAllListeners();
        previousButton.isOn = false;
        var previousRect = previousObject.GetComponent<RectTransform>();
        previousRect.anchorMin = new Vector2(0.5f, 0.5f);
        previousRect.anchorMax = new Vector2(0.5f, 0.5f);
        previousRect.localScale = new Vector3(0.05f, 0.05f, 1f);
        previousRect.anchoredPosition = new Vector2(-170f, 480f);
        var previousLabel = previousObject.GetComponentInChildren<TextMeshProUGUI>();
        if (previousLabel != null)
            previousLabel.text = "PREVIOUS";
        previousButton.onValueChanged.AddListener((UnityAction<bool>)(isOn =>
        {
            if (isOn)
            {
                previousButton.isOn = false;
                if (showingPaths)
                    ShowPathPage(pathPage - 1);
                else
                    ShowPickerPage(pickerPage - 1);
            }
        }));

        var nextObject = Object.Instantiate(__instance.collectionViewToggle.gameObject, pickerPanel.transform);
        var nextButton = nextObject.GetComponent<Toggle>();
        nextButton.onValueChanged.RemoveAllListeners();
        nextButton.isOn = false;
        var nextRect = nextObject.GetComponent<RectTransform>();
        nextRect.anchorMin = new Vector2(0.5f, 0.5f);
        nextRect.anchorMax = new Vector2(0.5f, 0.5f);
        nextRect.localScale = new Vector3(0.05f, 0.05f, 1f);
        nextRect.anchoredPosition = new Vector2(170f, 480f);
        var nextLabel = nextObject.GetComponentInChildren<TextMeshProUGUI>();
        if (nextLabel != null)
            nextLabel.text = "NEXT";
        nextButton.onValueChanged.AddListener((UnityAction<bool>)(isOn =>
        {
            if (isOn)
            {
                nextButton.isOn = false;
                if (showingPaths)
                    ShowPathPage(pathPage + 1);
                else
                    ShowPickerPage(pickerPage + 1);
            }
        }));

        SetPickerVisible(false);
        ShowPickerPage(0);

        foreach (var labelTransform in labels)
            labelTransform.SetAsLastSibling();
        pickerPanel.transform.SetAsLastSibling();
    }
}
