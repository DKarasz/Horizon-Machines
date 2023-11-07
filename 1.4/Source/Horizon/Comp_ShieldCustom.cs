using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Horizon
{
	public class CompShieldCustom : CompShield
	{
		private int lastAbsorbDamageTick = -9999;

		private Vector3 impactAngleVect;
		public new ShieldState ShieldState
		{
			get
			{
				if (parent is Pawn p && (p.IsCharging() || p.IsSelfShutdown()))
				{
					return ShieldState.Disabled;
				}
				CompCanBeDormant comp = parent.GetComp<CompCanBeDormant>();
				if (comp != null && !comp.Awake)
				{
					return ShieldState.Disabled;
				}
				if(Props.linkedBodyPartLabel != null && PawnOwner.health.hediffSet.GetNotMissingParts()?.FirstOrDefault(bpr => bpr.untranslatedCustomLabel == Props.linkedBodyPartLabel) == null)
                {
					return ShieldState.Disabled;
                }
				if (ticksToReset <= 0)
				{
					return ShieldState.Active;
				}
				return ShieldState.Resetting;
			}
		}
		public Material BubbleMat
		{
			get
			{
                if (Props.graphicData != null)
                {
					return Props.graphicData.Graphic.MatAt(PawnOwner.Rotation);

				}
				return MaterialPool.MatFrom(Props.BubbleGraphic, ShaderDatabase.Transparent);
			}
		}

		public new CompProperties_ShieldCustom Props => (CompProperties_ShieldCustom)props;

		public override void PostPreApplyDamage(DamageInfo dinfo, out bool absorbed)
		{
			absorbed = false;
			if (ShieldState != 0 || PawnOwner == null)
			{
				return;
			}
			if (!isShieldedFace(dinfo))
            {
				return;
            }
			if (dinfo.Def == Props.empDef)
			{
				energy = 0f;
				Break();
			}
			else if (dinfo.Def.isRanged || dinfo.Def.isExplosive || Props.BlocksMelee)
			{
				energy -= dinfo.Amount * Props.energyLossPerDamage;
				if (energy < 0f)
				{
					Break();
				}
				else
				{
					AbsorbedDamage(dinfo);
				}
				absorbed = true;
			}
		}

        public bool isShieldedFace(DamageInfo dinfo)
        {
			RotationDirection rot = getdirection(dinfo.Angle, PawnOwner);
            switch (rot)
            {
				case RotationDirection.Clockwise:
					return Props.BlocksRight;
				case RotationDirection.Counterclockwise:
					return Props.BlocksLeft;
				case RotationDirection.None:
					return Props.BlocksBack;
				case RotationDirection.Opposite:
					return Props.BlocksFront;
				default:
					return true;
			}
        }

        public static RotationDirection getdirection(float angle, Thing target)
		{
			if (angle == -500f)
			{
				return RotationDirection.Opposite;
			}
			Rot4 c;
			if (target is Pawn && (target as Pawn).Downed)
			{
				return RotationDirection.Opposite;
			}
			c = Rot4.FromAngleFlat(angle);
			Rot4 t = target.Rotation;
			return Rot4.GetRelativeRotation(c, t);//none=rear, opposite=front, clockwise=right, counterclockwise=left
		}

		private void AbsorbedDamage(DamageInfo dinfo)
		{
			SoundDefOf.EnergyShield_AbsorbDamage.PlayOneShot(new TargetInfo(PawnOwner.Position, PawnOwner.Map));
			impactAngleVect = Vector3Utility.HorizontalVectorFromAngle(dinfo.Angle);
			Vector3 loc = PawnOwner.TrueCenter() + impactAngleVect.RotatedBy(180f) * 0.5f;
			float num = Mathf.Min(10f, 2f + dinfo.Amount / 10f);
			FleckMaker.Static(loc, PawnOwner.Map, FleckDefOf.ExplosionFlash, num);
			int num2 = (int)num;
			for (int i = 0; i < num2; i++)
			{
				FleckMaker.ThrowDustPuff(loc, PawnOwner.Map, Rand.Range(0.8f, 1.2f));
			}
			lastAbsorbDamageTick = Find.TickManager.TicksGame;
			KeepDisplaying();
		}

		private void Break()
		{
			float scale = Mathf.Lerp(Props.minDrawSize, Props.maxDrawSize, energy);
			EffecterDefOf.Shield_Break.SpawnAttached(parent, parent.MapHeld, scale);
			FleckMaker.Static(PawnOwner.TrueCenter(), PawnOwner.Map, FleckDefOf.ExplosionFlash, 12f);
			for (int i = 0; i < 6; i++)
			{
				FleckMaker.ThrowDustPuff(PawnOwner.TrueCenter() + Vector3Utility.HorizontalVectorFromAngle(Rand.Range(0, 360)) * Rand.Range(0.3f, 0.6f), PawnOwner.Map, Rand.Range(0.8f, 1.2f));
			}
			energy = 0f;
			ticksToReset = Props.startingTicksToReset;
		}


		public override void CompDrawWornExtras()
		{
			base.CompDrawWornExtras();
			if (IsApparel)
			{
				Draw();
			}
		}

		public override void PostDraw()
		{
			if (!IsApparel)
			{
				Draw();
			}
		}

		public virtual void Draw()
		{
			if (ShieldState == ShieldState.Active && ShouldDisplay)
			{
				float num = Mathf.Lerp(Props.minDrawSize, Props.maxDrawSize, energy);
				Vector3 drawPos = PawnOwner.Drawer.DrawPos + (Props.graphicData?.DrawOffsetForRot(PawnOwner.Rotation) ?? Vector3.zero) ;
				//drawPos.y = AltitudeLayer.MoteOverhead.AltitudeFor();
				int num2 = Find.TickManager.TicksGame - lastAbsorbDamageTick;
				if (num2 < 8)
				{
					float num3 = (float)(8 - num2) / 8f * 0.05f;
					drawPos += impactAngleVect * num3;
					num -= num3;
				}
				float angle = Rand.Range(0, 360);
				Vector3 s = new Vector3(num, 1f, num);
				Matrix4x4 matrix = default(Matrix4x4);
				matrix.SetTRS(drawPos, Quaternion.AngleAxis(angle, Vector3.up), s);
				Graphics.DrawMesh(MeshPool.plane10, matrix, BubbleMat, 0);
				//DrawShieldGraphic();
			}
		}

        //public void DrawShieldGraphic()
        //{
        //	Mesh mesh3 = graphics.HairMeshSet.MeshAt(headFacing);
        //	if (!apparelRecord.sourceApparel.def.apparel.hatRenderedFrontOfFace)
        //	{
        //		Material material3 = apparelRecord.graphic.MatAt(bodyFacing);
        //		material3 = (flags.FlagSet(PawnRenderFlags.Cache) ? material3 : OverrideMaterialIfNeeded(material3, pawn, flags.FlagSet(PawnRenderFlags.Portrait)));
        //		GenDraw.DrawMeshNowOrLater(mesh3, onHeadLoc, quat, material3, flags.FlagSet(PawnRenderFlags.DrawNow));
        //	}
        //	else
        //	{
        //		Material material4 = apparelRecord.graphic.MatAt(bodyFacing);
        //		material4 = (flags.FlagSet(PawnRenderFlags.Cache) ? material4 : OverrideMaterialIfNeeded(material4, pawn, flags.FlagSet(PawnRenderFlags.Portrait)));
        //		Vector3 loc3 = rootLoc + headOffset;
        //		if (apparelRecord.sourceApparel.def.apparel.hatRenderedBehindHead)
        //		{
        //			loc3.y += 0.0221660212f;
        //		}
        //		else
        //		{
        //			loc3.y += ((bodyFacing == Rot4.North && !apparelRecord.sourceApparel.def.apparel.hatRenderedAboveBody) ? 0.00289575267f : 0.03185328f);
        //		}
        //		GenDraw.DrawMeshNowOrLater(mesh3, loc3, quat, material4, flags.FlagSet(PawnRenderFlags.DrawNow));
        //	}
        //}
    }
    public class CompProperties_ShieldCustom : CompProperties_Shield
	{
		public GraphicData graphicData;

		public string BubbleGraphic = "Other/ShieldBubble";

		public DamageDef empDef = DamageDefOf.EMP;

		public bool BlocksMelee = false;

		public bool BlocksFront = true;

		public bool BlocksLeft = true;

		public bool BlocksRight = true;

		public bool BlocksBack = true;

		public string linkedBodyPartLabel;

		public CompProperties_ShieldCustom()
		{
			compClass = typeof(CompShieldCustom);
		}
	}
}
