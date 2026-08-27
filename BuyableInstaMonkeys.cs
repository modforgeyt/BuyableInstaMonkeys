global using BTD_Mod_Helper.Extensions;
using HarmonyLib;
using Il2CppAssets.Scripts.Models.Profile;
using Il2CppAssets.Scripts.Unity;
using Il2CppAssets.Scripts.Unity.UI_New.Main.PowersSelect;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
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
    internal const int RandomTierZeroPrice = 100;

    // These are base tower IDs used by BTD6's player profile.
    private static readonly string[] TowerIds =
    {
        "DartMonkey", "BoomerangMonkey", "BombShooter", "TackShooter", "IceMonkey", "GlueGunner",
        "SniperMonkey", "MonkeySub", "MonkeyBuccaneer", "MonkeyAce", "HeliPilot", "MortarMonkey",
        "DartlingGunner", "WizardMonkey", "SuperMonkey", "NinjaMonkey", "Alchemist", "Druid",
        "Mermonkey", "Skywarden", "BananaFarm", "SpikeFactory", "MonkeyVillage", "EngineerMonkey"
    };

    private static readonly InstaOption[] InstaOptions = CreateInstaOptions();

    internal static readonly StoreOffer[] StoreOffers =
    {
        new("RANDOM 200 MM", 200),
        new("0-0-0 50 MM", 50, new[] { 0, 0, 0 }),
        new("T1 100 MM", 100, tier: 1),
        new("T2 250 MM", 250, tier: 2),
        new("T3 500 MM", 500, tier: 3),
        new("T4 600 MM", 600, tier: 4),
        new("T5 1000 MM", 1000, tier: 5)
    };

    private sealed class InstaOption
    {
        internal readonly int[] Tiers;
        internal readonly int Weight;

        internal InstaOption(int[] tiers)
        {
            Tiers = tiers;

            // 0-0-0 has weight 256, one upgrade has 128, two upgrades has 64, etc.
            // This makes every higher-tier Insta less likely than the one before it.
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

    internal static void BuyInsta(InstaTowerScreen screen, StoreOffer offer)
    {
        var player = Game.Player;
        if (player == null)
            return;

        if (player.GetMonkeyMoney() < offer.Price)
        {
            ModHelper.Msg<BuyableInstaMonkeys>("Not enough Monkey Money to buy an Insta Monkey.");
            return;
        }

        var towerId = TowerIds[Random.Range(0, TowerIds.Length)];
        var tiers = offer.ExactTiers ?? GetRandomUpgradeTiers(offer.MainTier);
        player.SpendMonkeyMoney(offer.Price, "Buyable Insta Monkey");
        player.AddInstaTower(towerId, new Il2CppStructArray<int>(tiers), 1);
        player.SaveNow();

        // The built-in screen owns the count and collection UI, so refresh it after every purchase.
        screen.UpdateTypesCounts();
        ModHelper.Msg<BuyableInstaMonkeys>($"Bought a {towerId} {tiers[0]}-{tiers[1]}-{tiers[2]} Insta Monkey for {offer.Price} Monkey Money.");
    }

    private static InstaOption[] CreateInstaOptions()
    {
        var options = new List<InstaOption>();
        var seen = new HashSet<string>();

        void AddOption(int[] tiers)
        {
            // Two low-tier paths can be described in either order while generating.
            // Only add each actual 3-number Insta combination once.
            if (seen.Add($"{tiers[0]}-{tiers[1]}-{tiers[2]}"))
                options.Add(new InstaOption(tiers));
        }

        AddOption(new[] { 0, 0, 0 });

        // An Insta can have one main path at tier 1-5 and one crosspath at tier 0-2.
        // Generate every legal combination without allowing upgrades on all three paths.
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

        return new[] { 0, 0, 0 }; // Unreachable, but keeps the method safe if the pool changes.
    }

    private static int GetMainTier(int[] tiers) => Mathf.Max(tiers[0], Mathf.Max(tiers[1], tiers[2]));
}

[HarmonyPatch(typeof(InstaTowerScreen), nameof(InstaTowerScreen.Awake))]
internal static class InstaTowerScreenAwakePatch
{
    [HarmonyPostfix]
    private static void Postfix(InstaTowerScreen __instance)
    {
        // Awake can run again when returning to this screen. Keep exactly one stack of mod buttons.
        if (__instance.collectionViewToggle.transform.parent.Find("BuyableInstaMonkeysButton") != null)
            return;

        var labels = new List<Transform>();

        for (var index = 0; index < BuyableInstaMonkeys.StoreOffers.Length; index++)
        {
            var offer = BuyableInstaMonkeys.StoreOffers[index];
            var buttonObject = Object.Instantiate(
                __instance.collectionViewToggle.gameObject,
                __instance.collectionViewToggle.transform.parent);
            buttonObject.name = index == 0 ? "BuyableInstaMonkeysButton" : $"BuyableInstaMonkeysButton{index}";

            var button = buttonObject.GetComponent<Toggle>();
            button.onValueChanged.RemoveAllListeners();
            button.isOn = false;

            var label = buttonObject.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
                label.text = offer.Label;

            var rect = buttonObject.GetComponent<RectTransform>();
            // The original collection button is much taller than a shop row. Shrink each
            // cloned row and leave a clear gap so the card artwork never overlaps.
            rect.localScale = new Vector3(0.35f, 0.35f, 1f);
            // Two columns keep all purchase choices reachable without a long overlapping stack.
            var column = index % 2;
            var row = index / 2;
            var xOffset = column == 0 ? -240f : 40f;
            rect.anchoredPosition += new Vector2(xOffset, -460f - 420f * row);

            if (label != null)
            {
                // Give each label its own position below the icon, in the same parent as
                // all card buttons, so it cannot be covered by a later card.
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

                BuyableInstaMonkeys.BuyInsta(__instance, offer);
                button.isOn = false;
            }));
        }

        // Ensure every detached text label is drawn after (above) all card icons.
        foreach (var labelTransform in labels)
            labelTransform.SetAsLastSibling();
    }
}
