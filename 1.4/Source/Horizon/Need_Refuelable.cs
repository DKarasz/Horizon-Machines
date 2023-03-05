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
    //need class for accessing the stored capacity of the fuel

    [HarmonyPatch(typeof(Pawn_NeedsTracker), "GetGizmos")]
    public static class Pawn_NeedsTracker_GetGizmos_Patch
    {
        public static void Postfix(IEnumerable<Gizmo> __result, Pawn_NeedsTracker __instance)
        {
            IEnumerable<Need_Refuelable> t = from a in __instance.MiscNeeds where a is Need_Refuelable select (Need_Refuelable)a;
            foreach (Need_Refuelable need in t)
            {
                need.GetGizmo();
            }
        }
    }
    public class FuelNeed : DefModExtension
    {
        public ThingDef fuelDef;
    }
    public class Need_Refuelable : Need
    {
        public Need_Refuelable(Pawn pawn) : base(pawn)
        {
        }

        public virtual IEnumerable<Gizmo> GetGizmo()//need some gizmo to empty need, or some button on the need menu
        {
            if (pawn.Faction != null && pawn.Faction == Faction.OfPlayer)
            {
                Command_Action command_Action = new Command_Action();
                command_Action.action = Activate;
                    command_Action.defaultLabel = "ExtractMaterials".Translate();
                    command_Action.defaultDesc = "ExtractMaterialsDesc".Translate();
                    //if (Need.cannotActivate)
                    //{
                    //    command_Action.Disable("NeedEmpty".Translate());
                    //}
                    //command_Action.hotKey = KeyBindingDefOf.Misc8;
                    command_Action.icon = ContentFinder<Texture2D>.Get("UI/Commands/SetTargetFuelLevel");
                    yield return command_Action;
            }
        }

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
            if (amount < 1)
            {
                return;
            }
            Thing thing = ThingMaker.MakeThing(fuelThingDef);
            thing.stackCount = (int)amount;
            GenPlace.TryPlaceThing(thing, pawn.Position, pawn.Map, ThingPlaceMode.Near);
            Messages.Message(pawn.ToString() + " produced item " + thing.ToString() + " x" + thing.stackCount.ToString(), MessageTypeDefOf.TaskCompletion, false);
        }
        public override void NeedInterval()
        {
            //throw new NotImplementedException();//want to only consume when in use by action
        }
    }
}
