using System;
using System.Collections.Generic;
using Artitas;
using CombatLog.Runtime;
using Common.Content;
using Common.Modding;
using HarmonyLib;
using UnityEngine;
using static CombatLog.ModConstants;

namespace CombatLog
{
    public class CombatLogLifecycle : IModLifecycle
    {
        private const string HostObjectName = "CombatLog.Host";
        private GameObject? _host;

        public void Create(Mod mod, Harmony patcher)
        {
            Log.Info($"{LogPrefix} Create - mod loaded");
            try
            {
                _host = new GameObject(HostObjectName);
                UnityEngine.Object.DontDestroyOnLoad(_host);
                _host.AddComponent<CombatLogHost>();
                Log.Info($"{LogPrefix} host GameObject attached");
            }
            catch (Exception ex)
            {
                Log.Error($"{LogPrefix} failed to create host GameObject: {ex}");
            }
            try
            {
                patcher.PatchAll(typeof(CombatLogLifecycle).Assembly);
                Log.Info($"{LogPrefix} Harmony patches applied (input click-through)");
            }
            catch (Exception ex)
            {
                Log.Error($"{LogPrefix} failed to apply Harmony patches: {ex}");
            }
        }

        public void Destroy()
        {
            Log.Info($"{LogPrefix} Destroy - mod unloaded");
            if (_host != null)
            {
                UnityEngine.Object.Destroy(_host);
                _host = null;
            }
            CombatLogFeed.Reset();
        }

        public void OnWorldCreate(IModLifecycle.Section section, WeakReference<World> world)
        {
            if (section != IModLifecycle.Section.GroundCombat)
                return;
            if (!world.TryGetTarget(out var w))
                return;

            try
            {
                w.RegisterSystem<CombatLogSystem>();
                CombatLogFeed.OnGroundCombatStarted();
                Log.Info($"{LogPrefix} CombatLogSystem registered on GroundCombat world");
            }
            catch (Exception ex)
            {
                Log.Error($"{LogPrefix} failed to register CombatLogSystem: {ex}");
            }
        }

        public void OnWorldDispose(IModLifecycle.Section section, WeakReference<World> world)
        {
            if (section != IModLifecycle.Section.GroundCombat)
                return;
            CombatLogFeed.OnGroundCombatEnded();
        }

        public IEnumerable<Descriptor> GetRequiredAssets(IModLifecycle.Section section)
        {
            return [];
        }
    }
}
