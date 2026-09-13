using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace EnergyCrystalPlus;

[HarmonyPatch(typeof(ChargingStation))]
internal class EnergyCrystalPlusPatch
{
    private static readonly String KeyChargeFloatX1000 = "energyCrystalPlusChargeFloat";
    private static readonly String KeyCrystalsPurchased = "crystalsOnLastShopEntry";
    private static readonly String KeyItemEnergyCrystal = "Item Power Crystal";
    

    private static void DebugLog(String text)
    {
        bool IsDebugEnabled = EnergyCrystalPlus.ModConfig.IsDebugEnabled();
        if (IsDebugEnabled)
        {
            Debug.Log((object)$"EnergyCrystal+: {text}");
        }
    }
    
    private static int GetEnergyCrystalValue()
    {
        // check nullables and get config values
        int configBase = EnergyCrystalPlus.ModConfig.GetBaseChargePerCrystal();
        int configAdditional = EnergyCrystalPlus.ModConfig.GetAdditionalPerPlayer();
        int configMax = EnergyCrystalPlus.ModConfig.GetMaxChargePerCrystal();
        DebugLog($"Config base={configBase} scaled={configAdditional} max={configMax}");

        // check lobby for other players for scaling
        int totalEnergyCrystalEnergy = configBase;
        Room currentRoom = PhotonNetwork.CurrentRoom;
        if (PhotonNetwork.InRoom && currentRoom != null && currentRoom.PlayerCount > 1) 
        {
            // check if energy goes over desired max
            DebugLog($"players={currentRoom.PlayerCount}");
            totalEnergyCrystalEnergy += (currentRoom.PlayerCount - 1) * configAdditional;
            totalEnergyCrystalEnergy = totalEnergyCrystalEnergy > configMax ? 
                configMax : totalEnergyCrystalEnergy;
        }

        DebugLog($"energyPlusPerCrystal={totalEnergyCrystalEnergy}");
        return totalEnergyCrystalEnergy;
    }

    private static void SaveRunStats(float chargeFloat, int crystalsPurchased)
    {
        SaveRunStatsChargeFloat((int)Math.Round(chargeFloat * 1000f));
        SaveRunStatsCrystalsPurchased(crystalsPurchased);
    }

    private static void SaveRunStatsChargeFloat(int chargeFloatX1000)
    {
        StatsManager.instance.runStats[KeyChargeFloatX1000] = chargeFloatX1000;
    }

    private static void SaveRunStatsCrystalsPurchased(int crystalsPurchased)
    {
        StatsManager.instance.runStats[KeyCrystalsPurchased] = crystalsPurchased;
    }
    
    [HarmonyPatch(nameof(ChargingStation.Start))]
    [HarmonyPrefix]
    private static void Prefix(ref float ___chargeFloat, ref int ___chargeInt, ref float ___chargeRate, ref int ___chargeTotal)
    {
        DebugLog("Prefix--------------------");
        if (SemiFunc.RunIsShop())
        {
            // save current crystal purchases if we are in the store
            SaveRunStatsCrystalsPurchased(StatsManager.instance.itemsPurchasedTotal[KeyItemEnergyCrystal]);
            DebugLog($"pre-shop-crystals={StatsManager.instance.runStats[KeyCrystalsPurchased]}");
        }
    }
    
    [HarmonyPatch(nameof(ChargingStation.Start))]
    [HarmonyPostfix]
    private static void Postfix(ref float ___chargeFloat, ref int ___chargeInt, ref float ___chargeRate, ref int ___chargeTotal)
    {
        DebugLog("--------------------Postfix");
        // pull run stats for saved data
        int crystalsOnLastShopEntry = StatsManager.instance.runStats.GetValueOrDefault(KeyCrystalsPurchased,-1);
        float chargeFloat = StatsManager.instance.runStats.GetValueOrDefault(KeyChargeFloatX1000,-1) / 1000f;
        int numCrystals = StatsManager.instance.itemsPurchasedTotal[KeyItemEnergyCrystal] - crystalsOnLastShopEntry;
        if (crystalsOnLastShopEntry < 0)
        {
            DebugLog("no shop data detected");
            // if we havent been to the store yet, save some initial data
            int level = StatsManager.instance.runStats.GetValueOrDefault("level", -1);
            if (SemiFunc.RunIsLevel() && level <= 0)
            {
                ___chargeTotal = GetEnergyCrystalValue();
                ___chargeFloat = ___chargeTotal / 100f;
                SaveRunStats(___chargeFloat, StatsManager.instance.itemsPurchasedTotal[KeyItemEnergyCrystal]);
            }
            DebugLog($"level {level} detected, initial value={___chargeFloat}");
        } 
        else if (numCrystals <= 0)
        {
            // pull from saved values if nothing is changed
            ___chargeFloat = chargeFloat;
            ___chargeTotal = (int)Math.Round(chargeFloat * 100);
            DebugLog($"not detecting any new crystals, saved value={___chargeFloat}");
        }
        else if (SemiFunc.RunIsLobby()) 
        {
            // calculate new values
            int totalChargeToAdd = numCrystals * GetEnergyCrystalValue();
            float newChargeTotal = chargeFloat + (totalChargeToAdd / 100f);
            
            // log results
            DebugLog($"initial={chargeFloat}, crystals={numCrystals}, " +
                     $"totaladd={totalChargeToAdd}, newtotal={newChargeTotal}");

            // set new value
            ___chargeFloat = newChargeTotal;
            ___chargeTotal = (int)Math.Round(newChargeTotal * 100);
            
            // save to stats and account for the new crystals
            SaveRunStats(___chargeFloat, StatsManager.instance.itemsPurchasedTotal[KeyItemEnergyCrystal]);
        }
    }
     
    [HarmonyPatch(nameof(ChargingStation.Update))]
    [HarmonyPostfix]
    private static void Update_Postfix(ref float ___chargeFloat)
    {
        int chargedFloatX1000 = (int)Math.Round(___chargeFloat * 1000f);
        bool valueUpdated = chargedFloatX1000 != StatsManager.instance.runStats.GetValueOrDefault(KeyChargeFloatX1000, -1);
        if (!SemiFunc.RunIsShop() && valueUpdated)
        {
            // if arnt in the shop we should keep track of the actual charge
            DebugLog($"update-post: {___chargeFloat}");
            SaveRunStatsChargeFloat(chargedFloatX1000);
        }
    }
}