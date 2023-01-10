using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Horizon
{

    //public class ProcessComposite
    //{
    //    public ThingDef Input;//
    //    public ThingDef Output;
    //    public int inputCount;//
    //    public int outputCount;
    //    public bool consumePollution = false;//
    //}
    //public class NeedComposite
    //{
    //    public NeedDef need;
    //    public float amount;
    //}
    public class CompProperties_DigPeriodicallyNeed : CompProperties
    {
        //A comp class that just makes an animal dig a resource every ticksToDig ticks, produces items, chunks, or corpses, can be specified terrain

        //check if needs are met
        public List<NeedComposite> inputNeed = null;
        //check if needs have room
        public List<NeedComposite> outputNeed = null;
        public bool ignoreOverflow = false;
        //input/output combos
        public List<ProcessComposite> thingToDig = null;

        //how long the action takes to do
        //public int ticksToDig = 600;
        public bool onlyWhenTamed = false;
        //Should items be spawned forbidden?
        public bool spawnForbidden = false;

        //Dig biome rocks. Animal will only dig rocks found on this biome, ignoring customThingToDig
        public bool digBiomeRocks = false;
        //If digBiomeRocks is true, do we also go further and turn those into bricks?
        public bool digBiomeBricks = false;
        public int customAmountToDigIfRocksOrBricks = 1;
        
        //Is the result a corpse? If so, spawn a pawn, and kill it
        public bool resultIsCorpse = false;
        //Frostmites dig for dead wildmen
        public List<PawnKindDef> digPawnKind = null;

        //timeToDig has a misleading name. It is a minimum counter. The user will not dig if less than timeToDig ticks have passed.
        //This is done to avoid an animal digging again if it's still hungry.
        public int timeToDig = 40000;
        public int timeDelay = 600;
        
        //A list of acceptable terrains can be specified
        public List<string> acceptedTerrains = null;
        public List<TerrainAffordanceDef> allowedAffordances = null;
        //Should the animal dig for items even if it's not hungry, every timeToDigForced ticks?
        public bool digAnywayEveryXTicks = true;
        public int timeToDigForced = 120000;


        //Dig only if during growing season
        public bool digOnlyOnGrowingSeason = false;
        public int minTemperature = 0;
        public int maxTemperature = 58;

        public CompProperties_DigPeriodicallyNeed()
        {
            this.compClass = typeof(CompDigPeriodicallyNeed);
        }
    }

    public class CompDigPeriodicallyNeed : ThingComp
    {
        public int stopdiggingcounter = 0;

        private Effecter effecter;

        public CompProperties_DigPeriodicallyNeed Props => (CompProperties_DigPeriodicallyNeed)this.props;

        public override void CompTick()
        {
            base.CompTick();
            Pawn pawn = this.parent as Pawn;
            if (canDoProcess(pawn))
            {
                makeProduct(pawn);
            }
        }

        public void makeProduct(Pawn pawn)
        {
            Thing newcorpse;
            ThingDef newThing;
            if (Props.resultIsCorpse)
            {
                PawnKindDef wildman = Props.digPawnKind.RandomElement();
                Pawn newPawn;
                if (wildman == null)
                {
                    wildman = PawnKindDef.Named("WildMan");
                }
                Faction faction = FactionUtility.DefaultFactionFrom(wildman.defaultFactionType);
                newPawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(wildman));
                newPawn.Kill(null, null);
                IntVec3 near = CellFinder.StandableCellNear(this.parent.Position, this.parent.Map, 1f);
                newcorpse = GenSpawn.Spawn(newPawn, near, pawn.Map, WipeMode.Vanish);
            }
            else if (Props.digBiomeRocks)
            {
                IEnumerable<ThingDef> rocksInThisBiome = Find.World.NaturalRockTypesIn(this.parent.Map.Tile);
                if (!Props.digBiomeBricks)
                {
                    newThing = Find.World.NaturalRockTypesIn(this.parent.Map.Tile).RandomElementWithFallback().building.mineableThing;
                }
                else
                {
                    newThing = Find.World.NaturalRockTypesIn(this.parent.Map.Tile).RandomElementWithFallback().building.mineableThing.butcherProducts.FirstOrFallback().thingDef;
                }
                newcorpse = GenSpawn.Spawn(newThing, pawn.Position, pawn.Map, WipeMode.Vanish);
                newcorpse.stackCount = Props.customAmountToDigIfRocksOrBricks;
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

        public bool canDoProcess(Pawn pawn)
        {
            if (HorizonFrameworkSettings.flagDigPeriodicallyNeed && (pawn.Map != null) && (pawn.Awake()) && 
                !pawn.Downed && !pawn.Dead && (!Props.onlyWhenTamed || (Props.onlyWhenTamed && pawn.Faction != null && pawn.Faction.IsPlayer)))
            {
                if ((!Props.digOnlyOnGrowingSeason || (Props.digOnlyOnGrowingSeason && 
                    (pawn.Map.mapTemperature.OutdoorTemp > Props.minTemperature && pawn.Map.mapTemperature.OutdoorTemp < Props.maxTemperature))) &&
                    ((pawn.needs.food?.CurLevelPercentage < pawn.needs.food?.PercentageThreshHungry) ||
                    (Props.digAnywayEveryXTicks && this.parent.IsHashIntervalTick(Props.timeToDigForced))))
                {
                    if (stopdiggingcounter > 0)
                    {
                        stopdiggingcounter--;
                        return false;
                    }
                    foreach (NeedComposite needComp in Props.inputNeed)
                    {
                        Need need = pawn.needs.TryGetNeed(needComp.need);
                        if (need == null || need.CurLevel < needComp.amount)
                        {
                            stopdiggingcounter = Props.timeDelay;
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
                                stopdiggingcounter = Props.timeDelay;
                                return false;
                            }
                        }
                    }
                    if (Props.allowedAffordances.NullOrEmpty() || pawn.Position.GetTerrain(pawn.Map).affordances.SharesElementWith(Props.allowedAffordances))
                    {
                        if (Props.acceptedTerrains != null)
                        {
                            if (Props.acceptedTerrains.Contains(pawn.Position.GetTerrain(pawn.Map).defName))
                            {
                                stopdiggingcounter = Props.timeToDig;
                                return true;
                            }
                            stopdiggingcounter = Props.timeDelay;
                            return false;
                        }
                        stopdiggingcounter = Props.timeToDig;
                        return true;
                    }
                }
            }
            return false;
        }

    }
}
