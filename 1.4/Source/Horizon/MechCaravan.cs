using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace Horizon
{
    //paramedic patch
    [HarmonyPatch(typeof(CaravanTendUtility),"IsValidDoctorFor")]
    public static class CaravanTendUtility_IsValidDoctorFor_Patch
    {
        public static bool Prefix(ref bool __result, Pawn doctor, Pawn patient, Caravan caravan)
        {
            if (doctor.IsColonyMech)
            {
                //Log.Message("checking mech");
                if (doctor == patient || doctor.Downed || doctor.InMentalState || doctor.WorkTypeIsDisabled(WorkTypeDefOf.Doctor))
                {
                    return true;
                }
                __result = true;
                //Log.Message("valid");
                return false;
            }
            return true;
        }
    }

    ////[HarmonyPatch(typeof(CaravanTendUtility), "FindBestDoctorFor")]
    //public static class CaravanTendUtility_FindBestDoctorFor_Patch
    //{
    //    public static void Postfix(ref Pawn __result, Caravan caravan, Pawn patient)
    //    {
    //        if(__result == null)
    //        {
    //            //Log.Message("no normal doctor");
    //            bool temp = true;
    //            List<Pawn> pawnsListForReading = caravan.PawnsListForReading;
    //            for (int i = 0; i < pawnsListForReading.Count; i++)
    //            {
    //                Pawn pawn = pawnsListForReading[i];
    //                if (!CaravanTendUtility_IsValidDoctorFor_Patch.Prefix(ref temp, pawn, patient, caravan))
    //                {
    //                    //Log.Message("paramedic found");
    //                    __result = pawn;
    //                    return;
    //                }
    //            }
    //        }
    //    }
    //}

    //caravan reforming
    [HarmonyPatch(typeof(FormCaravanComp),"CanFormOrReformCaravanNow", MethodType.Getter)]
    public static class RimWorld_Planet_FormCaravanComp_CanFormOrReformCaravanNow
    {
        public static void Postfix(ref bool __result, WorldObject ___parent, FormCaravanComp __instance)
        {
            MapParent mapParent = (MapParent)___parent;
            if (__result)
            {
                return;
            }
            if (!mapParent.HasMap||__instance.AnyActiveThreatNow)
            {
                return;
            }
            MapPawns mapPawns = mapParent.Map.mapPawns;
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
    public static class RimWorld_Planet_FormCaravanComp_GetGizmo_Transpiler_Patch
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            var targetMethod = typeof(FormCaravanComp).GetNestedTypes(AccessTools.all).SelectMany(innerType => AccessTools.GetDeclaredMethods(innerType))
                    .FirstOrDefault(method => method.Name.Contains("MoveNext") && method.ReturnType == typeof(bool) && method.GetParameters().Length == 0);
            yield return targetMethod;
        }
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) =>
            codes.MethodReplacer(AccessTools.PropertyGetter(typeof(MapPawns), "FreeColonistsSpawnedCount"), AccessTools.Method(typeof(RimWorld_Planet_FormCaravanComp_GetGizmo_Transpiler_Patch), "AddMechstoReform"));

        public static int AddMechstoReform(MapPawns mapPawns)
        {
            if (mapPawns.FreeColonistsSpawnedCount > 0)
            {
                return 1;
            }
            foreach (Pawn a in mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer))
            {
                if (a.IsColonyMechPlayerControlled && !(a.Downed || a.Dead))
                {
                    return 1;
                }
            }
            return 0;
        }
    }

    //exit map caravan formation
    [HarmonyPatch(typeof(FloatMenuMakerMap), "PawnGotoAction")]
    public static class FloatMenuMakerMap_PawnGotoAction_Patch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) =>
            codes.MethodReplacer(AccessTools.PropertyGetter(typeof(Pawn), "IsColonyMech"), AccessTools.Method(typeof(FloatMenuMakerMap_PawnGotoAction_Patch), "removeCheck"));
        public static bool removeCheck(Pawn pawn)
        {
            return false;
        }
    }

    [HarmonyPatch]
    public static class IsColonistOrMech_Transpiler_Patch
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(CaravanExitMapUtility), "CanExitMapAndJoinOrCreateCaravanNow");
            yield return AccessTools.Method(typeof(CaravanExitMapUtility), "ExitMapAndJoinOrCreateCaravan");
            yield return AccessTools.PropertyGetter(typeof(Dialog_FormCaravan), "ShowCancelButton");
            //yield return AccessTools.Method(typeof(Caravan), "GetGizmos");
            //<GetGizmos>b__117_0
            var targetMethod = typeof(Caravan).GetNestedTypes(AccessTools.all).SelectMany(innerType => AccessTools.GetDeclaredMethods(innerType))
                  .FirstOrDefault(method => method.Name.Contains("<GetGizmos>b__117_0") && method.ReturnType == typeof(bool) && method.GetParameters().Length == 1);
            yield return targetMethod;
            var targetMethod0 = typeof(Dialog_FormCaravan).GetNestedTypes(AccessTools.all).SelectMany(innerType => AccessTools.GetDeclaredMethods(innerType))
                   .FirstOrDefault(method => method.Name.Contains("<TryFormAndSendCaravan>b__4") && method.ReturnType == typeof(bool) && method.GetParameters().Length == 1);
            yield return targetMethod0;
            //<TryFormAndSendCaravan>b__4
            var targetMethod1 = typeof(Dialog_FormCaravan).GetNestedTypes(AccessTools.all).SelectMany(innerType => AccessTools.GetDeclaredMethods(innerType))
                   .FirstOrDefault(method => method.Name.Contains("<TryFormAndSendCaravan>b__0") && method.ReturnType == typeof(bool) && method.GetParameters().Length == 1);
            yield return targetMethod1;
            //<TryFormAndSendCaravan>b__0
           // yield return AccessTools.Method(typeof(Dialog_FormCaravan), "TryFormAndSendCaravan");
            var targetMethod2 = typeof(Dialog_FormCaravan).GetNestedTypes(AccessTools.all).SelectMany(innerType => AccessTools.GetDeclaredMethods(innerType))
                    .FirstOrDefault(method => method.Name.Contains("<CheckForErrors>b__1") && method.ReturnType == typeof(bool) && method.GetParameters().Length == 1);
            yield return targetMethod2;
            //yield return AccessTools.Method(typeof(Dialog_FormCaravan), "CheckForErrors");
            yield return AccessTools.Method(typeof(Dialog_FormCaravan), "TryFindExitSpot", new Type[] { typeof(List<Pawn>), typeof(bool), typeof(Rot4), typeof(IntVec3).MakeByRefType() });
            //<TryFindExitSpot>b__1
            var targetMethod3 = typeof(Dialog_FormCaravan).GetNestedTypes(AccessTools.all).SelectMany(innerType => AccessTools.GetDeclaredMethods(innerType))
                   .FirstOrDefault(method => method.Name.Contains("<TryFindExitSpot>b__1") && method.ReturnType == typeof(bool) && method.GetParameters().Length == 1);
            yield return targetMethod3;
        }
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) =>
            codes.MethodReplacer(AccessTools.PropertyGetter(typeof(Pawn), "IsColonist"), AccessTools.Method(typeof(IsColonistOrMech_Transpiler_Patch), "IsColonistOrMech"));
        public static bool IsColonistOrMech(Pawn pawn)
        {
            if (pawn.IsColonist || pawn.IsColonyMech)
            {
                return true;
            }
            return false;
        }
    }

    //escort patches
    [HarmonyPatch(typeof(CaravanExitMapUtility), "FindCaravanToJoinFor")]
    public static class CaravanExitMapUtility_FindCaravanToJoinFor_Patch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodBase activate1 = AccessTools.Method(typeof(MechanitorUtility), "GetMechWorkMode");

            MethodBase activate2 = AccessTools.Method(typeof(MechanitorUtility), "GetOverseer");

            bool first = false;
            bool second = false;
            object operand = null;

            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.operand as MethodBase == activate1)
                {
                    first = true;
                    yield return instruction;
                    continue;
                }
                if (instruction.operand as MethodBase == activate2)
                {
                    second = true;
                    yield return instruction;
                    continue;
                }
                if (first == true && instruction.opcode == OpCodes.Bne_Un_S)
                {
                    operand = instruction.operand;
                    first = false;
                    yield return instruction;
                    continue;
                }
                if (second == true && instruction.opcode == OpCodes.Brfalse_S)
                {
                    second = false;
                    yield return new CodeInstruction(OpCodes.Brfalse_S, operand);
                    continue;
                }
                else
                {
                    yield return instruction;
                }
            }
        }
    }

    [HarmonyPatch(typeof(JobGiver_ExitMapFollowOverseer), "TryGiveJob")]
    public static class MechEscort_Transpiler_Patch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) =>
            codes.MethodReplacer(AccessTools.Method(typeof(CaravanExitMapUtility), "CanExitMapAndJoinOrCreateCaravanNow"), AccessTools.Method(typeof(MechEscort_Transpiler_Patch), "CanExitMapAndJoinOrCreateCaravanNow"));

        public static bool CanExitMapAndJoinOrCreateCaravanNow(Pawn pawn)
        {
            if (!pawn.Spawned)
            {
                return false;
            }
            if (!pawn.Map.exitMapGrid.MapUsesExitGrid)
            {
                return false;
            }
            if (!pawn.IsColonist)
            {
                return FindOverseerCaravanToJoinFor(pawn) != null;
            }
            return true;
        }
        private static List<int> tmpNeighbors = new List<int>();
        public static Caravan FindOverseerCaravanToJoinFor(Pawn pawn)
        {
            if (pawn.Faction != Faction.OfPlayer && pawn.HostFaction != Faction.OfPlayer)
            {
                return null;
            }
            if (!pawn.Spawned || !pawn.CanReachMapEdge())
            {
                return null;
            }
            int tile = pawn.Map.Tile;
            Find.WorldGrid.GetTileNeighbors(tile, tmpNeighbors);
            tmpNeighbors.Add(tile);
            List<Caravan> caravans = Find.WorldObjects.Caravans;
            for (int i = 0; i < caravans.Count; i++)
            {
                Caravan caravan = caravans[i];
                if (!tmpNeighbors.Contains(caravan.Tile) || !caravan.autoJoinable)
                {
                    continue;
                }
                if (pawn.GetMechWorkMode() == MechWorkModeDefOf.Escort)
                {
                    if (caravan.PawnsListForReading.Contains(pawn.GetOverseer()))
                    {
                        return caravan;
                    }
                }
                else if (pawn.HostFaction == null)
                {
                    if (caravan.Faction == pawn.Faction)
                    {
                        return caravan;
                    }
                }
                else if (caravan.Faction == pawn.HostFaction)
                {
                    return caravan;
                }
            }
            return null;
        }
    }

    //caravan splitting
    [HarmonyPatch]
    public static class IsColonyMech_Transpiler_Patch
    {
        public static IEnumerable<MethodBase> TargetMethods()
        {
            var targetMethod3 = typeof(CaravanUIUtility).GetNestedTypes(AccessTools.all).SelectMany(innerType => AccessTools.GetDeclaredMethods(innerType))
                   .FirstOrDefault(method => method.Name.Contains("<AddPawnsSections>b__8_6") && method.ReturnType == typeof(bool) && method.GetParameters().Length == 1);
            yield return targetMethod3;
        }
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) =>
            codes.MethodReplacer(AccessTools.PropertyGetter(typeof(Pawn), "IsColonyMechPlayerControlled"), AccessTools.PropertyGetter(typeof(Pawn), "IsColonyMech"));
    }

    //remote disconnect
    [HarmonyPatch(typeof(FloatMenuMakerMap), "TryMakeFloatMenu")]
    public static class FloatMenuMakerMap_TryMakeFloatMenu_Patch
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) =>
            codes.MethodReplacer(AccessTools.PropertyGetter(typeof(Pawn), "Downed"), AccessTools.Method(typeof(FloatMenuMakerMap_TryMakeFloatMenu_Patch), "DownedAndNotMechControlled"));
        public static bool DownedAndNotMechControlled(Pawn pawn)
        {
            if (pawn.Downed)
            {
                if (!pawn.IsColonyMechPlayerControlled)
                {
                    return true;
                }
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn),"GetExtraFloatMenuOptionsFor")]
    public static class Pawn_GetExtraFloatMenuOptionsFor_Patch
    {
        public static void Postfix(ref IEnumerable<FloatMenuOption> __result, Pawn __instance)
        {
            if(__instance.IsColonyMechPlayerControlled && __instance.Downed)
            {
                __result = __result.Append(FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("DisconnectMech".Translate(__instance.LabelShort), delegate
                {
                    MechanitorUtility.ForceDisconnectMechFromOverseer(__instance);
                }), __instance, new LocalTargetInfo(__instance)));
            }
        }
    }
    //caravan forming

    [HarmonyPatch(typeof(CaravanArrivalAction_OfferGifts), "HasNegotiator")]
    public static class RimWorld_Planet_CaravanArrivalAction_OfferGifts_HasNegotiator_Patch
    {
        public static bool Prefix(ref bool __result, Caravan caravan)
        {
            __result = false;
            if (caravan == null)
            {
                return false;
            }
            Pawn pawn = BestCaravanPawnUtility.FindBestNegotiator(caravan);
            if (pawn != null)
            {
                __result = !(pawn.skills?.GetSkill(SkillDefOf.Social).TotallyDisabled ?? true);
            }
            return false;
        }
    }   

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
