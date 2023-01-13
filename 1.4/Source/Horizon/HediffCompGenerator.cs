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
	//base class for solar panels or other power generator organs
	[HarmonyPatch(typeof(Need_MechEnergy), "NeedInterval")]
	public static class Need_MechEnergy_NeedInterval_Patch
    {
		public static void Postfix(ref Need __instance, ref Pawn ___pawn)
        {
			IEnumerable<HediffComp_Generator> t = from a in ___pawn.health.hediffSet.hediffs
												   where a is HediffWithComps x && x.TryGetComp<HediffComp_Generator>() != null
												   select a.TryGetComp<HediffComp_Generator>();
			foreach (HediffComp_Generator comp in t)
			{
				__instance.CurLevel += comp.output/400;
			}
        }
    }

	public class HediffCompProperties_Generator : HediffCompProperties
	{
		public float baseOutput;
		public HediffCompProperties_Generator()
		{
			compClass = typeof(HediffComp_Generator);
		}
	}
	public class HediffComp_Generator : HediffComp
	{
		public bool active = true;
		public float output;
		public bool Active
        {
			get { return active; }
			set { active = value; }
        }

		public HediffCompProperties_Generator Props => (HediffCompProperties_Generator)props;
		public virtual void CurOutput()
		{
            output = Props.baseOutput;
		}
		public override void CompPostTick(ref float severityAdjustment)
		{
			base.CompPostTick(ref severityAdjustment);
			if (Active)
            {
				CurOutput();
            }
            else
            {
				output = 0;
            }
		}
	}
}
