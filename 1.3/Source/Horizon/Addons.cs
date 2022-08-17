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
    //addon code
    [HarmonyPatch(typeof(HarmonyPatches), nameof(HarmonyPatches.DrawAddons))]
    public static class HarmonyPatches_DrawAddons_Patch
    {
        public static void Postfix(PawnRenderFlags renderFlags, Vector3 vector, Vector3 headOffset, Pawn pawn, Quaternion quat, Rot4 rotation)
        {
            var extension = pawn.def.GetModExtension<AnimalBodyAddons>();
            if (extension == null)
            {
                return;
            }
            List<AlienPartGenerator.BodyAddon> bodyAddons = extension.bodyAddons;
            var comp = pawn.GetComp<AnimalComp>();
            if (comp == null)
            {
                return;
            }
            bool flag = renderFlags.FlagSet(PawnRenderFlags.Portrait);
            bool flag2 = renderFlags.FlagSet(PawnRenderFlags.Invisible);
            for (int i = 0; i < bodyAddons.Count; i++)
            {
                AlienPartGenerator.BodyAddon bodyAddon = bodyAddons[i];
                if (!bodyAddon.CanDrawAddon(pawn))
                {
                    continue;
                }
                Vector3 v = (bodyAddon.defaultOffsets.GetOffset(rotation)?.GetOffset(flag, BodyTypeDefOf.Male, "") ?? Vector3.zero) + (bodyAddon.offsets.GetOffset(rotation)?.GetOffset(flag, BodyTypeDefOf.Male, "") ?? Vector3.zero);
                v.y = (bodyAddon.inFrontOfBody ? (0.3f + v.y) : (-0.3f - v.y));
                float num = bodyAddon.angle;
                if (rotation == Rot4.North)
                {
                    if (bodyAddon.layerInvert)
                    {
                        v.y = 0f - v.y;
                    }
                    num = 0f;
                }
                if (rotation == Rot4.East)
                {
                    num = 0f - num;
                    v.x = 0f - v.x;
                }
                Graphic graphic = comp.addonGraphics[i];
                graphic.drawSize = ((flag && bodyAddon.drawSizePortrait != Vector2.zero) ? bodyAddon.drawSizePortrait : bodyAddon.drawSize) * ((!bodyAddon.scaleWithPawnDrawsize) ? Vector2.one : ((!bodyAddon.alignWithHead) ? (flag ? comp.customPortraitDrawSize : comp.customDrawSize) : (flag ? comp.customPortraitHeadDrawSize : comp.customHeadDrawSize))) * 1.5f;
                Material material = graphic.MatAt(rotation);
                if (!flag && flag2)
                {
                    material = InvisibilityMatPool.GetInvisibleMat(material);
                }
                GenDraw.DrawMeshNowOrLater(graphic.MeshAt(rotation), vector + (bodyAddon.alignWithHead ? headOffset : Vector3.zero) + v.RotatedBy(Mathf.Acos(Quaternion.Dot(Quaternion.identity, quat)) * 2f * 57.29578f), Quaternion.AngleAxis(num, Vector3.up) * quat, material, renderFlags.FlagSet(PawnRenderFlags.DrawNow));
            }
        }
    }

    [HarmonyPatch(typeof(HarmonyPatches), nameof(HarmonyPatches.ResolveAllGraphicsPrefix))]
    public static class HarmonyPatches_ResolveAllGraphicsPrefix_Patch
    {
        public static void Prefix(PawnGraphicSet __0)
        {
            var pawn = __0.pawn;
            if (!(pawn.def is AlienRace.ThingDef_AlienRace))
            {
                var comp = pawn.GetComp<AnimalComp>();
                if (comp != null)
                {
                    var extension = pawn.def.GetModExtension<AnimalBodyAddons>();
                    if (extension != null)
                    {
                        comp.addonGraphics = new List<Graphic>();
                        if (comp.addonVariants == null)
                        {
                            comp.addonVariants = new List<int>();
                        }
                        int sharedIndex = 0;
                        for (int i = 0; i < extension.bodyAddons.Count; i++)
                        {
                            Graphic path = extension.bodyAddons[i].GetPath(pawn, ref sharedIndex, (comp.addonVariants.Count > i) ? new int?(comp.addonVariants[i]) : null);
                            comp.addonGraphics.Add(path);
                            if (comp.addonVariants.Count <= i)
                            {
                                comp.addonVariants.Add(sharedIndex);
                            }
                        }
                    }
                }
            }
        }
    }

    [StaticConstructorOnStartup]
    public static class HARHandler
    {
        static HARHandler()
        {
            foreach (var def in DefDatabase<ThingDef>.AllDefs)
            {
                var extension = def.GetModExtension<AnimalBodyAddons>();
                if (extension != null)
                {
                    extension.GenerateMeshsAndMeshPools(def);
                    def.comps.Add(new CompProperties(typeof(AnimalComp)));
                }
            }
        }
    }

    public class AnimalComp : ThingComp
    {
        public List<Graphic> addonGraphics;
        public List<int> addonVariants;
        public Vector2 customDrawSize = Vector2.one;

        public Vector2 customHeadDrawSize = Vector2.one;

        public Vector2 customPortraitDrawSize = Vector2.one;

        public Vector2 customPortraitHeadDrawSize = Vector2.one;
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref addonVariants, "addonVariants", LookMode.Undefined);
        }
    }
    public class AnimalBodyAddons : DefModExtension
    {
        public List<BodyAddon> bodyAddons = new List<BodyAddon>();
        public List<OffsetNamed> offsetDefaults = new List<OffsetNamed>();
        public void GenerateMeshsAndMeshPools(ThingDef def)
        {
            offsetDefaults.Add(new OffsetNamed
            {
                name = "Center",
                offsets = new BodyAddonOffsets()
            });
            offsetDefaults.Add(new OffsetNamed
            {
                name = "Tail",
                offsets = new BodyAddonOffsets
                {
                    south = new RotationOffset
                    {
                        offset = new Vector2(0.42f, -0.22f)
                    },
                    north = new RotationOffset
                    {
                        offset = new Vector2(0f, -0.55f)
                    },
                    east = new RotationOffset
                    {
                        offset = new Vector2(0.42f, -0.22f)
                    },
                    west = new RotationOffset
                    {
                        offset = new Vector2(0.42f, -0.22f)
                    }
                }
            });
            offsetDefaults.Add(new OffsetNamed
            {
                name = "Head",
                offsets = new BodyAddonOffsets
                {
                    south = new RotationOffset
                    {
                        offset = new Vector2(0f, 0.5f)
                    },
                    north = new RotationOffset
                    {
                        offset = new Vector2(0f, 0.35f)
                    },
                    east = new RotationOffset
                    {
                        offset = new Vector2(-0.07f, 0.5f)
                    },
                    west = new RotationOffset
                    {
                        offset = new Vector2(-0.07f, 0.5f)
                    }
                }
            });
            new AlienRace.BodyAddonSupport.DefaultGraphicsLoader().LoadAllGraphics(def.defName, offsetDefaults, bodyAddons);
        }
    }

}
