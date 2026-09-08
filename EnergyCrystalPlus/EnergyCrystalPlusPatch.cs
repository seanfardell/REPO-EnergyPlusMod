using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace EnergyCrystalPlus;

[HarmonyPatch(typeof(ChargingStation), "Start")]
internal class EnergyCrystalPlusPatch
{
    private static readonly bool IsLoggingOn = true;
    private static readonly float ChargeTotalConverterUnit = 1000f;

    private static void DebugLog(String text)
    {
        if (IsLoggingOn)
        {
            Debug.Log((object)$"EnergyCrystal+: {text}");
        }
    }
    
    [HarmonyPatch(nameof(ChargingStation.Start))]
    [HarmonyPrefix]
    private static void Prefix(ref float ___chargeFloat, ref int ___chargeInt, ref float ___chargeRate, ref int ___chargeTotal)
    {
        DebugLog("Prefix--------------------");
        if (SemiFunc.RunIsShop())
        {
            // save current charge if we are in the store
            StatsManager.instance.runStats["crystalsOnLastShopEntry"] =
                StatsManager.instance.itemsPurchased["Item Power Crystal"];
            DebugLog($"pre-shop-crystals={StatsManager.instance.runStats["crystalsOnLastShopEntry"]}");
        }
    }
    
    [HarmonyPatch(nameof(ChargingStation.Start))]
    [HarmonyPostfix]
    private static void Postfix(ref float ___chargeFloat, ref int ___chargeInt, ref float ___chargeRate, ref int ___chargeTotal)
    {
        DebugLog("--------------------Postfix");
        // pull stat data for current energy
        int crystalsOnLastShopEntry = StatsManager.instance.runStats.GetValueOrDefault("crystalsOnLastShopEntry",-1);
        int chargeTotal = StatsManager.instance.runStats.GetValueOrDefault("energyCrystalPlusChargeTotal",-1);
        int numCrystals = StatsManager.instance.itemsPurchased["Item Power Crystal"] - crystalsOnLastShopEntry;
        float chargeTotalConverted = chargeTotal / ChargeTotalConverterUnit;
        if (crystalsOnLastShopEntry < 0)
        {
            DebugLog("shop has not been entered before, skipping postfix logic");
        } 
        else if (numCrystals == 0)
        {
            ___chargeFloat = chargeTotalConverted;
            ___chargeTotal = (int)Math.Round(chargeTotalConverted * 100);
            DebugLog($"not detecting any new crystals, saved value= {___chargeFloat}");
        }
        else if (SemiFunc.RunIsLobby()) 
        {
            // check nullables and get config values
            int configBase = EnergyCrystalPlus.ModConfig.GetBaseChargePerCrystal();
            int configAdditional = EnergyCrystalPlus.ModConfig.GetAdditionalPerPlayer();
            int configMax = EnergyCrystalPlus.ModConfig.GetMaxChargePerCrystal();
            DebugLog($"Config base={configBase} scaled={configAdditional} max={configMax}");

            // check lobby for other players for scaling
            int totalEnergyCrystalEnergy = configBase;
            try 
            {
                Room currentRoom = PhotonNetwork.CurrentRoom;
                if (PhotonNetwork.InRoom && currentRoom != null && currentRoom.PlayerCount > 1) 
                {
                    DebugLog($"players={currentRoom.PlayerCount}");
                    totalEnergyCrystalEnergy += (currentRoom.PlayerCount - 1) * configAdditional;
                    totalEnergyCrystalEnergy = totalEnergyCrystalEnergy > configMax ? 
                        configMax : totalEnergyCrystalEnergy;
                }
            } 
            catch (Exception e) 
            {
                DebugLog($"exception={e.Message}");
            }

            // run the math
            int totalChargeToAdd = numCrystals * totalEnergyCrystalEnergy;
            float newChargeTotal = chargeTotalConverted + (totalChargeToAdd / 100f);
            
            // log results for testing
            DebugLog($"initial={chargeTotalConverted}, crystals={numCrystals}, " +
                     $"totaladd={totalChargeToAdd}, newtotal={newChargeTotal}");

            // set new value
            ___chargeFloat = newChargeTotal;
            ___chargeTotal = (int)Math.Round(newChargeTotal * 100);
            
            // save to stats and account for the new crystals
            StatsManager.instance.runStats["energyCrystalPlusChargeTotal"] = (int)Math.Round(___chargeFloat * ChargeTotalConverterUnit);
            StatsManager.instance.runStats["crystalsOnLastShopEntry"] = StatsManager.instance.itemsPurchased["Item Power Crystal"];
        }
    }
     
    [HarmonyPatch(nameof(ChargingStation.Update))]
    [HarmonyPostfix]
    private static void Update_Postfix(ref float ___chargeFloat)
    {
        int roundedChargeFloat = (int)Math.Round(___chargeFloat * ChargeTotalConverterUnit);
        bool valueUpdated = roundedChargeFloat != StatsManager.instance.runStats.GetValueOrDefault("energyCrystalPlusChargeTotal", -1);
        if (!SemiFunc.RunIsShop() && valueUpdated)
        {
            // if arnt in the shop we should keep track of the actual charge
            DebugLog($"update-post: {___chargeFloat}");
            StatsManager.instance.runStats["energyCrystalPlusChargeTotal"] = roundedChargeFloat;
        }
    }
}