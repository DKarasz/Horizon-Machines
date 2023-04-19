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
using static Verse.DamageWorker;

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
			if (thing is MechSuit suit)
			{
				__result = suit;
			}
		}
	}
	[HarmonyPatch(typeof(Pawn), "GetDirectlyHeldThings")]
	public static class Pawn_GetDirectlyHeldThings_Patch
	{
		public static void Postfix(ref ThingOwner __result, Pawn __instance)
		{
			if (__instance is MechSuit mech)
			{
				__result = mech.innerContainer;
			}
		}
	}
	//[HarmonyPatch(typeof(Caravan), "AllOwnersDowned", MethodType.Getter)]
	//public static class Caravan_AllOwnersDowned_Patch
 //   {
	//	public static void prefix(Caravan __instance, ThingOwner<Pawn> ___pawns)
 //       {
	//		for (int i = 0; i < ___pawns.Count; i++)
	//		{
	//			if (__instance.IsOwner(___pawns[i]) && ___pawns[i].RaceProps.IsMechanoid)
	//			{
	//				Log.Message("mech owner check");
	//			}
	//		}
	//	}
 //   }
	//[HarmonyPatch(typeof(Caravan), "IsOwner")]
	//public static class Caravan_IsOwner_Patch
	//{
	//	public static void prefix(Pawn p, ThingOwner<Pawn> ___pawns)
	//	{
 //           if (p.RaceProps.IsMechanoid)
 //           {
	//			Log.Message("is mech");
	//			if (!___pawns.Contains(p))
 //               {
	//				Log.Message("not in pawns");
 //               }
 //           }
	//		Log.Message("is not mech");
	//	}
	//}
	[HarmonyPatch(typeof(Caravan),"RemovePawn")]
	public static class Caravan_RemovePawn_Patch
    {
		public static void Prefix(Pawn p)
        {
			if (p is MechSuit mech)
            {
				mech.EjectContents();
            }
        }
    }
	//[HarmonyPatch(typeof(Caravan), "RemoveAllPawns")]
	//public class Caravan_RemovePawn_Patch
	//{
	//	public static void Prefix(Pawn p)
	//	{
	//		if (p is MechSuit mech)
	//		{
	//			mech.EjectContents();
	//		}
	//	}
	//}

	[HarmonyPatch(typeof(Pawn), "ExitMap")]
	public static class Pawn_ExitMap_Patch
    {
		public static void Prefix(Pawn __instance)
        {
			if (__instance is MechSuit mech && !mech.IsColonyMech)
            {
				mech.EjectContents(true);
            }
        }
    }

	[HarmonyPatch(typeof(KidnappedPawnsTracker), "Kidnap")]
	public static class KidnappedPawnsTracker_Kidnap_Patch
    {
		public static void Prefix(Pawn pawn, Pawn kidnapper)
        {
			if (pawn is MechSuit mech)
            {
				foreach (Thing thing in mech.innerContainer)
                {
					if (thing is Pawn p)
                    {
						mech.innerContainer.Remove(pawn);
						if (p.Faction != null && p.Faction != kidnapper.Faction)
						{
							kidnapper.Faction.kidnapped.Kidnap(p, kidnapper);
						}
						else
						{
							Find.WorldPawns.PassToWorld(p);
						}
                    }
					else
					{
						mech.innerContainer.Remove(thing);
						thing.Notify_AbandonedAtTile(pawn.Tile);
					}
				}
            }
        }
    }



	[HarmonyPatch(typeof(DamageWorker_AddInjury), "FinalizeAndAddInjury", new Type[] { typeof(Pawn), typeof(float), typeof(DamageInfo), typeof(DamageResult) })]
	public static class DamageWorker_AddInjury_FinalizeAndAddInjury_Patch
    {
		public static void Postfix(DamageWorker_AddInjury __instance, float totalDamage, Pawn pawn, DamageInfo dinfo, DamageResult result)
        {
			if (pawn is MechSuit mech && mech.mechExtension.exposedPawn > 0 && result.headshot && mech.HasAnyContents)
            {
				//Log.Message("check if hit inside: " +dinfo.HitPart.ToString());
				float chance = mech.mechExtension.exposedPawn;
				foreach (Thing t in mech.innerContainer)
                {
					Pawn p = t as Pawn;
					if (p == null)
                    {
						continue;
                    }
                    if (Rand.Chance(chance))
                    {
						//Log.Message("hit");
						BattleLogEntry_RangedImpact log = new BattleLogEntry_RangedImpact(dinfo.Instigator, p, mech, dinfo.Weapon, null, null);
						DamageInfo dinfo2 = dinfo;
						dinfo2.SetHitPart(null);
						dinfo2.SetAmount(totalDamage);
						p.TakeDamage(dinfo2).AssociateWithLog(log);
                    }
				}
            }
        }
    }

	[HarmonyPatch(typeof(Pawn),"CurrentlyUsableForBills")]
	public static class Pawn_CurrentlyUsableForBills_Patch
    {
		public static bool Prefix(Pawn __instance, ref bool __result)
        {
            if (__instance.RaceProps.IsMechanoid)
            {
				if (__instance.Downed || __instance.IsSelfShutdown() || __instance.IsCharging())
                {
					__result = true;
					return false;
                }
            }
			return true;
        }
    }

    [HarmonyPatch(typeof(MechanitorUtility),"InMechanitorCommandRange")]
	public static class MechanitorUtility_InMechanitorCommandRange_Patch
    {
		public static void Postfix(ref bool __result, Pawn mech, LocalTargetInfo target)
		{
			if (mech is MechSuit mech1 && mech1.HasAnyContents && mech.CanCommandTo(target))
			{
				__result = true;
			}
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
                if (mech is MechSuit suit && !suit.mechExtension.canDraftRemote)
                {
					if (!suit.HasAnyContents)
					{
						mech.drafter.Drafted = false;
						__result = new AcceptanceReport("MechSuitUnoccupied".Translate());
						return;
                    }
                    else 
                    {
						Thing thing = suit.ContainedThing;
						if (thing is Corpse)
                        {
							mech.drafter.Drafted = false;
							__result = new AcceptanceReport("MechPilotDead".Translate());
							return;
                        }
                        Pawn pawn = thing as Pawn;
						if (pawn.Downed)
                        {
							mech.drafter.Drafted = false;
							__result = new AcceptanceReport("MechPilotDown".Translate());
							return;
						}
						else if (pawn.Dead)
						{
							mech.drafter.Drafted = false;
							__result = new AcceptanceReport("MechPilotDead".Translate());
							return;
						}
						else if (pawn.InMentalState)
						{
							mech.drafter.Drafted = false;
							__result = new AcceptanceReport("MechPilotMentalState".Translate());
							return;
						}
					}
                }

            }
        }
    }

	public class MechExtension: DefModExtension
    {
		public bool canDraftRemote = true;
		public bool needsPilot = false;
		public bool canBleed = false;
		public bool overseerOnly = false;
		public bool mechOffsetsPawn = true;// set to false if using dynamic offsets to prevent recursion, set to true if you want the pawn to have stat offsets from the mech (ie bladelink)
		public int pilotNumber = 1;
		public bool isViolent = false;
		public bool renderPawn = false;
		public GraphicData graphicData = new GraphicData();//only use for offsets of pawn
		public float exposedPawn = 0f;
		//public int mechConnections = 0;
	}
    //patch mechanitor utility, get mech gizmos: no need
    public class MechSuit: Pawn, IThingHolderWithDrawnPawn, IOpenable
    {
        public ThingOwner innerContainer;

		protected bool contentsKnown;

		public string openedSignal;
		public MechExtension mechExtension => def.GetModExtension<MechExtension>() ?? new MechExtension();
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
		public bool HasAnyCoPilot => innerContainer.Count > 1;

		public Thing ContainedCoPilot
		{
			get
			{
				if (HasAnyCoPilot)
				{
					return innerContainer[1];
				}
				return null;
			}
		}
		public virtual bool CanOpen => HasAnyContents;

		public float HeldPawnDrawPos_Y => 0f;

        public float HeldPawnBodyAngle => this.Drawer.renderer.BodyAngle();

        public PawnPosture HeldPawnPosture => this.GetPosture();

        public MechSuit()
		{
			innerContainer = new ThingOwner<Thing>(this, oneStackOnly: false);
		}

		//public new ThingOwner GetDirectlyHeldThings()
		//{
		//	return innerContainer;
		//}

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
		public override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
			base.DrawAt(drawLoc, flip);
			if (mechExtension.renderPawn && HasAnyContents)
            {
				Vector3 pilotdraw = new Vector3();
				pilotdraw = drawLoc;
				pilotdraw += this.mechExtension.graphicData.DrawOffsetForRot(Rotation).RotatedBy(this.Drawer.renderer.BodyAngle());
				((Pawn)ContainedThing).Drawer.renderer.RenderPawnAt(pilotdraw, Rotation, true);
            }
        }

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (Gizmo gizmo2 in base.GetGizmos())
			{
				yield return gizmo2;
			}
			Gizmo gizmo;
			foreach(Thing thing1 in innerContainer)
            {
				if ((gizmo = Building.SelectContainedItemGizmo(this, thing1)) != null)
				{
					yield return gizmo;
				}
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
				command_Action.defaultLabel = "CommandPodEjectAll".Translate(); //cryptosleep reference, needs change
				command_Action.defaultDesc = "CommandPodEjectDesc".Translate();//cryptosleep reference, needs change
				if (innerContainer.Count == 0)
				{
					command_Action.Disable("CommandPodEjectFailEmpty".Translate());
				}
				command_Action.hotKey = KeyBindingDefOf.Misc8;
				command_Action.icon = ContentFinder<Texture2D>.Get("UI/Commands/PodEject");
				yield return command_Action;
                if (innerContainer.Count > 1)
                {
					Command_Action command_Action1 = new Command_Action();
					command_Action1.action = delegate
					{
						List<FloatMenuOption> list = new List<FloatMenuOption>();
						List<Thing> freeColonists = innerContainer.ToList();
						for (int i = 0; i < freeColonists.Count; i++)
						{
							Pawn localPawn = freeColonists[i] as Pawn;
							if (localPawn != null)
							{
								list.Add(new FloatMenuOption(localPawn.LabelShortCap, delegate
								{
									EjectContentsThing(localPawn);
								}));
							}
						}
						Find.WindowStack.Add(new FloatMenu(list));
					};
					command_Action1.defaultLabel = "CommandPodEjectOne".Translate(); //cryptosleep reference, needs change
					command_Action1.defaultDesc = "CommandPodEjectDesc".Translate();//cryptosleep reference, needs change
					if (innerContainer.Count == 0)
					{
						command_Action1.Disable("CommandPodEjectFailEmpty".Translate());
					}
					command_Action1.icon = ContentFinder<Texture2D>.Get("UI/Commands/PodEject");
					yield return command_Action1;
                }
			}
		}
		//public new void GetChildHolders(List<IThingHolder> outChildren)
		//{
		//	ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, base.GetDirectlyHeldThings());
		//	ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, innerContainer);
		//	if (inventory != null)
		//	{
		//		outChildren.Add(inventory);
		//	}
		//	if (carryTracker != null)
		//	{
		//		outChildren.Add(carryTracker);
		//	}
		//	if (equipment != null)
		//	{
		//		outChildren.Add(equipment);
		//	}
		//	if (apparel != null)
		//	{
		//		outChildren.Add(apparel);
		//	}
		//}
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
			bool flag2 = false;
			if (HasAnyContents)
            {
				flag2 = true;
            }
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
					if (!flag2)
                    {
						Notify_Equipped(this);
                    }
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
			foreach(Thing thing in innerContainer)
            {
				if (thing is Pawn pawn)
                {
					pawn.Notify_AbandonedAtTile(tile);
                }
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
			EjectContents(false);
        }
		public virtual void EjectContents(bool kidnap = false)
		{
			if (!HasAnyContents)
            {
				return;
            }
			if (!base.Destroyed)
			{
				SoundDefOf.CryptosleepCasket_Eject.PlayOneShot(SoundInfo.InMap(new TargetInfo(base.Position, base.Map)));
			}
			Caravan caravan= this.GetCaravan();
			Notify_Unequipped(this);
            if (kidnap)
            {
				foreach (Thing thing in innerContainer)
				{
					if (thing is Pawn pawn)
					{
						Notify_Unequipped(pawn);
						if (ModsConfig.RoyaltyActive)
						{
							this.TryGetComp<CompBladelinkWeapon>()?.Notify_EquipmentLost(pawn);
							this.TryGetComp<CompBladelinkWeapon>()?.Notify_EquipmentLost(this);
						}
						innerContainer.Remove(pawn);
						if (base.Faction != null && base.Faction != pawn.Faction)
						{
							base.Faction.kidnapped.Kidnap(pawn, this);
                        }
                        else
                        {
							Find.WorldPawns.PassToWorld(pawn);
                        }
                    }
                    else
                    {
						innerContainer.Remove(thing);
						thing.Notify_AbandonedAtTile(base.Tile);
					}					
				}
				//drafter.Drafted = false;
				contentsKnown = true;
				return;
			}
			if (base.Spawned && caravan == null)
            {
				foreach (Thing thing in innerContainer)
				{
					if (thing is Pawn pawn)
					{
						Notify_Unequipped(pawn);
						if (ModsConfig.RoyaltyActive)
						{
							this.TryGetComp<CompBladelinkWeapon>()?.Notify_EquipmentLost(pawn);
							this.TryGetComp<CompBladelinkWeapon>()?.Notify_EquipmentLost(this);
						}
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
					innerContainer.Remove(thing);
					caravan.AddPawnOrItem(thing, addCarriedPawnToWorldPawnsIfAny: true);
					if (thing is Pawn pawn)
                    {
						Find.WorldPawns.PassToWorld(pawn);
						//Log.Message("test");
						Notify_Unequipped(pawn);
						//Log.Message("test0");

						if (ModsConfig.RoyaltyActive)
						{
							this.TryGetComp<CompBladelinkWeapon>()?.Notify_EquipmentLost(pawn);
							this.TryGetComp<CompBladelinkWeapon>()?.Notify_EquipmentLost(this);
						}
                    }
				}
				Log.Message("test1");
				//drafter.Drafted = false;
				Log.Message("test2");
				contentsKnown = true;
				Log.Message("test3");
				return;
			}
			Log.Error(this.ToStringSafe() + "tried to eject contents while not in a caravan or on a map");

		}
		public virtual void EjectContentsThing(Thing thing, bool kidnap=false)
		{
			if (!base.Destroyed)
			{
				SoundDefOf.CryptosleepCasket_Eject.PlayOneShot(SoundInfo.InMap(new TargetInfo(base.Position, base.Map)));
			}
			if (kidnap)
			{
				if (thing is Pawn pawn)
				{
					Notify_Unequipped(pawn);
					if (ModsConfig.RoyaltyActive)
					{
						this.TryGetComp<CompBladelinkWeapon>()?.Notify_EquipmentLost(pawn);
						this.TryGetComp<CompBladelinkWeapon>()?.Notify_EquipmentLost(this);
					}
					innerContainer.Remove(pawn);
					if (base.Faction != null && base.Faction != pawn.Faction)
					{
						base.Faction.kidnapped.Kidnap(pawn, this);
                    }
                    else
                    {
						Find.WorldPawns.PassToWorld(pawn);
					}
				}
				else
				{
					innerContainer.Remove(thing);
					thing.Notify_AbandonedAtTile(base.Tile);
				}
				//drafter.Drafted = false;
				contentsKnown = true;
				return;
			}
			Caravan caravan = this.GetCaravan();
			if (base.Spawned && caravan == null)
			{
                if (innerContainer.Contains(thing))
				{
					if (thing is Pawn pawn)
					{
						Notify_Unequipped(pawn);
						if (ModsConfig.RoyaltyActive)
						{
							this.TryGetComp<CompBladelinkWeapon>()?.Notify_EquipmentLost(pawn);
							this.TryGetComp<CompBladelinkWeapon>()?.Notify_EquipmentLost(this);
						}
					}
				}
				Thing thing1 = new Thing();
				innerContainer.TryDrop(thing, InteractionCell, base.Map, ThingPlaceMode.Near, out thing1);
				drafter.Drafted = false;
				contentsKnown = true;
				if (!HasAnyContents)
                {
					Notify_Unequipped(this);
				}
				return;
			}
			if (caravan != null)
			{
				if (innerContainer.Contains(thing))
				{
					innerContainer.Remove(thing);
					caravan.AddPawnOrItem(thing, addCarriedPawnToWorldPawnsIfAny: true);
					if (thing is Pawn pawn)
					{
						Find.WorldPawns.PassToWorld(pawn);
						Notify_Unequipped(pawn);
						if (ModsConfig.RoyaltyActive)
						{
							this.TryGetComp<CompBladelinkWeapon>()?.Notify_EquipmentLost(pawn);
							this.TryGetComp<CompBladelinkWeapon>()?.Notify_EquipmentLost(this);
						}
                    }
				}
				//drafter.Drafted = false;
				contentsKnown = true;
				if (!HasAnyContents)
				{
					Notify_Unequipped(this);
				}
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
		public virtual bool OtherRestrictions(Pawn pawn, out string reason)//postfix or subclass for custom reasons
        {
			reason = null;
			return true;
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
			//if (myPawn.IsColonyMechPlayerControlled && this.HasAnyContents && mechExtension.mechConnections != 0)
   //         {
			//	if (!(myPawn is MechSuit mech) || mech.HasAnyContents)
   //             {
			//		if (innerContainer.Where(thing => thing is Pawn pawn1 && pawn1.IsColonyMechPlayerControlled).Count() < mechExtension.mechConnections)
			//		{
			//			JobDef jobDef1 = JobMechDefOf.EnterMechSuit;
			//			string label1 = "MechAttachMechSuit".Translate();
			//			Action action1 = delegate
			//			{
			//				if (ModsConfig.BiotechActive && this.IsColonyMech)
			//				{
			//					if (!(myPawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.PsychicBond) is Hediff_PsychicBond hediff_PsychicBond) || !ThoughtWorker_PsychicBondProximity.NearPsychicBondedPerson(myPawn, hediff_PsychicBond))
			//					{
			//						myPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(jobDef1, this), JobTag.Misc);
			//					}
			//					else
			//					{
			//						Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation("PsychicBondDistanceWillBeActive_Cryptosleep".Translate(myPawn.Named("PAWN"), ((Pawn)hediff_PsychicBond.target).Named("BOND")), delegate
			//						{
			//							myPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(jobDef1, this), JobTag.Misc);
			//						}, destructive: true));
			//					}
			//				}
			//				else
			//				{
			//					myPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(jobDef1, this), JobTag.Misc);
			//				}
			//			};
			//			yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(label1, action1), myPawn, this);
			//		}
			//	}
   //         }
			if (innerContainer.Count >= mechExtension.pilotNumber)
			{
				yield return new FloatMenuOption("MechOccupied".Translate(), null);
				yield break;
			}
			if (!myPawn.CanReach(this, PathEndMode.InteractionCell, Danger.Deadly))
			{
				yield return new FloatMenuOption("CannotUseNoPath".Translate(), null);
				yield break;
			}
			Pawn overseer =this.GetOverseer();
			if (this.mechExtension.overseerOnly && (overseer==null || myPawn != overseer))
            {
				yield return new FloatMenuOption("OverseerOnly".Translate(), null);
				yield break;
			}
			if (!EquipmentUtility.CanEquip(this, myPawn, out string cantReason))
			{
				yield return new FloatMenuOption(cantReason, null);
				yield break;
            }
			if (this.mechExtension.isViolent && myPawn.WorkTagIsDisabled(WorkTags.Violent))
            {
				yield return new FloatMenuOption("IsIncapableOfViolence".Translate(myPawn.LabelShort, myPawn), null);
				yield break;
			}
			if (!this.OtherRestrictions(myPawn, out string cantReason1))
            {
				yield return new FloatMenuOption(cantReason1, null);
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
		public static JobDef IngestProcess;
		public static JobDef ExtractResources;
	}

	public class ThinkNode_ConditionalPlayerNoPilotMechSuit : ThinkNode_Conditional
	{
		protected override bool Satisfied(Pawn pawn)
		{
			if (pawn is MechSuit suit && suit.mechExtension.needsPilot)
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
