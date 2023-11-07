using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;


namespace Horizon
{


	[StaticConstructorOnStartup]
	public class CompProjectileInterceptorCustomDamage : CompProjectileInterceptor
	{
		private int lastInterceptTicks = -999999;

		private int nextChargeTick = -1;

		private bool shutDown;

		private StunHandler stunner;

		private Sustainer sustainer;

		private float lastInterceptAngle;

		private bool drawInterceptCone;

		private bool debugInterceptNonHostileProjectiles;

		private  Material ForceFieldMat => MaterialPool.MatFrom(Props.ForceFieldGraphic, ShaderDatabase.MoteGlow);

		private Material ForceFieldConeMat => MaterialPool.MatFrom(Props.ForceFieldConeGraphic, ShaderDatabase.MoteGlow);

		private static readonly MaterialPropertyBlock MatPropertyBlock = new MaterialPropertyBlock();

		private const float TextureActualRingSizeFactor = 1.16015625f;

		private static readonly Color InactiveColor = new Color(0.2f, 0.2f, 0.2f);

		private const int NumInactiveDots = 7;

		private Material ShieldDotMat => MaterialPool.MatFrom(Props.shieldDotGraphic, ShaderDatabase.MoteGlow);

		public new CompProperties_ProjectileInterceptorCustom Props => (CompProperties_ProjectileInterceptorCustom)props;



		public override void PostDeSpawn(Map map)
		{
			if (sustainer != null)
			{
				sustainer.End();
			}
		}

		public new bool CheckIntercept(Projectile projectile, Vector3 lastExactPos, Vector3 newExactPos)//issue, checked in projectile class, not overrideable
		{
			if (!ModLister.CheckRoyaltyOrBiotech("projectile interceptor"))
			{
				return false;
			}
			Vector3 vector = parent.Position.ToVector3Shifted();
			float num = Props.radius + projectile.def.projectile.SpeedTilesPerTick + 0.1f;
			if ((newExactPos.x - vector.x) * (newExactPos.x - vector.x) + (newExactPos.z - vector.z) * (newExactPos.z - vector.z) > num * num)
			{
				return false;
			}
			if (!Active)
			{
				return false;
			}
			if (!InterceptsProjectile(Props, projectile))
			{
				return false;
			}
			if ((projectile.Launcher == null || !projectile.Launcher.HostileTo(parent)) && !debugInterceptNonHostileProjectiles && !Props.interceptNonHostileProjectiles)
			{
				return false;
			}
			if (!Props.interceptOutgoingProjectiles && (new Vector2(vector.x, vector.z) - new Vector2(lastExactPos.x, lastExactPos.z)).sqrMagnitude <= Props.radius * Props.radius)
			{
				return false;
			}
			if (!GenGeo.IntersectLineCircleOutline(new Vector2(vector.x, vector.z), Props.radius, new Vector2(lastExactPos.x, lastExactPos.z), new Vector2(newExactPos.x, newExactPos.z)))
			{
				return false;
			}
			lastInterceptAngle = lastExactPos.AngleToFlat(parent.TrueCenter());
			lastInterceptTicks = Find.TickManager.TicksGame;
			drawInterceptCone = true;
			TriggerEffecter(newExactPos.ToIntVec3());
			if (projectile.def.projectile.damageDef == Props.empDef && Props.disarmedByEmpForTicks > 0)
			{
				BreakShieldEmp(new DamageInfo(projectile.def.projectile.damageDef, projectile.def.projectile.damageDef.defaultDamage));
				currentHitPoints = 0;
			}
			if (currentHitPoints > 0)
			{
				currentHitPoints -= projectile.DamageAmount;
				if (currentHitPoints < 0)
				{
					currentHitPoints = 0;
				}
				if (currentHitPoints == 0)
				{
					nextChargeTick = Find.TickManager.TicksGame;
					BreakShieldHitpoints(new DamageInfo(projectile.def.projectile.damageDef, projectile.def.projectile.damageDef.defaultDamage));
					return true;
				}
			}
			return true;
		}

        private void TriggerEffecter(IntVec3 pos)
        {
            Effecter effecter = new Effecter(Props.interceptEffect ?? EffecterDefOf.Interceptor_BlockedProjectile);
            effecter.Trigger(new TargetInfo(pos, parent.Map), TargetInfo.Invalid);
            effecter.Cleanup();
        }



		public override void PostDrawExtraSelectionOverlays()
		{
			if (!Active)
			{
				for (int i = 0; i < 7; i++)
				{
					Vector3 vector = new Vector3(0f, 0f, 1f).RotatedBy((float)i / 7f * 360f) * (Props.radius * 0.966f);
					Vector3 vector2 = parent.DrawPos + vector;
					Graphics.DrawMesh(MeshPool.plane10, new Vector3(vector2.x, AltitudeLayer.MoteOverhead.AltitudeFor(), vector2.z), Quaternion.identity, ShieldDotMat, 0);
				}
			}
		}


		public override void PostDraw()
		{
			Vector3 drawPos = parent.DrawPos;
			drawPos.y = AltitudeLayer.MoteOverhead.AltitudeFor();
			float currentAlpha = GetCurrentAlpha();
			if (currentAlpha > 0f)
			{
				Color value = ((!Active && Find.Selector.IsSelected(parent)) ? InactiveColor : Props.color);
				value.a *= currentAlpha;
				MatPropertyBlock.SetColor(ShaderPropertyIDs.Color, value);
				Matrix4x4 matrix = default(Matrix4x4);
				matrix.SetTRS(drawPos, Quaternion.identity, new Vector3(Props.radius * 2f * 1.16015625f, 1f, Props.radius * 2f * 1.16015625f));
				Graphics.DrawMesh(MeshPool.plane10, matrix, ForceFieldMat, 0, null, 0, MatPropertyBlock);
			}
			float currentConeAlpha_RecentlyIntercepted = GetCurrentConeAlpha_RecentlyIntercepted();
			if (currentConeAlpha_RecentlyIntercepted > 0f)
			{
				Color color = Props.color;
				color.a *= currentConeAlpha_RecentlyIntercepted;
				MatPropertyBlock.SetColor(ShaderPropertyIDs.Color, color);
				Matrix4x4 matrix2 = default(Matrix4x4);
				matrix2.SetTRS(drawPos, Quaternion.Euler(0f, lastInterceptAngle - 90f, 0f), new Vector3(Props.radius * 2f * 1.16015625f, 1f, Props.radius * 2f * 1.16015625f));
				Graphics.DrawMesh(MeshPool.plane10, matrix2, ForceFieldConeMat, 0, null, 0, MatPropertyBlock);
			}
		}

		private float GetCurrentAlpha()
		{
			return Mathf.Max(Mathf.Max(Mathf.Max(Mathf.Max(GetCurrentAlpha_Idle(), GetCurrentAlpha_Selected()), GetCurrentAlpha_RecentlyIntercepted()), GetCurrentAlpha_RecentlyActivated()), Props.minAlpha);
		}

		private float GetCurrentAlpha_Idle()
		{
			float idlePulseSpeed = Props.idlePulseSpeed;
			float minIdleAlpha = Props.minIdleAlpha;
			if (!Active)
			{
				return 0f;
			}
			if (parent.Faction == Faction.OfPlayer && !debugInterceptNonHostileProjectiles)
			{
				return 0f;
			}
			if (Find.Selector.IsSelected(parent))
			{
				return 0f;
			}
			return Mathf.Lerp(minIdleAlpha, 0.11f, (Mathf.Sin((float)(Gen.HashCombineInt(parent.thingIDNumber, 96804938) % 100) + Time.realtimeSinceStartup * idlePulseSpeed) + 1f) / 2f);
		}

		private float GetCurrentAlpha_Selected()
		{
			float num = Mathf.Max(2f, Props.idlePulseSpeed);
			if ((!Find.Selector.IsSelected(parent) && !Props.drawWithNoSelection) || stunner.Stunned || shutDown || !Active)
			{
				return 0f;
			}
			return Mathf.Lerp(0.2f, 0.62f, (Mathf.Sin((float)(Gen.HashCombineInt(parent.thingIDNumber, 35990913) % 100) + Time.realtimeSinceStartup * num) + 1f) / 2f);
		}

		private float GetCurrentAlpha_RecentlyIntercepted()
		{
			int num = Find.TickManager.TicksGame - lastInterceptTicks;
			return Mathf.Clamp01(1f - (float)num / 40f) * 0.09f;
		}

		private float GetCurrentAlpha_RecentlyActivated()
		{
			if (!Active)
			{
				return 0f;
			}
			int num = Find.TickManager.TicksGame - (lastInterceptTicks + Props.cooldownTicks);
			return Mathf.Clamp01(1f - (float)num / 50f) * 0.09f;
		}

		private float GetCurrentConeAlpha_RecentlyIntercepted()
		{
			if (!drawInterceptCone)
			{
				return 0f;
			}
			int num = Find.TickManager.TicksGame - lastInterceptTicks;
			return Mathf.Clamp01(1f - (float)num / 40f) * 0.82f;
		}


		public override void PostPreApplyDamage(DamageInfo dinfo, out bool absorbed)
		{
			absorbed = false; 
			if (dinfo.Def == Props.empDef && Props.disarmedByEmpForTicks > 0)
			{
				BreakShieldEmp(dinfo);
			}
		}

        private void BreakShieldEmp(DamageInfo dinfo)
        {
            float fTheta;
            Vector3 center;
            if (Active)
            {
                EffecterDefOf.Shield_Break.SpawnAttached(parent, parent.MapHeld, Props.radius);
                int num = Mathf.CeilToInt(Props.radius * 2f);
                fTheta = (float)Math.PI * 2f / (float)num;
                center = parent.TrueCenter();
                for (int i = 0; i < num; i++)
                {
                    FleckMaker.ConnectingLine(PosAtIndex(i), PosAtIndex((i + 1) % num), FleckDefOf.LineEMP, parent.Map, 1.5f);
                }
            }
            dinfo.SetAmount((float)Props.disarmedByEmpForTicks / 30f);
            stunner.Notify_DamageApplied(dinfo);
            Vector3 PosAtIndex(int index)
            {
                return new Vector3(Props.radius * Mathf.Cos(fTheta * (float)index) + center.x, 0f, Props.radius * Mathf.Sin(fTheta * (float)index) + center.z);
            }
        }

        private void BreakShieldHitpoints(DamageInfo dinfo)
		{
			EffecterDefOf.Shield_Break.SpawnAttached(parent, parent.MapHeld, Props.radius);
			stunner.Notify_DamageApplied(dinfo);
		}

	}

	public class CompProperties_ProjectileInterceptorCustom : CompProperties_ProjectileInterceptor
	{

		public string ForceFieldConeGraphic = "Other/ForceFieldCone";

		public string ForceFieldGraphic = "Other/ForceField";

		public string shieldDotGraphic = "Things/Mote/ShieldDownDot";

		public DamageDef empDef = DamageDefOf.EMP;

		public CompProperties_ProjectileInterceptorCustom()
		{
			compClass = typeof(CompProjectileInterceptorCustomDamage);
		}
	}

}
