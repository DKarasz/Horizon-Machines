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
	[HarmonyPatch(typeof(StatWorker), "StatOffsetFromGear")]
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
			if (thing == null)
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
}
