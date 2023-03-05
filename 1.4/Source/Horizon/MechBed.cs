using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Horizon
{
	//change of plans, find a way to get operations to occur at the mech chargers

	//[HarmonyPatch(typeof(RestUtility), "CanUseBedEver")]
	//public static class RestUtility_CanUseBedEver_Patch
	//{
	//	public static bool Prefix(Pawn p, ThingDef bedDef, ref bool __result)
	//	{
	//		bool mechbed = bedDef.GetModExtension<MechBed>()!=null;
	//		if (!p.RaceProps.IsMechanoid)
	//		{
 //               if (mechbed)
 //               {
	//				__result = false;
	//				return false;
 //               }
	//			return true;
	//		}
	//		if (p.BodySize > bedDef.building.bed_maxBodySize)
	//		{
	//			return true;
	//		}
	//		__result = true;
	//		return false;
	//	}
	//}

	//[HarmonyPatch(typeof(RestUtility), "FindBedFor", new Type[] { typeof(Pawn), typeof(Pawn), typeof(bool), typeof(bool), typeof(GuestStatus?)})]
	//public static class RestUtility_FindBedFor_Patch
 //   {
	//	public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) => 
	//		codes.MethodReplacer(AccessTools.PropertyGetter(typeof(RaceProperties), "IsMechanoid"), AccessTools.Method(typeof(RestUtility_FindBedFor_Patch), "MechOverride"));
	//	static bool MechOverride(RaceProperties RaceProps) => false;
	//}
	//[HarmonyPatch(typeof(PawnRenderer), "BodyAngle")]
	//public static class PawnRenderer_BodyAngle_Patch
 //   {
	//	public static bool prefix(Pawn ___pawn, ref float __result)
 //       {
	//		Building_Bed building_Bed = ___pawn.CurrentBed();
	//		if (building_Bed != null && ___pawn.RaceProps.IsMechanoid)
	//		{
	//			Rot4 rotation = building_Bed.Rotation;
	//			rotation.AsInt += 2;
	//			__result= rotation.AsAngle;
	//			return false;
	//		}
	//		return true;
	//	}
 //   }


	//public class MechBed : DefModExtension { }


	//[HarmonyPatch(typeof(ITab_Pawn_Health), "ShouldAllowOperations")]
	//public static class ITab_Pawn_Health_ShouldAllowOperations_Patch
	//{
	//	public static void Postfix(ref bool __result)
	//	{
	//		Thing SelThing = Find.Selector.SingleSelectedThing;
	//		Pawn PawnForHealth = null;
	//		Pawn SelPawn = SelThing as Pawn;
	//		if (SelPawn != null)
	//		{
	//			PawnForHealth = SelPawn;
	//		}
	//		else if (SelThing is Corpse corpse)
	//		{
	//			PawnForHealth = corpse.InnerPawn;
	//		}
	//		if (!PawnForHealth.Dead && PawnForHealth.IsColonyMechPlayerControlled && SelThing.def.AllRecipes.Any((RecipeDef x) => x.AvailableNow && x.AvailableOnNow(PawnForHealth)))
	//		{
	//			__result = true;
	//		}
	//	}
	//}

	[HarmonyPatch(typeof(Pawn_HealthTracker), "HealthTick")]
	public static class Pawn_HealthTracker_HealthTick_Patch
	{
		public static void Postfix(Pawn_HealthTracker __instance, Pawn ___pawn)
		{
			if (!__instance.Dead && ___pawn is MechSuit mech && mech.mechExtension.canBleed)
			{
				float num5 = CalculateMechBleedRate(___pawn, __instance.hediffSet) * ___pawn.BodySize;
				num5 = ((___pawn.GetPosture() != 0) ? (num5 * 0.0004f) : (num5 * 0.004f));
				if (Rand.Value < num5)
				{
					__instance.DropBloodFilth();
				}
			}
		}
		public static float CalculateMechBleedRate(Pawn pawn, HediffSet hediffSet)
		{
			if (pawn.Deathresting)
			{
				return 0f;
			}
			float num = 1f;
			float num2 = 0f;
			for (int i = 0; i < hediffSet.hediffs.Count; i++)
			{
				Hediff hediff = hediffSet.hediffs[i];
				HediffStage curStage = hediff.CurStage;
				if (curStage != null)
				{
					num *= curStage.totalBleedFactor;
				}
				num2 += hediff.BleedRate;
			}
			return num2 * num / pawn.HealthScale;
		}
	}


}
