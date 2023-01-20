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
using RimWorld.Planet;
using System.Reflection;
using System.Reflection.Emit;

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
	[HarmonyPatch(typeof(StatWorker), "GetValueUnfinalized")]
	public static class StatWorker_GetValueUnfinalized_Patch
	{
		public static void Postfix(ref float __result, StatRequest req, StatDef ___stat)
		{
			Pawn pawn = req.Thing as Pawn;
			if (pawn != null)
			{
				if (pawn.equipment != null && pawn.equipment.bondedWeapon != null && pawn.equipment.bondedWeapon is MechSuit mech && mech.ContainedThing == pawn)
				{
					//Log.Message("test mech offset pawn");
					__result += StatWorker.StatOffsetFromGear(pawn.equipment.bondedWeapon, ___stat);
				}
				if (pawn is MechSuit mech2 && mech2.HasAnyContents)
				{
					//Log.Message("test mech offset");

					__result += StatWorker.StatOffsetFromGear(pawn, ___stat);
				}
			}
		}
	}
	[HarmonyPatch(typeof(CompBiocodable), "CodeFor")]
	public static class CompBiocodable_CodeFor_Patch
	{
		public static bool Prefix(Pawn p)
		{
			if (p is MechSuit)
			{
				return false;
			}
			return true;
		}
	}
	[HarmonyPatch(typeof(WeaponTraitWorker), "Notify_KilledPawn")]
	public static class WeaponTraitWorker_Notify_KilledPawn_Patch
	{
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) =>
            codes.MethodReplacer(AccessTools.PropertyGetter(typeof(Pawn_EquipmentTracker), "Primary"), AccessTools.Method(typeof(WeaponTraitWorker_Notify_KilledPawn_Patch), "bondedEquipment"));
		static Thing bondedEquipment(Pawn_EquipmentTracker equipment) => equipment.bondedWeapon;

	}
	[HarmonyPatch(typeof(ThingWithComps), "Notify_UsedWeapon")]
	public static class ThingWithComps_Notify_UsedWeapon_Patch
    {
		public static void Postfix(ThingWithComps __instance, Pawn pawn)
        {
			if (pawn is MechSuit mech && mech.ContainedThing is Pawn pawn2)
            {
				//Log.Message("test used weapon pawn");
				__instance.Notify_UsedWeapon(pawn2);
				//pawn.Notify_UsedWeapon(pawn2);

			}
		}
    }
	[HarmonyPatch(typeof(Thought_WeaponTraitNotEquipped), "ShouldDiscard", MethodType.Getter)]
	public static class Thought_WeaponTraitNotEquipped_ShouldDiscard_Patch
    {
		public static void Postfix(ref bool __result, Thought_WeaponTraitNotEquipped __instance)
        {
            if (!__result)
            {
				__result = __instance.pawn.equipment.Contains(__instance.weapon);
				if (__instance.weapon is MechSuit Mech)
				{
					__result = true;
					if (__instance.pawn.ParentHolder != null && __instance.pawn.ParentHolder is MechSuit newMech)
					{
						__result = Mech == newMech;
					}
				}
			}
		}
    }
	[HarmonyPatch(typeof(Pawn_EquipmentTracker), "Notify_EquipmentAdded")]
	public static class Pawn_EquipmentTracker_Notify_EquipmentAdded_Patch
    {
		public static void Postfix(ThingWithComps eq, Thing ___bondedWeapon)
        {
			if (ModsConfig.RoyaltyActive && eq.def.equipmentType == EquipmentType.None && ___bondedWeapon != null && !___bondedWeapon.Destroyed)
			{
				___bondedWeapon.TryGetComp<CompBladelinkWeapon>()?.Notify_WieldedOtherWeapon();
			}
		}
    }

    [HarmonyPatch(typeof(ThoughtUtility), "CanGetThought")]
    public static class ThoughtUtility_CanGetThought_Patch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            MethodBase from = AccessTools.PropertyGetter(typeof(Thing), "Spawned");
            MethodBase to = AccessTools.Method(typeof(ThoughtUtility_CanGetThought_Patch), "IsInMech");
            foreach (CodeInstruction instruction in codes)
            {
                if (instruction.operand as MethodBase == from)
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Call, to);
                    continue;
                }
                yield return instruction;
            }
        }
        public static bool IsInMech(Pawn pawn, ThoughtDef def)
        {
            if (def.workerClass == typeof(ThoughtWorker_WeaponTraitBonded))
            {
                return ThingOwnerUtility.SpawnedOrAnyParentSpawned(pawn);
            }
            return pawn.Spawned;
        }
    }
    [HarmonyPatch(typeof(Pawn), "DoKillSideEffects")]
	public static class Pawn_DoKillSideEffects_Patch
	{
		public static void Postfix(Pawn __instance, DamageInfo? dinfo)
		{
			if (dinfo.HasValue && dinfo.Value.Instigator != null && dinfo.Value.Instigator is MechSuit mech && mech.ContainedThing is Pawn pawn)
			{
				RecordsUtility.Notify_PawnKilled(__instance, pawn);
				mech.Notify_KilledPawn(pawn);
				if (pawn.equipment != null)
				{
					pawn.equipment.Notify_KilledPawn();
				}
				if (__instance.RaceProps.Humanlike)
				{
					pawn.needs?.TryGetNeed<Need_KillThirst>()?.Notify_KilledPawn(dinfo);
				}
				if (pawn.health.hediffSet != null)
				{
					for (int i = 0; i < pawn.health.hediffSet.hediffs.Count; i++)
					{
						pawn.health.hediffSet.hediffs[i].Notify_KilledPawn(pawn, dinfo);
					}
				}
				if (HistoryEventUtility.IsKillingInnocentAnimal(pawn, __instance))
				{
					Find.HistoryEventsManager.RecordEvent(new HistoryEvent(HistoryEventDefOf.KilledInnocentAnimal, pawn.Named(HistoryEventArgsNames.Doer), __instance.Named(HistoryEventArgsNames.Victim)));
				}
				//if (spawned)
				//{
				//	Find.BattleLog.Add(new BattleLogEntry_StateTransition(__instance, __instance.RaceProps.DeathActionWorker.DeathRules, dinfo.HasValue ? (pawn) : null, exactCulprit, dinfo.HasValue ? dinfo.Value.HitPart : null));
				//}
			}
		}
	}

	[HarmonyPatch(typeof(GameEnder),"IsPlayerControlledWithFreeColonist")]
	public static class GameEnder_IsPlayerControlledWithFreeColonist_Patch
    {
		public static void Postfix(ref bool __result, Caravan caravan)
        {
			if (!caravan.IsPlayerControlled)
			{
				return;
			}
			List<Pawn> pawnsListForReading = caravan.PawnsListForReading;
			for (int i = 0; i < pawnsListForReading.Count; i++)
			{
				Pawn pawn = pawnsListForReading[i];
				if(pawn.IsColonyMech && pawn is MechSuit suit && suit.ContainedThing is Pawn pawn2 && pawn2.IsColonist && pawn2.HostFaction == null)
                {
					__result = true;
					return;
                }
			}
			return;
		}
    }
	[HarmonyPatch(typeof(MapPawns), "PlayerEjectablePodHolder")]
	public static class MapPawns_PlayerEjectablePodHolder_Patch
	{
		public static void postfix(ref IThingHolder __result, Thing thing, bool includeCryptosleepCaskets)
		{
			MechSuit suit = thing as MechSuit;
			if (includeCryptosleepCaskets && suit != null)
			{
				object obj = thing as IThingHolder;
				__result = (IThingHolder)obj;
			}
		}
	}
	[HarmonyPatch(typeof(Pawn), "GenerateNecessaryName")]
	public static class Pawn_GenerateNecessaryName_Patch
    {
		public static bool Prefix(Pawn __instance)
		{
			if (__instance.Name == null && __instance.Faction == Faction.OfPlayer && (__instance.RaceProps.Animal || (ModsConfig.BiotechActive && __instance.RaceProps.IsMechanoid)))
			{
				CompGeneratedNames compGeneratedNames = __instance.TryGetComp<CompGeneratedNames>();
				if (compGeneratedNames != null)
				{
					string tempstring = compGeneratedNames.TransformLabel(__instance.KindLabel);
					__instance.Name = new NameSingle(tempstring);
					return false;
				}
			}
			return true;
		}
	}
	//[HarmonyPatch(typeof(Pawn), "Name", MethodType.Setter)]
	//public static class Pawn_Name_Patch
 //   {
	//	public static void Postfix(Pawn __instance)
 //       {
	//		CompGeneratedNames compGeneratedNames = __instance.TryGetComp<CompGeneratedNames>();
	//		if(compGeneratedNames != null)
 //           {
	//			compGeneratedNames.Name
 //           }
	//	}
 //   }


	//[HarmonyPatch(typeof(CaravanUtility), "IsOwner")]
	//public static class RimWorld_Planet_CaravanUtility_Patch
	//   {
	//	public static void Postfix(ref bool __result, Pawn pawn, Faction caravanFaction)
	//	{
	//		if (caravanFaction == null)
	//		{
	//			return;
	//		}
	//		if (pawn is MechSuit suit && suit.HasAnyContents && suit.ContainedThing is Pawn pawn2)
	//		{
	//			__result = CaravanUtility.IsOwner(pawn2, caravanFaction);
	//		}
	//	}

	//}
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
			if (base.Faction == Faction.OfPlayer && innerContainer.Count > 0 && CanEject(Faction.OfPlayer))
			{
				Command_Action command_Action = new Command_Action();
				command_Action.action = EjectContents;
				command_Action.defaultLabel = "CommandPodEject".Translate(); //cryptosleep reference, needs change
				command_Action.defaultDesc = "CommandPodEjectDesc".Translate();//cryptosleep reference, needs change
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

		public bool CanEject(Faction fac, StringBuilder reason = null)
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
			}
			return false;
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
				if(thing is Pawn pawn)
                {
					Notify_Equipped(this);
					Notify_Equipped(pawn);
					if (ModsConfig.RoyaltyActive && pawn.equipment.bondedWeapon != null && !pawn.equipment.bondedWeapon.Destroyed)
					{
						pawn.equipment.bondedWeapon.TryGetComp<CompBladelinkWeapon>()?.Notify_WieldedOtherWeapon();
					}
				}
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
		public override void Notify_AbandonedAtTile(int tile)
        {
			base.Notify_AbandonedAtTile(tile);
			if (ContainedThing != null && ContainedThing is Pawn pawn)
            {
				pawn.Notify_AbandonedAtTile(tile);
            }
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
			Caravan caravan= this.GetCaravan();
			if (base.Spawned)
            {
				if (ContainedThing is Pawn pawn)
				{
					Notify_Unequipped(pawn);
					Notify_Unequipped(this);
					if (ModsConfig.RoyaltyActive)
					{
						this.TryGetComp<CompBladelinkWeapon>()?.Notify_EquipmentLost(pawn);
						this.TryGetComp<CompBladelinkWeapon>()?.Notify_EquipmentLost(this);
					}
				}
				innerContainer.TryDropAll(InteractionCell, base.Map, ThingPlaceMode.Near);
				drafter.Drafted = false;
				contentsKnown = true;
				return;
            }
			if (caravan != null)
            {
				foreach(Thing thing in innerContainer)
                {
					caravan.AddPawnOrItem(thing, true);
					innerContainer.Remove(thing);
					if (thing is Pawn pawn)
                    {
						Notify_Unequipped(pawn);
						Notify_Unequipped(this);
						if (ModsConfig.RoyaltyActive)
						{
							this.TryGetComp<CompBladelinkWeapon>()?.Notify_EquipmentLost(pawn);
							this.TryGetComp<CompBladelinkWeapon>()?.Notify_EquipmentLost(this);
						}
                    }
				}
				drafter.Drafted = false;
				contentsKnown = true;
				return;
			}
			Log.Error(this.ToStringSafe() + "tried to eject contents while not in a caravan or on a map");

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
				yield return new FloatMenuOption("CannotUseReason".Translate("MechSuitGuestsNotAllowed".Translate()), null);
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
			if (!EquipmentUtility.CanEquip(this, myPawn, out string cantReason))
			{
				yield return new FloatMenuOption(cantReason, null);
				yield break;
            }
			JobDef jobDef = JobMechDefOf.EnterMechSuit;
			string label = "EnterMechSuit".Translate();
			Action action = delegate
			{
				if (ModsConfig.BiotechActive && this.IsColonyMech)
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
