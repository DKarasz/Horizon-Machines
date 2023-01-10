using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

		public int ticksForWork = 100;
		public int timeInterval = 40000;
		public bool spawnForbidden = false;
        public bool onlyWhenTamed = false;

        public bool workOnlyOnGrowingSeason = false;
        public int minTemperature = 0;
        public int maxTemperature = 58;


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
		public ThingDef Output;
		public int inputCount=1;
		public int outputCount=1;
        public bool consumePollution = false;
        //special case, works on a 1 trash bag equivalence
        public float pollutionCount=6;
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
        public int recipeIndex = -1;
        public int terrainIndex = -1;
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

                    //Job job = JobMaker.MakeJob(JobDefOf.Ingest, Cake);
                    //job.count = 1;
                    //pawn.jobs.StartJob(job, JobCondition.InterruptOptional);
                }
            }
            return true;
        }
		public void makeProduct(Pawn pawn)
        {
            Thing newThing;
            ThingDef newThingDef;
            if (Props.product.cutChunks)
            {
                Thing chunk = FindInputChunks(pawn);
                newThingDef = chunk.def.building.mineableThing.butcherProducts.FirstOrFallback().thingDef;
                eatThing(chunk, pawn);
                newThing = GenSpawn.Spawn(newThingDef, pawn.Position, pawn.Map, WipeMode.Vanish);
                newThing.stackCount = Props.product.outputCount;
            }
            else
            {
                ProcessComposite thingToDig = this.Props.thingToDig.RandomElement();
                newThing = thingToDig.Output;
                newcorpse = GenSpawn.Spawn(newThing, pawn.Position, pawn.Map, WipeMode.Vanish);
                newcorpse.stackCount = thingToDig.outputCount;
            }
            if (Props.spawnForbidden)
            {
                newcorpse.SetForbidden(true);
            }

            if (this.effecter == null)
            {
                this.effecter = EffecterDefOf.Mine.Spawn();
            }
            this.effecter.Trigger(pawn, newcorpse);

            consumeNeeds(pawn);
        }

        public int eatThing(Thing thing, Pawn pawn)
        {
            Job job = JobMaker.MakeJob(JobDefOf.Ingest, thing);
            job.count = Math.Min(thing.stackCount, Props.product.inputCount);
            pawn.jobs.StartJob(job, JobCondition.InterruptOptional);
            return job.count;
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
            return GenClosest.ClosestThingReachable(pawn.PositionHeld, pawn.MapHeld, ThingRequest.ForGroup(ThingRequestGroup.Chunk), PathEndMode.OnCell, TraverseParms.For(pawn), 15.9f, (Thing t) => IsValidToybox(t, pawn, baby));
        }
        public bool IsValid(Thing thing, Pawn hauler)
        {
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
            if (hauler.Position.DistanceTo(thing.Position) > 15.9f)
            {
                return false;
            }
            return true;
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
            return GenClosest.ClosestThingReachable(pawn.PositionHeld, pawn.MapHeld, ThingRequest.ForGroup(ThingRequestGroup.Chunk), PathEndMode.OnCell, TraverseParms.For(pawn), 15.9f, (Thing t) => IsValidToybox(t, pawn, baby));
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