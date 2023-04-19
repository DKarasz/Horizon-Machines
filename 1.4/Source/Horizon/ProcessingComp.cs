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
    //extra copies for using multiple comps
    public class Comp_PawnProcessor1 : Comp_PawnProcessor { }
    public class Comp_PawnProcessor2 : Comp_PawnProcessor { }
    public class Comp_PawnProcessor3 : Comp_PawnProcessor { }
    public class Comp_PawnProcessor4 : Comp_PawnProcessor { }
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
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look<int>(ref inputCounter, "inputCounter", 0);
            Scribe_Values.Look<int>(ref counter, "counter", 0);
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
                        if (Props.onlyWhenTamed && pawn.IsColonyMechPlayerControlled && !MechanitorUtility.GetMechWorkMode(pawn).defName.Contains("ork"))//work or Work
                        {
                            return false;
                        }
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
                    inputCounter -= Props.product.inputCount;
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
                    inputCounter -= Props.product.inputCount;
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
                    inputCounter -= Props.product.inputCount;
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
                eatThing(bag, pawn, 1);
                inputCounter += 5;
                return true;
            }
            return findPollution(pawn);
        }

        public void eatThing(Thing thing, Pawn pawn, int maxCount = -1)
        {
            Job job = JobMaker.MakeJob(JobMechDefOf.IngestProcess, thing);// will need replace with custom job similar to animal eating
            if (maxCount >= 1)
            {
                job.count = maxCount;
            }
            else
            {
                job.count = Math.Min(thing.stackCount, Props.product.inputCount - inputCounter);
            }
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
            if (category != null) {
                if (!thing.HasThingCategory(category))
                {
                    return false;
                }
                if (category == ThingCategoryDefOf.PlantMatter && thing is Plant plant)
                {
                    return plant.Growth >= .9f;
                }
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
    public class JobDriver_IngestProcess : JobDriver
    {
        private bool usingNutrientPasteDispenser;

        private bool eatingFromInventory;

        public const float EatCorpseBodyPartsUntilFoodLevelPct = 0.9f;

        public const TargetIndex IngestibleSourceInd = TargetIndex.A;

        private const TargetIndex TableCellInd = TargetIndex.B;

        private const TargetIndex ExtraIngestiblesToCollectInd = TargetIndex.C;

        public bool EatingFromInventory => eatingFromInventory;

        private Thing IngestibleSource => job.GetTarget(TargetIndex.A).Thing;

        private float ChewDurationMultiplier
        {
            get
            {
                float eatingSpeed = pawn.GetStatValue(StatDefOf.EatingSpeed);
                return eatingSpeed == 0 ? 1 : 1 / eatingSpeed;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
        }

        public override string GetReport()
        {
            return base.GetReport();
        }

        public override void Notify_Starting()
        {
            base.Notify_Starting();
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (pawn.Faction != null)
            {
                Thing ingestibleSource = IngestibleSource;
                if (!pawn.Reserve(ingestibleSource, job, 10, job.count, null, errorOnFailed))
                {
                    return false;
                }
            }
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => !IngestibleSource.Destroyed);
            
            Toil chew = ChewResource(pawn, ChewDurationMultiplier, TargetIndex.A, TargetIndex.B).FailOn((Toil x) => !IngestibleSource.Spawned && (pawn.carryTracker == null || pawn.carryTracker.CarriedThing != IngestibleSource)).FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
            foreach (Toil item in PrepareToIngestToils(chew))
            {
                yield return item;
            }
            yield return chew;
            yield return FinalizeIngest(pawn, TargetIndex.A);
            yield return Toils_Jump.JumpIf(chew, () => job.GetTarget(TargetIndex.A).Thing is Corpse && pawn.needs.food.CurLevelPercentage < 0.9f);
            yield break;
        }

        private IEnumerable<Toil> PrepareToIngestToils(Toil chewToil)
        {
            return PrepareToIngestToils_NonToolUser();
        }
        public static Toil ChewResource(Pawn chewer, float durationMultiplier, TargetIndex ingestibleInd, TargetIndex eatSurfaceInd = TargetIndex.None)
        {
            Toil toil = new Toil();
            toil.initAction = delegate
            {
                Pawn actor = toil.actor;
                Thing thing4 = actor.CurJob.GetTarget(ingestibleInd).Thing;

                    actor.jobs.curDriver.ticksLeftThisToil = Mathf.RoundToInt(500f * durationMultiplier);
                    if (thing4.Spawned)
                    {
                        thing4.Map.physicalInteractionReservationManager.Reserve(chewer, actor.CurJob, thing4);
                    }
            };
            toil.tickAction = delegate
            {
                if (chewer != toil.actor)
                {
                    toil.actor.rotationTracker.FaceCell(chewer.Position);
                }
                else
                {
                    Thing thing3 = toil.actor.CurJob.GetTarget(ingestibleInd).Thing;
                    if (thing3 != null && thing3.Spawned)
                    {
                        toil.actor.rotationTracker.FaceCell(thing3.Position);
                    }
                    else if (eatSurfaceInd != 0 && toil.actor.CurJob.GetTarget(eatSurfaceInd).IsValid)
                    {
                        toil.actor.rotationTracker.FaceCell(toil.actor.CurJob.GetTarget(eatSurfaceInd).Cell);
                    }
                }
                toil.actor.GainComfortFromCellIfPossible();
            };
            toil.WithProgressBar(ingestibleInd, delegate
            {
                Thing thing2 = toil.actor.CurJob.GetTarget(ingestibleInd).Thing;
                return (thing2 == null) ? 1f : (1f - (float)toil.actor.jobs.curDriver.ticksLeftThisToil / Mathf.Round(500f * durationMultiplier));
            });
            toil.defaultCompleteMode = ToilCompleteMode.Delay;
            toil.FailOnDestroyedOrNull(ingestibleInd);
            toil.AddFinishAction(delegate
            {
                if (chewer != null && chewer.CurJob != null)
                {
                    Thing thing = chewer.CurJob.GetTarget(ingestibleInd).Thing;
                    if (thing != null && chewer.Map.physicalInteractionReservationManager.IsReservedBy(chewer, thing))
                    {
                        chewer.Map.physicalInteractionReservationManager.Release(chewer, toil.actor.CurJob, thing);
                    }
                }
            });
            toil.handlingFacing = true;
            //AddIngestionEffects(toil, chewer, ingestibleInd, eatSurfaceInd);
            return toil;
        }
        public static Toil FinalizeIngest(Pawn ingester, TargetIndex ingestibleInd)
        {
            Toil toil = new Toil();
            toil.initAction = delegate
            {
                Pawn actor = toil.actor;
                Job curJob = actor.jobs.curJob;
                Thing thing = curJob.GetTarget(ingestibleInd).Thing;
                //if (ingester.needs.mood != null && thing.def.IsNutritionGivingIngestible && thing.def.ingestible.chairSearchRadius > 10f)
                //{
                //    if (!(ingester.Position + ingester.Rotation.FacingCell).HasEatSurface(actor.Map) && ingester.GetPosture() == PawnPosture.Standing && !ingester.IsWildMan() && thing.def.ingestible.tableDesired)
                //    {
                //        ingester.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOf.AteWithoutTable);
                //    }
                //    Room room = ingester.GetRoom();
                //    if (room != null)
                //    {
                //        int scoreStageIndex = RoomStatDefOf.Impressiveness.GetScoreStageIndex(room.GetStat(RoomStatDefOf.Impressiveness));
                //        if (ThoughtDefOf.AteInImpressiveDiningRoom.stages[scoreStageIndex] != null)
                //        {
                //            ingester.needs.mood.thoughts.memories.TryGainMemory(ThoughtMaker.MakeThought(ThoughtDefOf.AteInImpressiveDiningRoom, scoreStageIndex));
                //        }
                //    }
                //}
                //float num = ingester.needs.food.NutritionWanted;
                //if (curJob.ingestTotalCount)
                //{
                //    num = thing.GetStatValue(StatDefOf.Nutrition) * (float)thing.stackCount;
                //}
                //else if (curJob.overeat)
                //{
                //    num = Mathf.Max(num, 0.75f);
                //}
                bool flag = false;
                int numTaken = curJob.count;
                if (numTaken > 0)
                {
                    if (thing.stackCount == 0)
                    {
                        Log.Error(string.Concat(thing, " stack count is 0."));
                    }
                    if (numTaken == thing.stackCount)
                    {
                        flag = true;
                    }
                    else
                    {
                        thing.SplitOff(numTaken);
                    }
                }
                if (flag)
                {
                    ingester.carryTracker?.innerContainer?.Remove(thing);
                }
                if (flag)
                {
                    thing.Destroy();
                }
                //if (!ingester.Dead)
                //{
                //    ingester.needs.food.CurLevel += num2;
                //}
                //ingester.records.AddTo(RecordDefOf.NutritionEaten, num2);
            };
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            return toil;
        }

        //private IEnumerable<Toil> PrepareToIngestToils_Dispenser()
        //{
        //    yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell).FailOnDespawnedNullOrForbidden(TargetIndex.A);
        //    yield return Toils_Ingest.TakeMealFromDispenser(TargetIndex.A, pawn);
        //    yield return Toils_Ingest.CarryIngestibleToChewSpot(pawn, TargetIndex.A).FailOnDestroyedNullOrForbidden(TargetIndex.A);
        //    yield return Toils_Ingest.FindAdjacentEatSurface(TargetIndex.B, TargetIndex.A);
        //}

        //private IEnumerable<Toil> PrepareToIngestToils_ToolUser(Toil chewToil)
        //{
        //    if (eatingFromInventory)
        //    {
        //        yield return Toils_Misc.TakeItemFromInventoryToCarrier(pawn, TargetIndex.A);
        //    }
        //    else
        //    {
        //        yield return ReserveFood();
        //        Toil gotoToPickup = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.A);
        //        yield return Toils_Jump.JumpIf(gotoToPickup, () => pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation));
        //        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch).FailOnDespawnedNullOrForbidden(TargetIndex.A);
        //        yield return Toils_Jump.Jump(chewToil);
        //        yield return gotoToPickup;
        //        yield return Toils_Ingest.PickupIngestible(TargetIndex.A, pawn);
        //    }
        //    if (job.takeExtraIngestibles > 0)
        //    {
        //        foreach (Toil item in TakeExtraIngestibles())
        //        {
        //            yield return item;
        //        }
        //    }
        //    if (!pawn.Drafted)
        //    {
        //        yield return Toils_Ingest.CarryIngestibleToChewSpot(pawn, TargetIndex.A).FailOnDestroyedOrNull(TargetIndex.A);
        //    }
        //    yield return Toils_Ingest.FindAdjacentEatSurface(TargetIndex.B, TargetIndex.A);
        //}

        private IEnumerable<Toil> PrepareToIngestToils_NonToolUser()
        {
            yield return ReserveFood();
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
        }

        //private IEnumerable<Toil> TakeExtraIngestibles()
        //{
        //    if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
        //    {
        //        yield break;
        //    }
        //    Toil reserveExtraFoodToCollect = Toils_Ingest.ReserveFoodFromStackForIngesting(TargetIndex.C);
        //    Toil findExtraFoodToCollect = ToilMaker.MakeToil("TakeExtraIngestibles");
        //    findExtraFoodToCollect.initAction = delegate
        //    {
        //        if (pawn.inventory.innerContainer.TotalStackCountOfDef(IngestibleSource.def) < job.takeExtraIngestibles)
        //        {
        //            Thing thing = GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForDef(IngestibleSource.def), PathEndMode.Touch, TraverseParms.For(pawn), 30f, (Thing x) => pawn.CanReserve(x, 10, 1) && !x.IsForbidden(pawn) && x.IsSociallyProper(pawn));
        //            if (thing != null)
        //            {
        //                job.SetTarget(TargetIndex.C, thing);
        //                JumpToToil(reserveExtraFoodToCollect);
        //            }
        //        }
        //    };
        //    findExtraFoodToCollect.defaultCompleteMode = ToilCompleteMode.Instant;
        //    yield return Toils_Jump.Jump(findExtraFoodToCollect);
        //    yield return reserveExtraFoodToCollect;
        //    yield return Toils_Goto.GotoThing(TargetIndex.C, PathEndMode.Touch);
        //    yield return Toils_Haul.TakeToInventory(TargetIndex.C, () => job.takeExtraIngestibles - pawn.inventory.innerContainer.TotalStackCountOfDef(IngestibleSource.def));
        //    yield return findExtraFoodToCollect;
        //}

        private Toil ReserveFood()
        {
            Toil toil = ToilMaker.MakeToil("ReserveFood");
            toil.initAction = delegate
            {
                if (pawn.Faction != null)
                {
                    Thing thing = job.GetTarget(TargetIndex.A).Thing;
                    if (pawn.carryTracker.CarriedThing != thing)
                    {
                        int maxAmountToPickup = FoodUtility.GetMaxAmountToPickup(thing, pawn, job.count);
                        if (maxAmountToPickup != 0)
                        {
                            if (!pawn.Reserve(thing, job, 10, maxAmountToPickup))
                            {
                                Log.Error(string.Concat("Pawn food reservation for ", pawn, " on job ", this, " failed, because it could not register food from ", thing, " - amount: ", maxAmountToPickup));
                                pawn.jobs.EndCurrentJob(JobCondition.Errored);
                            }
                            job.count = maxAmountToPickup;
                        }
                    }
                }
            };
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            toil.atomicWithPrevious = true;
            return toil;
        }

        //public override bool ModifyCarriedThingDrawPos(ref Vector3 drawPos, ref bool behind, ref bool flip)
        //{
        //    IntVec3 cell = job.GetTarget(TargetIndex.B).Cell;
        //    return ModifyCarriedThingDrawPosWorker(ref drawPos, ref behind, ref flip, cell, pawn);
        //}

        //public static bool ModifyCarriedThingDrawPosWorker(ref Vector3 drawPos, ref bool behind, ref bool flip, IntVec3 placeCell, Pawn pawn)
        //{
        //    if (pawn.pather.Moving)
        //    {
        //        return false;
        //    }
        //    Thing carriedThing = pawn.carryTracker.CarriedThing;
        //    if (carriedThing == null || !carriedThing.IngestibleNow)
        //    {
        //        return false;
        //    }
        //    if (placeCell.IsValid && placeCell.AdjacentToCardinal(pawn.Position) && placeCell.HasEatSurface(pawn.Map) && carriedThing.def.ingestible.ingestHoldUsesTable)
        //    {
        //        drawPos = new Vector3((float)placeCell.x + 0.5f, drawPos.y, (float)placeCell.z + 0.5f);
        //        return true;
        //    }
        //    if (carriedThing.def.ingestible.ingestHoldOffsetStanding != null)
        //    {
        //        HoldOffset holdOffset = carriedThing.def.ingestible.ingestHoldOffsetStanding.Pick(pawn.Rotation);
        //        if (holdOffset != null)
        //        {
        //            drawPos += holdOffset.offset;
        //            behind = holdOffset.behind;
        //            flip = holdOffset.flip;
        //            return true;
        //        }
        //    }
        //    return false;
        //}
    }

}