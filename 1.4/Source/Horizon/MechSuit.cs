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

	[HarmonyPatch(typeof(StatWorker), "GetValueUnfinalized")]
	public static class StatWorker_GetValueUnfinalized_Patch
	{
		public static void Postfix(ref float __result, StatRequest req, StatDef ___stat)
		{
			Pawn pawn = req.Thing as Pawn;
			if (pawn != null)
			{
				if(!pawn.Spawned && pawn.ParentHolder != null && pawn.ParentHolder is MechSuit suit && suit.mechExtension.mechOffsetsPawn)
                {
					__result += StatWorker.StatOffsetFromGear(suit, ___stat);
				}
				//else if (pawn.equipment != null && pawn.equipment.bondedWeapon != null && pawn.equipment.bondedWeapon is MechSuit mech && mech.ContainedThing == pawn)
				//{
				//	//Log.Message("test mech offset pawn");
				//	__result += StatWorker.StatOffsetFromGear(pawn.equipment.bondedWeapon, ___stat);
				//}
				if (pawn is MechSuit mech2 && mech2.HasAnyContents)
				{
					//Log.Message("test mech offset");

					__result += StatWorker.StatOffsetFromGear(pawn, ___stat);
				}
			}
		}
	}
	[HarmonyPatch(typeof(StatWorker), "GetExplanationUnfinalized")]
	public static class StatWorker_GetExplanationUnfinalized_Patch
	{
		public static void Postfix(ref string __result, StatRequest req, StatDef ___stat, StatWorker __instance)
		{
			StringBuilder stringBuilder = new StringBuilder(__result);
			Pawn pawn = req.Thing as Pawn;
			if (pawn != null)
			{
				if (!pawn.Spawned && pawn.ParentHolder != null && pawn.ParentHolder is MechSuit suit && suit.mechExtension.mechOffsetsPawn)
				{
					float f = StatWorker.StatOffsetFromGear(suit, ___stat);
					if(f != 0)
                    {
					stringBuilder.AppendLine(InfoTextLineFromGear(suit, ___stat, f));
                    }
				}
				//else if (pawn.equipment != null && pawn.equipment.bondedWeapon != null && pawn.equipment.bondedWeapon is MechSuit mech && mech.ContainedThing == pawn)
				//{
				//	//Log.Message("test mech offset pawn");
				//	__result += StatWorker.StatOffsetFromGear(pawn.equipment.bondedWeapon, ___stat);
				//}
				if (pawn is MechSuit mech2 && mech2.HasAnyContents)
				{
					//Log.Message("test mech offset");
					float f = StatWorker.StatOffsetFromGear(pawn, ___stat);
					if (f != 0)
                    {
						stringBuilder.AppendLine(InfoTextLineFromGear(pawn, ___stat, f));
                    }
				}
				__result = stringBuilder.ToString();
			}
		}
		public static string InfoTextLineFromGear(Thing gear, StatDef stat, float f)
		{
			return "    " + gear.LabelCap + ": " + f.ToStringByStyle(stat.finalizeEquippedStatOffset ? stat.toStringStyle : stat.ToStringStyleUnfinalized, ToStringNumberSense.Offset);
		}

	}
	[HarmonyPatch(typeof (StatWorker),"StatOffsetFromGear")]
	public static class StatWorker_StatOffsetFromGear_Patch
    {
		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
			bool first = true;
			MethodBase to = AccessTools.Method(typeof(StatWorker_StatOffsetFromGear_Patch), "checkcomps");
			foreach (CodeInstruction instruction in codes)
			{
				if (instruction.opcode == OpCodes.Stloc_0 && first == true)
				{
					first = false;
					yield return new CodeInstruction(OpCodes.Ldarg_0);
					yield return new CodeInstruction(OpCodes.Ldarg_1);
					yield return new CodeInstruction(OpCodes.Call, to);
					yield return instruction;
					continue;
				}
				yield return instruction;
			}
		}
		public static float checkcomps(float num, Thing gear, StatDef stat)
        {
			ThingWithComps thing = gear as ThingWithComps;
			if(thing == null)
            {
				return num;
            }
			IEnumerable<CompGearStatOffsetBase> comps = from a in thing.AllComps where a is CompGearStatOffsetBase select (CompGearStatOffsetBase)a;
			foreach (CompGearStatOffsetBase comp in comps)
            {
				if (comp.Props.statDef == stat)
				{
					num += comp.GetGearStatOffset(gear);
				}
			}
			return num;
        }
    }
	[HarmonyPatch(typeof(StatWorker), "GearHasCompsThatAffectStat")]
	public static class StatWorker_GearHasCompsThatAffectStat_Patch
    {
		public static void Postfix(bool __result, Thing gear, StatDef stat)
        {
			ThingWithComps thing = gear as ThingWithComps;
			if (thing == null)
			{
				return;
			}
			IEnumerable<CompGearStatOffsetBase> comps = from a in thing.AllComps where a is CompGearStatOffsetBase select (CompGearStatOffsetBase)a;
			foreach (CompGearStatOffsetBase comp in comps)
			{
				if (comp.Props.statDef == stat)
				{
                    if (comp.GetGearStatOffset(gear) != 0)
                    {
						__result = true;
                    }
				}
			}
		}
	}
	[HarmonyPatch(typeof(StatWorker), "GearAffectsStat")]
	public static class StatWorker_GearAffectsStat_Patch
	{
		public static void Postfix(bool __result, ThingDef gearDef, StatDef stat)
		{
			if (gearDef == null)
			{
				return;
			}
			IEnumerable<CompProperties_StatOffsetBase> comps = from a in gearDef.comps where typeof(CompGearStatOffsetBase).AllSubclasses().Contains(a.compClass) select (CompProperties_StatOffsetBase)a;
			foreach (CompProperties_StatOffsetBase comp in comps)
			{
				if (comp.statDef == stat)
				{
					__result = true;
				}
			}
		}
	}
	public class CompGearStatOffsetBase : CompStatOffsetBase
    {
        public override IEnumerable<string> GetExplanation()
        {
			for (int i = 0; i < Props.offsets.Count; i++)
			{
				string explanation = Props.offsets[i].GetExplanation(parent);
				if (!explanation.NullOrEmpty())
				{
					yield return explanation;
				}
			}
		}
		public virtual float GetGearStatOffset(Thing thing = null)
        {
			Pawn pawn = (thing.ParentHolder as Pawn_EquipmentTracker)?.pawn ?? null;
			return GetStatOffset(pawn);
        }
        public override float GetStatOffset(Pawn pawn = null)
        {
			float num = 0f;
			for (int i = 0; i < Props.offsets.Count; i++)
			{
				if (Props.offsets[i].CanApply(parent, pawn))
				{
					num += Props.offsets[i].GetOffset(parent, pawn);
				}
			}
			return num;
		}
    }
	public class CompMechStatOffsetBase : CompGearStatOffsetBase
    {
        public override float GetGearStatOffset(Thing thing = null)
        {
			MechSuit mech = thing as MechSuit;
			if (mech != null && mech.HasAnyContents && mech.ContainedThing is Pawn pawn)
            {
				return GetStatOffset(pawn);
            }
			return 0;
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
				__result = __instance.pawn.equipment.Contains(__instance.weapon)|| __instance.pawn.apparel.Contains(__instance.weapon);
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
		//unused, example of how to rewrite a persona check to return the bonded weapon if the weapon is applicable
		//public static Thing checkBond(Pawn pawn)
  //      {
		//	if (pawn == null)
  //          {
		//		return null;
  //          }
		//	if(pawn is MechSuit mech)
  //          {
		//		return checkBond((Pawn)mech.ContainedThing);
  //          }
		//	Thing thing = pawn.equipment.bondedWeapon;
		//	if (thing == null)
  //          {
		//		return null;
  //          }
		//	if (thing is MechSuit Mech)
		//	{
		//		if (pawn.ParentHolder != null && pawn.ParentHolder is MechSuit newMech)
		//		{
		//			if (Mech == newMech)
		//				return thing;
		//		}
		//	}
  //          if (pawn.equipment.Contains(thing))
  //          {
		//		return thing;
  //          }
		//	return null;
		//}
	}


	[HarmonyPatch(typeof(Pawn_EquipmentTracker), "Notify_KilledPawn")]
	public static class Pawn_EquipmentTracker_Notify_KilledPawn
    {
		public static void Postfix(Thing ___bondedWeapon, Pawn ___pawn)
        {
			if (___bondedWeapon is ThingWithComps thing && ___pawn.apparel.Contains(thing))
            {
				thing.Notify_KilledPawn(___pawn);
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
    [HarmonyPatch(typeof(Pawn), "DoKillSideEffects")]//need to add hook into apparel kill side effects
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
			if (thing is MechSuit suit)
			{
				__result = suit;
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
    //{
    //    public static void Postfix(Pawn __instance)
    //    {
    //        CompGeneratedNames compGeneratedNames = __instance.TryGetComp<CompGeneratedNames>();
    //        if (compGeneratedNames != null)
    //        {
				//compGeneratedNames.AccessTools.FieldRef("name");
    //            compGeneratedNames.Name
    //        }
    //    }
    //}


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
		public new void GetChildHolders(List<IThingHolder> outChildren)
		{
			ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, base.GetDirectlyHeldThings());
			ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, innerContainer);
			if (inventory != null)
			{
				outChildren.Add(inventory);
			}
			if (carryTracker != null)
			{
				outChildren.Add(carryTracker);
			}
			if (equipment != null)
			{
				outChildren.Add(equipment);
			}
			if (apparel != null)
			{
				outChildren.Add(apparel);
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
			if (!base.Destroyed)
			{
				SoundDefOf.CryptosleepCasket_Eject.PlayOneShot(SoundInfo.InMap(new TargetInfo(base.Position, base.Map)));
			}
			Caravan caravan= this.GetCaravan();
			Notify_Unequipped(this);
			if (base.Spawned)
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
					caravan.AddPawnOrItem(thing, true);
					innerContainer.Remove(thing);
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
				drafter.Drafted = false;
				contentsKnown = true;
				return;
			}
			Log.Error(this.ToStringSafe() + "tried to eject contents while not in a caravan or on a map");

		}
		public virtual void EjectContentsThing(Thing thing)
		{
			if (!base.Destroyed)
			{
				SoundDefOf.CryptosleepCasket_Eject.PlayOneShot(SoundInfo.InMap(new TargetInfo(base.Position, base.Map)));
			}
			Caravan caravan = this.GetCaravan();
			if (base.Spawned)
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
					caravan.AddPawnOrItem(thing, true);
					innerContainer.Remove(thing);
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
				drafter.Drafted = false;
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
