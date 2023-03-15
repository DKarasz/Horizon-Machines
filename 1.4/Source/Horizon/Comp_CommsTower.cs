using HarmonyLib;
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
	//ground scanner patch
	[HarmonyPatch(typeof(CompDeepScanner), "ShouldShowDeepResourceOverlay")]
	public static class CompDeepScanner_ShouldShowDeepResourceOverlay_Patch
    {
		public static bool Prefix(CompPowerTrader ___powerComp, ref bool __result)
        {
			if (___powerComp == null)
            {
				__result = true;
				return false;
            }
			return true;
        }
    }
	[HarmonyPatch(typeof(DeepResourceGrid),"AnyActiveDeepScannersOnMap")]
	public static class DeepResourceGrid_AnyActiveDeepScannerOnMap_Patch
    {
		public static void Postfix(Map ___map, ref bool __result)
        {
            if (__result)
            {
				return;
            }
			foreach(Pawn pawn in ___map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer))
            {
				CompDeepScanner compDeepScanner = pawn.TryGetComp<CompDeepScanner>();
				if (compDeepScanner != null && compDeepScanner.ShouldShowDeepResourceOverlay())
                {
					__result = true;
					return;
                }
            }
        }
    }

	public class CompProperties_CommsTower : CompProperties
	{
		public CompProperties_CommsTower()
		{
			compClass = typeof(Comp_CommsTower);
		}
	}
	public class Comp_CommsTower: ThingComp
	{
		public CompPowerTrader powerComp;
		//public CompProperties_CommsTower Props => (CompProperties_CommsTower)props;
		public bool CanUseCommsNow
		{
			get
			{
				if (parent.Spawned && parent.Map.gameConditionManager.ElectricityDisabled)
				{
					return false;
				}
				if (powerComp != null)
				{
					return powerComp.PowerOn;
				}
				if (parent is Pawn pawn)
				{
					if (pawn.Faction != null && pawn.Faction == Faction.OfPlayer)
					{
						return true;
					}
					return false;
				}
				return true;
			}
		}

		public override void PostSpawnSetup(bool respawningAfterLoad)
		{
			base.PostSpawnSetup(respawningAfterLoad);
			powerComp = parent.GetComp<CompPowerTrader>();
			LessonAutoActivator.TeachOpportunity(ConceptDefOf.BuildOrbitalTradeBeacon, OpportunityType.GoodToKnow);
			LessonAutoActivator.TeachOpportunity(ConceptDefOf.OpeningComms, OpportunityType.GoodToKnow);
		}

		private void UseAct(Pawn myPawn, ICommunicable commTarget)
		{
			Job job = JobMaker.MakeJob(HorizonJobDefOf.UseCommsConsoleItem, parent);
			job.commTarget = commTarget;
			myPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
			PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.OpeningComms, KnowledgeAmount.Total);
		}

		private FloatMenuOption GetFailureReason(Pawn myPawn)
		{
			if(parent is Pawn pawn && !pawn.IsColonyMechPlayerControlled)
            {
				return new FloatMenuOption("CannotUseReason".Translate("CannotOrderNonControlledLower".Translate()), null);
			}
			if (!myPawn.CanReach(parent, PathEndMode.InteractionCell, Danger.Some))
			{
				return new FloatMenuOption("CannotUseNoPath".Translate(), null);
			}
			if (parent.Spawned && parent.Map.gameConditionManager.ElectricityDisabled)
			{
				return new FloatMenuOption("CannotUseSolarFlare".Translate(), null);
			}
			if (powerComp != null && !powerComp.PowerOn)
			{
				return new FloatMenuOption("CannotUseNoPower".Translate(), null);
			}
			if (!myPawn.health.capacities.CapableOf(PawnCapacityDefOf.Talking))
			{
				return new FloatMenuOption("CannotUseReason".Translate("IncapableOfCapacity".Translate(PawnCapacityDefOf.Talking.label, myPawn.Named("PAWN"))), null);
			}
			if (!GetCommTargets(myPawn).Any())
			{
				return new FloatMenuOption("CannotUseReason".Translate("NoCommsTarget".Translate()), null);
			}
			if (!CanUseCommsNow)
			{
				Log.Error(string.Concat(myPawn, " could not use comm console for unknown reason."));
				return new FloatMenuOption("Cannot use now", null);
			}
			return null;
		}

		public IEnumerable<ICommunicable> GetCommTargets(Pawn myPawn)
		{
			return myPawn.Map.passingShipManager.passingShips.Cast<ICommunicable>().Concat(Find.FactionManager.AllFactionsVisibleInViewOrder.Where((Faction f) => !f.temporary && !f.IsPlayer).Cast<ICommunicable>());
		}

		public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn myPawn)
		{
			FloatMenuOption failureReason = GetFailureReason(myPawn);
			if (failureReason != null)
			{
				yield return failureReason;
				yield break;
			}
			foreach (ICommunicable commTarget in GetCommTargets(myPawn))
			{
				FloatMenuOption floatMenuOption = this.CommFloatMenuOption(commTarget, myPawn);
				if (floatMenuOption != null)
				{
					yield return floatMenuOption;
				}
			}
			foreach (FloatMenuOption floatMenuOption2 in base.CompFloatMenuOptions(myPawn))
			{
				yield return floatMenuOption2;
			}
		}
		public FloatMenuOption CommFloatMenuOption(ICommunicable commTarget, Pawn negotiator)
        {
			if(commTarget is Faction faction)
			{
				if (faction.IsPlayer)
				{
					return null;
				}
				string text = "CallOnRadio".Translate(faction.GetCallLabel());
				text = text + " (" + faction.PlayerRelationKind.GetLabelCap() + ", " + faction.PlayerGoodwill.ToStringWithSign() + ")";
				if (!LeaderIsAvailableToTalk(faction))
				{
					string text2 = ((faction.leader == null) ? ((string)"LeaderUnavailableNoLeader".Translate()) : ((string)"LeaderUnavailable".Translate(faction.leader.LabelShort, faction.leader)));
					return new FloatMenuOption(text + " (" + text2 + ")", null, faction.def.FactionIcon, faction.Color);
				}
				return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(text, delegate
				{
					this.GiveUseCommsJob(negotiator, faction);
				}, faction.def.FactionIcon, faction.Color, MenuOptionPriority.InitiateSocial), negotiator, parent);
			}
			else if(commTarget is PassingShip ship)
            {
				string label = "CallOnRadio".Translate(ship.GetCallLabel());
				Action action = null;
				AcceptanceReport canCommunicate = CanCommunicateWith(negotiator, ship);
				if (!canCommunicate.Accepted)
				{
					if (!canCommunicate.Reason.NullOrEmpty())
					{
						action = delegate
						{
							Messages.Message(canCommunicate.Reason, parent, MessageTypeDefOf.RejectInput, historical: false);
						};
					}
				}
				else
				{
					action = delegate
					{
						if (!Building_OrbitalTradeBeacon.AllPowered(parent.Map).Any())
						{
							Messages.Message("MessageNeedBeaconToTradeWithShip".Translate(), parent, MessageTypeDefOf.RejectInput, historical: false);
						}
						else
						{
							GiveUseCommsJob(negotiator, ship);
						}
					};
				}
				return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(label, action, MenuOptionPriority.InitiateSocial), negotiator, parent);
			}
            else
            {
				return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(commTarget.GetCallLabel(), delegate
				{
					GiveUseCommsJob(negotiator, commTarget);
				}, MenuOptionPriority.InitiateSocial), negotiator, (LocalTargetInfo)parent, "ReservedBy");
            }
		}
		public static bool LeaderIsAvailableToTalk(Faction faction)
		{
			if (faction.leader == null)
			{
				return false;
			}
			if (faction.leader.Spawned && (faction.leader.Downed || faction.leader.IsPrisoner || !faction.leader.Awake() || faction.leader.InMentalState))
			{
				return false;
			}
			return true;
		}

		public static AcceptanceReport CanCommunicateWith(Pawn negotiator, PassingShip ship)
		{
			if(ship is TradeShip trader)
            {
				return negotiator.CanTradeWith(trader.Faction, trader.TraderKind).Accepted;
            }
			return AcceptanceReport.WasAccepted;
		}
		public void GiveUseCommsJob(Pawn negotiator, ICommunicable target)
		{
			Job job = JobMaker.MakeJob(HorizonJobDefOf.UseCommsConsoleItem, parent);
			job.commTarget = target;
			negotiator.jobs.TryTakeOrderedJob(job, JobTag.Misc);
			PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.OpeningComms, KnowledgeAmount.Total);
		}
	}
    [DefOf]
    public static class HorizonJobDefOf
    {
        public static JobDef UseCommsConsoleItem;

        static HorizonJobDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(HorizonJobDefOf));
        }
    }

	public class JobDriver_UseCommsConsoleItem : JobDriver
	{
		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDespawnedOrNull(TargetIndex.A);
			yield return Toils_Goto.GotoCell(TargetIndex.A, PathEndMode.InteractionCell).FailOn((Toil to) => !(to.actor.jobs.curJob.GetTarget(TargetIndex.A).Thing.TryGetComp<Comp_CommsTower>()).CanUseCommsNow);
			Toil openComms = ToilMaker.MakeToil("MakeNewToils");
			openComms.initAction = delegate
			{
				Pawn actor = openComms.actor;
				if ((actor.jobs.curJob.GetTarget(TargetIndex.A).Thing.TryGetComp<Comp_CommsTower>()).CanUseCommsNow)
				{
					actor.jobs.curJob.commTarget.TryOpenComms(actor);
				}
			};
			yield return openComms;
		}
	}

	[HarmonyPatch(typeof(CommsConsoleUtility), "PlayerHasPoweredCommsConsole", new Type[] {typeof(Map) })]
	public static class CommsConsoleUtility_PlayerHasPoweredCommsConsole_Patch
	{
		public static void Postfix(ref bool __result, Map map)
		{
			IEnumerable<Pawn> item = (IEnumerable<Pawn>)(from t in map.listerThings.ThingsInGroup(ThingRequestGroup.Pawn)
														 where t is Pawn x && x.TryGetComp<Comp_CommsTower>() != null select t);
			foreach (Pawn t in item)
			{
				if (t.Faction == Faction.OfPlayer)
				{
					CompPowerTrader compPowerTrader = t.TryGetComp<CompPowerTrader>();
					if (compPowerTrader == null || compPowerTrader.PowerOn)
					{
						__result=true;
						return;
					}
				}
			}
			return;
		}
	}
}
