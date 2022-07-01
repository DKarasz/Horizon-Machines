using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;

namespace Horizon
{
    //isflesh
    //animal, getlabelfor(humanlike), kill pretraded prekidnapped(relations)

    //ismechanoid
    //

    //check caravanhealth.dorow, questnodeismechanoid.matches, statworker.shouldshowfor(statrequest), mechkindsuitableforcluster
    //notify_pawnkilled/pawndowned kills count both mech and animal
    //specialdisplaystats(thingdef, statrequest), randompawnforcombat

    [StaticConstructorOnStartup]
    public static class Horizon
    {
        //public static bool IsAnimal()
        //{
        //    return RaceProperties.IsFlesh || RaceProperties.Animal;
        //}
        //public static bool IsNotAnimal()
        //{
        //    return !IsAnimal();
        //}
        static Horizon()
        {
            new Harmony("Horizon.Mod").PatchAll();
        }
    }
    public class MechAnimal : DefModExtension{}


    [HarmonyPatch(typeof(RaceProperties), "Animal", MethodType.Getter)]
    public static class MechAnimal_Patch
    {
        public static bool Prefix(RaceProperties __instance, ref bool __result)
        {
            __result = !__instance.ToolUser;
            return false;
        }
    }
    
    [HarmonyPatch(typeof(PawnComponentsUtility), "AddAndRemoveDynamicComponents")]
    public static class DynamicComponents_Patch
    {
        public static bool Prefix(Pawn pawn, bool actAsIfSpawned = false)
        {
            var mechanimal = pawn.kindDef.race.GetModExtension<MechAnimal>();
            if (mechanimal != null)
            {
                bool flag = pawn.Faction != null && pawn.Faction.IsPlayer;
                bool flag2 = pawn.HostFaction != null && pawn.HostFaction.IsPlayer;
                if (pawn.RaceProps.Humanlike && !pawn.Dead)
                {
                    if (pawn.mindState.wantsToTradeWithColony)
                    {
                        if (pawn.trader == null)
                        {
                            pawn.trader = new Pawn_TraderTracker(pawn);
                        }
                    }
                    else
                    {
                        pawn.trader = null;
                    }
                }
                if (pawn.RaceProps.Humanlike)
                {
                    if ((flag || flag2) && pawn.foodRestriction == null)
                    {
                        pawn.foodRestriction = new Pawn_FoodRestrictionTracker(pawn);
                    }
                    if (flag)
                    {
                        if (pawn.outfits == null)
                        {
                            pawn.outfits = new Pawn_OutfitTracker(pawn);
                        }
                        if (pawn.drugs == null)
                        {
                            pawn.drugs = new Pawn_DrugPolicyTracker(pawn);
                        }
                        if (pawn.timetable == null)
                        {
                            pawn.timetable = new Pawn_TimetableTracker(pawn);
                        }
                        if (pawn.inventoryStock == null)
                        {
                            pawn.inventoryStock = new Pawn_InventoryStockTracker(pawn);
                        }
                        if ((pawn.Spawned || actAsIfSpawned) && pawn.drafter == null)
                        {
                            pawn.drafter = new Pawn_DraftController(pawn);
                        }
                    }
                    else
                    {
                        pawn.drafter = null;
                    }
                }
                if ((flag || flag2) && pawn.playerSettings == null)
                {
                    pawn.playerSettings = new Pawn_PlayerSettings(pawn);
                }
                if ((int)pawn.RaceProps.intelligence <= 1 && pawn.Faction != null && pawn.training == null)
                {
                    pawn.training = new Pawn_TrainingTracker(pawn);
                }
                if (pawn.needs != null)
                {
                    pawn.needs.AddOrRemoveNeedsAsAppropriate();
                }
                return false;
            }
           return true;
        }
    }

    //[HarmonyPatch(typeof(RaceProperties), "BloodDef", MethodType.Getter)]
    //public static class BloodDef_Patch
    //{
    //    public static bool Prefix(RaceProperties __instance, ref ThingDef __result)
    //    {
    //        if (__instance.BloodDef != null)
    //        {
    //            __result = __instance.BloodDef;
    //        }
    //        if (!__instance.IsMechanoid)
    //        {
    //            __result = ThingDefOf.Filth_Blood;
    //        }
    //        __result = null;
    //        return false;
    //    }
    //}




    //public class CompProperties_ArmorPlate : CompProperties//adds comp for items
    //{
    //    public bool hitSibling = true;
    //    public CompProperties_ArmorPlate()
    //    {
    //        this.compClass = typeof(CompArmorPlate);
    //    }
    //}

    //public class CompArmorPlate : ThingComp
    //{
    //    public CompProperties_ArmorPlate Props => base.props as CompProperties_ArmorPlate;
    //    public bool Sibling => Props.hitSibling;
    //    public override void SetHitPart(BodyPartRecord forceHitPart)
    //    {
    //        base.PostPreApplyDamage();
    //        if (num2 != 0)
    //        {
    //            IEnumerable<BodyPartRecord> enumerable = dinfo.HitPart.GetDirectChildParts();
    //            pawn.health.hediffSet.GetRandomNotMissingPart();
    //            if (dinfo.HitPart.parent != null)
    //            {
    //                enumerable = enumerable.Concat(dinfo.HitPart.parent);
    //                if (dinfo.HitPart.parent.parent != null)
    //                {
    //                    enumerable = enumerable.Concat(dinfo.HitPart.parent.GetDirectChildParts());
    //                }
    //            }
    //            list2 = (from x in enumerable.Except(dinfo.HitPart).InRandomOrder().Take(num2)
    //                     where !x.def.conceptual
    //                     select x).ToList();
    //        }
    //        pawn.health.hediffSet.GetRandomNotMissingPart(dinfo.Def, dinfo.Height, dinfo.Depth, parent);
    //        if
    //    }
    //}
}
