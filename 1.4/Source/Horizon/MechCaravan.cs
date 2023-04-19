using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Horizon
{
    [HarmonyPatch(typeof(CaravanUtility), "IsOwner")]
    public static class RimWorld_Planet_CaravanUtility_IsOwner_Patch
    {
        public static void Postfix(ref bool __result, Pawn pawn, Faction caravanFaction)
        {
            if (caravanFaction == null)
            {
                return;
            }
            if (pawn.IsColonyMech)
            {
                __result = true;
            }
        }
    }
    [HarmonyPatch(typeof(FormCaravanComp),"CanFormOrReformCaravanNow", MethodType.Getter)]
    public static class RimWorld_Planet_FormCaravanComp_CanFormOrReformCaravanNow
    {
        public static void Postfix(ref bool __result, WorldObject ___parent, FormCaravanComp __instance)
        {
            MapParent mapParent = (MapParent)___parent;
            MapPawns mapPawns = mapParent.Map.mapPawns;
            if (__result)
            {
                return;
            }
            if (!mapParent.HasMap||__instance.AnyActiveThreatNow)
            {
                return;
            }
            if (__instance.Reform)
            {
                foreach (Pawn a in mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer))
                {
                    if (a.IsColonyMechPlayerControlled && !(a.Downed || a.Dead))
                    {
                        __result = true;
                        return;
                    }
                }
            }
            return;
        }
    }

    [HarmonyPatch]
    public static class Dialog_FormCaravan_TrySend_Internal_Patch
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            var targetMethod = typeof(Dialog_FormCaravan).GetNestedTypes(AccessTools.all).SelectMany(innerType => AccessTools.GetDeclaredMethods(innerType))
                    .FirstOrDefault(method => method.Name.Contains("<TrySend>b__89") && method.ReturnType == typeof(bool) && method.GetParameters().Length == 1);
            yield return targetMethod;
        }
        public static bool Prefix(ref bool __result, Pawn pawn)
        {
            if (CaravanUtility.IsOwner(pawn, Faction.OfPlayer))
            {
                __result = !pawn.skills?.GetSkill(SkillDefOf.Social).TotallyDisabled ?? false;
                return false;
            }
            __result = false;
            return false;
        }
    }
}
