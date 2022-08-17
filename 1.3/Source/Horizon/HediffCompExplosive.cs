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

        public float explosiveExpandPerFuel;

        public EffecterDef explosionEffect;

        public SoundDef explosionSound;

        public List<DamageDef> startWickOnDamageTaken;

        public float startWickHitPointsPercent;

        public IntRange wickTicks = new IntRange(140, 150);

        public float wickScale = 1f;

        public float chanceNeverExplodeFromDamage;

        public float destroyThingOnExplosionSize = 999f;

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
                for (int i = __instance.health.hediffSet.hediffs.Count - 1; i >= 0; i--)
                {
                    var hediff = __instance.health.hediffSet.hediffs[i];
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
                for (int i = __instance.health.hediffSet.hediffs.Count - 1; i >= 0; i--)
                {
                    var hediff = __instance.health.hediffSet.hediffs[i];
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

        [HarmonyPatch(typeof(DamageWorker_AddInjury), "FinalizeAndAddInjury", new Type[]
        {
            typeof(Pawn),
            typeof(Hediff_Injury),
            typeof(DamageInfo),
            typeof(DamageWorker.DamageResult)
        })]
        public static class DamageWorker_AddInjury_FinalizeAndAddInjury_Patch
        {
            public static void Prefix(ref DamageWorker_AddInjury __instance, ref float __result, Pawn pawn, ref Hediff_Injury injury, ref DamageInfo dinfo, ref DamageWorker.DamageResult result)
            {
                for (int i = pawn.health.hediffSet.hediffs.Count - 1; i >= 0; i--)
                {
                    var hediff = pawn.health.hediffSet.hediffs[i];
                    var comp = hediff.TryGetComp<HediffCompExplosive>();
                    if (comp != null)
                    {
                        comp.PostPreApplyDamage(ref dinfo);
                    }
                }
            }

            public static void Postfix(ref DamageWorker_AddInjury __instance, ref float __result, Pawn pawn, ref Hediff_Injury injury, ref DamageInfo dinfo, ref DamageWorker.DamageResult result)
            {
                for (int i = pawn.health.hediffSet.hediffs.Count - 1; i >= 0; i--)
                {
                    var hediff = pawn.health.hediffSet.hediffs[i];
                    var comp = hediff.TryGetComp<HediffCompExplosive>();
                    if (comp != null)
                    {
                        comp.PostPostApplyDamage(ref dinfo);
                    }
                }
            }
        }

        public void PostPreApplyDamage(ref DamageInfo dinfo)
        {
            if (!CanEverExplodeFromDamage)
            {
                return;
            }
            if (!wickStarted && Props.startWickOnDamageTaken != null && Props.startWickOnDamageTaken.Contains(dinfo.Def) && CanExplodeFrom(dinfo))
            {
                StartWick(dinfo.Instigator);
            }
            else if (dinfo.Def.ExternalViolenceFor(Pawn) && dinfo.Amount >= (float)this.Pawn.health.hediffSet.GetPartHealth(this.parent.Part) && CanExplodeFromDamageType(dinfo) && Props.explodeOnKilled)
            {
                if (Pawn.MapHeld != null)
                {
                    instigator = dinfo.Instigator;
                    Detonate(Pawn.MapHeld);
                }
            }
        }

        public void PostPostApplyDamage(ref DamageInfo dinfo)
        {
            if (CanEverExplodeFromDamage && !Pawn.Destroyed)
            {
                if (!wickStarted && Props.startWickOnDamageTaken != null && Props.startWickOnDamageTaken.Contains(dinfo.Def) && CanExplodeFrom(dinfo))
                {
                    StartWick(dinfo.Instigator);
                }
                if (wickStarted && dinfo.Def == DamageDefOf.Stun)
                {
                    StopWick();
                }
                else if (!wickStarted && (Pawn.health.summaryHealth.SummaryHealthPercent <= StartWickThreshold && this.parent.Part == null || this.parent.Part != null && 
                    (this.Pawn.health.hediffSet.GetPartHealth(this.parent.Part) / this.parent.Part.def.GetMaxHealth(Pawn)) <= StartWickThreshold) && dinfo.Def.ExternalViolenceFor(Pawn))
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
            if (compProperties_Explosive.explosiveExpandPerFuel > 0f && parent.TryGetComp<HediffCompRefuelable>() != null)
            {
                num += Mathf.Sqrt(parent.TryGetComp<HediffCompRefuelable>().Fuel * compProperties_Explosive.explosiveExpandPerFuel);
            }
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
            if (compProperties_Explosive.explosiveExpandPerFuel > 0f && parent.TryGetComp<HediffCompRefuelable>() != null)
            {
                parent.TryGetComp<HediffCompRefuelable>().ConsumeFuel(parent.TryGetComp<HediffCompRefuelable>().Fuel);
            }
            if (compProperties_Explosive.destroyThingOnExplosionSize <= num && !Pawn.Destroyed)
            {
                destroyedThroughDetonation = true;
                Pawn.Kill(null);
            }
            EndWickSustainer();
            wickStarted = false;
            UpdateOverlays();
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
            var part = this.parent.Part;
            Pawn.health.RemoveHediff(this.parent);
            if (part != null)
            {
                Pawn.health.AddHediff(HediffDefOf.MissingBodyPart, part);
            }
        }

        private bool CanExplodeFromDamageType(DamageInfo dinfo)
        {
            if (CanExplodeFrom(dinfo))
            {
                if (Props.requiredDamageTypeToExplode != null)
                {
                    return Props.requiredDamageTypeToExplode == dinfo.Def;
                }
                return true;
            }
            return false;
        }

        public bool CanExplodeFrom(DamageInfo dinfo) => dinfo.HitPart == this.parent.Part || this.parent.Part is null;
    }
}
