using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Horizon
{
	public class HediffCompProperties_Solar : HediffCompProperties_Generator
	{
		public HediffCompProperties_Solar()
		{
			compClass = typeof(HediffComp_Solar);
		}
	}
	public class HediffComp_Solar : HediffComp_Generator
	{
		public new HediffCompProperties_Solar Props => (HediffCompProperties_Solar)props;
		public override float output
        {
            get
            {
				return Mathf.Lerp(0f, 1f, parent.pawn.Map.skyManager.CurSkyGlow) * RoofedPowerOutputFactor * Props.baseOutput;

			}
		}
		public float RoofedPowerOutputFactor
		{
			get
			{
				int num = 0;
				int num2 = 0;
				foreach (IntVec3 item in parent.pawn.OccupiedRect())
				{
					num++;
					if (parent.pawn.Map.roofGrid.Roofed(item))
					{
						num2++;
					}
				}
				return (float)(num - num2) / (float)num;
			}
		}
	}
}
