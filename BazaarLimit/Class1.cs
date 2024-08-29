using BepInEx;
using MonoMod.Cil;
using RoR2;
using UnityEngine;
using System;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using BepInEx.Configuration;
using System.Runtime.CompilerServices;

namespace R2API.Utils
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class ManualNetworkRegistrationAttribute : Attribute
    {
    }
}

namespace BazaarLimit
{
    [BepInDependency("com.rune580.riskofoptions", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin("com.Moffein.BazaarLimit", "BazaarLimit", "1.1.1")]
    public class BazaarLimitPlugin : BaseUnityPlugin
    {
        private static bool usedNewt = false;
        private static SceneDef bazaarSceneDef = Addressables.LoadAssetAsync<SceneDef>("RoR2/Base/bazaar/bazaar.asset").WaitForCompletion();

        private static int loopBazaarCount = 0;
        public static ConfigEntry<int> maxLoopBazaarCount;

        private static int loopNewtUses = 0;
        public static ConfigEntry<int> maxLoopNewtUses;

        public static ConfigEntry<bool> fixedRandomPortalChance;

        public void Awake()
        {
            maxLoopBazaarCount = Config.Bind("General", "Max Bazaar Entries per Loop", -1, "How many times you can visit the Bazaar Between Time each loop. When the limit is reached, random blue portals stop spawning and Newt Altars become unable to be used. -1 disables this check.");
            maxLoopNewtUses = Config.Bind("General", "Max Newt Altars per Loop", 1, "How many times you can use Newt Altars per loop. When the limit is reached, Newt Altars become unable to be used. -1 disables this check.");
            fixedRandomPortalChance = Config.Bind("General", "Random Bazaar Portal - Fixed Chance", true, "Random blue orbs have a fixed chance, instead of lowering based on bazaar visits.");

            if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.rune580.riskofoptions")) RiskOfOptionsSupport();

            On.RoR2.Run.Start += (orig, self) =>
            {
                usedNewt = false;
                loopNewtUses = 0;
                loopBazaarCount = 0;
                orig(self);
            };

            RoR2.Stage.onStageStartGlobal += Stage_onStageStartGlobal;

            IL.RoR2.TeleporterInteraction.Start += (il) =>
            {
                ILCursor c = new ILCursor(il);
                c.GotoNext(MoveType.After,
                    x => x.MatchLdfld(typeof(RoR2.TeleporterInteraction), "baseShopSpawnChance"));
                c.EmitDelegate<Func<float, float>>(origChance =>
                {
                    return (loopBazaarCount >= maxLoopBazaarCount.Value) ? 0f : origChance;
                });
            };

            IL.RoR2.TeleporterInteraction.Start += (il) =>
            {
                ILCursor c = new ILCursor(il);
                c.GotoNext(MoveType.After,
                    x => x.MatchLdfld(typeof(RoR2.Run), "shopPortalCount"));
                c.EmitDelegate<Func<int, int>>(origCount =>
                {
                    return fixedRandomPortalChance.Value ? 0 : origCount;
                });
            };

            On.RoR2.PortalStatueBehavior.GrantPortalEntry += (orig, self) =>
            {
                orig(self);
                if (NetworkServer.active && self.portalType == PortalStatueBehavior.PortalType.Shop)
                {
                    usedNewt = true;
                }
            };

            On.RoR2.SceneExitController.Begin += (orig, self) =>
            {
                orig(self);
                if (self.destinationScene == bazaarSceneDef)
                {
                    if (usedNewt)
                    {
                        loopNewtUses++;
                    }
                }
            };
        }

        private void Stage_onStageStartGlobal(Stage obj)
        {
            usedNewt = false;
            if (SceneCatalog.GetSceneDefForCurrentScene() == bazaarSceneDef)
            {
                loopBazaarCount++;
            }

            //Runs after count is incremented. Visiting Bazaar on stage 5 will still let you visit the bazaar on the next loop.
            if (Run.instance.stageClearCount % 5 == 0)
            {
                loopBazaarCount = 0;
                loopNewtUses = 0;
            }

            bool reachedMaxBazaarCount = maxLoopBazaarCount.Value >= 0 && loopBazaarCount >= maxLoopBazaarCount.Value;
            bool reachedMaxNewtCount = maxLoopNewtUses.Value >= 0 && loopNewtUses >= maxLoopNewtUses.Value;

            //Copied from PortalStatueBehavior code
            if (NetworkServer.active && (reachedMaxBazaarCount || reachedMaxNewtCount))
            {
                foreach (PortalStatueBehavior portalStatueBehavior in UnityEngine.Object.FindObjectsOfType<PortalStatueBehavior>())
                {
                    if (portalStatueBehavior.portalType == PortalStatueBehavior.PortalType.Shop)
                    {
                        PurchaseInteraction component = portalStatueBehavior.GetComponent<PurchaseInteraction>();
                        if (component)
                        {
                            component.Networkavailable = false;
                        }
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void RiskOfOptionsSupport()
        {
            RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.IntSliderOption(maxLoopBazaarCount, new RiskOfOptions.OptionConfigs.IntSliderConfig() { min = -1, max = 5 }));
            RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.IntSliderOption(maxLoopNewtUses, new RiskOfOptions.OptionConfigs.IntSliderConfig() { min = -1, max = 5 }));
            RiskOfOptions.ModSettingsManager.AddOption(new RiskOfOptions.Options.CheckBoxOption(fixedRandomPortalChance));
        }
    }
}
