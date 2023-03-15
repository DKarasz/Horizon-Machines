using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Horizon
{
	[HarmonyPatch(typeof(StatWorker), "GetValueUnfinalized")]
	public static class StatWorker_GetValueUnfinalized_Patch
	{
		public static void Postfix(ref float __result, StatRequest req, StatDef ___stat)
		{
			Pawn pawn = req.Thing as Pawn;
			if (pawn != null)
			{
				if (!pawn.Spawned && pawn.ParentHolder != null && pawn.ParentHolder is MechSuit suit && suit.mechExtension.mechOffsetsPawn)
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
					if (f != 0)
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
				__result = __instance.pawn.equipment.Contains(__instance.weapon) || __instance.pawn.apparel.Contains(__instance.weapon);
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
	[HarmonyPatch(typeof(Pawn), "DoKillSideEffects")]//need to add hook into apparel kill side effects-- hook added in equipment tracker killed pawn
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

}
