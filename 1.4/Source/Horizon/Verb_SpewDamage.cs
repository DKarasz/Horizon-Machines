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
	public class Verb_SpewDamage : Verb
	{
		//angle:verbprops.spraywidth
		//radius: verbProps.range
		//damType: verbProps.beamDamageDef
		//damAmount: verbProps.meleeDamageBaseAmount
		//armorPenetration: verbProps.meleeArmorPenetrationBase
		//explosionSound: verbProps.soundCast
		//postExplosionSpawnThingDef: verbProps.spawnDef
		//postExplosionSpawnChance: verbProps.beamChanceToAttachFire
		//postExplosionSpawnThingCount: 1
		//chanceToStartFire: verbProps.beamChanceToStartFire
		//damageFalloff: verbProps.canGoWild
		//propagationSpeed: verbProps.beamFullWidthRange
		//excludeRadius: verbProps.minRange
		public float spewWidth => verbProps.sprayWidth / 2;
		protected override bool TryCastShot()
		{
			if (currentTarget.HasThing && currentTarget.Thing.Map != caster.Map)
			{
				return false;
			}
			if (base.EquipmentSource != null)
			{
				base.EquipmentSource.GetComp<CompChangeableProjectile>()?.Notify_ProjectileLaunched();
				base.EquipmentSource.GetComp<CompReloadable>()?.UsedOnce();
			}
			IntVec3 position = caster.Position;
			float num = Mathf.Atan2(-(currentTarget.Cell.z - position.z), currentTarget.Cell.x - position.x) * 57.29578f;
			GenExplosion.DoExplosion(affectedAngle: new FloatRange(num - spewWidth, num + spewWidth), center: position, map: caster.MapHeld, radius: verbProps.range, damType: verbProps.beamDamageDef, instigator: caster, damAmount: verbProps.meleeDamageBaseAmount, armorPenetration: verbProps.meleeArmorPenetrationBase, explosionSound: verbProps.soundCast, weapon: EquipmentSource.def, projectile: null, intendedTarget: null, postExplosionSpawnThingDef: verbProps.spawnDef, postExplosionSpawnChance: verbProps.beamChanceToAttachFire, postExplosionSpawnThingCount: 1, postExplosionGasType: null, applyDamageToExplosionCellsNeighbors: false, preExplosionSpawnThingDef: null, preExplosionSpawnChance: 0f, preExplosionSpawnThingCount: 1, chanceToStartFire: verbProps.beamChanceToStartFire, damageFalloff: verbProps.canGoWild, direction: null, ignoredThings: null, doVisualEffects: false, propagationSpeed: verbProps.beamFullWidthRange, excludeRadius: verbProps.minRange, doSoundEffects: verbProps.soundCast!=null);
            if (verbProps.sprayEffecterDef != null)
            {
				AddEffecterToMaintain(verbProps.sprayEffecterDef.Spawn(caster.Position, currentTarget.Cell, caster.Map), caster.Position, currentTarget.Cell, 14, caster.Map);
            }
			lastShotTick = Find.TickManager.TicksGame;
			return true;
		}

		public override bool Available()
		{
			if (!base.Available())
			{
				return false;
			}
			if (CasterIsPawn)
			{
				Pawn casterPawn = CasterPawn;
				if (casterPawn.Faction != Faction.OfPlayer && casterPawn.mindState.MeleeThreatStillThreat && casterPawn.mindState.meleeThreat.Position.AdjacentTo8WayOrInside(casterPawn.Position))
				{
					return false;
				}
			}
			return true;
		}
	}
}