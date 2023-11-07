using MVCF.Utilities;
using MVCF.VerbComps;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse.AI;
using Verse;
using UnityEngine.UIElements;
using Verse.Sound;
using MVCF.Comps;
using UnityEngine.UI;
using Verse.Noise;
using System.Security.Policy;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;
using MVCF;

namespace Horizon
{
    [HarmonyPatch(typeof(Verb_LaunchProjectile),"TryCastShot")]
    public static class Verb_LaunchProjectile_TryCastShot_Patch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodBase from = AccessTools.PropertyGetter(typeof(Thing), "DrawPos");
            MethodBase to = AccessTools.Method(typeof(Verb_LaunchProjectile_TryCastShot_Patch), "drawposModifier");
            foreach (CodeInstruction instruction in instructions)
            {
                yield return instruction;
                if (instruction.operand as MethodBase == from)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, to);
                }
            }
        }
        public static Vector3 drawposModifier(Vector3 drawpos, Verb verb)
        {
            ManagedVerb managed = verb.Managed();
            if (managed != null && managed.TryGetComp<VerbComp_Turret>() is VerbComp_Turret comp)
            {
                return comp.DrawPos(verb.CurrentTarget, verb.CasterPawn, drawpos);
            }
            return drawpos;
        }
    }
    public class VerbComp_HediffTurret: VerbComp_Turret
    {
        private Sustainer sustainer;

        protected Effecter effecter;

        private Mote aimLineMote;

        private Mote aimChargeMote;

        private Mote aimTargetMote;

        //private bool targetStartedDowned;

        private bool drawAimPie = true;

        private bool needsReInitAfterLoad;
        public new VerbCompProperties_HediffTurret Props => props as VerbCompProperties_HediffTurret;

        public int startedTick;

        public bool fired = false;
        protected override void TryStartCast()
        {
            startedTick = Find.TickManager.TicksGame;
            fired = false;
            InitEffects();
            base.TryStartCast();
        }
        public void InitEffects(bool afterReload = false)
        {
            if (parent == null || !Target.IsValid)
            {
                return;
            }
            VerbProperties verbProps = parent.Verb.verbProps;
            if (verbProps.soundAiming != null)
            {
                SoundInfo info = SoundInfo.InMap(parent.Verb.caster, MaintenanceType.PerTick);
                if (parent.Verb.CasterIsPawn)
                {
                    info.pitchFactor = 1f / parent.Verb.CasterPawn.GetStatValue(StatDefOf.AimingDelayFactor);
                }
                sustainer = verbProps.soundAiming.TrySpawnSustainer(info);
            }
            if (verbProps.warmupEffecter != null && parent.Verb.Caster != null)
            {
                effecter = verbProps.warmupEffecter.Spawn(parent.Verb.Caster, parent.Verb.Caster.Map);
                effecter.Trigger(parent.Verb.Caster, Target.ToTargetInfo(parent.Verb.Caster.Map));
            }
            if (parent.Verb.Caster == null)
            {
                return;
            }
            Map map = parent.Verb.Caster.Map;
            if (verbProps.aimingLineMote != null)
            {
                Vector3 vector = TargetPos();
                IntVec3 cell = vector.ToIntVec3();
                aimLineMote = MoteMaker.MakeInteractionOverlay(verbProps.aimingLineMote, parent.Verb.Caster, new TargetInfo(cell, map), Vector3.zero, vector - cell.ToVector3Shifted());
                if (afterReload)
                {
                    aimLineMote?.ForceSpawnTick(startedTick);
                }
            }
            if (verbProps.aimingChargeMote != null)
            {
                aimChargeMote = MoteMaker.MakeStaticMote(parent.Verb.Caster.DrawPos, map, verbProps.aimingChargeMote, 1f, makeOffscreen: true);
                if (afterReload)
                {
                    aimChargeMote?.ForceSpawnTick(startedTick);
                }
            }
            if (verbProps.aimingTargetMote == null)
            {
                return;
            }
            aimTargetMote = MoteMaker.MakeStaticMote(Target.CenterVector3, map, verbProps.aimingTargetMote, 1f, makeOffscreen: true);
            if (aimTargetMote != null)
            {
                aimTargetMote.exactRotation = AimDir().ToAngleFlat();
                if (afterReload)
                {
                    aimTargetMote.ForceSpawnTick(startedTick);
                }
            }
        }
        private Vector3 TargetPos()
        {
            VerbProperties verbProps = parent.Verb.verbProps;
            Vector3 result = Target.CenterVector3;
            if (verbProps.aimingLineMoteFixedLength.HasValue)
            {
                result = parent.Verb.Caster.DrawPos + AimDir() * verbProps.aimingLineMoteFixedLength.Value;
            }
            return result;
        }

        private Vector3 AimDir()
        {
            Vector3 result = Target.CenterVector3 - parent.Verb.Caster.DrawPos;
            result.y = 0f;
            result.Normalize();
            return result;
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref startedTick, "startedTick", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                needsReInitAfterLoad = true;
            }
        }
        public override Vector3 DrawPos(LocalTargetInfo target, Pawn p, Vector3 drawPos)
        {
            IVerbOwner verbowner = parent.Verb.DirectOwner;
            Vector3 temp = Props.DrawPos(p, drawPos, p.Rotation, verbowner);
            if(Props.drawAsEquipment)
            {
                return base.DrawPos(target, p, temp);
            }
            return temp;
        }
        //public override void DrawOnAt(Pawn p, Vector3 drawPos)
        //{
        //    //if (Props.invisible) return;
        //    base.DrawOnAt(p, drawPos);
        //    //if (Target.IsValid)
        //    //{

        //    //    //replace with mote drawing
        //    //    if (warmUpTicksLeft > 0)
        //    //        GenDraw.DrawAimPie(p, Target, warmUpTicksLeft, 0.2f);
        //    //    if (cooldownTicksLeft > 0)
        //    //        GenDraw.DrawCooldownCircle(p.DrawPos, cooldownTicksLeft * 0.002f);
        //    //    GenDraw.DrawLineBetween(drawPos, Target.HasThing ? Target.Thing.DrawPos : Target.Cell.ToVector3());
        //    //}
        //}
        public override void CompTick()
        {
            base.CompTick();
            if (fired || !Target.IsValid)
            {
                return;
            }
            if (needsReInitAfterLoad)
            {
                InitEffects(afterReload: true);
                needsReInitAfterLoad = false;
            }
            if (sustainer != null && !sustainer.Ended)
            {
                sustainer.Maintain();
            }
            effecter?.EffectTick(parent.Verb.Caster, Target.ToTargetInfo(parent.Verb.Caster.Map));
            Vector3 vector = AimDir();
            float exactRotation = vector.AngleFlat();
            Vector3 offset = DrawPos(Target, (Pawn)parent.Verb.Caster, Vector3.zero);
            //bool stunned = stanceTracker.stunner.Stunned;
            if (aimLineMote != null)
            {
                //aimLineMote.paused = stunned;
                aimLineMote.Maintain();
                Vector3 vector2 = TargetPos();
                IntVec3 cell = vector2.ToIntVec3();
                ((MoteDualAttached)aimLineMote).UpdateTargets(parent.Verb.Caster, new TargetInfo(cell, parent.Verb.Caster.Map), offset, vector2 - cell.ToVector3Shifted());
            }
            if (aimTargetMote != null)
            {
                //aimTargetMote.paused = stunned;
                aimTargetMote.exactPosition = Target.CenterVector3;
                aimTargetMote.exactRotation = exactRotation;
                aimTargetMote.Maintain();
            }
            if (aimChargeMote != null)
            {
                //aimChargeMote.paused = stunned;
                aimChargeMote.exactRotation = exactRotation;
                aimChargeMote.exactPosition = parent.Verb.Caster.Position.ToVector3Shifted() + vector * parent.Verb.verbProps.aimingChargeMoteOffset + offset;
                aimChargeMote.Maintain();
            }
            //if (!stanceTracker.stunner.Stunned)
            //{
            //    if (!targetStartedDowned && focusTarg.HasThing && focusTarg.Thing is Pawn && ((Pawn)focusTarg.Thing).Downed)
            //    {
            //        stanceTracker.SetStance(new Stance_Mobile());
            //        return;
            //    }
            //    if (focusTarg.HasThing && (!focusTarg.Thing.Spawned || verb == null || !verb.CanHitTargetFrom(base.Pawn.Position, focusTarg)))
            //    {
            //        stanceTracker.SetStance(new Stance_Mobile());
            //        return;
            //    }
            //    if (focusTarg == base.Pawn.mindState.enemyTarget)
            //    {
            //        base.Pawn.mindState.Notify_EngagedTarget();
            //    }
            //}
            //if (!stanceTracker.stunner.Stunned)
            //{
            //    ticksLeft--;
            //    if (ticksLeft <= 0)
            //    {
            //        Expire();
            //    }
            //}
        }
        protected override void TryCast()
        {
            base.TryCast();
            fired = true;
            if (parent.Verb.TryStartCastOn(Target) && parent.Verb.verbProps.warmupTime > 0f)
            {
                effecter?.Cleanup();
            }
        }
    }
    
    public class VerbCompProperties_HediffTurret : VerbCompProperties_Turret
    {
        //check race, then check hediff, then check bodytype, then default
        public List<RacePosition> RacePositions;//xml race based locations

        public Dictionary<string, RacePosition> ThingDefPositions;//exposed dictionary of races

        public List<HediffPosition> hediffPositions;//xml default hediff based locations

        public Dictionary<string, HediffPosition> hediffLabelPositions;//exposed dictionary of default hediffs

        public List<DrawYPosition> bodyTypePositions; //bodytype positions
        
        public Dictionary<BodyTypeDef, DrawYPosition> bodyDefPositions;

        public new DrawYPosition defaultPosition;


        public override void PostLoadSpecial(VerbProperties verbProps, AdditionalVerbProps additionalProps,Def parentDef)
        {
            base.PostLoadSpecial(verbProps, additionalProps, parentDef);
            //base.PostLoadSpecial(verbProps, additionalProps);
            //if (graphic != null)
            //{
            //    Graphic = graphic.Graphic;
            //    additionalProps.Icon = (Texture2D)Graphic.ExtractInnerGraphicFor(null).MatNorth.mainTexture;
            //}

            if (ThingDefPositions == null)
            {
                ThingDefPositions = new Dictionary<string, RacePosition>();

                if (RacePositions != null)
                    foreach (var pos in RacePositions)
                        if (!ThingDefPositions.ContainsKey(pos.defName))
                        {
                            ThingDefPositions.Add(pos.defName, pos);
                            pos.PostLoadSpecial(verbProps,additionalProps,parentDef);
                        }
            }
            if (hediffLabelPositions == null)
            {
                hediffLabelPositions = new Dictionary<string, HediffPosition>();

                if (hediffPositions != null)
                    foreach (var pos in hediffPositions)
                        if (!hediffLabelPositions.ContainsKey(pos.label))
                        {
                            hediffLabelPositions.Add(pos.label, pos);
                            pos.PostLoadSpecial(verbProps, additionalProps, parentDef);
                        }
            }
            if (bodyDefPositions == null)
            {
                bodyDefPositions = new Dictionary<BodyTypeDef, DrawYPosition>();

                if (bodyTypePositions != null)
                    foreach (var pos in bodyTypePositions)
                        if (!bodyDefPositions.ContainsKey(pos.BodyType))
                        {
                            bodyDefPositions.Add(pos.BodyType, pos);
                            pos.PostLoadSpecial(verbProps, additionalProps, parentDef);
                        }
            }
            //if (scale == null)
            //{
            //    scale = new Dictionary<string, Dictionary<BodyTypeDef, Scaling>>();
            //    if (scalings != null)
            //        foreach (var scaling in scalings)
            //            if (scale.ContainsKey(scaling.defName))
            //                scale[scaling.defName].Add(scaling.BodyType, scaling);
            //            else
            //                scale.Add(scaling.defName,
            //                    new Dictionary<BodyTypeDef, Scaling> { { scaling.BodyType, scaling } });
            //}
        }
        public Vector3 DrawPos(Pawn pawn, Vector3 drawPos, Rot4 rot, IVerbOwner verbOwner)
        {
            DrawYPosition pos = null;
            Dictionary<string, HediffPosition> hediffs = hediffLabelPositions;
            Dictionary<BodyTypeDef, DrawYPosition> bodytypes = bodyDefPositions;
            DrawYPosition defaultPos = defaultPosition;
            if (ThingDefPositions.TryGetValue(pawn.def.defName, out var race))
            {
                hediffs = race.hediffLabelPositions;
                bodytypes = race.bodyDefPositions;
                defaultPos = race;
            }
            if(verbOwner is HediffComp_VerbGiver hediffComp && hediffs.TryGetValue(hediffComp.parent.Part?.LabelCap, out var hedf))
            {
                //Log.Message("ishediff");
                bodytypes = hedf.bodyDefPositions;
                defaultPos = hedf;
                
            }
            if (pawn.story?.bodyType != null && bodytypes.TryGetValue(pawn.story.bodyType, out pos))
            {
                defaultPos = pos;
            }
            if (defaultPos == null)
                defaultPos = defaultPosition ?? DrawYPosition.Zero;
            return drawPos + defaultPos.ForRot(rot) + defaultPos.ForRotY(rot);
        }
    }
    public class RacePosition:HediffPosition
    {
        private static readonly Vector2 PLACEHOLDER = Vector2.positiveInfinity;

        public Dictionary<string, HediffPosition> hediffLabelPositions;

        public List<HediffPosition> hediffPositions;// hediff label specific positions


        //public DrawPosition defaultPosition;
        //public BodyTypeDef BodyType = AdditionalVerbProps.NA;

        //public Vector2 Default = PLACEHOLDER;

        //public string defName;//racename

        //public Vector3 Down = PLACEHOLDER;

        //public Vector3 Left = PLACEHOLDER;

        //public Vector3 Right = PLACEHOLDER;

        //public Vector3 Up = PLACEHOLDER;

        //public static DrawPosition Zero => new DrawPosition
        //{
        //    defName = "",
        //    Default = Vector2.zero
        //};

        //public Vector3 ForRot(Rot4 rot)
        //{
        //    var vec = PLACEHOLDER;
        //    switch (rot.AsInt)
        //    {
        //        case 0:
        //            vec = Up;
        //            break;
        //        case 1:
        //            vec = Right;
        //            break;
        //        case 2:
        //            vec = Down;
        //            break;
        //        case 3:
        //            vec = Left;
        //            break;
        //        default:
        //            vec = Default;
        //            break;
        //    }

        //    if (double.IsPositiveInfinity(vec.x)) vec = Default;
        //    if (double.IsPositiveInfinity(vec.x)) vec = Vector2.zero;
        //    return new Vector3(vec.x, 0, vec.y);
        //}
        public override void PostLoadSpecial(VerbProperties verbProps, AdditionalVerbProps additionalProps, Def parentDef)
        {
            base.PostLoadSpecial(verbProps, additionalProps, parentDef);
            if (hediffLabelPositions == null)
            {
                hediffLabelPositions = new Dictionary<string, HediffPosition>();

                if (hediffPositions != null)
                    foreach (var pos in hediffPositions)
                        if (!hediffLabelPositions.ContainsKey(pos.label))
                        {
                            hediffLabelPositions.Add(pos.label, pos);
                            pos.PostLoadSpecial(verbProps, additionalProps, parentDef);
                        }
            }
        }
    }

    public class HediffPosition:DrawYPosition
    {
        //private static readonly Vector2 PLACEHOLDER = Vector2.positiveInfinity;

        //public static List<HediffPosition> hediffPositions;

        //public BodyTypeDef BodyType = AdditionalVerbProps.NA;

        //public Vector2 Default = PLACEHOLDER;

        public string label;

        public List<DrawYPosition> bodyTypePositions;// bodytype def default specific positions

        public Dictionary<BodyTypeDef, DrawYPosition> bodyDefPositions;

        //public Vector2 Down = PLACEHOLDER;

        //public Vector2 Left = PLACEHOLDER;

        //public Vector2 Right = PLACEHOLDER;

        //public Vector2 Up = PLACEHOLDER;


        //public Vector3 ForRot(Rot4 rot)
        //{
        //    var vec = PLACEHOLDER;
        //    switch (rot.AsInt)
        //    {
        //        case 0:
        //            vec = Up;
        //            break;
        //        case 1:
        //            vec = Right;
        //            break;
        //        case 2:
        //            vec = Down;
        //            break;
        //        case 3:
        //            vec = Left;
        //            break;
        //        default:
        //            vec = Default;
        //            break;
        //    }

        //    if (double.IsPositiveInfinity(vec.x)) vec = Default;
        //    if (double.IsPositiveInfinity(vec.x)) vec = Vector2.zero;
        //    return new Vector3(vec.x, 0, vec.y);
        //}
        public override void PostLoadSpecial(VerbProperties verbProps, AdditionalVerbProps additionalProps, Def parentDef)
        {
            base.PostLoadSpecial(verbProps, additionalProps, parentDef);
            if (bodyDefPositions == null)
            {
                bodyDefPositions = new Dictionary<BodyTypeDef, DrawYPosition>();

                if (bodyTypePositions != null)
                    foreach (var pos in bodyTypePositions)
                        if (!bodyDefPositions.ContainsKey(pos.BodyType))
                        {
                            bodyDefPositions.Add(pos.BodyType, pos);
                            pos.PostLoadSpecial(verbProps, additionalProps, parentDef);
                        }
            }
        }
    }
    public class DrawYPosition : DrawPosition
    {
        public float DownY = 0f;

        public float LeftY = 0f;

        public float RightY = 0f;

        public float UpY = 0f;

        public static new DrawYPosition Zero => new DrawYPosition
        {
            defName = "",
            Default = Vector2.zero
        };

        public virtual void PostLoadSpecial(VerbProperties verbProps, AdditionalVerbProps additionalProps, Def parentDef)
        {

        }
        public Vector3 ForRotY(Rot4 rot)
        {
            float vec;
            switch (rot.AsInt)
            {
                case 0:
                    vec = UpY;
                    break;
                case 1:
                    vec = RightY;
                    break;
                case 2:
                    vec = DownY;
                    break;
                case 3:
                    vec = LeftY;
                    break;
                default:
                    vec = 0f;
                    break;
            }

            return new Vector3(0f, vec, 0f);
        }
    }
}
