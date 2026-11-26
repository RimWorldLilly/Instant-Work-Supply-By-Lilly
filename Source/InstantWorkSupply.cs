using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace Lilly.InstantWorkSupply
{
    [StaticConstructorOnStartup]
    internal static class InstantWorkSupplyStartup
    {
        static InstantWorkSupplyStartup()
        {
            new Harmony("lilly.instantworksupply").PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    public sealed class InstantWorkSupplyMod : Mod
    {
        internal static InstantWorkSupplySettings CurrentSettings { get; private set; }

        public InstantWorkSupplyMod(ModContentPack content) : base(content)
        {
            CurrentSettings = GetSettings<InstantWorkSupplySettings>();
        }

        public override string SettingsCategory()
        {
            return "Super Blink";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            InstantWorkSupplySettings settings = CurrentSettings;
            if (settings == null)
                return;

            bool affectPlayerFactionPawns = settings.AffectPlayerFactionPawns;
            bool affectOtherFactionPawns = settings.AffectOtherFactionPawns;
            bool affectFactionlessPawns = settings.AffectFactionlessPawns;

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.Label("Pawn target scope / 적용 대상");
            listing.CheckboxLabeled(
                "Player-faction pawns / 플레이어 세력 폰",
                ref affectPlayerFactionPawns,
                "Colonists, animals and mechanoids belonging to the player faction.");
            listing.CheckboxLabeled(
                "Other-faction pawns / 다른 세력 폰",
                ref affectOtherFactionPawns,
                "Pawns belonging to factions other than the player faction.");
            listing.CheckboxLabeled(
                "Factionless pawns / 소속 세력이 없는 폰",
                ref affectFactionlessPawns,
                "Pawns that do not belong to any faction, including wild animals.");
            listing.End();

            settings.AffectPlayerFactionPawns = affectPlayerFactionPawns;
            settings.AffectOtherFactionPawns = affectOtherFactionPawns;
            settings.AffectFactionlessPawns = affectFactionlessPawns;
        }
    }

    public sealed class InstantWorkSupplySettings : ModSettings
    {
        public bool AffectPlayerFactionPawns = true;
        public bool AffectOtherFactionPawns = false;
        public bool AffectFactionlessPawns = false;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref AffectPlayerFactionPawns, "affectPlayerFactionPawns", true);
            Scribe_Values.Look(ref AffectOtherFactionPawns, "affectOtherFactionPawns", false);
            Scribe_Values.Look(ref AffectFactionlessPawns, "affectFactionlessPawns", false);
        }
    }

    internal static class InstantWorkSupplyUtility
    {
        internal static bool ShouldAffectPawn(Pawn pawn)
        {
            if (pawn == null)
                return false;

            InstantWorkSupplySettings settings = InstantWorkSupplyMod.CurrentSettings;
            if (settings == null)
                return pawn.Faction == Faction.OfPlayer;

            if (pawn.Faction == Faction.OfPlayer)
                return settings.AffectPlayerFactionPawns;
            if (pawn.Faction != null)
                return settings.AffectOtherFactionPawns;
            return settings.AffectFactionlessPawns;
        }

        internal static bool IsReservedPickup(Pawn pawn, TargetIndex targetIndex)
        {
            if (!ShouldAffectPawn(pawn) || pawn.Map == null || pawn.CurJob == null ||
                pawn.carryTracker.CarriedThing != null)
                return false;

            Thing target = pawn.CurJob.GetTarget(targetIndex).Thing;
            return target != null &&
                !(target is Pawn) &&
                target.Spawned &&
                target.def.EverHaulable &&
                !target.IsForbidden(pawn) &&
                pawn.Map.reservationManager.ReservedBy(target, pawn, pawn.CurJob);
        }

        internal static void SkipPickupTravel(Toil toil, TargetIndex targetIndex)
        {
            if (toil == null || toil.initAction == null)
                return;

            Action originalInitAction = toil.initAction;
            toil.initAction = delegate
            {
                Pawn pawn = toil.actor;
                if (IsReservedPickup(pawn, targetIndex))
                {
                    toil.defaultCompleteMode = ToilCompleteMode.Instant;
                    return;
                }

                originalInitAction();
            };
        }

        internal static void TeleportPawnAfterPickup(Toil toil, TargetIndex targetIndex)
        {
            if (toil == null || toil.initAction == null)
                return;

            Action originalInitAction = toil.initAction;
            toil.initAction = delegate
            {
                Pawn pawn = toil.actor;
                if (!IsReservedPickup(pawn, targetIndex))
                {
                    originalInitAction();
                    return;
                }

                Thing target = pawn.CurJob.GetTarget(targetIndex).Thing;
                if (target == null || !target.Spawned || target.Map != pawn.Map)
                {
                    originalInitAction();
                    return;
                }

                // Remember the pickup cell, then let vanilla complete the pickup.
                // The Thing itself is never moved or modified by this patch.
                Map pickupMap = pawn.Map;
                IntVec3 pickupPosition = target.Position;

                originalInitAction();

                // Only teleport after vanilla successfully puts an item in the
                // pawn's carry tracker. This keeps stack counts and carry handling
                // entirely under vanilla control.
                if (pawn.Spawned &&
                    pawn.Map == pickupMap &&
                    pawn.carryTracker.CarriedThing != null &&
                    pickupPosition.IsValid &&
                    pickupPosition.InBounds(pickupMap) &&
                    pawn.Position != pickupPosition)
                {
                    pawn.Position = pickupPosition;
                    pawn.Notify_Teleported(false, true);
                }
            };
        }
    }

    [HarmonyPatch]
    internal static class Toils_Goto_GotoThing_Patch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            return AccessTools.GetDeclaredMethods(typeof(Toils_Goto))
                .Where(method =>
                {
                    if (method.Name != nameof(Toils_Goto.GotoThing) || method.ReturnType != typeof(Toil))
                        return false;

                    ParameterInfo[] parameters = method.GetParameters();
                    return parameters.Length >= 2 &&
                        parameters[0].ParameterType == typeof(TargetIndex) &&
                        parameters[1].ParameterType == typeof(PathEndMode);
                });
        }

        private static void Postfix(object[] __args, ref Toil __result)
        {
            if (__args != null && __args.Length > 0 && __args[0] is TargetIndex targetIndex)
                InstantWorkSupplyUtility.SkipPickupTravel(__result, targetIndex);
        }
    }

    [HarmonyPatch]
    internal static class Toils_Haul_StartCarryThing_Patch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            return AccessTools.GetDeclaredMethods(typeof(Toils_Haul))
                .Where(method =>
                    method.Name == nameof(Toils_Haul.StartCarryThing) &&
                    method.ReturnType == typeof(Toil));
        }

        private static void Postfix(object[] __args, ref Toil __result)
        {
            if (__args != null && __args.Length > 0 && __args[0] is TargetIndex targetIndex)
                InstantWorkSupplyUtility.TeleportPawnAfterPickup(__result, targetIndex);
        }
    }

    [HarmonyPatch]
    internal static class Pawn_PathFollower_PatherTick_Patch
    {
        private const int MinimumTeleportDistanceSquared = 25;

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Pawn_PathFollower), "PatherTick");
        }

        private static bool Prefix(Pawn_PathFollower __instance, Pawn ___pawn)
        {
            Pawn pawn = ___pawn;
            if (pawn == null || !pawn.Spawned || pawn.Map == null ||
                !InstantWorkSupplyUtility.ShouldAffectPawn(pawn))
                return true;

            PawnPath path = __instance.curPath;
            if (path == null || !path.Found)
                return true;

            Map map = pawn.Map;
            IntVec3 destination = path.LastNode;
            if (!destination.IsValid || !destination.InBounds(map))
                return true;

            if (destination.DistanceToSquared(pawn.Position) < MinimumTeleportDistanceSquared)
                return true;

            pawn.Position = destination;
            pawn.Notify_Teleported(false, true);
            pawn.jobs?.curDriver?.Notify_PatherArrived();
            return false;
        }
    }
}