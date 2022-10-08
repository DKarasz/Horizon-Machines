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
using static Verse.DamageWorker;
using MonoMod.Utils;
using static AlienRace.AlienPartGenerator;
using AlienRace;
using Verse.Sound;

namespace Horizon
{
    public class HediffCompProperties_Refuelable : HediffCompProperties
    {
		public float fuelConsumptionRate = 1f;

		public float fuelCapacity = 2f;

		public float initialFuelPercent;

		public float autoRefuelPercent = 0.3f;

		public float fuelConsumptionPerTickInRain;

		public ThingFilter fuelFilter;

		public bool destroyOnNoFuel;

		public bool consumeFuelOnlyWhenUsed;

		public bool consumeFuelOnlyWhenPowered;

		public bool showFuelGizmo;

		public bool initialAllowAutoRefuel = true;

		public bool showAllowAutoRefuelToggle;

		public bool allowRefuelIfNotEmpty = true;

		public bool fuelIsMortarBarrel;

		public bool targetFuelLevelConfigurable;

		public float initialConfigurableTargetFuelLevel;

		public bool drawOutOfFuelOverlay = true;

		public float minimumFueledThreshold;

		public bool drawFuelGaugeInMap;

		public bool atomicFueling;

		private float fuelMultiplier = 1f;

		public bool factorByDifficulty;

		public string fuelLabel;

		public string fuelGizmoLabel;

		public string outOfFuelMessage;

		public string fuelIconPath;

		private Texture2D fuelIcon;

		public string FuelLabel
		{
			get
			{
				if (fuelLabel.NullOrEmpty())
				{
					return "Fuel".TranslateSimple();
				}
				return fuelLabel;
			}
		}

		public string FuelGizmoLabel
		{
			get
			{
				if (fuelGizmoLabel.NullOrEmpty())
				{
					return "Fuel".TranslateSimple();
				}
				return fuelGizmoLabel;
			}
		}

		public Texture2D FuelIcon
		{
			get
			{
				if (fuelIcon == null)
				{
					if (!fuelIconPath.NullOrEmpty())
					{
						fuelIcon = ContentFinder<Texture2D>.Get(fuelIconPath);
					}
					else
					{
						ThingDef thingDef = ((fuelFilter.AnyAllowedDef == null) ? ThingDefOf.Chemfuel : fuelFilter.AnyAllowedDef);
						fuelIcon = thingDef.uiIcon;
					}
				}
				return fuelIcon;
			}
		}

		public float FuelMultiplierCurrentDifficulty
		{
			get
			{
				if (factorByDifficulty && Find.Storyteller?.difficulty != null)
				{
					return fuelMultiplier / Find.Storyteller.difficulty.maintenanceCostFactor;
				}
				return fuelMultiplier;
			}
		}

		public HediffCompProperties_Refuelable()
        {
            compClass = typeof(HediffCompExplosive);
        }

		public override void ResolveReferences(HediffDef parentDef)
		{
			base.ResolveReferences(parentDef);
			fuelFilter.ResolveReferences();
		}

		public override IEnumerable<string> ConfigErrors(HediffDef parentDef)
		{
			foreach (string item in base.ConfigErrors(parentDef))
			{
				yield return item;
			}
			if (destroyOnNoFuel && initialFuelPercent <= 0f)
			{
				yield return "Refuelable component has destroyOnNoFuel, but initialFuelPercent <= 0";
			}
			if ((!consumeFuelOnlyWhenUsed || fuelConsumptionPerTickInRain > 0f) && parentDef.parent.tickerType != TickerType.Normal)
			{
				yield return $"Refuelable component set to consume fuel per tick, but parent tickertype is {parentDef.tickerType} instead of {TickerType.Normal}";
			}
		}

		//public override IEnumerable<StatDrawEntry> SpecialDisplayStats(StatRequest req)
		//{
		//	foreach (StatDrawEntry item in base.SpecialDisplayStats(req))
		//	{
		//		yield return item;
		//	}
		//	if (((ThingDef)req.Def).building.IsTurret)
		//	{
		//		TaggedString taggedString = "RearmCostExplanation".Translate();
		//		if (factorByDifficulty)
		//		{
		//			taggedString += " (" + "RearmCostExplanationDifficulty".Translate() + ")";
		//		}
		//		taggedString += ".";
		//		yield return new StatDrawEntry(StatCategoryDefOf.Building, "RearmCost".Translate(), GenLabel.ThingLabel(fuelFilter.AnyAllowedDef, null, (int)(fuelCapacity / FuelMultiplierCurrentDifficulty)).CapitalizeFirst(), taggedString, 3171);
		//		yield return new StatDrawEntry(StatCategoryDefOf.Building, "ShotsBeforeRearm".Translate(), ((int)fuelCapacity).ToString(), "ShotsBeforeRearmExplanation".Translate(), 3171);
		//	}
		//}

    }
    public class HediffCompRefuelable : HediffComp
    {

        public HediffCompProperties_Refuelable Props => (HediffCompProperties_Refuelable)props;

		private float fuel;

		private float configuredTargetFuelLevel = -1f;

		public bool allowAutoRefuel = true;

		private CompFlickable flickComp;

		public const string RefueledSignal = "Refueled";

		public const string RanOutOfFuelSignal = "RanOutOfFuel";

		private static readonly Texture2D SetTargetFuelLevelCommand = ContentFinder<Texture2D>.Get("UI/Commands/SetTargetFuelLevel");

		private static readonly Vector2 FuelBarSize = new Vector2(1f, 0.2f);

		private static readonly Material FuelBarFilledMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.6f, 0.56f, 0.13f));

		private static readonly Material FuelBarUnfilledMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.3f, 0.3f, 0.3f));

		public float TargetFuelLevel
		{
			get
			{
				if (configuredTargetFuelLevel >= 0f)
				{
					return configuredTargetFuelLevel;
				}
				if (Props.targetFuelLevelConfigurable)
				{
					return Props.initialConfigurableTargetFuelLevel;
				}
				return Props.fuelCapacity;
			}
			set
			{
				configuredTargetFuelLevel = Mathf.Clamp(value, 0f, Props.fuelCapacity);
			}
		}

		public float Fuel => fuel;

		public float FuelPercentOfTarget => fuel / TargetFuelLevel;

		public float FuelPercentOfMax => fuel / Props.fuelCapacity;

		public bool IsFull => TargetFuelLevel - fuel < 1f;

		public bool HasFuel
		{
			get
			{
				if (fuel > 0f)
				{
					return fuel >= Props.minimumFueledThreshold;
				}
				return false;
			}
		}

		private float ConsumptionRatePerTick => Props.fuelConsumptionRate / 60000f;

		public bool ShouldAutoRefuelNow
		{
			get
			{
				if (FuelPercentOfTarget <= Props.autoRefuelPercent && !IsFull && TargetFuelLevel > 0f)
				{
					return ShouldAutoRefuelNowIgnoringFuelPct;
				}
				return false;
			}
		}

		public bool ShouldAutoRefuelNowIgnoringFuelPct
		{
			get
			{
				if (!parent.pawn.IsBurning() && (flickComp == null || flickComp.SwitchIsOn) && parent.pawn.Map.designationManager.DesignationOn(parent.pawn, DesignationDefOf.Flick) == null)
				{
					return parent.pawn.Map.designationManager.DesignationOn(parent.pawn, DesignationDefOf.Deconstruct) == null;
				}
				return false;
			}
		}

		public override void CompPostMake()
		{
			allowAutoRefuel = Props.initialAllowAutoRefuel;
			fuel = Props.fuelCapacity * Props.initialFuelPercent;
			flickComp = parent.pawn.GetComp<CompFlickable>();
		}

		public override void CompExposeData()
		{
			base.CompExposeData();
			Scribe_Values.Look(ref fuel, "fuel", 0f);
			Scribe_Values.Look(ref configuredTargetFuelLevel, "configuredTargetFuelLevel", -1f);
			Scribe_Values.Look(ref allowAutoRefuel, "allowAutoRefuel", defaultValue: false);
			if (Scribe.mode == LoadSaveMode.PostLoadInit && !Props.showAllowAutoRefuelToggle)
			{
				allowAutoRefuel = Props.initialAllowAutoRefuel;
			}
		}

		public override void PostDraw()
		{
			base.PostDraw();
			if (!allowAutoRefuel)
			{
				parent.pawn.Map.overlayDrawer.DrawOverlay(parent.pawn, OverlayTypes.ForbiddenRefuel);
			}
			else if (!HasFuel && Props.drawOutOfFuelOverlay)
			{
				parent.pawn.Map.overlayDrawer.DrawOverlay(parent.pawn, OverlayTypes.OutOfFuel);
			}
			if (Props.drawFuelGaugeInMap)
			{
				GenDraw.FillableBarRequest r = default(GenDraw.FillableBarRequest);
				r.center = parent.pawn.DrawPos + Vector3.up * 0.1f;
				r.size = FuelBarSize;
				r.fillPercent = FuelPercentOfMax;
				r.filledMat = FuelBarFilledMat;
				r.unfilledMat = FuelBarUnfilledMat;
				r.margin = 0.15f;
				Rot4 rotation = parent.pawn.Rotation;
				rotation.Rotate(RotationDirection.Clockwise);
				r.rotation = rotation;
				GenDraw.DrawFillableBar(r);
			}
		}

		public override void PostDestroy(DestroyMode mode, Map previousMap)
		{
			base.PostDestroy(mode, previousMap);
			if ((!Props.fuelIsMortarBarrel || !Find.Storyteller.difficulty.classicMortars) && previousMap != null && Props.fuelFilter.AllowedDefCount == 1 && Props.initialFuelPercent == 0f)
			{
				ThingDef thingDef = Props.fuelFilter.AllowedThingDefs.First();
				int num = GenMath.RoundRandom(1f * fuel);
				while (num > 0)
				{
					Thing thing = ThingMaker.MakeThing(thingDef);
					thing.stackCount = Mathf.Min(num, thingDef.stackLimit);
					num -= thing.stackCount;
					GenPlace.TryPlaceThing(thing, parent.pawn.Position, previousMap, ThingPlaceMode.Near);
				}
			}
		}

		public override string CompInspectStringExtra()
		{
			if (Props.fuelIsMortarBarrel && Find.Storyteller.difficulty.classicMortars)
			{
				return string.Empty;
			}
			string text = Props.FuelLabel + ": " + fuel.ToStringDecimalIfSmall() + " / " + Props.fuelCapacity.ToStringDecimalIfSmall();
			if (!Props.consumeFuelOnlyWhenUsed && HasFuel)
			{
				int numTicks = (int)(fuel / Props.fuelConsumptionRate * 60000f);
				text = text + " (" + numTicks.ToStringTicksToPeriod() + ")";
			}
			if (!HasFuel && !Props.outOfFuelMessage.NullOrEmpty())
			{
				text += $"\n{Props.outOfFuelMessage} ({GetFuelCountToFullyRefuel()}x {Props.fuelFilter.AnyAllowedDef.label})";
			}
			if (Props.targetFuelLevelConfigurable)
			{
				text += "\n" + "ConfiguredTargetFuelLevel".Translate(TargetFuelLevel.ToStringDecimalIfSmall());
			}
			return text;
		}

		public override void CompPostTick(ref float severityAdjustment)
		{
			base.CompPostTick(ref severityAdjustment);
			CompPowerTrader comp = parent.GetComp<CompPowerTrader>();
			if (!Props.consumeFuelOnlyWhenUsed && (flickComp == null || flickComp.SwitchIsOn) && (!Props.consumeFuelOnlyWhenPowered || (comp != null && comp.PowerOn)))
			{
				ConsumeFuel(ConsumptionRatePerTick);
			}
			if (Props.fuelConsumptionPerTickInRain > 0f && parent.pawn.Spawned && parent.pawn.Map.weatherManager.RainRate > 0.4f && !parent.pawn.Map.roofGrid.Roofed(parent.pawn.Position))
			{
				ConsumeFuel(Props.fuelConsumptionPerTickInRain);
			}
		}

		public void ConsumeFuel(float amount)
		{
			if ((Props.fuelIsMortarBarrel && Find.Storyteller.difficulty.classicMortars) || fuel <= 0f)
			{
				return;
			}
			fuel -= amount;
			if (fuel <= 0f)
			{
				fuel = 0f;
				if (Props.destroyOnNoFuel)
				{
					parent.Destroy();
				}
				parent.BroadcastCompSignal("RanOutOfFuel");
			}
		}

		public void Refuel(List<Thing> fuelThings)
		{
			if (Props.atomicFueling && fuelThings.Sum((Thing t) => t.stackCount) < GetFuelCountToFullyRefuel())
			{
				Log.ErrorOnce("Error refueling; not enough fuel available for proper atomic refuel", 19586442);
				return;
			}
			int num = GetFuelCountToFullyRefuel();
			while (num > 0 && fuelThings.Count > 0)
			{
				Thing thing = fuelThings.Pop();
				int num2 = Mathf.Min(num, thing.stackCount);
				Refuel(num2);
				thing.SplitOff(num2).Destroy();
				num -= num2;
			}
		}

		public void Refuel(float amount)
		{
			fuel += amount * Props.FuelMultiplierCurrentDifficulty;
			if (fuel > Props.fuelCapacity)
			{
				fuel = Props.fuelCapacity;
			}
			parent.BroadcastCompSignal("Refueled");
		}

		public void Notify_UsedThisTick()
		{
			ConsumeFuel(ConsumptionRatePerTick);
		}

		public int GetFuelCountToFullyRefuel()
		{
			if (Props.atomicFueling)
			{
				return Mathf.CeilToInt(Props.fuelCapacity / Props.FuelMultiplierCurrentDifficulty);
			}
			return Mathf.Max(Mathf.CeilToInt((TargetFuelLevel - fuel) / Props.FuelMultiplierCurrentDifficulty), 1);
		}

		public override IEnumerable<Gizmo> CompGetGizmos()
		{
			if (Props.fuelIsMortarBarrel && Find.Storyteller.difficulty.classicMortars)
			{
				yield break;
			}
			if (Props.targetFuelLevelConfigurable)
			{
				Command_SetHediffTargetFuelLevel command_SetTargetFuelLevel = new Command_SetHediffTargetFuelLevel();
				command_SetTargetFuelLevel.refuelable = this;
				command_SetTargetFuelLevel.defaultLabel = "CommandSetTargetFuelLevel".Translate();
				command_SetTargetFuelLevel.defaultDesc = "CommandSetTargetFuelLevelDesc".Translate();
				command_SetTargetFuelLevel.icon = SetTargetFuelLevelCommand;
				yield return command_SetTargetFuelLevel;
			}
			if (Props.showFuelGizmo && Find.Selector.SingleSelectedThing == parent.pawn)
			{
				Gizmo_RefuelableFuelStatus gizmo_RefuelableFuelStatus = new Gizmo_RefuelableFuelStatus();
				gizmo_RefuelableFuelStatus.refuelable = this;
				yield return gizmo_RefuelableFuelStatus;
			}
			if (Props.showAllowAutoRefuelToggle)
			{
				Command_Toggle command_Toggle = new Command_Toggle();
				command_Toggle.defaultLabel = "CommandToggleAllowAutoRefuel".Translate();
				command_Toggle.defaultDesc = "CommandToggleAllowAutoRefuelDesc".Translate();
				command_Toggle.hotKey = KeyBindingDefOf.Command_ItemForbid;
				command_Toggle.icon = (allowAutoRefuel ? TexCommand.ForbidOff : TexCommand.ForbidOn);
				command_Toggle.isActive = () => allowAutoRefuel;
				command_Toggle.toggleAction = delegate
				{
					allowAutoRefuel = !allowAutoRefuel;
				};
				yield return command_Toggle;
			}
			if (Prefs.DevMode)
			{
				Command_Action command_Action = new Command_Action();
				command_Action.defaultLabel = "Debug: Set fuel to 0";
				command_Action.action = delegate
				{
					fuel = 0f;
					parent.BroadcastCompSignal("Refueled");
				};
				yield return command_Action;
				Command_Action command_Action2 = new Command_Action();
				command_Action2.defaultLabel = "Debug: Set fuel to 0.1";
				command_Action2.action = delegate
				{
					fuel = 0.1f;
					parent.BroadcastCompSignal("Refueled");
				};
				yield return command_Action2;
				Command_Action command_Action3 = new Command_Action();
				command_Action3.defaultLabel = "Debug: Set fuel to max";
				command_Action3.action = delegate
				{
					fuel = Props.fuelCapacity;
					parent.BroadcastCompSignal("Refueled");
				};
				yield return command_Action3;
			}
		}
    }
	[StaticConstructorOnStartup]
	public class Command_SetHediffTargetFuelLevel : Command
	{
		public HediffCompRefuelable refuelable;

		private List<HediffCompRefuelable> refuelables;

		public override void ProcessInput(Event ev)
		{
			base.ProcessInput(ev);
			if (refuelables == null)
			{
				refuelables = new List<HediffCompRefuelable>();
			}
			if (!refuelables.Contains(refuelable))
			{
				refuelables.Add(refuelable);
			}
			int num = int.MaxValue;
			for (int i = 0; i < refuelables.Count; i++)
			{
				if ((int)refuelables[i].Props.fuelCapacity < num)
				{
					num = (int)refuelables[i].Props.fuelCapacity;
				}
			}
			int startingValue = num / 2;
			for (int j = 0; j < refuelables.Count; j++)
			{
				if ((int)refuelables[j].TargetFuelLevel <= num)
				{
					startingValue = (int)refuelables[j].TargetFuelLevel;
					break;
				}
			}
			Func<int, string> textGetter = ((!refuelable.parent.def.building.hasFuelingPort) ? ((Func<int, string>)((int x) => "SetTargetFuelLevel".Translate(x))) : ((Func<int, string>)((int x) => "SetPodLauncherTargetFuelLevel".Translate(x, CompLaunchable.MaxLaunchDistanceAtFuelLevel(x)))));
			Dialog_Slider window = new Dialog_Slider(textGetter, 0, num, delegate (int value)
			{
				for (int k = 0; k < refuelables.Count; k++)
				{
					refuelables[k].TargetFuelLevel = value;
				}
			}, startingValue);
			Find.WindowStack.Add(window);
		}

		public override bool InheritInteractionsFrom(Gizmo other)
		{
			if (refuelables == null)
			{
				refuelables = new List<CompRefuelable>();
			}
			refuelables.Add(((Command_SetTargetFuelLevel)other).refuelable);
			return false;
		}
	}

}
