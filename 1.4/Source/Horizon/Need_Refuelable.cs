using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Horizon
{
    //need class for accessing the stored capacity of the fuel
    public class FuelNeed : DefModExtension
    {
        public ThingDef fuelDef;
    }
    public class Need_Refuelable : Need
    {
        public override float MaxLevel
        {
            get
            {
                float result = 0;
                IEnumerable<HediffComp_Refuelable> t = from a in pawn.health.hediffSet.hediffs
                                          where a is HediffWithComps x && x.TryGetComp<HediffComp_Refuelable>() != null
                                          select a.TryGetComp<HediffComp_Refuelable>() into b where b.Props.Need.Equals(def) select b;
                foreach (HediffComp_Refuelable comp in t)
                {
                    result += comp.Props.fullChargeAmount;
                }
                return result;
            }
        }
        public void Activate()
        {
            ThingDef fuelThingDef = def.GetModExtension<FuelNeed>()?.fuelDef ?? null;
            if (fuelThingDef == null)
            {
                return;
            }
            float amount = 0;
            IEnumerable<HediffComp_Refuelable> t = from a in pawn.health.hediffSet.hediffs
                                                   where a is HediffWithComps x && x.TryGetComp<HediffComp_Refuelable>() != null
                                                   select a.TryGetComp<HediffComp_Refuelable>() into b
                                                   where b.Props.Need.Equals(def)
                                                   select b;
            foreach (HediffComp_Refuelable comp in t)
            {
                amount += comp.Activate();
            }
            Thing thing = ThingMaker.MakeThing(fuelThingDef);
            thing.stackCount = (int)amount;
            GenPlace.TryPlaceThing(thing, pawn.Position, pawn.Map, ThingPlaceMode.Near);
        }
        public override void NeedInterval()
        {
            throw new NotImplementedException();//want to only consume when in use by action
        }
    }
}
