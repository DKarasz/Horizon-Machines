using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Horizon
{
    internal class bodyaddonsPatches
    {
        public static bool ResolveAllGraphicsPrefix(PawnGraphicSet __instance)
        {
            Pawn alien = __instance.pawn;
            if (alien.def is ThingDef_AlienRace alienProps)
            {
                AlienPartGenerator.AlienComp alienComp = __instance.pawn.GetComp<AlienPartGenerator.AlienComp>();

                if (alienComp != null)
                {
                    AlienPartGenerator apg = alienProps.alienRace.generalSettings.alienPartGenerator;

                    if (alienComp.fixGenderPostSpawn)
                    {
                        float? maleGenderProbability = alien.kindDef.GetModExtension<Info>()?.maleGenderProbability ?? alienProps.alienRace.generalSettings.maleGenderProbability;
                        __instance.pawn.gender = Rand.Value >= maleGenderProbability ? Gender.Female : Gender.Male;
                        __instance.pawn.Name = PawnBioAndNameGenerator.GeneratePawnName(__instance.pawn);

                        CachedData.headGraphicPath(__instance.pawn.story) = alienProps.alienRace.graphicPaths.GetCurrentGraphicPath(alien.ageTracker.CurLifeStage).head.NullOrEmpty()
                                                                                ? ""
                                                                                : apg.RandomAlienHead(alienProps.alienRace.graphicPaths.GetCurrentGraphicPath(alien.ageTracker.CurLifeStage).head,
                                                                                                      __instance.pawn);

                        alienComp.fixGenderPostSpawn = false;
                    }

                    GraphicPaths graphicPaths = alienProps.alienRace.graphicPaths.GetCurrentGraphicPath(alien.ageTracker?.CurLifeStage ?? alienProps.race.lifeStageAges.Last().def);

                    alienComp.customDrawSize = graphicPaths.customDrawSize;
                    alienComp.customHeadDrawSize = graphicPaths.customHeadDrawSize;
                    alienComp.customPortraitDrawSize = graphicPaths.customPortraitDrawSize;
                    alienComp.customPortraitHeadDrawSize = graphicPaths.customPortraitHeadDrawSize;

                    alienComp.AssignProperMeshs();

                    CachedData.headGraphicPath(alien.story) = alienComp.crownType.NullOrEmpty()
                                                                  ? apg.RandomAlienHead(graphicPaths.head, alien)
                                                                  : AlienPartGenerator.GetAlienHead(graphicPaths.head, apg.useGenderedHeads ? alien.gender.ToString() : "", alienComp.crownType);

                    string bodyMask = graphicPaths.bodyMasks.NullOrEmpty()
                                          ? string.Empty
                                          : graphicPaths.bodyMasks + ((alienComp.bodyMaskVariant >= 0
                                                                           ? alienComp.bodyMaskVariant
                                                                           : (alienComp.bodyMaskVariant =
                                                                                  Rand.Range(min: 0, graphicPaths.BodyMaskCount))) > 0
                                                                          ? alienComp.bodyMaskVariant.ToString()
                                                                          : string.Empty);

                    __instance.nakedGraphic = !graphicPaths.body.NullOrEmpty()
                                                  ? apg.GetNakedGraphic(alien.story.bodyType, ContentFinder<Texture2D>.Get(
                                                                                                                           AlienPartGenerator.GetNakedPath(alien.story.bodyType, graphicPaths.body,
                                                                                                                               apg.useGenderedBodies ? alien.gender.ToString() : "") +
                                                                                                                           "_northm", reportFailure: false) == null
                                                                                                  ? graphicPaths.skinShader?.Shader ?? ShaderDatabase.Cutout
                                                                                                  : ShaderDatabase.CutoutComplex, __instance.pawn.story.SkinColor,
                                                                        apg.SkinColor(alien, first: false), graphicPaths.body,
                                                                        alien.gender.ToString(), bodyMask)
                                                  : null;

                    __instance.rottingGraphic = !graphicPaths.body.NullOrEmpty()
                                                    ? apg.GetNakedGraphic(alien.story.bodyType, graphicPaths.skinShader?.Shader ?? ShaderDatabase.Cutout,
                                                                          PawnGraphicSet.RottingColorDefault, PawnGraphicSet.RottingColorDefault, graphicPaths.body,
                                                                          alien.gender.ToString(), bodyMask)
                                                    : null;
                    __instance.dessicatedGraphic = !graphicPaths.skeleton.NullOrEmpty()
                                                       ? GraphicDatabase
                                                       .Get<
                                                               Graphic_Multi>((graphicPaths.skeleton == GraphicPaths.VANILLA_SKELETON_PATH ? alien.story.bodyType.bodyDessicatedGraphicPath : graphicPaths.skeleton),
                                                                              ShaderDatabase.Cutout)
                                                       : null;

                    __instance.headGraphic = alien.health.hediffSet.HasHead && !alien.story.HeadGraphicPath.NullOrEmpty()
                                                 ? GraphicDatabase.Get<Graphic_Multi>(alien.story.HeadGraphicPath,
                                                                                      ContentFinder<Texture2D>.Get(alien.story.HeadGraphicPath + "_northm", reportFailure: false) == null &&
                                                                                      graphicPaths.headMasks.NullOrEmpty()
                                                                                          ? graphicPaths.skinShader?.Shader ?? ShaderDatabase.Cutout
                                                                                          : ShaderDatabase.CutoutComplex, Vector2.one, alien.story.SkinColor,
                                                                                      apg.SkinColor(alien, first: false), null,
                                                                                      graphicPaths.headMasks.NullOrEmpty()
                                                                                          ? string.Empty
                                                                                          : graphicPaths.headMasks + ((alienComp.headMaskVariant >= 0
                                                                                                                           ? alienComp.headMaskVariant
                                                                                                                           : (alienComp.headMaskVariant =
                                                                                                                                  Rand.Range(min: 0, graphicPaths.HeadMaskCount))) > 0
                                                                                                                          ? alienComp.headMaskVariant.ToString()
                                                                                                                          : string.Empty))
                                                 : null;

                    __instance.desiccatedHeadGraphic = alien.health.hediffSet.HasHead && !alien.story.HeadGraphicPath.NullOrEmpty()
                                                           ? GraphicDatabase.Get<Graphic_Multi>(alien.story.HeadGraphicPath, ShaderDatabase.Cutout, Vector2.one,
                                                                                                PawnGraphicSet.RottingColorDefault)
                                                           : null;
                    __instance.skullGraphic = alien.health.hediffSet.HasHead && !graphicPaths.skull.NullOrEmpty()
                                                  ? GraphicDatabase.Get<Graphic_Multi>(graphicPaths.skull, ShaderDatabase.Cutout, Vector2.one, Color.white)
                                                  : null;

                    if (__instance.pawn.story.hairDef != null && alienProps.alienRace.styleSettings[typeof(HairDef)].hasStyle)
                        __instance.hairGraphic = GraphicDatabase.Get<Graphic_Multi>(__instance.pawn.story.hairDef.texPath,
                                                                                    ContentFinder<Texture2D>.Get(__instance.pawn.story.hairDef.texPath + "_northm", reportFailure: false) == null
                                                                                        ? (alienProps.alienRace.styleSettings[typeof(HairDef)].shader?.Shader ?? ShaderDatabase.Transparent)
                                                                                        : ShaderDatabase.CutoutComplex, Vector2.one, alien.story.hairColor,
                                                                                    alienComp.GetChannel(channel: "hair").second);
                    __instance.headStumpGraphic = !graphicPaths.stump.NullOrEmpty()
                                                      ? GraphicDatabase.Get<Graphic_Multi>(graphicPaths.stump,
                                                                                           alien.story.SkinColor == apg.SkinColor(alien, first: false)
                                                                                               ? ShaderDatabase.Cutout
                                                                                               : ShaderDatabase.CutoutComplex, Vector2.one,
                                                                                           alien.story.SkinColor, apg.SkinColor(alien, first: false))
                                                      : null;
                    __instance.desiccatedHeadStumpGraphic = !graphicPaths.stump.NullOrEmpty()
                                                                ? GraphicDatabase.Get<Graphic_Multi>(graphicPaths.stump,
                                                                                                     ShaderDatabase.Cutout, Vector2.one,
                                                                                                     PawnGraphicSet.RottingColorDefault)
                                                                : null;

                    if (alien.style != null && ModsConfig.IdeologyActive)
                    {
                        AlienPartGenerator.ExposableValueTuple<Color, Color> tattooColor = alienComp.GetChannel("tattoo");

                        if (alien.style.FaceTattoo != null && alien.style.FaceTattoo != TattooDefOf.NoTattoo_Face)
                            __instance.faceTattooGraphic = GraphicDatabase.Get<Graphic_Multi>(alien.style.FaceTattoo.texPath,
                                                                                              (alienProps.alienRace.styleSettings[typeof(TattooDef)].shader?.Shader ??
                                                                                               ShaderDatabase.CutoutSkinOverlay),
                                                                                              Vector2.one, tattooColor.first, tattooColor.second, null, alien.story.HeadGraphicPath);
                        else
                            __instance.faceTattooGraphic = null;

                        if (alien.style.BodyTattoo != null && alien.style.BodyTattoo != TattooDefOf.NoTattoo_Body)
                            __instance.bodyTattooGraphic = GraphicDatabase.Get<Graphic_Multi>(alien.style.BodyTattoo.texPath,
                                                                                              (alienProps.alienRace.styleSettings[typeof(TattooDef)].shader?.Shader ??
                                                                                               ShaderDatabase.CutoutSkinOverlay),
                                                                                              Vector2.one, tattooColor.first, tattooColor.second, null, __instance.nakedGraphic.path);
                        else
                            __instance.bodyTattooGraphic = null;
                    }

                    if (alien.style?.beardDef != null)
                        __instance.beardGraphic = GraphicDatabase.Get<Graphic_Multi>(alien.style.beardDef.texPath,
                                                                                     (alienProps.alienRace.styleSettings[typeof(BeardDef)].shader?.Shader ?? ShaderDatabase.Transparent), Vector2.one,
                                                                                     alien.story.hairColor);

                    alienComp.OverwriteColorChannel("hair", alien.story.hairColor);
                    if (alien.Corpse?.GetRotStage() == RotStage.Rotting)
                        alienComp.OverwriteColorChannel("skin", PawnGraphicSet.RottingColorDefault);

                    alienComp.RegenerateColorChannelLinks();

                    alienComp.addonGraphics = new List<Graphic>();
                    if (alienComp.addonVariants == null)
                        alienComp.addonVariants = new List<int>();
                    int sharedIndex = 0;
                    for (int i = 0; i < apg.bodyAddons.Count; i++)
                    {
                        Graphic g = apg.bodyAddons[i].GetPath(alien, ref sharedIndex,
                                                              alienComp.addonVariants.Count > i ? (int?)alienComp.addonVariants[i] : null);
                        alienComp.addonGraphics.Add(g);
                        if (alienComp.addonVariants.Count <= i)
                            alienComp.addonVariants.Add(sharedIndex);
                    }

                    __instance.ResolveApparelGraphics();

                    PortraitsCache.SetDirty(alien);
                    GlobalTextureAtlasManager.TryMarkPawnFrameSetDirty(alien);

                    return false;
                }
            }

            return true;
        }

        public static IEnumerable<CodeInstruction> RenderPawnInternalTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            FieldInfo humanlikeHeadInfo = AccessTools.Field(typeof(MeshPool), nameof(MeshPool.humanlikeHeadSet));
            MethodInfo drawHeadHairInfo = AccessTools.Method(typeof(PawnRenderer), "DrawHeadHair");
            MethodInfo flagSetInfo = AccessTools.Method(typeof(PawnRenderFlagsExtension), nameof(PawnRenderFlagsExtension.FlagSet));

            List<CodeInstruction> instructionList = instructions.ToList();

            for (int i = 0; i < instructionList.Count; i++)
            {
                CodeInstruction instruction = instructionList[i];


                if (instruction.OperandIs(humanlikeHeadInfo))
                {
                    instructionList.RemoveRange(i, count: 2);
                    yield return new CodeInstruction(OpCodes.Ldarg_S, operand: 6); // renderFlags
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(PawnRenderer), name: "pawn"));
                    yield return new CodeInstruction(OpCodes.Ldloc_S, operand: 7); //headfacing
                    yield return new CodeInstruction(OpCodes.Ldc_I4_0);
                    instruction = new CodeInstruction(OpCodes.Call, AccessTools.Method(patchType, nameof(GetPawnMesh)));
                }
                else if (i > 6 && instructionList[i - 2].OperandIs(drawHeadHairInfo) && instructionList[i + 1].OperandIs(flagSetInfo))
                {
                    yield return new CodeInstruction(OpCodes.Dup); // renderFlags
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Ldloc_S, 6); //b (aka headoffset)
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(PawnRenderer), name: "pawn"));
                    yield return new CodeInstruction(OpCodes.Ldloc_0);             // quat
                    yield return new CodeInstruction(OpCodes.Ldarg_S, operand: 4); // bodyfacing
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(patchType, nameof(DrawAddons)));
                }

                yield return instruction;
            }
        }

        public static void DrawAddons(PawnRenderFlags renderFlags, Vector3 vector, Vector3 headOffset, Pawn pawn, Quaternion quat, Rot4 rotation)
        {
            if (!(pawn.def is ThingDef_AlienRace alienProps)) return;

            List<AlienPartGenerator.BodyAddon> addons = alienProps.alienRace.generalSettings.alienPartGenerator.bodyAddons;
            AlienPartGenerator.AlienComp alienComp = pawn.GetComp<AlienPartGenerator.AlienComp>();

            if (alienComp != null)
            {
                bool isPortrait = renderFlags.FlagSet(PawnRenderFlags.Portrait);
                bool isInvisible = renderFlags.FlagSet(PawnRenderFlags.Invisible);

                for (int i = 0; i < addons.Count; i++)
                {
                    AlienPartGenerator.BodyAddon ba = addons[i];
                    if (!ba.CanDrawAddon(pawn)) continue;

                    Vector3 offsetVector = (ba.defaultOffsets.GetOffset(rotation)?.GetOffset(isPortrait, pawn.story.bodyType, alienComp.crownType) ?? Vector3.zero) +
                                           (ba.offsets.GetOffset(rotation)?.GetOffset(isPortrait, pawn.story.bodyType, alienComp.crownType) ?? Vector3.zero);

                    //Defaults for tails 
                    //south 0.42f, -0.3f, -0.22f
                    //north     0f,  0.3f, -0.55f
                    //east -0.42f, -0.3f, -0.22f   

                    offsetVector.y = ba.inFrontOfBody ? 0.3f + offsetVector.y : -0.3f - offsetVector.y;

                    float num = ba.angle;

                    if (rotation == Rot4.North)
                    {
                        if (ba.layerInvert)
                            offsetVector.y = -offsetVector.y;
                        num = 0;
                    }

                    if (rotation == Rot4.East)
                    {
                        num = -num; //Angle
                        offsetVector.x = -offsetVector.x;
                    }

                    Graphic addonGraphic = alienComp.addonGraphics[i];
                    addonGraphic.drawSize = (isPortrait && ba.drawSizePortrait != Vector2.zero ? ba.drawSizePortrait : ba.drawSize) *
                                            (ba.scaleWithPawnDrawsize ?
                                                 ba.alignWithHead ?
                                                     isPortrait ?
                                                         alienComp.customPortraitHeadDrawSize :
                                                         alienComp.customHeadDrawSize :
                                                     isPortrait ?
                                                         alienComp.customPortraitDrawSize :
                                                         alienComp.customDrawSize
                                                 : Vector2.one) * 1.5f;

                    Material mat = addonGraphic.MatAt(rotation);
                    if (!isPortrait && isInvisible)
                        mat = InvisibilityMatPool.GetInvisibleMat(mat);

                    DrawAddonsFinalHook(pawn, ba, ref addonGraphic, ref offsetVector, ref num, ref mat);

                    //                                                                                   Angle calculation to not pick the shortest, taken from Quaternion.Angle and modified
                    GenDraw.DrawMeshNowOrLater(
                                               addonGraphic.MeshAt(rotation),
                                               vector + (ba.alignWithHead ? headOffset : Vector3.zero) + offsetVector.RotatedBy(Mathf.Acos(Quaternion.Dot(Quaternion.identity, quat)) * 2f * 57.29578f),
                                               Quaternion.AngleAxis(num, Vector3.up) * quat, mat, renderFlags.FlagSet(PawnRenderFlags.DrawNow));
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void DrawAddonsFinalHook(Pawn pawn, AlienPartGenerator.BodyAddon addon, ref Graphic graphic, ref Vector3 offsetVector, ref float angle, ref Material mat)
        {

        }


    }






}
