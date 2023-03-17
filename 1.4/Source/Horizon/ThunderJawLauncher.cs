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
    public class LauncherExtension : DefModExtension
    {
		public SimpleCurve heightCurve;//align height to distance curve to match desired visuals, should end at where it began
		public SimpleCurve distanceCurve;//adjust progress visually by eval progress fraction to curve, should end at 1
		public ThingDef aimingTargetMote;//effect on ground
		public ThingDef aimingChargeMote;//effect on projectile
		public ThingDef aimingLineMote;//line between two
		public float? aimingLineMoteFixedLength;
		public float aimingChargeMoteOffset;
	}

	[StaticConstructorOnStartup]
	public class Projectile_ThunderJawDisk : Projectile_Explosive
    {
		public LauncherExtension launcherExtension => def.GetModExtension<LauncherExtension>() ?? new LauncherExtension();

		public float Height
        {
            get
            {
				return launcherExtension.heightCurve.Evaluate(DistanceCoveredFraction);
            }
        }
		public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags, bool preventFriendlyFire = false, Thing equipment = null, ThingDef targetCoverDef = null)
		{
			base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);
			startedTick = Find.TickManager.TicksGame;
			DrawAimMote();
		}
        public override void ExposeData()
        {
            base.ExposeData();
			Scribe_Values.Look(ref startedTick, "startedTick", 0);
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				needsReInitAfterLoad = true;
			}
		}
        //use soundimpactanticipate for sound in last second, use sound ambient for constant sound

        //if (ticksToImpact == 60 && Find.TickManager.CurTimeSpeed == TimeSpeed.Normal && def.projectile.soundImpactAnticipate != null)
        //{
        //	def.projectile.soundImpactAnticipate.PlayOneShot(this);
        //}
        //else if (ambientSustainer != null)
        //{
        //	ambientSustainer.Maintain();
        //}
        public override Vector3 ExactPosition
		{
			get
			{
				Vector3 vector = (destination - origin).Yto0() * launcherExtension.distanceCurve.Evaluate(DistanceCoveredFraction);
				return origin.Yto0() + vector + Vector3.up * def.Altitude;
			}
		}

        public override void Tick()
        {
			base.Tick();
			if (needsReInitAfterLoad)
			{
				DrawAimMote(afterReload: true);
				needsReInitAfterLoad = false;
			}
			Vector3 vector = AimDir();
			float exactRotation = vector.AngleFlat();
			if (aimLineMote != null)
			{
				aimLineMote.Maintain();
				Vector3 vector2 = TargetPos();
				IntVec3 cell = vector2.ToIntVec3();
				((MoteDualAttached)aimLineMote).UpdateTargets(this, new TargetInfo(cell, Map), Vector3.up * Height, vector2 - cell.ToVector3Shifted());
			}
			if (aimTargetMote != null)
			{
				aimTargetMote.exactPosition = destination;
				aimTargetMote.exactRotation = exactRotation;
				aimTargetMote.Maintain();
			}
			if (aimChargeMote != null)
			{
				aimChargeMote.exactRotation = exactRotation;
				aimChargeMote.exactPosition = DrawPos + vector * launcherExtension.aimingChargeMoteOffset;
				aimChargeMote.Maintain();
			}
        }
        //protected float StartingTicksToImpact
        //{
        //	get
        //	{
        //		float num = (origin - destination).magnitude / def.projectile.SpeedTilesPerTick;
        //		if (num <= 0f)
        //		{
        //			num = 0.001f;
        //		}
        //		return num;
        //	}
        //}
		public override Vector3 DrawPos
        {
            get
            {
				float num = Height;
				Vector3 drawPos = base.DrawPos;
				Vector3 position = drawPos + new Vector3(0f, 0f, 1f) * num;
				return position;
			}
		}
        public override void Draw()
		{
			if (def.projectile.shadowSize > 0f)
			{
				DrawShadow(DrawPos, Height);
			}
			Graphics.DrawMesh(MeshPool.GridPlane(def.graphicData.drawSize), DrawPos, ExactRotation, DrawMat, 0);
			Comps_PostDraw();
		}
		public void DrawAimMote(bool afterReload = false)
        {
			Vector3 vector = TargetPos();
			float height = Height;
			IntVec3 cell = vector.ToIntVec3();
			if (launcherExtension.aimingLineMote != null)
			{
				aimLineMote = MoteMaker.MakeInteractionOverlay(launcherExtension.aimingLineMote, this, new TargetInfo(cell, Map), Vector3.up * height, vector - cell.ToVector3Shifted());
				if (afterReload)
				{
					aimLineMote?.ForceSpawnTick(startedTick);
				}
			}
			if (launcherExtension.aimingChargeMote != null)
			{
				aimChargeMote = MoteMaker.MakeStaticMote(DrawPos, Map, launcherExtension.aimingChargeMote, 1f, makeOffscreen: true);
				if (afterReload)
				{
					aimChargeMote?.ForceSpawnTick(startedTick);
				}
			}
			if (launcherExtension.aimingTargetMote == null)
			{
				return;
			}
			aimTargetMote = MoteMaker.MakeStaticMote(destination, Map, launcherExtension.aimingTargetMote, 1f, makeOffscreen: true);
			if (aimTargetMote != null)
			{
				aimTargetMote.exactRotation = AimDir().ToAngleFlat();
				if (afterReload)
				{
					aimTargetMote.ForceSpawnTick(startedTick);
				}
			}
		}
		private static readonly Material shadowMaterial = MaterialPool.MatFrom("Things/Skyfaller/SkyfallerShadowCircle", ShaderDatabase.Transparent);
        public Mote aimLineMote;
        public Mote aimChargeMote;
        public Mote aimTargetMote;
        public bool needsReInitAfterLoad;
        private int startedTick;

        private Vector3 TargetPos()//line ending position
		{
			Vector3 result = destination;
			if (launcherExtension.aimingLineMoteFixedLength.HasValue)
			{
				result = DrawPos + AimDir() * launcherExtension.aimingLineMoteFixedLength.Value;
			}
			return result;
		}
		private Vector3 AimDir()//angle between current position and final
		{
			Vector3 result = destination - DrawPos;
			//result.y = 0f;
			result.Normalize();
			return result;
		}

		private void DrawShadow(Vector3 drawLoc, float height)
		{
			if (!(shadowMaterial == null))
			{
				float num = def.projectile.shadowSize * Mathf.Lerp(1f, 0.6f, height);
				Vector3 s = new Vector3(num, 1f, num);
				Vector3 vector = new Vector3(0f, -0.01f, 0f);
				Matrix4x4 matrix = default(Matrix4x4);
				matrix.SetTRS(drawLoc + vector, Quaternion.identity, s);
				Graphics.DrawMesh(MeshPool.plane10, matrix, shadowMaterial, 0);
			}
		}
	}
}
