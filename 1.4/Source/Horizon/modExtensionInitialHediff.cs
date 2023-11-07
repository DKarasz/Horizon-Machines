using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Horizon
{
    [HarmonyPatch(typeof(PawnGenerator),"GenerateInitialHediffs")]
    public static class PawnGenerator_GenerateInitialHediffs_Patch
    {
        public static void Postfix(ref Pawn pawn)
        {
            pawn.kindDef.GetModExtension<InitialHediff>()?.assignHediffs(pawn, false);
        }
    }


    [HarmonyPatch(typeof(Pawn_HealthTracker), "RemoveAllHediffs")]
    public static class Pawn_HealthTracker_RemoveAllHediffs_Patch
    {
        public static void Postfix(ref Pawn ___pawn)
        {
            ___pawn.kindDef.GetModExtension<InitialHediff>()?.assignHediffs(___pawn, true);
        }
    }
    public class InitialHediff : DefModExtension
    {
        List<HediffComposite> hediffs;

        public void assignHediffs(Pawn pawn, bool reset)
        {
            bool link = true;
            foreach(HediffComposite hediff in hediffs)
            {
                if (!hediff.restoreOnReset && reset)
                {
                    link = false;
                    continue;
                }
                if (hediff.linkChanceWithPrevious && !link)
                {
                    continue;
                }
                if (hediff.linkChanceWithPrevious || Rand.Chance(hediff.chance))
                {
                    link = true;
                    if (!hediff.customPartLabel.NullOrEmpty())
                    {
                        foreach (string custompart in hediff.customPartLabel)
                        {
                            IEnumerable<BodyPartRecord> source = from x in pawn.health.hediffSet.GetNotMissingParts()
                                                                 where x.customLabel == custompart
                                                                 select x;
                            foreach (BodyPartRecord record in source)
                            {
                                Hediff hediffPart = HediffMaker.MakeHediff(hediff.def, pawn);
                                hediffPart.Part = record;
                                pawn.health.AddHediff(hediffPart);
                            }
                        }
                    }
                    else if (!hediff.bodyPart.NullOrEmpty())
                    {
                        foreach(BodyPartDef part in hediff.bodyPart)
                        {
                            IEnumerable<BodyPartRecord> source = from x in pawn.health.hediffSet.GetNotMissingParts()
                                                                 where x.def == part
                                                                 select x;
                            foreach(BodyPartRecord record in source)
                            {
                                Hediff hediffPart = HediffMaker.MakeHediff(hediff.def, pawn);
                                hediffPart.Part = record;
                                pawn.health.AddHediff(hediffPart);
                            }
                        }
                    }
                    else
                    {
                        Hediff hediffPart = HediffMaker.MakeHediff(hediff.def, pawn);
                        pawn.health.AddHediff(hediffPart);
                    }
                }
                else
                {
                    link = false;
                }
            }
        }
    }
    public class HediffComposite
    {
        public HediffDef def;
        public float chance = 1f;
        public bool linkChanceWithPrevious = false;
        public bool restoreOnReset = true;
        public List<BodyPartDef> bodyPart;
        public List<string> customPartLabel;
        public float severity = 0.5f;
    }

}
