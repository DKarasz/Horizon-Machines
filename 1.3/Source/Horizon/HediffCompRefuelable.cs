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
        public HediffCompProperties_Refuelable()
        {
            compClass = typeof(HediffCompExplosive);
        }
    }
    public class HediffCompRefuelable : HediffComp
    {

        public HediffCompProperties_Explosive Props => (HediffCompProperties_Explosive)props;

        public float Fuel;

        public void ConsumeFuel(float fuel)
        {
            throw new NotImplementedException();
        }
    }
}
