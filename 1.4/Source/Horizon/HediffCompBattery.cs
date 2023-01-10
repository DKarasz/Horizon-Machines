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
	//class for power capacity increasing organs
	[HarmonyPatch(typeof(Need_MechEnergy), "MaxLevel", MethodType.Getter)]
	public class Need_MechEnergy__MaxLevel_Patch
	{
		public void Postfix(ref float __result, ref Pawn ___pawn)
		{
			IEnumerable<HediffComp_Battery> t = from a in ___pawn.health.hediffSet.hediffs
												where a is HediffWithComps x && x.TryGetComp<HediffComp_Battery>() != null
												select a.TryGetComp<HediffComp_Battery>();
			foreach (HediffComp_Battery comp in t)
			{
				__result += comp.Props.fullChargeAmount;
			}
		}
	}

	//locks waste generation to be per point of energy instead of percent of total energy
	[HarmonyPatch(typeof(Building_MechCharger), "WasteProducedPerTick", MethodType.Getter)]
	public class Building_MechCharger_WasteProducedPerTick
    {
		public bool Prefix(ref float __result, Pawn currentlyChargingMech)
        {
			__result= currentlyChargingMech.GetStatValue(StatDefOf.WastepacksPerRecharge) * (0.000833333354f / 100f);
			return false;
		}
    }


	public class HediffCompProperties_Battery : HediffCompProperties_Chargeable
	{
		public HediffCompProperties_Battery()
		{
			compClass = typeof(HediffComp_Battery);
		}
	}
	public class HediffComp_Battery : HediffComp_Chargeable
	{
		private float charge;
		public new HediffCompProperties_Battery Props => (HediffCompProperties_Battery)props;
		public new float Charge
		{
			get
			{
				return charge;
			}
			protected set
			{
				charge = parent.pawn.needs.energy.CurLevelPercentage * Props.fullChargeAmount;
				if (charge > Props.fullChargeAmount)
				{
					charge = Props.fullChargeAmount;
				}
			}
		}
		//activate can indicate given amount of charge for product creation, need greedy consume or trycharge
		public override void TryCharge(float desiredChargeAmount)
		{
			parent.pawn.needs.energy.CurLevel += desiredChargeAmount;
			Charge += desiredChargeAmount;
		}
		public new float GreedyConsume(float desiredCharge)
		{
			float num;
			if (desiredCharge >= charge)
			{
				num = charge;
				parent.pawn.needs.energy.CurLevel -= num;
			}
			else
			{
				num = desiredCharge;
				parent.pawn.needs.energy.CurLevel -= num;
			}
			return num;
		}
		//greedyconsume can be used to fill/consume incremental/activation amounts
	}
}
