using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Horizon
{
	public class CompProperties_PawnProcessor : CompProperties
	{
        //eats items or pollution, produces items, ignores terrain

		//check if needs are met
		public List<NeedComposite> inputNeed = null;
		//check if needs have room
		public List<NeedComposite> outputNeed = null;
		public bool ignoreOverflow = false;
		//input/output combo
		public ProcessComposite product;

        //performance delay, on fail, rechecks for resource this many ticks instead of every tick
		public int ticksForDelay = 600;
        //time between production minimum
		public int timeInterval = 40000;
		public bool spawnForbidden = false;
        public bool onlyWhenTamed = false;

        public bool workOnlyOnGrowingSeason = false;
        public int minTemperature = 0;
        public int maxTemperature = 58;
        public float range = 15.9f;


        public CompProperties_PawnProcessor()
		{
			compClass = typeof(Comp_PawnProcessor);
		}
	}
  //  public class TerrainComposite
  //  {
		//public string Terrain;
		//public string OutputTerrain;
  //  }
    public class ProcessComposite
    {
		public ThingDef Input;
        public ThingCategoryDef InputCategory= null;
		public ThingDef Output;
        //for pollution, this works on a pollution/tile basis, 1 bag= 6 pollution
		public int inputCount=1;
		public int outputCount=1;
        public bool consumePollution = false;
        //special case for cutting chunks into bricks
        public bool cutChunks = false;

    }
    public class NeedComposite
    {
		public NeedDef need;
		public float amount;
    }
	public class Comp_PawnProcessor : ThingComp
	{
        public CompProperties_PawnProcessor Props => (CompProperties_PawnProcessor)props;

        public int inputCounter = 0;
        public int counter = 0;
        public Effecter effecter;

        public override void CompTick()
        {
            base.CompTick();
            Pawn pawn = this.parent as Pawn;
            if (canDoProcess(pawn))
            {
                makeProduct(pawn);
            }
        }

        public bool canDoProcess(Pawn pawn)
        {
            if (HorizonFrameworkSettings.flagDigPeriodicallyNeed && (pawn.Map != null) && (pawn.Awake()) &&
                    !pawn.Downed && !pawn.Dead && (!Props.onlyWhenTamed || (Props.onlyWhenTamed && pawn.Faction != null && pawn.Faction.IsPlayer)))
            {
                if (!Props.workOnlyOnGrowingSeason || (Props.workOnlyOnGrowingSeason &&
                    (pawn.Map.mapTemperature.OutdoorTemp > Props.minTemperature && pawn.Map.mapTemperature.OutdoorTemp < Props.maxTemperature)))
                {
                    if (counter <= 0)
                    {
                        if (Props.product.consumePollution && !ModLister.CheckBiotech("Clear pollution"))
                        {
                            return false;
                        }
                        foreach (NeedComposite needComp in Props.inputNeed)
                        {
                            Need need = pawn.needs.TryGetNeed(needComp.need);
                            if (need == null || need.CurLevel < needComp.amount)
                            {
                                return false;
                            }
                        }
                        if (!Props.ignoreOverflow)
                        {
                            foreach (NeedComposite needComp in Props.outputNeed)
                            {
                                Need need = pawn.needs.TryGetNeed(needComp.need);
                                if (need == null || need.MaxLevel - need.CurLevel < needComp.amount)
                                {
                                    return false;
                                }
                            }
                        }
                        return true;
                    }
                    counter--;
                    //Job job = JobMaker.MakeJob(JobDefOf.Ingest, Cake);
                    //job.count = 1;
                    //pawn.jobs.StartJob(job, JobCondition.InterruptOptional);
                }
            }
            return false;
        }
		public void makeProduct(Pawn pawn)
        {
            Thing newThing = null;
            ThingDef newThingDef;
            counter = Props.ticksForDelay;// delay each eat attempt
            if (Props.product == null)
            {
                if (this.effecter == null)
                {
                    this.effecter = EffecterDefOf.Mine.Spawn();
                }
                this.effecter.Trigger(pawn, newThing);
                consumeNeeds(pawn);
                counter = Props.timeInterval;
                return;
            }
            if (Props.product.cutChunks)
            {
                Thing chunk = FindInputChunks(pawn);
                if (chunk == null)
                {
                    return;
                }
                newThingDef = chunk.def.building.mineableThing.butcherProducts.FirstOrFallback().thingDef;
                eatThing(chunk, pawn);
                if(inputCounter >= Props.product.inputCount)
                {
                    inputCounter = 0;
                    newThing = GenSpawn.Spawn(newThingDef, pawn.Position, pawn.Map, WipeMode.Vanish);
                    newThing.stackCount = Props.product.outputCount;
                }
            }
            else if (Props.product.InputCategory!= null)
            {
                Thing category = FindInputCategory(pawn, Props.product.InputCategory);
                if (category == null)
                {
                    return;
                }
                newThingDef = Props.product.Output;
                eatThing(category, pawn);
                if (inputCounter >= Props.product.inputCount)
                {
                    inputCounter = 0;
                    newThing = GenSpawn.Spawn(newThingDef, pawn.Position, pawn.Map, WipeMode.Vanish);
                    newThing.stackCount = Props.product.outputCount;
                }

            }
            else if (Props.product.consumePollution)
            {
                if (inputCounter < Props.product.inputCount)
                {
                    if (!eatBag(pawn))//look for and eat a bag or clean up enough pollution
                    {
                        return;
                    }
                }
                //    Thing bag = FindInput(pawn, ThingDefOf.Wastepack);
                //if (bag != null)
                //{
                //    eatBag(bag, pawn);
                //}
                newThingDef = Props.product.Output;

                if (inputCounter >= Props.product.inputCount)
                {
                    inputCounter -= Props.product.inputCount;
                    newThing = GenSpawn.Spawn(newThingDef, pawn.Position, pawn.Map, WipeMode.Vanish);
                    newThing.stackCount = Props.product.outputCount;
                }
            }
            else
            {
                Thing bite = FindInput(pawn, Props.product.Input);
                if (bite == null)
                {
                    return;
                }
                newThingDef = Props.product.Output;
                eatThing(bite, pawn);
                if (inputCounter >= Props.product.inputCount)
                {
                    inputCounter = 0;
                    newThing = GenSpawn.Spawn(Props.product.Output, pawn.Position, pawn.Map, WipeMode.Vanish);
                    newThing.stackCount = Props.product.outputCount;
                }
            }
            if (newThing == null)
            {
                return;
            }
            if (Props.spawnForbidden)
            {
                newThing.SetForbidden(true);
            }

            if (this.effecter == null)
            {
                this.effecter = EffecterDefOf.Mine.Spawn();
            }
            this.effecter.Trigger(pawn, newThing);
            consumeNeeds(pawn);
            counter = Props.timeInterval;
        }

        private bool eatBag(Pawn pawn)
        {
            Thing bag = FindInput(pawn, ThingDefOf.Wastepack);
            if (bag != null)
            {
                eatThing(bag, pawn);
                inputCounter += 5;
                return true;
            }
            return findPollution(pawn);
        }

        public void eatThing(Thing thing, Pawn pawn)
        {
            Job job = JobMaker.MakeJob(JobDefOf.Ingest, thing);// will need replace with custom job similar to animal eating
            job.count = Math.Min(thing.stackCount, Props.product.inputCount - inputCounter);
            pawn.jobs.StartJob(job, JobCondition.InterruptOptional);
            inputCounter += job.count;
            return;
        }

        public bool findPollution(Pawn pawn)
        {
            int count = UnpolluteRadially(pawn.Position, pawn.Map, pawn);
            if (count == 0)
            {
                return false;
            }
            inputCounter += count;
            return true;
        }
        public int UnpolluteRadially(IntVec3 root, Map map, Pawn pawn, int maxToUnpollute = 5, bool ignoreOtherPawnsCleaningCell = false)
        {
            int num = 0;
            foreach (IntVec3 item in GenRadial.RadialCellsAround(root, Props.range, useCenter: true))
            {
                if (CanUnpollute(pawn, root, map, item))
                {
                    item.Unpollute(map);
                    num++;
                    if (num >= maxToUnpollute)
                    {
                        return num;
                    }
                }
            }
            return num;
        }
        private bool CanUnpollute(Pawn pawn, IntVec3 root, Map map, IntVec3 c, bool ignoreOtherPawnsCleaningCell = false)
        {
            if (!c.IsPolluted(map))
            {
                return false;
            }
            if (!ignoreOtherPawnsCleaningCell && AnyOtherPawnCleaning(pawn, c))
            {
                return false;
            }
            if (root.GetRoom(map) != c.GetRoom(map))
            {
                return false;
            }
            if (c.DistanceToSquared(root) > Mathf.Pow(Props.range,2))
            {
                return false;
            }
            return true;
        }
        private bool AnyOtherPawnCleaning(Pawn pawn, IntVec3 cell)
        {
            List<Pawn> freeColonistsSpawned = pawn.Map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < freeColonistsSpawned.Count; i++)
            {
                if (freeColonistsSpawned[i] != pawn && freeColonistsSpawned[i].CurJobDef == JobDefOf.ClearPollution)
                {
                    LocalTargetInfo target = freeColonistsSpawned[i].CurJob.GetTarget(TargetIndex.A);
                    if (target.IsValid && target.Cell == cell)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public Thing FindInput(Pawn pawn, ThingDef thing)
        {
            Room room = pawn.GetRoom();
            if (room != null)
            {
                IEnumerable<ThingDef> rocksInThisBiome = Find.World.NaturalRockTypesIn(this.parent.Map.Tile);
                List<ThingDef> chunksInThisBiome = new List<ThingDef>();
                foreach (ThingDef rock in rocksInThisBiome)
                {
                    chunksInThisBiome.Add(rock.building.mineableThing);
                }
                foreach (Thing item in room.ContainedThingsList(chunksInThisBiome))
                {
                    if (IsValid(item, pawn))
                    {
                        return item;
                    }
                }
            }
            return GenClosest.ClosestThingReachable(pawn.PositionHeld, pawn.MapHeld, ThingRequest.ForGroup(ThingRequestGroup.Chunk), PathEndMode.OnCell, TraverseParms.For(pawn), 15.9f, (Thing t) => IsValid(t, pawn));
        }
        public bool IsValid(Thing thing, Pawn hauler, ThingCategoryDef category=null)
        {
            if (category != null && !thing.HasThingCategory(category))
            {
                return false;
            }
            if (thing.IsForbidden(hauler))
            {
                return false;
            }
            if (thing.IsBurning())
            {
                return false;
            }
            if (!hauler.CanReserveAndReach(thing, PathEndMode.Touch, Danger.None))
            {
                return false;
            }
            if (hauler.Position.DistanceTo(thing.Position) > Props.range)
            {
                return false;
            }
            return true;
        }
        public Thing FindInputCategory(Pawn pawn, ThingCategoryDef category)
        {
            Room room = pawn.GetRoom();
            if (room != null)
            {
                foreach (Thing item in room.ContainedThingsList(category.childThingDefs))
                {
                    if (IsValid(item, pawn, category))
                    {
                        return item;
                    }
                }
            }
            return GenClosest.ClosestThingReachable(pawn.PositionHeld, pawn.MapHeld, ThingRequest.ForGroup(ThingRequestGroup.Undefined), PathEndMode.OnCell, TraverseParms.For(pawn), Props.range, (Thing t) => IsValid(t, pawn, category));
        }
        public Thing FindInputChunks(Pawn pawn)
        {
            Room room = pawn.GetRoom();
            if (room != null)
            {
                IEnumerable<ThingDef> rocksInThisBiome = Find.World.NaturalRockTypesIn(this.parent.Map.Tile);
                List<ThingDef> chunksInThisBiome = new List<ThingDef>();
                foreach (ThingDef rock in rocksInThisBiome)
                {
                    chunksInThisBiome.Add(rock.building.mineableThing);
                }
                foreach (Thing item in room.ContainedThingsList(chunksInThisBiome))
                {
                    if (IsValid(item, pawn))
                    {
                        return item;
                    }
                }
            }
            return GenClosest.ClosestThingReachable(pawn.PositionHeld, pawn.MapHeld, ThingRequest.ForGroup(ThingRequestGroup.Chunk), PathEndMode.OnCell, TraverseParms.For(pawn), Props.range, (Thing t) => IsValid(t, pawn));
        }
        public void consumeNeeds(Pawn pawn)
        {
            foreach (NeedComposite needComp in Props.inputNeed)
            {
                pawn.needs.TryGetNeed(needComp.need).CurLevel -= needComp.amount;
            }
            foreach (NeedComposite needComp in Props.outputNeed)
            {
                pawn.needs.TryGetNeed(needComp.need).CurLevel += needComp.amount;
            }
        }
    }
}