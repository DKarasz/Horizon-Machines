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
	[HarmonyPatch(typeof(Need_MechEnergy), "MaxLevel", MethodType.Getter)]
	public class Need_MechEnergy__MaxLevel_Patch
    {
		public void Postfix(ref float __result, ref Pawn ___pawn)
        {
			IEnumerable<HediffComp_Refuelable> t = from a in ___pawn.health.hediffSet.hediffs
												   where a is HediffWithComps x && x.TryGetComp<HediffComp_Refuelable>() != null
												   select a.TryGetComp<HediffComp_Refuelable>();
			foreach (HediffComp_Refuelable comp in t)
			{
				__result += comp.Props.fullChargeAmount;
			}
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
