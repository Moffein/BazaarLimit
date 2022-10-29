using BepInEx;
using MonoMod.Cil;
using RoR2;
using UnityEngine;
using System;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;

namespace BazaarLimit
{
    [BepInPlugin("com.Moffein.BazaarLimit", "BazaarLimit", "1.0.0")]
    public class BazaarLimitPlugin : BaseUnityPlugin
    {
        public static int loopBazaarCount = 0;
        public static int maxLoopBazaarCount = 1;
        public static bool allowRandomPortalSpawn = true;
        public static bool fixedRandomPortalChance = true;

        public void Awake()
        {
            maxLoopBazaarCount = Config.Bind<int>("General", "Max Bazaar Visits per Loop", 1, "How many times you can visit the Bazaar Between Time each loop before Newt Altars are disabled.").Value;
            allowRandomPortalSpawn = Config.Bind<bool>("General", "Random Bazaar Portal - Always Allow Spawn", true, "Random blue orbs can spawn even if the Bazaar Limit is reached.").Value;
            fixedRandomPortalChance = Config.Bind<bool>("General", "Random Bazaar Portal - Fixed Chance", true, "Random blue orbs have a fixed chance, instead of lowering based on bazaar visits.").Value;

            On.RoR2.Run.Start += (orig, self) =>
            {
                 loopBazaarCount = 0;
                 orig(self);
            };

            On.RoR2.Stage.Start += (orig, self) =>
            {
                orig(self);

                SceneDef sceneDef = SceneCatalog.GetSceneDefForCurrentScene();
                if (sceneDef && sceneDef.baseSceneName.Equals("bazaar"))
                {
                    loopBazaarCount++;
                }

                //Runs after count is incremented. Visiting Bazaar on stage 5 will still let you visit the bazaar on the next loop.
                if (Run.instance.stageClearCount % 5 == 0)
                {
                    loopBazaarCount = 0;
                }

                //Copied from PortalStatueBehavior code
                if (loopBazaarCount >= maxLoopBazaarCount)
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
            };

            if (!allowRandomPortalSpawn)
            {
                IL.RoR2.TeleporterInteraction.Start += (il) =>
                {
                    ILCursor c = new ILCursor(il);
                    c.GotoNext(MoveType.After,
                        x => x.MatchLdfld(typeof(RoR2.TeleporterInteraction), "baseShopSpawnChance"));
                    c.EmitDelegate<Func<float, float>>(origChance =>
                    {
                        if (loopBazaarCount >= maxLoopBazaarCount) return 0f;
                        return origChance;
                    });
                };
            }

            if (fixedRandomPortalChance)
            {
                IL.RoR2.TeleporterInteraction.Start += (il) =>
                {
                    ILCursor c = new ILCursor(il);
                    c.GotoNext(MoveType.After,
                        x => x.MatchLdfld(typeof(RoR2.Run), "shopPortalCount"));
                    c.EmitDelegate<Func<int, int>>(origCount =>
                    {
                        return 0;
                    });
                };
            }
        }
    }
}
