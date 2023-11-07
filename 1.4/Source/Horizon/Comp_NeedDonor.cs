using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Horizon
{
	public class CompProperties_NeedDonor : CompProperties
	{
		public List<NeedDef> needs;
		public bool canDonateToLikeDonors = true;//donate to any target
		public bool isStorageDonor = false;//can not donate from storage donor to storage donor
		public float initialThreshold = 0.5f;
		public CompProperties_NeedDonor()
		{
			compClass = typeof(Comp_NeedDonor);
		}
	}
	public class Comp_NeedDonor : ThingComp
    {
		//need to check if resource to be donated is in donation list of target
		//need to check if target is storage donor if existing is storage donor
    }
}
