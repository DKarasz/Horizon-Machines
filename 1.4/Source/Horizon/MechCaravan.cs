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
