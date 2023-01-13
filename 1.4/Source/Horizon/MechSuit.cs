using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;
using Verse.Sound;
using UnityEngine;

namespace Horizon
{
	[HarmonyPatch(typeof(Pawn_MechanitorTracker), "CanControlMechs", MethodType.Getter)]
	public static class Pawn_MechanitorTracker_CanControlMechs_Patch
    {
		public static void Postfix(ref AcceptanceReport __result, Pawn ___pawn)
        {
			if (!___pawn.Spawned)
			{
				Thing spawnedParentOrMe = ___pawn.SpawnedParentOrMe;
				if (spawnedParentOrMe is MechSuit)
				{
					__result = true;
				}
			}
		}
    }

	[HarmonyPatch(typeof(MechanitorUtility),"InMechanitorCommandRange")]
	public static class MechanitorUtility_InMechanitorCommandRange_Patch
    {
		public static void Postfix(ref bool __result, Pawn mech, LocalTargetInfo target)
		{
			Pawn overseer = mech.GetOverseer();
			if (overseer != null && !overseer.Spawned && mech is MechSuit)
			{
				if (mech == overseer.SpawnedParentOrMe)
				{
					__result = true;
					return;
				}
				if (mech.CanCommandTo(target))
				{
					__result = true;
					return;
				}
			}
		}
		public static bool CanCommandTo(this Pawn pawn, LocalTargetInfo target)
		{
			if (!target.Cell.InBounds(pawn.MapHeld))
			{
				return false;
			}
			return (float)pawn.Position.DistanceToSquared(target.Cell) < 620.01f;
		}
	}

    [HarmonyPatch(typeof(MechanitorUtility), "CanDraftMech")]
    public static class MechanitorUtility_CanDraftMech_Patch
    {
        public static void Postfix(ref AcceptanceReport __result, Pawn mech)
        {
            if (mech.IsColonyMech)
            {
                if (mech is MechSuit suit)
                {
					if (!suit.HasAnyContents)
					{
						__result = new AcceptanceReport("MechSuitUnoccupied".Translate());
					}
                }

            }
        }
    }

    //patch mechanitor utility, get mech gizmos: no need
    public class MechSuit: Pawn, IThingHolder, IOpenable
    {
        protected ThingOwner innerContainer;

		protected bool contentsKnown;

		public string openedSignal;

		public virtual int OpenTicks => 300;

		public bool HasAnyContents => innerContainer.Count > 0;

		public Thing ContainedThing
		{
			get
			{
				if (innerContainer.Count != 0)
				{
					return innerContainer[0];
				}
				return null;
			}
		}

		public virtual bool CanOpen => HasAnyContents;

		public MechSuit()
		{
			innerContainer = new ThingOwner<Thing>(this, oneStackOnly: false);
		}

		public new ThingOwner GetDirectlyHeldThings()
		{
			return innerContainer;
		}

		public override void TickRare()
		{
			base.TickRare();
			innerContainer.ThingOwnerTickRare();
		}

		public override void Tick()
		{
			base.Tick();
			innerContainer.ThingOwnerTick();
		}

		public virtual void Open()
		{
			if (HasAnyContents)
			{
				EjectContents();
				if (!openedSignal.NullOrEmpty())
				{
					Find.SignalManager.SendSignal(new Signal(openedSignal, this.Named("SUBJECT")));
				}
			}
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (Gizmo gizmo2 in base.GetGizmos())
			{
				yield return gizmo2;
			}
			Gizmo gizmo;
			if ((gizmo = Building.SelectContainedItemGizmo(this, ContainedThing)) != null)
			{
				yield return gizmo;
			}
			if (DebugSettings.ShowDevGizmos && CanOpen)
			{
				Command_Action command_Action = new Command_Action();
				command_Action.defaultLabel = "DEV: Open";
				command_Action.action = delegate
				{
					Open();
				};
				yield return command_Action;
			}
			if (base.Faction == Faction.OfPlayer && innerContainer.Count > 0 && def.thingClass== typeof(MechSuit))
			{
				Command_Action command_Action = new Command_Action();
				command_Action.action = EjectContents;
				command_Action.defaultLabel = "CommandPodEject".Translate();
				command_Action.defaultDesc = "CommandPodEjectDesc".Translate();
				if (innerContainer.Count == 0)
				{
					command_Action.Disable("CommandPodEjectFailEmpty".Translate());
				}
				command_Action.hotKey = KeyBindingDefOf.Misc8;
				command_Action.icon = ContentFinder<Texture2D>.Get("UI/Commands/PodEject");
				yield return command_Action;
			}
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
			Scribe_Values.Look(ref contentsKnown, "contentsKnown", defaultValue: false);
			Scribe_Values.Look(ref openedSignal, "openedSignal");
		}

		public override void SpawnSetup(Map map, bool respawningAfterLoad)
		{
			base.SpawnSetup(map, respawningAfterLoad);
			if (base.Faction != null && base.Faction.IsPlayer)
			{
				contentsKnown = true;
			}
		}

		public override bool ClaimableBy(Faction fac, StringBuilder reason = null)
		{
			if (innerContainer.Any)
			{
				for (int i = 0; i < innerContainer.Count; i++)
				{
					if (innerContainer[i].Faction == fac)
					{
						return true;
					}
				}
				return false;
			}
			return base.ClaimableBy(fac, reason);
		}

		public virtual bool Accepts(Thing thing)
		{
			return innerContainer.CanAcceptAnyOf(thing);
		}

		public virtual bool TryAcceptThing(Thing thing, bool allowSpecialEffects = true)
		{
			if (!Accepts(thing))
			{
				return false;
			}
			bool flag = false;
			if (thing.holdingOwner != null)
			{
				thing.holdingOwner.TryTransferToContainer(thing, innerContainer, thing.stackCount);
				flag = true;
			}
			else
			{
				flag = innerContainer.TryAdd(thing);
			}
			if (flag)
			{
				if (thing.Faction != null && thing.Faction.IsPlayer)
				{
					contentsKnown = true;
				}
				if (allowSpecialEffects)
				{
					SoundDefOf.CryptosleepCasket_Accept.PlayOneShot(new TargetInfo(base.Position, base.Map));
				}
				return true;
			}
			return false;
		}

		public override void Kill(DamageInfo? dinfo, Hediff exactCulprit = null)
		{
			if (innerContainer.Count > 0)
			{
				List<Pawn> list = new List<Pawn>();
				foreach (Thing item2 in (IEnumerable<Thing>)innerContainer)
				{
					if (item2 is Pawn item)
					{
						list.Add(item);
					}
				}
				foreach (Pawn item3 in list)
				{
					HealthUtility.DamageUntilDowned(item3);
				}
				EjectContents();
			}
			innerContainer.ClearAndDestroyContents();
			base.Kill(dinfo, exactCulprit);
		}
		public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
		{
			if (innerContainer.Count > 0 && (mode == DestroyMode.Deconstruct || mode == DestroyMode.KillFinalize))
			{
				if (mode != DestroyMode.Deconstruct)
				{
					List<Pawn> list = new List<Pawn>();
					foreach (Thing item2 in (IEnumerable<Thing>)innerContainer)
					{
						if (item2 is Pawn item)
						{
							list.Add(item);
						}
					}
					foreach (Pawn item3 in list)
					{
						HealthUtility.DamageUntilDead(item3);
					}
				}
				EjectContents();
			}
			innerContainer.ClearAndDestroyContents();
			base.Destroy(mode);
		}

		public virtual void EjectContents()
		{
			if (!base.Destroyed)
			{
				SoundDefOf.CryptosleepCasket_Eject.PlayOneShot(SoundInfo.InMap(new TargetInfo(base.Position, base.Map)));
			}
			innerContainer.TryDropAll(InteractionCell, base.Map, ThingPlaceMode.Near);
			drafter.Drafted = false;
			contentsKnown = true;
		}
		public static MechSuit FindMechSuitFor(Pawn p, Pawn traveler, bool ignoreOtherReservations = false)
		{
			foreach (ThingDef item in DefDatabase<ThingDef>.AllDefs.Where((ThingDef def) => def.thingClass== typeof(MechSuit)))
			{
				MechSuit mechsuit = (MechSuit)GenClosest.ClosestThingReachable(p.PositionHeld, p.MapHeld, ThingRequest.ForDef(item), PathEndMode.InteractionCell, TraverseParms.For(traveler), 9999f, (Thing x) => !((MechSuit)x).HasAnyContents && traveler.CanReserve(x, 1, -1, null, ignoreOtherReservations));
				if (mechsuit != null)
				{
					return mechsuit;
				}
			}
			return null;
		}

		public override string GetInspectString()
		{
			string text = base.GetInspectString();
			string str = (contentsKnown ? innerContainer.ContentsString : ((string)"UnknownLower".Translate()));
			if (!text.NullOrEmpty())
			{
				text += "\n";
			}
			return text + ("CasketContains".Translate() + ": " + str.CapitalizeFirst());
		}
		public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn myPawn)
		{
			if (myPawn.IsQuestLodger())
			{
				yield return new FloatMenuOption("CannotUseReason".Translate("CryptosleepCasketGuestsNotAllowed".Translate()), null);
				yield break;
			}
			foreach (FloatMenuOption floatMenuOption in base.GetFloatMenuOptions(myPawn))
			{
				yield return floatMenuOption;
			}
			if (innerContainer.Count != 0)
			{
				yield break;
			}
			if (!myPawn.CanReach(this, PathEndMode.InteractionCell, Danger.Deadly))
			{
				yield return new FloatMenuOption("CannotUseNoPath".Translate(), null);
				yield break;
			}
			Pawn overseer =this.GetOverseer();
			if (this.def.devNote=="OverseerOnly" && (overseer==null || myPawn != overseer))
            {
				yield return new FloatMenuOption("OverseerOnly".Translate(), null);
				yield break;
			}
			JobDef jobDef = JobMechDefOf.EnterMechSuit;
			string label = "EnterCryptosleepCasket".Translate();
			Action action = delegate
			{
				if (ModsConfig.BiotechActive)
				{
					if (!(myPawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.PsychicBond) is Hediff_PsychicBond hediff_PsychicBond) || !ThoughtWorker_PsychicBondProximity.NearPsychicBondedPerson(myPawn, hediff_PsychicBond))
					{
						myPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(jobDef, this), JobTag.Misc);
					}
					else
					{
						Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("PsychicBondDistanceWillBeActive_Cryptosleep".Translate(myPawn.Named("PAWN"), ((Pawn)hediff_PsychicBond.target).Named("BOND")), delegate
						{
							myPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(jobDef, this), JobTag.Misc);
						}, destructive: true));
					}
				}
				else
				{
					myPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(jobDef, this), JobTag.Misc);
				}
			};
			yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(label, action), myPawn, this);
		}

	}
	[DefOf]
	public static class JobMechDefOf
	{
		public static JobDef EnterMechSuit;
	}

	public class ThinkNode_ConditionalPlayerNoPilotMechSuit : ThinkNode_Conditional
	{
		protected override bool Satisfied(Pawn pawn)
		{
			if (pawn is MechSuit suit)
            {
				return !suit.HasAnyContents && pawn.IsColonyMechPlayerControlled;
			}
			return false;
		}
	}


	public class JobDriver_EnterMechSuit : JobDriver
	{
		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return pawn.Reserve(Mech, job, 1, -1, null, errorOnFailed);
		}
		protected Pawn Mech => (Pawn)job.GetTarget(TargetIndex.A).Thing;
		protected virtual bool Remote => false;
		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDestroyedOrNull(TargetIndex.A);
			this.FailOnForbidden(TargetIndex.A);
			this.FailOn(() => Mech.IsAttacking());
			if (!Remote)
			{
				yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
			}
			Toil toil = (Remote ? Toils_General.Wait(500) : Toils_General.WaitWith(TargetIndex.A, 500, useProgressBar: true, maintainPosture: true, maintainSleep: true));
			//toil.WithProgressBarToilDelay(TargetIndex.A);
			yield return toil;
			Toil enter = ToilMaker.MakeToil("MakeNewToils");
			enter.initAction = delegate
			{
				Pawn actor = enter.actor;
				MechSuit pod = (MechSuit)actor.CurJob.targetA.Thing;
				Action action = delegate
				{
					bool flag = actor.DeSpawnOrDeselect();
					if (pod.TryAcceptThing(actor) && flag)
					{
						Find.Selector.Select(actor, playSound: false, forceDesignatorDeselect: false);
					}
				};
				if (!(pod.def.building?.isPlayerEjectable ?? true))
				{
					if (base.Map.mapPawns.FreeColonistsSpawnedOrInPlayerEjectablePodsCount <= 1)
					{
						Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("CasketWarning".Translate(actor.Named("PAWN")).AdjustedFor(actor), action));
					}
					else
					{
						action();
					}
				}
				else
				{
					action();
				}
			};
			toil.AddFinishAction(delegate
			{
				if (Mech.jobs?.curJob != null)
				{
					Mech.jobs.EndCurrentJob(JobCondition.InterruptForced);
				}
			});
			enter.defaultCompleteMode = ToilCompleteMode.Instant;
			yield return enter;
		}
	}

}
