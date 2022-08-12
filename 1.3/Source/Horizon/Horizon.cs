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
    //mechanimal extension
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
    //isflesh patches
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
    //ismech patches
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
        public static bool AdvancedArmor = false;
        public static bool AdvancedAccuracy = false;
        public static bool bones = false;
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref AdvancedArmor, "AdvancedArmor", false, true);
            Scribe_Values.Look(ref AdvancedArmor, "AdvancedAccuracy", false, true);
            Scribe_Values.Look(ref AdvancedArmor, "Bones", false, true);


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
            listingStandard.CheckboxLabeled("Hf.AdvanceAccuracy".Translate(), ref AdvancedAccuracy, "Hf.AAcctooltip".Translate());
            listingStandard.GapLine();
            listingStandard.CheckboxLabeled("Hf.lethalBones".Translate(), ref bones, "Hf.Bonestooltip".Translate());
            listingStandard.GapLine();
            listingStandard.End();
        }
        public void ApplySettings()
        {
            ArmorUtility_ApplyArmor_Patch.AArmor = AdvancedArmor;
            ShotReport_HitReportFor_Patch.AAccuracy = AdvancedAccuracy;
            Pawn_HealthTracker_ShouldBeDeadFromLethalDamageThreshold_Patch.lethalBones = bones;
        }
    }
    //advanced armor patches (will be split off into separate mod)
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
    //advanced accuracy patches (not yet working) (will be split off into separate mod)
    [HarmonyPatch(typeof(ShotReport), "HitReportFor")]
    public static class ShotReport_HitReportFor_Patch
    {
        public static bool AAccuracy;
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodBase from = AccessTools.Method(typeof(VerbProperties), "GetHitChanceFactor");
            MethodBase from1 = AccessTools.PropertyGetter(typeof(WeatherManager), "CurWeatherAccuracyMultiplier");
            MethodBase to = AccessTools.Method(typeof(ShotReport_HitReportFor_Patch), "statBump");

            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.operand as MethodBase == from || instruction.operand as MethodBase == from1)//find method to replace
                {
                    yield return instruction;//add first instruction back
                    yield return new CodeInstruction(OpCodes.Ldarg_0);//load in next argument
                    yield return new CodeInstruction(OpCodes.Call, to);//execute new method
                }
                else
                {
                    yield return instruction;
                }
            }
        }
        public static float statBump(float factor,Thing caster)
        {
            if (AAccuracy)
            {
                float shootstat = Mathf.Max(1, (caster is Pawn) ? caster.GetStatValue(StatDefOf.ShootingAccuracyPawn, false): caster.GetStatValue(StatDefOf.ShootingAccuracyTurret,false));//StatDefOf.ShootingAccuracyPawn.Worker.GetValue(StatRequest.For(caster), false) : StatDefOf.ShootingAccuracyTurret.Worker.GetValue(StatRequest.For(caster), false)
                factor = Mathf.Pow(factor, 1 / shootstat);
            }
            return factor;
        }
    }
    //no food taming patches (wip)
    [HarmonyPatch(typeof(JobDriver_InteractAnimal), "RequiredNutritionPerFeed")]
    public static class JobDriver_InteractAnimal_RequiredNutritionPerFeed_Patch
    {
        public static bool Prefix(ref float __result, Pawn animal)
        {
            __result= Mathf.Min((animal.needs.food?.MaxLevel ?? 0f) * 0.15f, 0.3f);
            return false;
        }
    }
    //mech part specific patches
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

        public static BodyPartDef Armor;

        public static BodyPartDef ArmorChild;
        static MechPartDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(MechPartDefOf));
        }
    }
    //need remover (testing)
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
    [HarmonyPatch(typeof (Pawn_IdeoTracker), "CertaintyChangePerDay", MethodType.Getter)]
    public static class Pawn_IdeoTracker_CertaintyChangePerDay_Patch
    {
        public static bool Prefix(ref float __result, ref Pawn ___pawn)
        {
            if (___pawn.needs.mood == null)
            {
                __result = 0;
                return false;
            }
            return true;
        }
    }
    //armor bones code
    [HarmonyPatch(typeof(DamageWorker_AddInjury), "ApplyDamageToPart")]
    public static class DamageWorker_AddInjury_ApplyDamageToPart_Patch
    {
        public static void Prefix(Pawn pawn, ref DamageInfo dinfo, DamageResult result)
        {
            if (dinfo.HitPart?.def.destroyableByDamage is false && pawn.health.hediffSet.GetPartHealth(dinfo.HitPart) == 1)
            {
                var hitPart = dinfo.HitPart;
                var nonMissingParts = pawn.health.hediffSet.GetNotMissingParts();
                var children = hitPart.GetDirectChildParts();
                Log.Message("Children of " + hitPart + " - " + String.Join(", ", children));
                if (children.TryRandomElementByWeight(x => x.coverage, out var child) && nonMissingParts.Contains(child))
                {
                    dinfo.SetHitPart(child);
                    Log.Message("Armor: Choosen: " + hitPart + " for damage: " + dinfo + " for pawn " + pawn);
                    return;
                }
                dinfo.SetHitPart(hitPart.parent);
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
    //social remover (wip, issues with NRE on createrelation)
    public class NoSocial : DefModExtension { }
    //remove relation creation
    [HarmonyPatch]
    public static class PawnRelationWorker_CreateRelation_NoSocialPatch
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(PawnRelationWorker), "CreateRelation");
            yield return AccessTools.Method(typeof(PawnRelationWorker_Child), "CreateRelation");
            yield return AccessTools.Method(typeof(PawnRelationWorker_ExLover), "CreateRelation");
            yield return AccessTools.Method(typeof(PawnRelationWorker_ExSpouse), "CreateRelation");
            yield return AccessTools.Method(typeof(PawnRelationWorker_Fiance), "CreateRelation");
            yield return AccessTools.Method(typeof(PawnRelationWorker_Lover), "CreateRelation");
            yield return AccessTools.Method(typeof(PawnRelationWorker_Parent), "CreateRelation");
            yield return AccessTools.Method(typeof(PawnRelationWorker_Sibling), "CreateRelation");
            yield return AccessTools.Method(typeof(PawnRelationWorker_Spouse), "CreateRelation");
        }
        public static bool Prefix(Pawn generated, Pawn other)
        {
            var pawnSocial = generated.kindDef.HasModExtension<NoSocial>();
            var otherSocial = other.kindDef.HasModExtension<NoSocial>();
            if (pawnSocial == true || otherSocial == true)
            {
                return false;
            }
            return true;
        }
    }
    [HarmonyPatch(typeof(RelationsUtility), "TryDevelopBondRelation")]
    public static class RelationsUtility_TryDevelopBondRelation_Patch
    {
        public static bool Prefix(Pawn humanlike, Pawn animal, ref bool __result)
        {
            var pawnSocial = humanlike.kindDef.HasModExtension<NoSocial>();
            var otherSocial = animal.kindDef.HasModExtension<NoSocial>();
            if (pawnSocial == true || otherSocial == true)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }
    //other pawns dont think of pawn as anything
    [HarmonyPatch(typeof(Pawn_RelationsTracker), "OpinionOf")]
    public static class Pawn_RelationsTracker_OpinionOf_NoSocialPatch
    {
        public static bool Prefix(Pawn other, ref int __result, ref Pawn ___pawn)
        {
            var pawnSocial = ___pawn.kindDef.HasModExtension<NoSocial>();
            var otherSocial = other.kindDef.HasModExtension<NoSocial>();
            if (pawnSocial == true || otherSocial == true)
            {
                __result= 0;
                return false;
            }
            return true;
        }
    }
    //hide social tab
    [HarmonyPatch(typeof(ITab_Pawn_Social), "IsVisible", MethodType.Getter)]
    public static class ITab_Pawn_Social_IsVisible_NoSocialPatch
    {
        private static Func<ITab_Pawn_Social, Pawn> selPawn = AccessTools.PropertyGetter(typeof(ITab_Pawn_Social), "SelPawnForSocialInfo").CreateDelegate<Func<ITab_Pawn_Social, Pawn>>();
        public static bool Prefix(ITab_Pawn_Social __instance, ref bool __result)
        {
            Pawn pawn = selPawn(__instance);
            var pawnSocial = pawn.kindDef.HasModExtension<NoSocial>();
            if (pawnSocial == true)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }
}
