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
using static AlienRace.AlienPartGenerator;
using AlienRace;
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



    //addon code
    [HarmonyPatch(typeof(HarmonyPatches), nameof(HarmonyPatches.DrawAddons))]
    public static class HarmonyPatches_DrawAddons_Patch
    {
        public static void Postfix(PawnRenderFlags renderFlags, Vector3 vector, Vector3 headOffset, Pawn pawn, Quaternion quat, Rot4 rotation)
        {
            var extension = pawn.def.GetModExtension<AnimalBodyAddons>();
            if (extension == null)
            {
                return;
            }
            Log.Message("Drawing body addons: " + extension);
            List<AlienPartGenerator.BodyAddon> bodyAddons = extension.bodyAddons;
            var comp = pawn.GetComp<AnimalComp>();
            if (comp == null)
            {
                return;
            }
            bool flag = renderFlags.FlagSet(PawnRenderFlags.Portrait);
            bool flag2 = renderFlags.FlagSet(PawnRenderFlags.Invisible);
            for (int i = 0; i < bodyAddons.Count; i++)
            {
                AlienPartGenerator.BodyAddon bodyAddon = bodyAddons[i];
                Log.Message("1 Drawing addon: " + bodyAddon);
                if (!bodyAddon.CanDrawAddon(pawn))
                {
                    continue;
                }
                Log.Message("Drawing addon: " + bodyAddon);
                Vector3 v = (bodyAddon.defaultOffsets.GetOffset(rotation)?.GetOffset(flag, BodyTypeDefOf.Male, "") ?? Vector3.zero) + (bodyAddon.offsets.GetOffset(rotation)?.GetOffset(flag, BodyTypeDefOf.Male, "") ?? Vector3.zero);
                v.y = (bodyAddon.inFrontOfBody ? (0.3f + v.y) : (-0.3f - v.y));
                float num = bodyAddon.angle;
                if (rotation == Rot4.North)
                {
                    if (bodyAddon.layerInvert)
                    {
                        v.y = 0f - v.y;
                    }
                    num = 0f;
                }
                if (rotation == Rot4.East)
                {
                    num = 0f - num;
                    v.x = 0f - v.x;
                }
                Graphic graphic = comp.addonGraphics[i];
                graphic.drawSize = ((flag && bodyAddon.drawSizePortrait != Vector2.zero) ? bodyAddon.drawSizePortrait : bodyAddon.drawSize) * ((!bodyAddon.scaleWithPawnDrawsize) ? Vector2.one : ((!bodyAddon.alignWithHead) ? (flag ? comp.customPortraitDrawSize : comp.customDrawSize) : (flag ? comp.customPortraitHeadDrawSize : comp.customHeadDrawSize))) * 1.5f;
                Material material = graphic.MatAt(rotation);
                if (!flag && flag2)
                {
                    material = InvisibilityMatPool.GetInvisibleMat(material);
                }
                GenDraw.DrawMeshNowOrLater(graphic.MeshAt(rotation), vector + (bodyAddon.alignWithHead ? headOffset : Vector3.zero) + v.RotatedBy(Mathf.Acos(Quaternion.Dot(Quaternion.identity, quat)) * 2f * 57.29578f), Quaternion.AngleAxis(num, Vector3.up) * quat, material, renderFlags.FlagSet(PawnRenderFlags.DrawNow));
            }
        }
    }

    [HarmonyPatch(typeof(HarmonyPatches), nameof(HarmonyPatches.ResolveAllGraphicsPrefix))]
    public static class HarmonyPatches_ResolveAllGraphicsPrefix_Patch
    {
        public static void Prefix(PawnGraphicSet __0)
        {
            var pawn = __0.pawn;
            if (!(pawn.def is AlienRace.ThingDef_AlienRace))
            {
                var comp = pawn.GetComp<AnimalComp>();
                if (comp != null)
                {
                    var extension = pawn.def.GetModExtension<AnimalBodyAddons>();
                    if (extension != null)
                    {
                        comp.addonGraphics = new List<Graphic>();
                        if (comp.addonVariants == null)
                        {
                            comp.addonVariants = new List<int>();
                        }
                        int sharedIndex = 0;
                        for (int i = 0; i < extension.bodyAddons.Count; i++)
                        {
                            Graphic path = extension.bodyAddons[i].GetPath(pawn, ref sharedIndex, (comp.addonVariants.Count > i) ? new int?(comp.addonVariants[i]) : null);
                            comp.addonGraphics.Add(path);
                            if (comp.addonVariants.Count <= i)
                            {
                                comp.addonVariants.Add(sharedIndex);
                            }
                        }
                    }
                }
            }
        }
    }

    [StaticConstructorOnStartup]
    public static class HARHandler
    {
        static HARHandler()
        {
            foreach (var def in DefDatabase<ThingDef>.AllDefs)
            {
                var extension = def.GetModExtension<AnimalBodyAddons>();
                if (extension != null)
                {
                    extension.GenerateMeshsAndMeshPools(def);
                    def.comps.Add(new CompProperties(typeof(AnimalComp)));
                }
            }
        }
    }

    public class AnimalComp : ThingComp
    {
        public List<Graphic> addonGraphics;
        public List<int> addonVariants;
        public Vector2 customDrawSize = Vector2.one;

        public Vector2 customHeadDrawSize = Vector2.one;

        public Vector2 customPortraitDrawSize = Vector2.one;

        public Vector2 customPortraitHeadDrawSize = Vector2.one;
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref addonVariants, "addonVariants", LookMode.Undefined);
        }
    }
    public class AnimalBodyAddons : DefModExtension
    {
        public List<BodyAddon> bodyAddons = new List<BodyAddon>();
        public List<OffsetNamed> offsetDefaults = new List<OffsetNamed>();
        public void GenerateMeshsAndMeshPools(ThingDef def)
        {
            offsetDefaults.Add(new OffsetNamed
            {
                name = "Center",
                offsets = new BodyAddonOffsets()
            });
            offsetDefaults.Add(new OffsetNamed
            {
                name = "Tail",
                offsets = new BodyAddonOffsets
                {
                    south = new RotationOffset
                    {
                        offset = new Vector2(0.42f, -0.22f)
                    },
                    north = new RotationOffset
                    {
                        offset = new Vector2(0f, -0.55f)
                    },
                    east = new RotationOffset
                    {
                        offset = new Vector2(0.42f, -0.22f)
                    },
                    west = new RotationOffset
                    {
                        offset = new Vector2(0.42f, -0.22f)
                    }
                }
            });
            offsetDefaults.Add(new OffsetNamed
            {
                name = "Head",
                offsets = new BodyAddonOffsets
                {
                    south = new RotationOffset
                    {
                        offset = new Vector2(0f, 0.5f)
                    },
                    north = new RotationOffset
                    {
                        offset = new Vector2(0f, 0.35f)
                    },
                    east = new RotationOffset
                    {
                        offset = new Vector2(-0.07f, 0.5f)
                    },
                    west = new RotationOffset
                    {
                        offset = new Vector2(-0.07f, 0.5f)
                    }
                }
            });
            new AlienRace.BodyAddonSupport.DefaultGraphicsLoader().LoadAllGraphics(def.defName, offsetDefaults, bodyAddons);
        }
    }

    public class HediffCompProperties_Explosive : HediffCompProperties
    {
        public float explosiveRadius = 1.9f;

        public DamageDef explosiveDamageType;

        public int damageAmountBase = -1;

        public float armorPenetrationBase = -1f;

        public ThingDef postExplosionSpawnThingDef;

        public float postExplosionSpawnChance;

        public int postExplosionSpawnThingCount = 1;

        public bool applyDamageToExplosionCellsNeighbors;

        public ThingDef preExplosionSpawnThingDef;

        public float preExplosionSpawnChance;

        public int preExplosionSpawnThingCount = 1;

        public float chanceToStartFire;

        public bool damageFalloff;

        public bool explodeOnKilled;

        public float explosiveExpandPerStackcount;//unneeded, kept for copy paste-ability of comp explosive

        public float explosiveExpandPerFuel;//unneeded, kept for copy paste-ability of comp explosive

        public EffecterDef explosionEffect;

        public SoundDef explosionSound;

        public List<DamageDef> startWickOnDamageTaken;

        public float startWickHitPointsPercent = 0.2f;

        public IntRange wickTicks = new IntRange(140, 150);

        public float wickScale = 1f;

        public float chanceNeverExplodeFromDamage;

        public float destroyThingOnExplosionSize;

        public DamageDef requiredDamageTypeToExplode;

        public IntRange? countdownTicks;

        public string extraInspectStringKey;

        public List<WickMessage> wickMessages;

        public HediffCompProperties_Explosive()
        {
            compClass = typeof(HediffCompExplosive);
        }
        public override void ResolveReferences(HediffDef parent)
        {
            base.ResolveReferences(parent);
            if (explosiveDamageType == null)
            {
                explosiveDamageType = DamageDefOf.Bomb;
            }
        }
    }
    public class HediffCompExplosive : HediffComp
    {
        public bool wickStarted;

        protected int wickTicksLeft;

        private Thing instigator;

        private int countdownTicksLeft = -1;

        public bool destroyedThroughDetonation;

        private List<Thing> thingsIgnoredByExplosion;

        public float? customExplosiveRadius;

        protected Sustainer wickSoundSustainer;

        private OverlayHandle? overlayBurningWick;

        public HediffCompProperties_Explosive Props => (HediffCompProperties_Explosive)props;

        protected float StartWickThreshold => Props.startWickHitPointsPercent;

        private bool CanEverExplodeFromDamage
        {
            get
            {
                if (Props.chanceNeverExplodeFromDamage < 1E-05f)
                {
                    return true;
                }
                Rand.PushState();
                Rand.Seed = Pawn.thingIDNumber.GetHashCode();
                bool result = Rand.Value > Props.chanceNeverExplodeFromDamage;
                Rand.PopState();
                return result;
            }
        }

        public void AddThingsIgnoredByExplosion(List<Thing> things)
        {
            if (thingsIgnoredByExplosion == null)
            {
                thingsIgnoredByExplosion = new List<Thing>();
            }
            thingsIgnoredByExplosion.AddRange(things);
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_References.Look(ref instigator, "instigator");
            Scribe_Collections.Look(ref thingsIgnoredByExplosion, "thingsIgnoredByExplosion", LookMode.Reference);
            Scribe_Values.Look(ref wickStarted, "wickStarted", defaultValue: false);
            Scribe_Values.Look(ref wickTicksLeft, "wickTicksLeft", 0);
            Scribe_Values.Look(ref destroyedThroughDetonation, "destroyedThroughDetonation", defaultValue: false);
            Scribe_Values.Look(ref countdownTicksLeft, "countdownTicksLeft", 0);
            Scribe_Values.Look(ref customExplosiveRadius, "explosiveRadius");
        }

        [HarmonyPatch(typeof(Pawn), "SpawnSetup")]
        public static class Pawn_SpawnSetup_Patch
        {
            public static void Postfix(Pawn __instance)
            {
                foreach (var hediff in __instance.health.hediffSet.hediffs)
                {
                    var comp = hediff.TryGetComp<HediffCompExplosive>();
                    if (comp != null)
                    {
                        comp.PostSpawnSetup();
                    }
                }
            }
        }

        public void PostSpawnSetup()
        {
            if (Props.countdownTicks.HasValue)
            {
                countdownTicksLeft = Props.countdownTicks.Value.RandomInRange;
            }
            UpdateOverlays();
        }
        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (countdownTicksLeft > 0)
            {
                countdownTicksLeft--;
                if (countdownTicksLeft == 0)
                {
                    StartWick();
                    countdownTicksLeft = -1;
                }
            }
            if (!wickStarted)
            {
                return;
            }
            if (wickSoundSustainer == null)
            {
                StartWickSustainer();
            }
            else
            {
                wickSoundSustainer.Maintain();
            }
            if (Props.wickMessages != null)
            {
                foreach (WickMessage wickMessage in Props.wickMessages)
                {
                    if (wickMessage.ticksLeft == wickTicksLeft && wickMessage.wickMessagekey != null)
                    {
                        Messages.Message(wickMessage.wickMessagekey.Translate(Pawn, wickTicksLeft.ToStringSecondsFromTicks()), Pawn, wickMessage.messageType ?? MessageTypeDefOf.NeutralEvent, historical: false);
                    }
                }
            }
            wickTicksLeft--;
            if (wickTicksLeft <= 0)
            {
                Detonate(Pawn.MapHeld);
            }
        }

        private void StartWickSustainer()
        {
            SoundDefOf.MetalHitImportant.PlayOneShot(new TargetInfo(Pawn.Position, Pawn.Map));
            SoundInfo info = SoundInfo.InMap(Pawn, MaintenanceType.PerTick);
            wickSoundSustainer = SoundDefOf.HissSmall.TrySpawnSustainer(info);
        }

        private void EndWickSustainer()
        {
            if (wickSoundSustainer != null)
            {
                wickSoundSustainer.End();
                wickSoundSustainer = null;
            }
        }

        private void UpdateOverlays()
        {
            if (Pawn.Spawned)
            {
                Pawn.Map.overlayDrawer.Disable(Pawn, ref overlayBurningWick);
                if (wickStarted)
                {
                    overlayBurningWick = Pawn.Map.overlayDrawer.Enable(Pawn, OverlayTypes.BurningWick);
                }
            }
        }

        [HarmonyPatch(typeof(Pawn), "Destroy")]
        public static class Pawn_Destroy_Patch
        {
            public static void Prefix(Pawn __instance, out Map __state)
            {
                __state = __instance.Map;
            }

            public static void Postfix(Pawn __instance, DestroyMode mode, Map __state)
            {
                foreach (var hediff in __instance.health.hediffSet.hediffs)
                {
                    var comp = hediff.TryGetComp<HediffCompExplosive>();
                    if (comp != null)
                    {
                        comp.PostDestroy(mode, __state);
                    }
                }
            }
        }

        public void PostDestroy(DestroyMode mode, Map previousMap)
        {
            if (mode == DestroyMode.KillFinalize && Props.explodeOnKilled)
            {
                Detonate(previousMap, ignoreUnspawned: true);
            }
        }

        [HarmonyPatch(typeof(Pawn), "PreApplyDamage")]
        public static class Pawn_PreApplyDamage_Patch
        {
            public static void Postfix(Pawn __instance, ref DamageInfo dinfo, ref bool absorbed)
            {
                foreach (var hediff in __instance.health.hediffSet.hediffs)
                {
                    var comp = hediff.TryGetComp<HediffCompExplosive>();
                    if (comp != null)
                    {
                        comp.PostPreApplyDamage(dinfo, out absorbed);
                    }
                }
            }
        }

        public void PostPreApplyDamage(DamageInfo dinfo, out bool absorbed)
        {
            absorbed = false;
            if (!CanEverExplodeFromDamage)
            {
                return;
            }
            if (dinfo.Def.ExternalViolenceFor(Pawn) && CanExplodeFromDamageType(dinfo.Def))
            {
                if (Pawn.MapHeld != null)
                {
                    instigator = dinfo.Instigator;
                    Detonate(Pawn.MapHeld);
                    if (Pawn.Destroyed)
                    {
                        absorbed = true;
                    }
                }
            }
            else if (!wickStarted && Props.startWickOnDamageTaken != null && Props.startWickOnDamageTaken.Contains(dinfo.Def))
            {
                StartWick(dinfo.Instigator);
            }
        }

        [HarmonyPatch(typeof(Pawn), "PostApplyDamage")]
        public static class Pawn_PostApplyDamage_Patch
        {
            public static void Postfix(Pawn __instance, DamageInfo dinfo, float totalDamageDealt)
            {
                foreach (var hediff in __instance.health.hediffSet.hediffs)
                {
                    var comp = hediff.TryGetComp<HediffCompExplosive>();
                    if (comp != null)
                    {
                        comp.PostPostApplyDamage(dinfo, totalDamageDealt);
                    }
                }
            }
        }

        public void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            if (CanEverExplodeFromDamage && CanExplodeFromDamageType(dinfo.Def) && !Pawn.Destroyed)
            {
                if (wickStarted && dinfo.Def == DamageDefOf.Stun)
                {
                    StopWick();
                }
                else if (!wickStarted && Pawn.health.summaryHealth.SummaryHealthPercent <= StartWickThreshold && dinfo.Def.ExternalViolenceFor(Pawn))
                {
                    StartWick(dinfo.Instigator);
                }
            }
        }

        public void StartWick(Thing instigator = null)
        {
            if (!wickStarted && !(ExplosiveRadius() <= 0f))
            {
                this.instigator = instigator;
                wickStarted = true;
                wickTicksLeft = Props.wickTicks.RandomInRange;
                StartWickSustainer();
                GenExplosion.NotifyNearbyPawnsOfDangerousExplosive(Pawn, Props.explosiveDamageType, null, instigator);
                UpdateOverlays();
            }
        }

        public void StopWick()
        {
            wickStarted = false;
            instigator = null;
            UpdateOverlays();
        }

        public float ExplosiveRadius()
        {
            HediffCompProperties_Explosive compProperties_Explosive = Props;
            float num = customExplosiveRadius ?? Props.explosiveRadius;
            return num;
        }

        protected void Detonate(Map map, bool ignoreUnspawned = false)
        {
            if (!ignoreUnspawned && !Pawn.SpawnedOrAnyParentSpawned)
            {
                return;
            }
            HediffCompProperties_Explosive compProperties_Explosive = Props;
            float num = ExplosiveRadius();
            if (compProperties_Explosive.destroyThingOnExplosionSize <= num && !Pawn.Destroyed)
            {
                destroyedThroughDetonation = true;
                Pawn.Kill(null);
            }
            EndWickSustainer();
            wickStarted = false;
            if (map == null)
            {
                Log.Warning("Tried to detonate CompExplosive in a null map.");
                return;
            }
            if (compProperties_Explosive.explosionEffect != null)
            {
                Effecter effecter = compProperties_Explosive.explosionEffect.Spawn();
                effecter.Trigger(new TargetInfo(Pawn.PositionHeld, map), new TargetInfo(Pawn.PositionHeld, map));
                effecter.Cleanup();
            }
            GenExplosion.DoExplosion(instigator: (instigator == null || (instigator.HostileTo(Pawn.Faction) && Pawn.Faction != Faction.OfPlayer)) ? Pawn : instigator, center: Pawn.PositionHeld, map: map, radius: num, damType: compProperties_Explosive.explosiveDamageType, damAmount: compProperties_Explosive.damageAmountBase, armorPenetration: compProperties_Explosive.armorPenetrationBase, explosionSound: compProperties_Explosive.explosionSound, weapon: null, projectile: null, intendedTarget: null, postExplosionSpawnThingDef: compProperties_Explosive.postExplosionSpawnThingDef, postExplosionSpawnChance: compProperties_Explosive.postExplosionSpawnChance, postExplosionSpawnThingCount: compProperties_Explosive.postExplosionSpawnThingCount, applyDamageToExplosionCellsNeighbors: compProperties_Explosive.applyDamageToExplosionCellsNeighbors, preExplosionSpawnThingDef: compProperties_Explosive.preExplosionSpawnThingDef, preExplosionSpawnChance: compProperties_Explosive.preExplosionSpawnChance, preExplosionSpawnThingCount: compProperties_Explosive.preExplosionSpawnThingCount, chanceToStartFire: compProperties_Explosive.chanceToStartFire, damageFalloff: compProperties_Explosive.damageFalloff, direction: null, ignoredThings: thingsIgnoredByExplosion);
        }

        private bool CanExplodeFromDamageType(DamageDef damage)
        {
            if (Props.requiredDamageTypeToExplode != null)
            {
                return Props.requiredDamageTypeToExplode == damage;
            }
            return true;
        }
    }
}
