using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace EnergyCrystalPlus;

[BepInPlugin("Omeron.EnergyCrystalPlus", "EnergyCrystal+", "1.0.0")]
public class EnergyCrystalPlus : BaseUnityPlugin
{
    internal static EnergyCrystalPlus Instance { get; private set; } = null!;
    internal new static ManualLogSource Logger => Instance._logger;
    private ManualLogSource _logger => base.Logger;
    internal Harmony? Harmony { get; set; }
    
    private void Awake()
    {
        Instance = this;
        ModConfig.Bind(Config);
        Patch();

        Logger.LogInfo($"{Info.Metadata.GUID} v{Info.Metadata.Version} has loaded!");
    }

    internal static class ModConfig
    {
        internal static int DefaultBase = 10;
        internal static int DefaultAdditional = 5;
        internal static int DefaultMax = 30;
        internal static bool DefaultDebug = true;
        
        internal static ConfigEntry<int>? BaseChargePerCrystal { get; private set; }
        internal static ConfigEntry<int>? AdditionalPerPlayer { get; private set; }
        internal static ConfigEntry<int>? MaxChargePerCrystal { get; private set; }
        internal static ConfigEntry<bool>? EnableDebug { get; private set; }

        public static int GetBaseChargePerCrystal() 
        {
            return BaseChargePerCrystal != null
                ? BaseChargePerCrystal.Value
                : DefaultBase;
            
        }

        public static int GetAdditionalPerPlayer() 
        {
            return AdditionalPerPlayer != null
                ? AdditionalPerPlayer.Value
                : DefaultAdditional;
        }

        public static int GetMaxChargePerCrystal() 
        {
            return MaxChargePerCrystal != null
                ? MaxChargePerCrystal.Value
                : DefaultMax;
        }
        
        public static bool IsDebugEnabled() 
        {
            return EnableDebug != null
                ? EnableDebug.Value
                : DefaultDebug;
        }
        
        internal static void Bind(ConfigFile config)
        {
            // Bind configuration settings
            BaseChargePerCrystal = config.Bind("Base Energy Crystal",
                "BaseCrystalCharge",
                DefaultBase,
                new ConfigDescription("Adjusts the base charge per energy crystal. (10 default)",
                    new AcceptableValueRange<int>(5, 100))
            );

            AdditionalPerPlayer = config.Bind("Multiplayer Scaling",
                "ChargePerPlayer",
                DefaultAdditional,
                new ConfigDescription("Adjusts how much charge gets added per non-host player in lobby.",
                    new AcceptableValueRange<int>(0, 25))
            );

            MaxChargePerCrystal = config.Bind("Multiplayer Scaling",
                "MaxCrystalCharge",
                DefaultMax,
                new ConfigDescription(
                    "Adjusts the max scaled charge a crystal can give based on the number of players.",
                    new AcceptableValueRange<int>(5, 100))
            );
            
            EnableDebug = config.Bind("Developer",
                "EnableDebug",
                DefaultDebug,
                new ConfigDescription(
                    "Controls whether the debug logging shows in the console.")
            );

            Logger.LogInfo(
                $"EnergyCrystal+ loaded with base={BaseChargePerCrystal.Value}, additional={AdditionalPerPlayer.Value}, max={MaxChargePerCrystal.Value}, debug={EnableDebug}");
        }
    }

    internal void Patch()
    {
        Harmony ??= new Harmony(Info.Metadata.GUID);
        Harmony.PatchAll();
    }

    internal void Unpatch()
    {
        Harmony?.UnpatchSelf();
    }

    private void Update()
    {
        // Code that runs every frame goes here
    }
}