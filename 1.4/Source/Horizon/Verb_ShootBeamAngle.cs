using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Horizon
{
    public class indBeam
    {
        public MoteDualAttached mote;

        public Effecter endEffecter;

        public Sustainer sustainer;
    }
    public class Verb_ShootBeamAngle : Verb
    {
        private List<Vector3> path = new List<Vector3>();//paths are based on where they are from the target position?

        private int ticksToNextPathStep;//ticks per tile distance

        private Vector3 initialTargetPosition;

        private MoteDualAttached mote;

        private Effecter endEffecter;

        private Sustainer sustainer;

        private const int NumSubdivisionsPerUnitLength = 1;

        public List<indBeam> beamMotes = new List<indBeam>();
        protected override int ShotsPerBurst => verbProps.burstShotCount;//change to shots per beam

        public float ShotProgress => (float)ticksToNextPathStep / (float)verbProps.ticksBetweenBurstShots;

        public Vector3 InterpolatedPosition
        {
            get
            {
                Vector3 vector = base.CurrentTarget.CenterVector3 - initialTargetPosition;//get target position
                int current = Math.Max((int)verbProps.beamFullWidthRange - burstShotsLeft, 0);
                return Vector3.Lerp(path[Mathf.Min(current + 1, path.Count - 1)], path[current], ShotProgress) + vector;//measure out distance between two points in the path, and add the target position
            }
        }

        public float angle => verbProps.beamWidth / (ShotsPerBurst + 1);

        public override float? AimAngleOverride
        {
            get
            {
                if (state != VerbState.Bursting)
                {
                    return null;
                }
                return (InterpolatedPosition - caster.DrawPos).AngleFlat();
            }
        }

        protected override bool TryCastShot()
        {
            if (currentTarget.HasThing && currentTarget.Thing.Map != caster.Map)
            {
                return false;
            }
            ShootLine resultingLine;
            bool flag = TryFindShootLineFromTo(caster.Position, currentTarget, out resultingLine);
            if (verbProps.stopBurstWithoutLos && !flag)
            {
                return false;
            }
            if (base.EquipmentSource != null)
            {
                base.EquipmentSource.GetComp<CompChangeableProjectile>()?.Notify_ProjectileLaunched();
                base.EquipmentSource.GetComp<CompReloadable>()?.UsedOnce();
            }
            lastShotTick = Find.TickManager.TicksGame;
            ticksToNextPathStep = verbProps.ticksBetweenBurstShots;
            float startangle = verbProps.beamWidth / 2;
            foreach (indBeam beam in beamMotes)
            {
                startangle -= angle;
                Vector3 vectorinit = (InterpolatedPosition - caster.Position.ToVector3Shifted()).RotatedBy(startangle)+ caster.Position.ToVector3Shifted();
                IntVec3 intVec = vectorinit.Yto0().ToIntVec3();
                IntVec3 intVec2 = GenSight.LastPointOnLineOfSight(caster.Position, intVec, (IntVec3 c) => c.CanBeSeenOverFast(caster.Map), skipFirstCell: true);
                HitCell(intVec2.IsValid ? intVec2 : intVec);
            }

            return true;
        }

        public override bool TryStartCastOn(LocalTargetInfo castTarg, LocalTargetInfo destTarg, bool surpriseAttack = false, bool canHitNonTargetPawns = true, bool preventFriendlyFire = false, bool nonInterruptingSelfCast = false)
        {
            return base.TryStartCastOn(verbProps.beamTargetsGround ? ((LocalTargetInfo)castTarg.Cell) : castTarg, destTarg, surpriseAttack, canHitNonTargetPawns, preventFriendlyFire, nonInterruptingSelfCast);
        }

        public override void BurstingTick()
        {
            ticksToNextPathStep--;
            Vector3 vector = InterpolatedPosition;//current position
            Vector3 vectorinit = InterpolatedPosition - caster.Position.ToVector3Shifted();//vector beteen loc and cast

            float startangle = verbProps.beamWidth / 2;
            foreach (indBeam beam in beamMotes)
            {
                startangle -= angle;
                Vector3 vector2 = vectorinit.RotatedBy(startangle);
                Vector3 normalized = vector2.Yto0().normalized;//unit vector direction
                Vector3 vectortemp = (vector2 + caster.Position.ToVector3Shifted());

                IntVec3 intVec = vectortemp.ToIntVec3();//current cell
                float num = vector2.MagnitudeHorizontal();//distance
                IntVec3 intVec2 = GenSight.LastPointOnLineOfSight(caster.Position, intVec, (IntVec3 c) => c.CanBeSeenOverFast(caster.Map), skipFirstCell: true);//cell los
                if (intVec2.IsValid)
                {
                    num -= (intVec - intVec2).LengthHorizontal;//remove excess distance
                    vectortemp = caster.Position.ToVector3Shifted() + normalized * num;//
                    intVec = vectortemp.ToIntVec3();
                }
                Vector3 offsetA = normalized * verbProps.beamStartOffset;
                Vector3 vector3 = vectortemp - intVec.ToVector3Shifted();

                beam.mote.UpdateTargets(new TargetInfo(caster.Position, caster.Map), new TargetInfo(intVec, caster.Map), offsetA, vector3);
                beam.mote.Maintain();

                //mote.UpdateTargets(new TargetInfo(caster.Position, caster.Map), new TargetInfo(intVec, caster.Map), offsetA, vector3);
                //mote.Maintain();
                if (verbProps.beamGroundFleckDef != null && Rand.Chance(verbProps.beamFleckChancePerTick))
                {
                    FleckMaker.Static(vectortemp, caster.Map, verbProps.beamGroundFleckDef);
                }
                if (beam.endEffecter == null && verbProps.beamEndEffecterDef != null)
                {
                    beam.endEffecter = verbProps.beamEndEffecterDef.Spawn(intVec, caster.Map, vector3);
                }
                if (beam.endEffecter != null)
                {
                    beam.endEffecter.offset = vector3;
                    beam.endEffecter.EffectTick(new TargetInfo(intVec, caster.Map), TargetInfo.Invalid);
                    beam.endEffecter.ticksLeft--;
                }
                if (verbProps.beamLineFleckDef != null)
                {
                    float num2 = 1f * num;
                    for (int i = 0; (float)i < num2; i++)
                    {
                        if (Rand.Chance(verbProps.beamLineFleckChanceCurve.Evaluate((float)i / num2)))
                        {
                            Vector3 vector4 = i * normalized - normalized * Rand.Value + normalized / 2f;
                            FleckMaker.Static(caster.Position.ToVector3Shifted() + vector4, caster.Map, verbProps.beamLineFleckDef);
                        }
                    }
                }
                beam.sustainer?.Maintain();
            }
            sustainer?.Maintain();

        }

        public override void WarmupComplete()
        {
            //burstShotsLeft = ShotsPerBurst;
            //state = VerbState.Bursting;
            //initialTargetPosition = currentTarget.CenterVector3;
            //path.Clear();
            //Vector3 vector = (currentTarget.CenterVector3 - caster.Position.ToVector3Shifted()).Yto0();//line between caster and target
            //float magnitude = vector.magnitude;
            //Vector3 normalized = vector.normalized;// unit vector between pawn and target 
            //Vector3 vector2 = normalized.RotatedBy(-90f);//unit vector turned 90 degrees
            //float num = ((verbProps.beamFullWidthRange > 0f) ? Mathf.Min(magnitude / verbProps.beamFullWidthRange, 1f) : 1f);//beam is at full width at this range, remap to beam width being an angle, full width being number of segments
            //float num2 = (verbProps.beamWidth + 1f) * num / (float)ShotsPerBurst;//path broken up into burst segments
            //Vector3 vector3 = currentTarget.CenterVector3.Yto0() - vector2 * verbProps.beamWidth / 2f * num;//initial beam location, 1/2 beam length at given distance in perpendicular direction
            //path.Add(vector3);//initial path node
            //for (int i = 0; i < ShotsPerBurst; i++)
            //{
            //    Vector3 vector4 = normalized * (Rand.Value * verbProps.beamMaxDeviation) - normalized / 2f;//take random deviation amplitude, subtract .5?
            //    Vector3 vector5 = Mathf.Sin(((float)i / (float)ShotsPerBurst + 0.5f) * (float)Math.PI * 57.29578f) * verbProps.beamCurvature * -normalized - normalized * verbProps.beamMaxDeviation / 2f;//fit sin curve to path points, curvature being amplitude, adjust deviation back down from previous increase
            //    path.Add(vector3 + (vector4 + vector5) * num);//original path, plus vertical deviation
            //    vector3 += vector2 * num2;//move horizontally by x burst unit
            //}


            burstShotsLeft = (int)verbProps.beamFullWidthRange;
            state = VerbState.Bursting;
            initialTargetPosition = currentTarget.CenterVector3;
            path.Clear();
            beamMotes.Clear();
            Vector3 vector = (currentTarget.CenterVector3 - caster.Position.ToVector3Shifted()).Yto0();//line between caster and target
            Vector3 normalized = vector.normalized;// unit vector between pawn and target 
            Vector3 vector2 = normalized.RotatedBy(-90f);//unit vector turned 90 degrees
            //angle between each beam (beam width is total angle, shots per burst is number of beams)
            float segment = (verbProps.range- verbProps.minRange)/verbProps.beamFullWidthRange;//length of path segments (ticks is how long the full beam is, beam full width is how many path segments)
            //num = ((verbProps.beamFullWidthRange > 0f) ? Mathf.Min(magnitude / verbProps.beamFullWidthRange, 1f) : 1f);//beam is at full width at this range, remap to beam width being an angle, full width being number of segments
            //num2 = (verbProps.beamWidth + 1f) * num / (float)ShotsPerBurst;//path broken up into burst segments
            Vector3 vector3a = caster.Position.ToVector3Shifted().Yto0() + normalized * verbProps.minRange;//initial beam location, 1/2 beam length at given distance in perpendicular direction

            //can modify here in future for custom defined pathways, instead of multiplying by verbprops.minrange, we take the progress, feed it into a distance and angle curve defaulted to range values and 0 angle

            path.Add(vector3a);//initial path node
            for (int i = 0; i < verbProps.beamFullWidthRange; i++)
            {
                Vector3 vector4 = vector2 * (Rand.Value * verbProps.beamMaxDeviation) - vector2 / 2f;//take random deviation amplitude, subtract .5?
                Vector3 vector5 = Mathf.Sin(((float)i / verbProps.beamFullWidthRange + 0.5f) * (float)Math.PI * 57.29578f) * verbProps.beamCurvature * -vector2 - vector2 * verbProps.beamMaxDeviation / 2f;//fit sin curve to path points, curvature being amplitude, adjust deviation back down from previous increase
                float CurrentMag= Mathf.Tan((angle/360)*Mathf.PI)* ((vector3a - caster.Position.ToVector3Shifted()).Yto0()).magnitude;
                path.Add(vector3a + (vector4 + vector5) * CurrentMag);//original path, plus vertical deviation, can add rotation later
                vector3a += normalized * segment;//move horizontally by x burst unit, can rotate later based on burst number. rotate normalized by angle, and mult segment by interpolated value of length 
                //change to vector3a= original 3a+ normalized.rotatedby(lerped angle) + range difference*lerped distance
            }
            //use progress of shot total, fed into min to max range distance value, coupled with corresponding 



            if (verbProps.beamMoteDef != null)
            {
                moteGenerator();
                //mote = MoteMaker.MakeInteractionOverlay(verbProps.beamMoteDef, caster, new TargetInfo(path[0].ToIntVec3(), caster.Map));
            }
            TryCastNextBurstShot();
            ticksToNextPathStep = verbProps.ticksBetweenBurstShots;
            foreach (indBeam beam in beamMotes)
            {
                beam.endEffecter?.Cleanup();
            }
            if (verbProps.soundCastBeam != null)
            {
                sustainer = verbProps.soundCastBeam.TrySpawnSustainer(SoundInfo.InMap(caster, MaintenanceType.PerTick));
            }
        }
        public void moteGenerator()
        {
            float startangle = verbProps.beamWidth/2;
            for(int i = 0; i < ShotsPerBurst; i++)
            {
                startangle -= angle;
                indBeam beam = new indBeam();
                beam.mote = MoteMaker.MakeInteractionOverlay(verbProps.beamMoteDef, caster, new TargetInfo(path[0].RotatedBy(startangle).ToIntVec3(), caster.Map));
                beamMotes.Add(beam);
            }
        }
        private bool CanHit(Thing thing)
        {
            if (!thing.Spawned)
            {
                return false;
            }
            return !CoverUtility.ThingCovered(thing, caster.Map);
        }

        private void HitCell(IntVec3 cell)
        {
            ApplyDamage(VerbUtility.ThingsToHit(cell, caster.Map, CanHit).RandomElementWithFallback(), cell);
        }

        private void ApplyDamage(Thing thing, IntVec3 cell)
        {
            IntVec3 intVec = cell;
            IntVec3 intVec2 = GenSight.LastPointOnLineOfSight(caster.Position, intVec, (IntVec3 c) => c.CanBeSeenOverFast(caster.Map), skipFirstCell: true);
            if (intVec2.IsValid)
            {
                intVec = intVec2;
            }
            Map map = caster.Map;
            if (thing == null || verbProps.beamDamageDef == null)
            {
                return;
            }
            float angleFlat = (currentTarget.Cell - caster.Position).AngleFlat;
            BattleLogEntry_RangedImpact log = new BattleLogEntry_RangedImpact(caster, thing, currentTarget.Thing, base.EquipmentSource.def, null, null);
            DamageInfo dinfo = new DamageInfo(verbProps.beamDamageDef, verbProps.beamDamageDef.defaultDamage, verbProps.beamDamageDef.defaultArmorPenetration, angleFlat, caster, null, base.EquipmentSource.def, DamageInfo.SourceCategory.ThingOrUnknown, currentTarget.Thing);
            thing.TakeDamage(dinfo).AssociateWithLog(log);
            if (thing.CanEverAttachFire())
            {
                if (Rand.Chance(verbProps.beamChanceToAttachFire))
                {
                    thing.TryAttachFire(verbProps.beamFireSizeRange.RandomInRange);
                }
            }
            else if (Rand.Chance(verbProps.beamChanceToStartFire))
            {
                FireUtility.TryStartFireIn(intVec, map, verbProps.beamFireSizeRange.RandomInRange);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref path, "path", LookMode.Value);
            Scribe_Values.Look(ref ticksToNextPathStep, "ticksToNextPathStep", 0);
            Scribe_Values.Look(ref initialTargetPosition, "initialTargetPosition");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && path == null)
            {
                path = new List<Vector3>();
            }
        }
    }

}
