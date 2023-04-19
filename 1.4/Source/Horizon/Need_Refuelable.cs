using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Horizon
{
    //need class for accessing the stored capacity of the fuel

    //[HarmonyPatch(typeof(Pawn), "GetFloatMenuOptions")]
    //public static class Pawn_NeedsTracker_GetGizmos_Patch
    //{
    //    public static void Postfix(IEnumerable<FloatMenuOption> __result, Pawn __instance, Pawn myPawn)
    //    {
    //        if (__instance.Faction == null || __instance.Faction != Faction.OfPlayer)
    //        {
    //            return;
    //        }
    //        IEnumerable<Need_Refuelable> t = from a in __instance.needs.MiscNeeds where a is Need_Refuelable select (Need_Refuelable)a;
    //        foreach (Need_Refuelable need in t)
    //        {
    //            __result.Concat(need.GetFloatMenuOption(myPawn));
    //        }
    //    }
    //}

    public class JobDriver_ExtractResource : JobDriver
    {
        private const int BaseTicks = 600;

        private int DurationTicks => Mathf.CeilToInt(BaseTicks * (1f / pawn.GetStatValue(StatDefOf.AnimalGatherSpeed)));

        private Pawn target => (Pawn)job.GetTarget(TargetIndex.A).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(target, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch).FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_General.WaitWith(TargetIndex.A, DurationTicks, true, true)
                .FailOnDespawnedOrNull(TargetIndex.A)
                .FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.Do(Extract);
        }

        private void Extract()
        {
            //if (Canextract())
            //{
            //    Thing thing = need.extract();
            //    GenPlace.TryPlaceThing(thing, pawn.Position, target.Map, ThingPlaceMode.Near);
            //}
        }
    }
    public class FuelNeed : DefModExtension
    {
        public ThingDef fuelDef;
    }
    public class Need_Refuelable : Need
    {
        public Need_Refuelable(Pawn pawn) : base(pawn)
        {
        }
        public ThingDef fuelThingDef => def.GetModExtension<FuelNeed>()?.fuelDef ?? null;
        public virtual IEnumerable<FloatMenuOption> GetFloatMenuOption(Pawn myPawn)
        {
            if (CurLevel < 1)
            {
                yield break;
            }
            JobDef jobDef = JobMechDefOf.ExtractResources;
            string label = "ExtractResource".Translate(fuelThingDef.label);
            Action action = delegate
            {
                
                    myPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(jobDef, pawn), JobTag.Misc);
            };
            yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(label, action), myPawn, pawn);
        }



        //public virtual IEnumerable<Gizmo> GetGizmo()//need some gizmo to empty need, or some button on the need menu
        //{
        //    if (pawn.Faction != null && pawn.Faction == Faction.OfPlayer)
        //    {
        //        Command_Action command_Action = new Command_Action();
        //        command_Action.action = Activate;
        //            command_Action.defaultLabel = "ExtractMaterials".Translate();
        //            command_Action.defaultDesc = "ExtractMaterialsDesc".Translate();
        //            //if (Need.cannotActivate)
        //            //{
        //            //    command_Action.Disable("NeedEmpty".Translate());
        //            //}
        //            //command_Action.hotKey = KeyBindingDefOf.Misc8;
        //            command_Action.icon = ContentFinder<Texture2D>.Get("UI/Commands/SetTargetFuelLevel");
        //            yield return command_Action;
        //    }
        //}

        public override float MaxLevel
        {
            get
            {
                float result = 0;
                IEnumerable<HediffComp_Refuelable> t = from a in pawn.health.hediffSet.hediffs
                                          where a is HediffWithComps x && x.TryGetComp<HediffComp_Refuelable>() != null
                                          select a.TryGetComp<HediffComp_Refuelable>() into b where b.Props.Need.Equals(def) select b;
                foreach (HediffComp_Refuelable comp in t)
                {
                    result += comp.Props.fullChargeAmount;
                }
                return result;
            }
        }
        public int Extract()
        {
            int output = (int)(CurLevel % 1);
            CurLevel -= output;
            return output;
        }
        public void Activate()
        {
            if (fuelThingDef == null)
            {
                return;
            }
            float amount = 0;
            IEnumerable<HediffComp_Refuelable> t = from a in pawn.health.hediffSet.hediffs
                                                   where a is HediffWithComps x && x.TryGetComp<HediffComp_Refuelable>() != null
                                                   select a.TryGetComp<HediffComp_Refuelable>() into b
                                                   where b.Props.Need.Equals(def)
                                                   select b;
            foreach (HediffComp_Refuelable comp in t)
            {
                amount += comp.Activate();
            }
            if (amount < 1)
            {
                return;
            }
            Thing thing = ThingMaker.MakeThing(fuelThingDef);
            thing.stackCount = (int)amount;
            GenPlace.TryPlaceThing(thing, pawn.Position, pawn.Map, ThingPlaceMode.Near);
            Messages.Message(pawn.ToString() + " produced item " + thing.ToString() + " x" + thing.stackCount.ToString(), MessageTypeDefOf.TaskCompletion, false);
        }
        public override void NeedInterval()
        {
            //throw new NotImplementedException();//want to only consume when in use by action
        }
    }
}
