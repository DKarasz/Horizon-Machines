using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace Horizon
{
    //<WorkGiverDef>
    //    <defName>Drill</defName>
    //    <label>drill at drill spot</label>
    //    <giverClass>WorkGiver_RemoteDrill</giverClass>
    //    <workType>Mining</workType>
    //    <priorityInType>50</priorityInType>
    //    <verb>drill</verb>
    //    <gerund>drilling</gerund>
    //    <requiredCapacities>
    //        <li>Manipulation</li>
    //    </requiredCapacities>
    //    <canBeDoneByMechs>true</canBeDoneByMechs>
    //</WorkGiverDef>

	public class WorkNeedControl
    {
		public List<NeedComposite> inputNeed = null;
		//check if needs have room
		public List<NeedComposite> outputNeed = null;
		public bool ignoreOverflow = false;

		public bool canDoProcess(Pawn pawn)
		{
			foreach (NeedComposite needComp in inputNeed)
			{
				Need need = pawn.needs.TryGetNeed(needComp.need);
				if (need == null || need.CurLevel < needComp.amount)
				{
					return false;
				}
			}
			if (!ignoreOverflow)
			{
				foreach (NeedComposite needComp in outputNeed)
				{
					Need need = pawn.needs.TryGetNeed(needComp.need);
					if (need == null || need.MaxLevel - need.CurLevel < needComp.amount)
					{
						return false;
					}
				}
			}
			return true;
		}
		public void doProcess(Pawn pawn)
        {
			foreach (NeedComposite needComp in inputNeed)
			{
				Need need = pawn.needs.TryGetNeed(needComp.need);
				if (need != null)
				{
					need.CurLevel -= needComp.amount;
				}
			}
			foreach (NeedComposite needComp in outputNeed)
			{
				Need need = pawn.needs.TryGetNeed(needComp.need);
				if (need != null)
				{
					need.CurLevel += needComp.amount;
				}
			}
		}
	}
	public class RemoteDrill : DefModExtension
	{
		public WorkNeedControl needControl;
	}

	[DefOf]
	public static class MechThingDefOf
	{
		public static ThingDef DrillSpot;

	}
	public class WorkGiver_RemoteDrill: WorkGiver_DeepDrill
    {
		public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(MechThingDefOf.DrillSpot);

		public override PathEndMode PathEndMode => PathEndMode.InteractionCell;

		public override Danger MaxPathDanger(Pawn pawn)
		{
			return Danger.Deadly;
		}
		public static List<Pawn> disallowedPawns = new List<Pawn>();
		public override bool ShouldSkip(Pawn pawn, bool forced = false)
		{
            if (disallowedPawns.Contains(pawn))
            {
				return true;
            }
			RemoteDrill drill = pawn.def.GetModExtension<RemoteDrill>();
			if (drill == null)
            {
				foreach(Hediff hediff in pawn.health.hediffSet.hediffs)
                {
					HediffComp_Drill comp = hediff.TryGetComp<HediffComp_Drill>();
					if (comp != null)
                    {
						drill = comp.Props.extension;
						break;
                    }
                }
            }
			if (drill == null)
            {
				disallowedPawns.Add(pawn);
				return true;
            }
			if(drill.needControl != null && !drill.needControl.canDoProcess(pawn))
            {
				return true;
            }
			List<Building> allBuildingsColonist = pawn.Map.listerBuildings.allBuildingsColonist;
			for (int i = 0; i < allBuildingsColonist.Count; i++)
			{
				Building building = allBuildingsColonist[i];
				if (building.def == MechThingDefOf.DrillSpot)
				{
					CompPowerTrader comp = building.GetComp<CompPowerTrader>();
					if ((comp == null || comp.PowerOn) && building.Map.designationManager.DesignationOn(building, DesignationDefOf.Uninstall) == null)
					{
						return false;
					}
				}
			}
			return true;
		}
		

		public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
		{
			if (t.Faction != pawn.Faction)
			{
				return false;
			}
			if (!(t is Building building))
			{
				return false;
			}
			if (building.IsForbidden(pawn))
			{
				return false;
			}
			if (!pawn.CanReserve(building, 1, -1, null, forced))
			{
				return false;
			}
			if (!building.TryGetComp<CompDeepDrill>().CanDrillNow())
			{
				return false;
			}
			if (building.Map.designationManager.DesignationOn(building, DesignationDefOf.Uninstall) != null)
			{
				return false;
			}
			if (building.IsBurning())
			{
				return false;
			}
			return true;
		}

		public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
		{
			return JobMaker.MakeJob(JobMechDefOf.OperateDeepDrillSpot, t, 1500, checkOverrideOnExpiry: true);
		}
	}
	public class HediffCompProperties_Drill : HediffCompProperties
	{
		public RemoteDrill extension;
	}
	public class HediffComp_Drill : HediffComp
    {
		public HediffCompProperties_Drill Props => (HediffCompProperties_Drill)props;
        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
			WorkGiver_RemoteDrill.disallowedPawns.Clear();
        }
    }
	public class JobDriver_OperateDeepDrillSpot : JobDriver
	{
		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
		}
		public virtual RemoteDrill drillextension
        {
            get
            {
				RemoteDrill drill = pawn.def.GetModExtension<RemoteDrill>();
				if (drill == null)
				{
					foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
					{
						HediffComp_Drill comp = hediff.TryGetComp<HediffComp_Drill>();
						if (comp != null)
						{
							drill = comp.Props.extension;
							break;
						}
					}
				}
				return drill;
            }
        }
		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
			this.FailOnBurningImmobile(TargetIndex.A);
			this.FailOnThingHavingDesignation(TargetIndex.A, DesignationDefOf.Uninstall);
			this.FailOn(() => !job.targetA.Thing.TryGetComp<CompDeepDrill>().CanDrillNow()||!drillextension.needControl.canDoProcess(pawn));
			yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
			Toil work = ToilMaker.MakeToil("MakeNewToils");
			work.tickAction = delegate
			{
				Pawn actor = work.actor;
				((Building)actor.CurJob.targetA.Thing).GetComp<CompDeepDrill>().DrillWorkDone(actor);
				drillextension.needControl.doProcess(pawn);
				actor.skills?.Learn(SkillDefOf.Mining, 0.065f);
			};
			work.defaultCompleteMode = ToilCompleteMode.Never;
			work.WithEffect(EffecterDefOf.Drill, TargetIndex.A);
			work.FailOnCannotTouch(TargetIndex.A, PathEndMode.InteractionCell);
			work.FailOnDespawnedNullOrForbidden(TargetIndex.A);
			work.activeSkill = () => SkillDefOf.Mining;
			yield return work;
		}
	}
}
