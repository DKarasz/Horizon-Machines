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
using UnityEngine;
using Verse.AI.Group;
using static Verse.DamageWorker;
using MonoMod.Utils;
using Verse.Sound;

namespace Horizon
{
    //(not changed but potentially)
    //pawn_healthtracker.healthtick(machines dont heal, requires more advanced transpiler to change), toxicfallout(immune), isacceptableprey(not prey),TryAnestize(cant anestize), diseasecontractchancefactor(immune), immunitychangepertick(immune)
    //tickrare(doesnt push out heat), corpseingestiblenow(not edible)
    //checkdrugaddictionteachopportunity (swap isflesh to !ismech, maybe)(ignorefor now), prepostingested(swap !isflesh to ismech maybe), postingested(isflesh to !ismech),
    //disease.potentialvictims(immune),
    //debugoutputseconomy.wool, animaleconomy, animalbreeding(unsure)
    //recipedefgenerator.drugadmminiserdefs(swap isflesh with ismech if want to administer drug bills)
    //mindstatetick(weather related mood debuff), generateaddictionsandtolerancesfor(!sflesh to ismech, unless not wanted)(Ignorefor now)

    [StaticConstructorOnStartup]
    public static class Horizon
    {
        static Horizon()
        {

            //Harmony.DEBUG = true;
            new Harmony("Horizon.Mod").PatchAll();

        }
    }
    ////mechanimal extension: delayed until re-evaluation
    //public class MechAnimal : DefModExtension{}

    //[HarmonyPatch(typeof(RaceProperties),"IsMechanoid", MethodType.Getter)]
    //public static class MechAnimalPatch
    //{
    //    public static bool Prefix(ref bool __result, RaceProperties __instance)
    //    {
    //        if (__instance.AnyPawnKind.HasModExtension<MechAnimal>())
    //        {
    //            __result = false;
    //            return false;
    //        }
    //        return true;
    //    }
    //}
    ////isflesh patches
    //[HarmonyPatch(typeof(PawnCapacityDef), "GetLabelFor", new Type[] { typeof(bool), typeof(bool) })]
    //public static class PawnCapacityDef_GetLabelFor_Patch
    //{
    //    public static void Postfix(ref string __result, bool isFlesh, PawnCapacityDef __instance)
    //    {
    //        if (!isFlesh && !__instance.labelMechanoids.NullOrEmpty())
    //        {
    //            __result = __instance.labelMechanoids;
    //        }
    //    }
    //}
    //[HarmonyPatch]
    //public static class Isflesh_to_Isnotmech_Patch
    //{
    //    static IEnumerable<MethodBase> TargetMethods()
    //    {
    //        yield return AccessTools.Method(typeof(PawnComponentsUtility), "CreateInitialComponents");
    //        yield return AccessTools.Method(typeof(PawnComponentsUtility), "AddComponentsForSpawn");
    //        yield return AccessTools.PropertyGetter(typeof(RaceProperties), "Animal");
    //        yield return AccessTools.Method(typeof(Hediff_Pregnant), "DoBirthSpawn");
    //        yield return AccessTools.Method(typeof(Pawn), "SpawnSetup");
    //        yield return AccessTools.Method(typeof(Pawn), "Kill");
    //        yield return AccessTools.Method(typeof(Pawn), "PreTraded");
    //        yield return AccessTools.Method(typeof(Pawn), "PreKidnapped");
    //        yield return AccessTools.Method(typeof(PawnGenerator), "IsValidCandidateToRedress");
    //        yield return AccessTools.Method(typeof(PawnGenerator), "WorldPawnSelectionWeight");
    //        yield return AccessTools.Method(typeof(Pawn_HealthTracker), "PreApplyDamage");
    //        yield return AccessTools.Method(typeof(DebugToolsPawns), "AddRemovePawnRelation");
    //        yield return AccessTools.Method(typeof(JobGiver_MarryAdjacentPawn), "TryGiveJob");
    //        yield return AccessTools.Method(typeof(WorkGiver_TakeToBedToOperate), "HasJobOnThing");
    //        yield return AccessTools.Method(typeof(Bill_Medical), "Notify_IterationCompleted");
    //        yield return AccessTools.Method(typeof(PawnDiedOrDownedThoughtsUtility), "AppendThoughts_Relations");
    //        yield return AccessTools.Method(typeof(PawnApparelGenerator), "GenerateStartingApparelFor");
    //        yield return AccessTools.Method(typeof(LovePartnerRelationUtility), "ExistingLeastLikedRel");
    //        yield return AccessTools.Method(typeof(LovePartnerRelationUtility), "ExistingMostLikedLovePartnerRel");
    //        yield return AccessTools.Method(typeof(ParentRelationUtility), "GetMother");
    //        yield return AccessTools.Method(typeof(ParentRelationUtility), "GetFather");
    //        yield return AccessTools.Method(typeof(PawnRelationUtility), "Notify_PawnsSeenByPlayer");
    //        yield return AccessTools.Method(typeof(SpouseRelationUtility), "GetFirstSpouse");
    //        yield return AccessTools.Method(typeof(SpouseRelationUtility), "GetSpouses");
    //        yield return AccessTools.Method(typeof(SpouseRelationUtility), "GetMostLikedSpouseRelation");
    //        yield return AccessTools.Method(typeof(SpouseRelationUtility), "GetLeastLikedSpouseRelation");
    //        yield return AccessTools.Method(typeof(SpouseRelationUtility), "GetSpouseCount");
    //        yield return AccessTools.Method(typeof(RelationsUtility), "PawnsKnowEachOther");
    //        yield return AccessTools.Method(typeof(Faction), "TryGenerateNewLeader");
    //        yield return AccessTools.Method(typeof(CompHatcher), "Hatch");
    //        yield return AccessTools.Method(typeof(ReleaseAnimalToWildUtility), "CheckWarnAboutBondedAnimal");
    //        yield return AccessTools.Method(typeof(SlaughterDesignatorUtility), "CheckWarnAboutBondedAnimal");
    //        yield return AccessTools.Method(typeof(PawnRelationUtility), "GetRelations");
    //        yield return AccessTools.Method(typeof(HealthCardUtility), "CreateSurgeryBill");
    //        yield return AccessTools.Method(typeof(ITab_Pawn_Health), "ShouldAllowOperations");
    //        yield return AccessTools.PropertyGetter(typeof(ITab_Pawn_Social), "IsVisible");
    //        yield return AccessTools.Method(typeof(PawnColumnWorker_Hunt), "HasCheckbox");
    //        yield return AccessTools.Method(typeof(PawnColumnWorker_ReleaseAnimalToWild), "HasCheckbox");
    //        yield return AccessTools.Method(typeof(PawnColumnWorker_Slaughter), "HasCheckbox");
    //        yield return AccessTools.Method(typeof(CompRitualHediffGiverInRoom), "CompTick");
    //        yield return AccessTools.Method(typeof(RimWorld.Planet.WITab_Caravan_Social), "DoRows");

    //    }
    //    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) =>
    //        codes.MethodReplacer(AccessTools.PropertyGetter(typeof(RaceProperties), "IsFlesh"), AccessTools.Method(typeof(Isflesh_to_Isnotmech_Patch), "IsNotMechanoid"));
    //    static bool IsNotMechanoid(RaceProperties RaceProps) => !RaceProps.IsMechanoid;
    //}

    //[HarmonyPatch(typeof(HealthCardUtility), "DrawOverviewTab")]
    //public static class HealthCardUtility_DrawOverviewTab_Patch
    //{
    //    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    //    {
    //        int found = 0;
    //        MethodBase from = AccessTools.PropertyGetter(typeof(RaceProperties), "IsFlesh");
    //        MethodBase to = AccessTools.Method(typeof(Isflesh_to_Isnotmech_Patch), "IsNotMechanoid");
    //        MethodBase to2 = AccessTools.Method(typeof(HealthCardUtility_DrawOverviewTab_Patch), "feelsPain");
    //        foreach (CodeInstruction instruction in instructions)
    //        {
    //            if (instruction.operand as MethodBase == from && found == 0)
    //            {
    //                instruction.opcode = (to.IsConstructor ? OpCodes.Newobj : OpCodes.Call);
    //                instruction.operand = to;
    //                found = 1;
    //                yield return instruction;
    //            }
    //            if (instruction.operand as MethodBase == from && found == 1)
    //            {
    //                instruction.opcode = (to2.IsConstructor ? OpCodes.Newobj : OpCodes.Call);
    //                instruction.operand = to2;
    //                found = 2;
    //            }
    //            yield return instruction;
    //        }
    //    }
    //    static bool feelsPain(RaceProperties RaceProps)
    //    {
    //        if (RaceProps.IsFlesh == false && RaceProps.AnyPawnKind.HasModExtension<FeelsPain>())
    //        {
    //            return true;
    //        }
    //        return RaceProps.IsFlesh;
    //    }
    //}

    ////paincheck
    //public class FeelsPain : DefModExtension { }
    
    //[HarmonyPatch(typeof(HediffSet), "CalculatePain")]
    //public static class HediffSet_CalculatePain_Patch
    //{
    //    static void Postfix(List<Hediff> ___hediffs, Pawn ___pawn, ref float __result)
    //    {
    //        if (___pawn.kindDef.HasModExtension<FeelsPain>() && !___pawn.Dead)
    //        {
    //            float num = 0f;
    //            for (int i = 0; i < ___hediffs.Count; i++)
    //            {
    //                num += ___hediffs[i].PainOffset;
    //            }
    //            for (int j = 0; j < ___hediffs.Count; j++)
    //            {
    //                num *= ___hediffs[j].PainFactor;
    //            }
    //            __result = Mathf.Clamp(num, 0f, 1f);
    //        }
    //    }
    //}

    ////ismech patches
    //[HarmonyPatch]
    //public static class Ismech_to_Isnotflesh_Patch
    //{
    //    static IEnumerable<MethodBase> TargetMethods()
    //    {
    //        yield return AccessTools.Method(typeof(PawnBreathMoteMaker), "BreathMoteMakerTick");
    //        yield return AccessTools.Method(typeof(RimWorld.Planet.WITab_Caravan_Health), "DoRow", new Type[] { typeof(Rect), typeof(Pawn) });
    //        yield return AccessTools.Method(typeof(HealthCardUtility), "DrawHealthSummary");
    //        yield return AccessTools.Method(typeof(CompGiveHediffSeverity), "AppliesTo");
    //        yield return AccessTools.PropertyGetter(typeof(StunHandler), "EMPAdaptationTicksDuration");
    //        yield return AccessTools.Method(typeof(Recipe_RemoveBodyPart), "GetLabelWhenUsedOn");
    //        yield return AccessTools.Method(typeof(CompAbilityEffect_Neuroquake), "Apply", new Type[] { typeof(LocalTargetInfo), typeof(LocalTargetInfo) });
    //        yield return AccessTools.Method(typeof(CompAbilityEffect_GiveMentalState), "Apply", new Type[] { typeof(LocalTargetInfo), typeof(LocalTargetInfo) });
    //        yield return AccessTools.Method(typeof(ArmorUtility), "ApplyArmor");

    //    }
    //    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) =>
    //        codes.MethodReplacer(AccessTools.PropertyGetter(typeof(RaceProperties), "IsMechanoid"), AccessTools.Method(typeof(Ismech_to_Isnotflesh_Patch), "IsNotFlesh"));
    //    static bool IsNotFlesh(RaceProperties RaceProps) => !RaceProps.IsFlesh;
    //}
    //settings
    public class HorizonFrameworkMod : Mod
    {
        public static HorizonFrameworkSettings settings;
        public HorizonFrameworkMod(ModContentPack pack) : base(pack)
        {
            settings = GetSettings<HorizonFrameworkSettings>();
        }
        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
            settings.DoSettingsWindowContents(inRect);
        }
        public override string SettingsCategory()
        {
            return this.Content.Name;
        }
        public override void WriteSettings()
        {
            base.WriteSettings();
            settings.ApplySettings();
        }
    }
    public class HorizonFrameworkSettings : ModSettings
    {
        public static bool ArmorBones = true;
        public static bool LethalBones = false;
        public static bool flagDigPeriodicallyNeed = true;
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ArmorBones, "ArmorBones", true);
            Scribe_Values.Look(ref LethalBones, "LethalBones", false);


        }
        public void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);
            listingStandard.GapLine();
            listingStandard.Label("Hf.generaltab".Translate());
            listingStandard.GapLine();
            listingStandard.CheckboxLabeled("Hf.ArmorBones".Translate(), ref ArmorBones, "Hf.ABonestooltip".Translate());
            listingStandard.GapLine();
            listingStandard.CheckboxLabeled("Hf.lethalBones".Translate(), ref LethalBones, "Hf.LBonestooltip".Translate());
            listingStandard.GapLine();
            listingStandard.End();
        }
        public void ApplySettings()
        {
            Pawn_HealthTracker_ShouldBeDeadFromLethalDamageThreshold_Patch.lethalBones = LethalBones;
            DamageWorker_AddInjury_GetExactPartFromDamageInfo_Patch.ArmorBones = ArmorBones;
        }
    }

    ////no food taming patches (wip)
    //[HarmonyPatch(typeof(JobDriver_InteractAnimal), "RequiredNutritionPerFeed")]
    //public static class JobDriver_InteractAnimal_RequiredNutritionPerFeed_Patch
    //{
    //    public static bool Prefix(ref float __result, Pawn animal)
    //    {
    //        __result= Mathf.Min((animal.needs.food?.MaxLevel ?? 0f) * 0.15f, 0.3f);
    //        return false;
    //    }
    //}


    ////mech part specific patches
    //[HarmonyPatch(typeof(ExecutionUtility), "ExecuteCutPart")]
    //public static class ExecutionUtility_ExecuteCutPart_Patch
    //{
    //    public static bool Prefix(ref BodyPartRecord __result, Pawn pawn)
    //    {
    //        if (!pawn.kindDef.HasModExtension<MechAnimal>())
    //        {
    //            return true;
    //        }
    //        BodyPartRecord bodyPartRecord = pawn.health.hediffSet.GetNotMissingParts().FirstOrDefault((BodyPartRecord x) => x.def == MechPartDefOf.MechanicalNeck);
    //        if (bodyPartRecord != null)
    //        {
    //            __result = bodyPartRecord;
    //            return false;
    //        }
    //        bodyPartRecord = pawn.health.hediffSet.GetNotMissingParts().FirstOrDefault((BodyPartRecord x) => x.def == MechPartDefOf.MechanicalHead);
    //        if (bodyPartRecord != null)
    //        {
    //            __result = bodyPartRecord;
    //            return false;
    //        }
    //        bodyPartRecord = pawn.health.hediffSet.GetNotMissingParts().FirstOrDefault((BodyPartRecord x) => x.def == MechPartDefOf.MechanicalThorax);
    //        if (bodyPartRecord != null)
    //        {
    //            __result = bodyPartRecord;
    //            return false;
    //        }
    //        bodyPartRecord = pawn.health.hediffSet.GetNotMissingParts().FirstOrDefault((BodyPartRecord x) => x.def == MechPartDefOf.MechanicalThoraxCanManipulate);
    //        if (bodyPartRecord != null)
    //        {
    //            __result = bodyPartRecord;
    //            return false;
    //        }
    //        return true;
    //    }
    //}
    //[HarmonyPatch(typeof(JobDriver_Blind), "Blind")]
    //public static class JobDriver_Blind_Blind_Patch
    //{
    //    public static bool Prefix(Pawn pawn, Pawn doer)
    //    {
    //        Lord lord = pawn.GetLord();
    //        IEnumerable<BodyPartRecord> enumerable = from p in pawn.health.hediffSet.GetNotMissingParts()
    //                                                 where p.def == MechPartDefOf.SightSensor
    //                                                 select p;
    //        if (lord != null && lord.LordJob is LordJob_Ritual_Mutilation lordJob_Ritual_Mutilation && enumerable.Count() == 1)
    //        {
    //            lordJob_Ritual_Mutilation.mutilatedPawns.Add(pawn);
    //        }
    //        foreach (BodyPartRecord item in enumerable)
    //        {
    //            if (item.def == MechPartDefOf.SightSensor)
    //            {
    //                pawn.TakeDamage(new DamageInfo(DamageDefOf.SurgicalCut, 99999f, 999f, -1f, null, item));
    //                break;
    //            }
    //        }
    //        if (pawn.Dead)
    //        {
    //            ThoughtUtility.GiveThoughtsForPawnExecuted(pawn, doer, PawnExecutionKind.GenericBrutal);
    //            return false;
    //        }
    //        return true;
    //    }
    //}
    //[DefOf]
    //public static class MechPartDefOf
    //{
    //    public static BodyPartDef Reactor;

    //    public static BodyPartDef MechanicalLeg;

    //    public static BodyPartDef FluidReprocessor;

    //    public static BodyPartDef ArtificialBrain;

    //    public static BodyPartDef SightSensor;

    //    public static BodyPartDef SmellSensor;

    //    public static BodyPartDef MechanicalArm;

    //    public static BodyPartDef MechanicalHand;

    //    public static BodyPartDef MechanicalNeck;

    //    public static BodyPartDef MechanicalHead;

    //    public static BodyPartDef MechanicalThorax;

    //    public static BodyPartDef MechanicalThoraxCanManipulate;

    //    public static BodyPartDef Armor;

    //    public static BodyPartDef ArmorChild;
    //    static MechPartDefOf()
    //    {
    //        DefOfHelper.EnsureInitializedInCtor(typeof(MechPartDefOf));
    //    }
    //}


    ////need remover (testing)
    //public class RemoveNeed : DefModExtension
    //{
    //    public List<NeedDef> Need;
    //}
    //[HarmonyPatch(typeof(Pawn_NeedsTracker), "ShouldHaveNeed")]
    //public static class Pawn_NeedsTracker_ShouldHaveNeed_Patch
    //{
    //    public static bool Prefix(ref bool __result, NeedDef nd, ref Pawn ___pawn)
    //    {
    //        var RemoveNeeds = ___pawn.kindDef.GetModExtension<RemoveNeed>();
    //        if (RemoveNeeds != null && RemoveNeeds.Need.Contains(nd))
    //        {
    //            __result = false;
    //            return false;   
    //        }
    //        return true;
    //    }
    //}
    //[HarmonyPatch(typeof (Pawn_IdeoTracker), "CertaintyChangePerDay", MethodType.Getter)]
    //public static class Pawn_IdeoTracker_CertaintyChangePerDay_Patch
    //{
    //    public static bool Prefix(ref float __result, ref Pawn ___pawn)
    //    {
    //        if (___pawn.needs.mood == null)
    //        {
    //            __result = 0;
    //            return false;
    //        }
    //        return true;
    //    }
    //}


    //armor bones code
    [HarmonyPatch(typeof(DamageWorker_AddInjury), "GetExactPartFromDamageInfo")]
    public static class DamageWorker_AddInjury_GetExactPartFromDamageInfo_Patch
    {
        public static bool ArmorBones;
        public static void Postfix(Pawn pawn, DamageInfo dinfo, ref BodyPartRecord __result)
        {
            if (!ArmorBones)
            {
                return;
            }
            var partToBeAffected = __result;
            if (partToBeAffected?.def.destroyableByDamage is false && pawn.health.hediffSet.GetPartHealth(__result) == 1)
            {
                var hitPart = __result;
                var nonMissingParts = pawn.health.hediffSet.GetNotMissingParts();
                var children = hitPart.GetDirectChildParts();
                //Log.Message("Children of " + hitPart + " - " + String.Join(", ", children));
                if (children.TryRandomElementByWeight(x => x.coverageAbs, out var child) && nonMissingParts.Contains(child))
                {
                    __result = child;
                    //Log.Message("Armor: Choosen: " + hitPart + " Child for damage: " + dinfo + " for pawn " + pawn);
                    return;
                }
                if(!hitPart.parent.def.conceptual)
                {
                    __result = hitPart.parent;
                    //Log.Message("Armor: Choosen: " + hitPart + " Parent for damage: " + dinfo + " for pawn " + pawn);

                }
            }
        }
    }
    [HarmonyPatch(typeof(Pawn_HealthTracker), "ShouldBeDeadFromLethalDamageThreshold")]
    public static class Pawn_HealthTracker_ShouldBeDeadFromLethalDamageThreshold_Patch
    {
        public static bool lethalBones;
        public static bool Prefix(ref bool __result, ref Pawn_HealthTracker __instance)
        {
            if (lethalBones)
            {
                return true;
            }
            float num = 0f;
            for (int i = 0; i < __instance.hediffSet.hediffs.Count; i++)
            {
                if (__instance.hediffSet.hediffs[i] is Hediff_Injury)
                {
                    if (__instance.hediffSet.hediffs[i].Part.def.destroyableByDamage == true)
                    {
                        num += __instance.hediffSet.hediffs[i].Severity;
                    }
                }
            }
            bool flag = num >= __instance.LethalDamageThreshold;
            if (flag && DebugViewSettings.logCauseOfDeath)
            {
                Log.Message("CauseOfDeath: lethal damage " + num + " >= " + __instance.LethalDamageThreshold);
            }
            __result= flag;
            return false;
        }
    }


    ////social remover (wip, issues with NRE on createrelation)
    //public class NoSocial : DefModExtension { }
    ////remove relation creation
    ////[HarmonyPatch]
    ////public static class PawnRelationWorker_CreateRelation_NoSocialPatch
    ////{
    ////    static IEnumerable<MethodBase> TargetMethods()
    ////    {
    ////        yield return AccessTools.Method(typeof(PawnRelationWorker), "CreateRelation");
    ////        yield return AccessTools.Method(typeof(PawnRelationWorker_Child), "CreateRelation");
    ////        yield return AccessTools.Method(typeof(PawnRelationWorker_ExLover), "CreateRelation");
    ////        yield return AccessTools.Method(typeof(PawnRelationWorker_ExSpouse), "CreateRelation");
    ////        yield return AccessTools.Method(typeof(PawnRelationWorker_Fiance), "CreateRelation");
    ////        yield return AccessTools.Method(typeof(PawnRelationWorker_Lover), "CreateRelation");
    ////        yield return AccessTools.Method(typeof(PawnRelationWorker_Parent), "CreateRelation");
    ////        yield return AccessTools.Method(typeof(PawnRelationWorker_Sibling), "CreateRelation");
    ////        yield return AccessTools.Method(typeof(PawnRelationWorker_Spouse), "CreateRelation");
    ////    }
    ////    public static bool Prefix(Pawn generated, Pawn other)
    ////    {
    ////        var pawnSocial = generated.kindDef.HasModExtension<NoSocial>();
    ////        var otherSocial = other.kindDef.HasModExtension<NoSocial>();
    ////        if (pawnSocial == true || otherSocial == true)
    ////        {
    ////            return false;
    ////        }
    ////        return true;
    ////    }
    ////}
    //[HarmonyPatch(typeof(RelationsUtility), "TryDevelopBondRelation")]
    //public static class RelationsUtility_TryDevelopBondRelation_Patch
    //{
    //    public static bool Prefix(Pawn humanlike, Pawn animal, ref bool __result)
    //    {
    //        var pawnSocial = humanlike.kindDef.HasModExtension<NoSocial>();
    //        var otherSocial = animal.kindDef.HasModExtension<NoSocial>();
    //        if (pawnSocial == true || otherSocial == true)
    //        {
    //            __result = false;
    //            return false;
    //        }
    //        return true;
    //    }
    //}
    ////other pawns dont think of pawn as anything
    //[HarmonyPatch(typeof(Pawn_RelationsTracker), "OpinionOf")]
    //public static class Pawn_RelationsTracker_OpinionOf_NoSocialPatch
    //{
    //    public static bool Prefix(Pawn other, ref int __result, ref Pawn ___pawn)
    //    {
    //        var pawnSocial = ___pawn.kindDef.HasModExtension<NoSocial>();
    //        var otherSocial = other.kindDef.HasModExtension<NoSocial>();
    //        if (pawnSocial == true || otherSocial == true)
    //        {
    //            __result= 0;
    //            return false;
    //        }
    //        return true;
    //    }
    //}
    ////hide social tab
    //[HarmonyPatch(typeof(ITab_Pawn_Social), "IsVisible", MethodType.Getter)]
    //public static class ITab_Pawn_Social_IsVisible_NoSocialPatch
    //{
    //    private static Func<ITab_Pawn_Social, Pawn> selPawn = AccessTools.PropertyGetter(typeof(ITab_Pawn_Social), "SelPawnForSocialInfo").CreateDelegate<Func<ITab_Pawn_Social, Pawn>>();
    //    public static bool Prefix(ITab_Pawn_Social __instance, ref bool __result)
    //    {
    //        Pawn pawn = selPawn(__instance);
    //        var pawnSocial = pawn.kindDef.HasModExtension<NoSocial>();
    //        if (pawnSocial == true)
    //        {
    //            __result = false;
    //            return false;
    //        }
    //        return true;
    //    }
    //}

}
