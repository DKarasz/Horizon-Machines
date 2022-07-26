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
    public static class HealthCardUtility_DrawOverviewTab_Patch
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
            yield return AccessTools.Method(typeof(RimWorld.Planet.WITab_Caravan_Health), "DoRow", new Type[] { typeof(Rect), typeof(Pawn) });
            yield return AccessTools.Method(typeof(HealthCardUtility), "DrawHealthSummary");
            yield return AccessTools.Method(typeof(CompGiveHediffSeverity), "AppliesTo");
            yield return AccessTools.PropertyGetter(typeof(StunHandler), "EMPAdaptationTicksDuration");
            yield return AccessTools.Method(typeof(Recipe_RemoveBodyPart), "GetLabelWhenUsedOn");
            yield return AccessTools.Method(typeof(CompAbilityEffect_Neuroquake), "Apply", new Type[] { typeof(LocalTargetInfo), typeof(LocalTargetInfo) });
            yield return AccessTools.Method(typeof(CompAbilityEffect_GiveMentalState), "Apply", new Type[] { typeof(LocalTargetInfo), typeof(LocalTargetInfo) });
            yield return AccessTools.Method(typeof(ArmorUtility), "ApplyArmor");

        }
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) =>
            codes.MethodReplacer(AccessTools.PropertyGetter(typeof(RaceProperties), "IsMechanoid"), AccessTools.Method(typeof(Ismech_to_Isnotflesh_Patch), "IsNotFlesh"));
        static bool IsNotFlesh(RaceProperties RaceProps) => !RaceProps.IsFlesh;
    }
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
    }
    public class HorizonFrameworkSettings : ModSettings
    {
        public static bool AdvancedArmor = false;
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref AdvancedArmor, "AdvancedArmor", false, true);
        }
        public void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);
            listingStandard.GapLine();
            listingStandard.Label("Hf.generaltab".Translate());
            listingStandard.GapLine();
            listingStandard.CheckboxLabeled("Hf.AdvanceArmor".Translate(), ref AdvancedArmor, "Hf.AAtooltip".Translate());
            listingStandard.GapLine();
            listingStandard.End();
            Rect rect = inRect.BottomPart(0.1f).LeftPart(0.1f);
            bool flag = Widgets.ButtonText(rect, "Apply Settings", true, true, true);
            if (flag)
            {
                ApplySettings();
            }
        }
        public static void ApplySettings()
        {
            ArmorUtility_ApplyArmor_Patch.AArmor = AdvancedArmor;
        }
    }
    [HarmonyPatch(typeof(ArmorUtility), "ApplyArmor")]
    public static class ArmorUtility_ApplyArmor_Patch
    {
        public static bool AArmor;
        public static bool Prefix(ref float armorPenetration, ref float armorRating)
        {
            if (AArmor)
            {
                armorPenetration *= 2;
                armorRating *= 2;
            }
            return true;
        }
    }
    [HarmonyPatch]
    public static class AdvanceArmor_postfix
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(VerbProperties), nameof(VerbProperties.AdjustedArmorPenetration), parameters: new Type[] { typeof(Tool), typeof(Pawn), typeof(Thing), typeof(HediffComp_VerbGiver) });
            yield return AccessTools.Method(typeof(VerbProperties), nameof(VerbProperties.AdjustedArmorPenetration), parameters: new Type[] { typeof(Tool), typeof(Pawn), typeof(ThingDef), typeof(ThingDef), typeof(HediffComp_VerbGiver) });
            yield return AccessTools.Method(typeof(ExtraDamage), nameof(ExtraDamage.AdjustedArmorPenetration), parameters: new Type[] { });
            yield return AccessTools.Method(typeof(ExtraDamage), nameof(ExtraDamage.AdjustedArmorPenetration), parameters: new Type[] { typeof(Verb), typeof(Pawn) });
            yield return AccessTools.Method(typeof(ProjectileProperties), nameof(ProjectileProperties.GetArmorPenetration), parameters: new Type[] { typeof(float), typeof(StringBuilder) });
        }
        public static void Postfix(ref float __result)
        {
            if (ArmorUtility_ApplyArmor_Patch.AArmor)
            {
                __result *= 2;
            }
        }
    }

    //RequiredNutritionPerFeed, nullcheck food need with needs.food ?? 0
    [HarmonyPatch(typeof(JobDriver_InteractAnimal), "RequiredNutritionPerFeed")]
    public static class JobDriver_InteractAnimal_RequiredNutritionPerFeed_Patch
    {
        public static bool Prefix(ref float __result, Pawn animal)
        {
            __result= Mathf.Min((animal.needs.food?.MaxLevel ?? 0f) * 0.15f, 0.3f);
            return false;
        }
    }
    

    [HarmonyPatch(typeof(ExecutionUtility), "ExecuteCutPart")]
    public static class ExecutionUtility_ExecuteCutPart_Patch
    {
        public static bool Prefix(ref BodyPartRecord __result, Pawn pawn)
        {
            if (!pawn.kindDef.HasModExtension<MechAnimal>())
            {
                return true;
            }
            BodyPartRecord bodyPartRecord = pawn.health.hediffSet.GetNotMissingParts().FirstOrDefault((BodyPartRecord x) => x.def == MechPartDefOf.MechanicalNeck);
            if (bodyPartRecord != null)
            {
                __result = bodyPartRecord;
                return false;
            }
            bodyPartRecord = pawn.health.hediffSet.GetNotMissingParts().FirstOrDefault((BodyPartRecord x) => x.def == MechPartDefOf.MechanicalHead);
            if (bodyPartRecord != null)
            {
                __result = bodyPartRecord;
                return false;
            }
            bodyPartRecord = pawn.health.hediffSet.GetNotMissingParts().FirstOrDefault((BodyPartRecord x) => x.def == MechPartDefOf.MechanicalThorax);
            if (bodyPartRecord != null)
            {
                __result = bodyPartRecord;
                return false;
            }
            bodyPartRecord = pawn.health.hediffSet.GetNotMissingParts().FirstOrDefault((BodyPartRecord x) => x.def == MechPartDefOf.MechanicalThoraxCanManipulate);
            if (bodyPartRecord != null)
            {
                __result = bodyPartRecord;
                return false;
            }
            return true;
        }
    }
    [HarmonyPatch(typeof(JobDriver_Blind), "Blind")]
    public static class JobDriver_Blind_Blind_Patch
    {
        public static bool Prefix(Pawn pawn, Pawn doer)
        {
            Lord lord = pawn.GetLord();
            IEnumerable<BodyPartRecord> enumerable = from p in pawn.health.hediffSet.GetNotMissingParts()
                                                     where p.def == MechPartDefOf.SightSensor
                                                     select p;
            if (lord != null && lord.LordJob is LordJob_Ritual_Mutilation lordJob_Ritual_Mutilation && enumerable.Count() == 1)
            {
                lordJob_Ritual_Mutilation.mutilatedPawns.Add(pawn);
            }
            foreach (BodyPartRecord item in enumerable)
            {
                if (item.def == MechPartDefOf.SightSensor)
                {
                    pawn.TakeDamage(new DamageInfo(DamageDefOf.SurgicalCut, 99999f, 999f, -1f, null, item));
                    break;
                }
            }
            if (pawn.Dead)
            {
                ThoughtUtility.GiveThoughtsForPawnExecuted(pawn, doer, PawnExecutionKind.GenericBrutal);
                return false;
            }
            return true;
        }
    }

    [DefOf]
    public static class MechPartDefOf
    {
        public static BodyPartDef Reactor;

        public static BodyPartDef MechanicalLeg;

        public static BodyPartDef FluidReprocessor;

        public static BodyPartDef ArtificialBrain;

        public static BodyPartDef SightSensor;

        public static BodyPartDef SmellSensor;

        public static BodyPartDef MechanicalArm;

        public static BodyPartDef MechanicalHand;

        public static BodyPartDef MechanicalNeck;

        public static BodyPartDef MechanicalHead;

        public static BodyPartDef MechanicalThorax;

        public static BodyPartDef MechanicalThoraxCanManipulate;

        static MechPartDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(MechPartDefOf));
        }
    }

    public class RemoveNeed : DefModExtension
    {
        public List<NeedDef> Need;
    }
    [HarmonyPatch(typeof(Pawn_NeedsTracker), "ShouldHaveNeed")]
    public static class Pawn_NeedsTracker_ShouldHaveNeed_Patch
    {
        public static bool Prefix(ref bool __result, NeedDef nd, ref Pawn ___pawn)
        {
            var RemoveNeeds = ___pawn.kindDef.GetModExtension<RemoveNeed>();
            if (RemoveNeeds != null && RemoveNeeds.Need.Contains(nd))
            {
                __result = false;
                return false;   
            }
            return true;
        }
    }






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
