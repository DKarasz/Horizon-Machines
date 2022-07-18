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
            new Harmony("Horizon.Mod").PatchAll();
        }
    }
    public class MechAnimal : DefModExtension{}

    [HarmonyPatch(typeof(RaceProperties),"IsMechanoid", MethodType.Getter)]
    public static class MechAnimalPatch
    {
        public static bool Prefix(ref bool __result, RaceProperties __instance)
        {
            if (__instance.AnyPawnKind.HasModExtension<MechAnimal>())
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(PawnCapacityDef), "GetLabelFor", new Type[] { typeof(bool), typeof(bool) })]
    public static class PawnCapacityDef_GetLabelFor_Patch
    {
        public static void Postfix(ref string __result, bool isFlesh, PawnCapacityDef __instance)
        {
            if (!isFlesh && !__instance.labelMechanoids.NullOrEmpty())
            {
                __result = __instance.labelMechanoids;
            }
        }
    }

    [HarmonyPatch]
    [HarmonyDebug]
    public static class Isflesh_to_Isnotmech_Patch
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(PawnComponentsUtility), "CreateInitialComponents");
            yield return AccessTools.Method(typeof(PawnComponentsUtility), "AddComponentsForSpawn");
            yield return AccessTools.PropertyGetter(typeof(RaceProperties), "Animal");
            yield return AccessTools.Method(typeof(Hediff_Pregnant), "DoBirthSpawn");
            yield return AccessTools.Method(typeof(Pawn), "SpawnSetup");
            yield return AccessTools.Method(typeof(Pawn), "Kill");
            yield return AccessTools.Method(typeof(Pawn), "PreTraded");
            yield return AccessTools.Method(typeof(Pawn), "PreKidnapped");
            yield return AccessTools.Method(typeof(PawnGenerator), "IsValidCandidateToRedress");
            yield return AccessTools.Method(typeof(PawnGenerator), "WorldPawnSelectionWeight");
            yield return AccessTools.Method(typeof(Pawn_HealthTracker), "PreApplyDamage");
            yield return AccessTools.Method(typeof(DebugToolsPawns), "AddRemovePawnRelation");
            yield return AccessTools.Method(typeof(JobGiver_MarryAdjacentPawn), "TryGiveJob");
            yield return AccessTools.Method(typeof(WorkGiver_TakeToBedToOperate), "HasJobOnThing");
            yield return AccessTools.Method(typeof(Bill_Medical), "Notify_IterationCompleted");
            yield return AccessTools.Method(typeof(PawnDiedOrDownedThoughtsUtility), "AppendThoughts_Relations");
            yield return AccessTools.Method(typeof(PawnApparelGenerator), "GenerateStartingApparelFor");
            yield return AccessTools.Method(typeof(LovePartnerRelationUtility), "ExistingLeastLikedRel");
            yield return AccessTools.Method(typeof(LovePartnerRelationUtility), "ExistingMostLikedLovePartnerRel");
            yield return AccessTools.Method(typeof(ParentRelationUtility), "GetMother");
            yield return AccessTools.Method(typeof(ParentRelationUtility), "GetFather");
            yield return AccessTools.Method(typeof(PawnRelationUtility), "Notify_PawnsSeenByPlayer");
            yield return AccessTools.Method(typeof(SpouseRelationUtility), "GetFirstSpouse");
            yield return AccessTools.Method(typeof(SpouseRelationUtility), "GetSpouses");
            yield return AccessTools.Method(typeof(SpouseRelationUtility), "GetMostLikedSpouseRelation");
            yield return AccessTools.Method(typeof(SpouseRelationUtility), "GetLeastLikedSpouseRelation");
            yield return AccessTools.Method(typeof(SpouseRelationUtility), "GetSpouseCount");
            yield return AccessTools.Method(typeof(RelationsUtility), "PawnsKnowEachOther");
            yield return AccessTools.Method(typeof(Faction), "TryGenerateNewLeader");
            yield return AccessTools.Method(typeof(CompHatcher), "Hatch");
            yield return AccessTools.Method(typeof(ReleaseAnimalToWildUtility), "CheckWarnAboutBondedAnimal");
            yield return AccessTools.Method(typeof(SlaughterDesignatorUtility), "CheckWarnAboutBondedAnimal");
            yield return AccessTools.Method(typeof(PawnRelationUtility), "GetRelations");
            yield return AccessTools.Method(typeof(HealthCardUtility), "CreateSurgeryBill");
            yield return AccessTools.Method(typeof(ITab_Pawn_Health), "ShouldAllowOperations");
            yield return AccessTools.PropertyGetter(typeof(ITab_Pawn_Social), "IsVisible");
            yield return AccessTools.Method(typeof(PawnColumnWorker_Hunt), "HasCheckbox");
            yield return AccessTools.Method(typeof(PawnColumnWorker_ReleaseAnimalToWild), "HasCheckbox");
            yield return AccessTools.Method(typeof(PawnColumnWorker_Slaughter), "HasCheckbox");
            yield return AccessTools.Method(typeof(CompRitualHediffGiverInRoom), "CompTick");
            yield return AccessTools.Method(typeof(RimWorld.Planet.WITab_Caravan_Social), "DoRows");

        }
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) =>
            codes.MethodReplacer(AccessTools.PropertyGetter(typeof(RaceProperties), "IsFlesh"), AccessTools.Method(typeof(Isflesh_to_Isnotmech_Patch), "IsNotMechanoid"));
        static bool IsNotMechanoid(RaceProperties RaceProps) => !RaceProps.IsMechanoid;
    }

    [HarmonyPatch(typeof(HealthCardUtility), "DrawOverviewTab")]
    public static class HealthCaravanUtility_DrawOverviewTab_Patch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            bool firstfound = false;
            MethodBase from = AccessTools.PropertyGetter(typeof(RaceProperties), "IsFlesh");
            MethodBase to = AccessTools.Method(typeof(Isflesh_to_Isnotmech_Patch), "IsNotMechanoid");
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.operand as MethodBase == from && firstfound == false)
                {
                    instruction.opcode = (to.IsConstructor ? OpCodes.Newobj : OpCodes.Call);
                    instruction.operand = to;
                    firstfound = true;
                }
                yield return instruction;
            }
        }
    }

    [HarmonyPatch]
    public static class Ismech_to_Isnotflesh_Patch
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(PawnBreathMoteMaker), "BreathMoteMakerTick");
            yield return AccessTools.Method(typeof(RimWorld.Planet.WITab_Caravan_Health), "DoRow");
            yield return AccessTools.Method(typeof(HealthCardUtility), "DrawHealthSummary");
            yield return AccessTools.Method(typeof(CompGiveHediffSeverity), "AppliesTo");
            yield return AccessTools.PropertyGetter(typeof(StunHandler), "EMPAdaptationTicksDuration");
            yield return AccessTools.Method(typeof(Recipe_RemoveBodyPart), "GetLabelWhenUsedOn");
            yield return AccessTools.Method(typeof(CompAbilityEffect_Neuroquake), "Apply");
            yield return AccessTools.Method(typeof(CompAbilityEffect_GiveMentalState), "Apply");
            yield return AccessTools.Method(typeof(ArmorUtility), "ApplyArmor");

        }
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) =>
            codes.MethodReplacer(AccessTools.PropertyGetter(typeof(RaceProperties), "IsMechanoid"), AccessTools.Method(typeof(Ismech_to_Isnotflesh_Patch), "IsNotFlesh"));
        static bool IsNotFlesh(RaceProperties RaceProps) => !RaceProps.IsFlesh;
    }

    //[HarmonyPatch(typeof(ITab_Pawn_Social), "IsVisible", MethodType.Getter)]

    
   




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
