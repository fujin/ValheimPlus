using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using ValheimPlus.Configurations;

namespace ValheimPlus.GameClasses
{
    // Observation only: no ownership claims, state writes, or exception suppression.
    [HarmonyPatch]
    internal static class SmelterDiagnostics
    {
        private sealed class Timers
        {
            public Timers() { }
            internal long Update;
            internal long Error;
        }

        internal sealed class CallState
        {
            internal string Before;
        }

        private static readonly ConditionalWeakTable<Smelter, Timers> Timings = new();
        internal static bool Enabled => Configuration.Current?.Smelter?.diagnosticLogging == true;

        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (string name in new[] { "Awake", "OnAddOre", "OnAddFuel", "RPC_AddOre", "RPC_AddFuel",
                         "UpdateSmelter", "Spawn", "SpawnProcessed", "OnDestroyed" })
                yield return AccessTools.DeclaredMethod(typeof(Smelter), name);
        }

        internal static bool Tracked(Smelter smelter) => smelter != null &&
            (smelter.m_name == SmelterDefinitions.SmelterName || smelter.m_name == SmelterDefinitions.KilnName ||
             smelter.m_name == SmelterDefinitions.FurnaceName);

        private static bool Due(ref long last)
        {
            long now = Stopwatch.GetTimestamp();
            if (last != 0 && now - last < Stopwatch.Frequency * 10) return false;
            last = now;
            return true;
        }

        [HarmonyPrefix, HarmonyPriority(Priority.First)]
        private static void Before(Smelter __instance, MethodBase __originalMethod, object[] __args,
            out CallState __state)
        {
            __state = null;
            if (!Enabled || !Tracked(__instance)) return;
            try
            {
                if (__originalMethod.Name == "UpdateSmelter" && !Due(ref Timings.GetOrCreateValue(__instance).Update))
                    return;
                __state = new CallState { Before = Describe(__instance) };
                Write($"{__originalMethod.Name} before args=[{Arguments(__args)}] {__state.Before}");
            }
            catch (Exception e) { Write($"snapshot failed: {e.GetType().Name}: {e.Message}"); }
        }

        [HarmonyPostfix, HarmonyPriority(Priority.Last)]
        private static void After(Smelter __instance, MethodBase __originalMethod, bool __runOriginal,
            CallState __state)
        {
            if (__state == null) return;
            try
            {
                Write($"{__originalMethod.Name} after originalRan={__runOriginal} " +
                      $"suppressedByPrefix={!__runOriginal} {Describe(__instance)}; before=[{__state.Before}]");
            }
            catch (Exception e) { Write($"snapshot failed: {e.GetType().Name}: {e.Message}"); }
        }

        [HarmonyFinalizer]
        private static void Failure(Smelter __instance, MethodBase __originalMethod, Exception __exception)
        {
            if (__exception == null || !Enabled || !Tracked(__instance)) return;
            try
            {
                if (Due(ref Timings.GetOrCreateValue(__instance).Error))
                    Write($"{__originalMethod.Name} THREW (exception preserved) {Describe(__instance)}: {__exception}");
            }
            catch { /* Diagnostics must not replace the original exception. */ }
        }

        internal static string Arguments(object[] args) => string.Join(", ", args.Select(arg =>
            arg is ItemDrop.ItemData item ? $"item={item.m_dropPrefab?.name},stack={item.m_stack}" :
            arg == null ? "null" : arg is string || arg is bool || arg is int || arg is long
                ? arg.ToString() : arg.GetType().Name));

        internal static string Describe(Smelter smelter)
        {
            var config = Configuration.Current;
            string section = smelter.m_name == SmelterDefinitions.SmelterName ? "Smelter" :
                smelter.m_name == SmelterDefinitions.KilnName ? "Kiln" : "Furnace";
            bool enabled = section == "Smelter" ? config.Smelter.IsEnabled :
                section == "Kiln" ? config.Kiln.IsEnabled : config.Furnace.IsEnabled;
            bool fuel = section == "Smelter" ? config.Smelter.autoFuel :
                section == "Kiln" ? config.Kiln.autoFuel : config.Furnace.autoFuel;
            bool deposit = section == "Smelter" ? config.Smelter.autoDeposit :
                section == "Kiln" ? config.Kiln.autoDeposit : config.Furnace.autoDeposit;
            var view = smelter.m_nview;
            var zdo = view != null ? view.GetZDO() : null;
            bool valid = view != null && view.IsValid();
            string state = zdo == null ? "zdo=null" :
                $"zdo={zdo.m_uid} prefabHash={zdo.GetPrefab()} ownerPeer={zdo.GetOwner()} " +
                $"ore={zdo.GetInt(ZDOVars.s_queued)}/{smelter.m_maxOre} " +
                $"fuel={zdo.GetFloat(ZDOVars.s_fuel)}/{smelter.m_maxFuel} " +
                $"item0={zdo.GetString(ZDOVars.s_item0)} " +
                $"output={zdo.GetString(ZDOVars.s_spawnOre)}:{zdo.GetInt(ZDOVars.s_spawnAmount)} " +
                $"bake={zdo.GetFloat(ZDOVars.s_bakeTimer)}";
            return $"object={smelter.gameObject.name} type={smelter.m_name} section={section} " +
                   $"enabled={enabled} autoFuel={fuel} autoDeposit={deposit} valid={valid} " +
                   $"IsOwner={(valid && view.IsOwner())} localPlayer={(Player.m_localPlayer != null)} " +
                   $"localPeer={ZDOMan.GetSessionID()} {state} " +
                   $"rpcOreRegistered={Registered(view, "RPC_AddOre")} rpcFuelRegistered={Registered(view, "RPC_AddFuel")} " +
                   $"updateScheduled={smelter.IsInvoking("UpdateSmelter")} " +
                   $"blockedSmoke={smelter.m_blockedSmoke} haveRoof={smelter.m_haveRoof}";
        }

        private static bool Registered(ZNetView view, string rpc) => view != null &&
            view.m_functions.ContainsKey(rpc.GetStableHashCode());

        internal static void Write(string message) => ValheimPlusPlugin.Logger.LogInfo("[SmelterDiag] " + message);
    }

    [HarmonyPatch(typeof(ZNetView), nameof(ZNetView.InvokeRPC), new[] { typeof(string), typeof(object[]) })]
    internal static class SmelterRpcDiagnostics
    {
        [HarmonyPrefix]
        private static void Before(ZNetView __instance, string method, object[] parameters)
        {
            if (!SmelterDiagnostics.Enabled || (method != "RPC_AddOre" && method != "RPC_AddFuel")) return;
            try
            {
                var smelter = __instance.GetComponentInChildren<Smelter>();
                if (!SmelterDiagnostics.Tracked(smelter)) return;
                SmelterDiagnostics.Write($"InvokeRPC rpc={method} targetOwner={__instance.GetZDO()?.GetOwner()} " +
                    $"args=[{SmelterDiagnostics.Arguments(parameters)}] {SmelterDiagnostics.Describe(smelter)}");
            }
            catch (Exception e) { SmelterDiagnostics.Write($"RPC snapshot failed: {e.GetType().Name}: {e.Message}"); }
        }
    }
}
