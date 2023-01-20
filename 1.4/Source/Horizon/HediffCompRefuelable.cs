using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;
using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Verse.AI.Group;
using MonoMod.Utils;
using Verse.Sound;

namespace Horizon
{
	//class for organs that increase storage of a need, also stablized storage
    public class HediffCompProperties_Refuelable : HediffCompProperties_Chargeable
    {
		//public int ticksToFullCharge = -1;

		//public float initialCharge;

		//public float fullChargeAmount = 1f;

		//public float minChargeToActivate;

		//public string labelInBrackets;

		public NeedDef Need;

		public HediffCompProperties_Refuelable()
		{
			compClass = typeof(HediffComp_Refuelable);
		}
	}
	public class HediffComp_Refuelable: HediffComp_Chargeable
    {
		private float charge;
		public new HediffCompProperties_Refuelable Props => (HediffCompProperties_Refuelable)props;
		public new float Charge
		{
			get
			{
				return charge;
			}
			protected set
			{
				charge = parent.pawn.needs.TryGetNeed(Props.Need).CurLevelPercentage * Props.fullChargeAmount;
				if (charge > Props.fullChargeAmount)
				{
					charge = Props.fullChargeAmount;
				}
			}
		}
		public float Activate()
        {
			float amount = 0;
			while (CanActivate)
            {
				TryCharge(-(Props.minChargeToActivate));
				amount++;
            }
			return amount;
		}
		//activate can indicate given amount of charge for product creation, need greedy consume or trycharge
		public override void TryCharge(float desiredChargeAmount)
		{
			parent.pawn.needs.TryGetNeed(Props.Need).CurLevel += desiredChargeAmount;
			Charge += desiredChargeAmount;
		}
		public new float GreedyConsume(float desiredCharge)
		{
			float num;
			if (desiredCharge >= charge)
			{
				num = charge;
				parent.pawn.needs.TryGetNeed(Props.Need).CurLevel -= num;
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
