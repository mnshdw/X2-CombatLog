using System;
using System.Collections.Generic;
using Artitas;
using Artitas.Systems;
using Artitas.Utils;
using Common.Mechanics.Factions;
using Xenonauts;
using Xenonauts.GroundCombat;
using Xenonauts.GroundCombat.Events;
using static CombatLog.ModConstants;

namespace CombatLog.Runtime
{
    // Event subscriber registered on the GroundCombat world, that translates combat events into
    // text entries pushed onto the static CombatLogFeed for the UI.
    public sealed class CombatLogSystem : EventSystem
    {
        private readonly Dictionary<IImpactReport, ImpactState> _pending = new();

        [Subscriber]
        public void OnProjectileImpact(ProjectileImpactReport ev)
        {
            if (ev == null)
                return;
            var conflict = ev.Conflict;
            if (conflict == null)
                return;
            var attacker = conflict.Attacker;
            var target = ev.Target ?? conflict.Target;
            if (attacker == null || target == null)
                return;
            if (!ShouldShowCombatants(attacker, target))
                return;
            var roll = ev.Projectile != null ? (int?)Math.Round(ev.Projectile.Roll) : null;
            // Per-shot chance = TrueChanceToHit * CalculateRecoilModifierForBurst(index).
            // Each shot in a burst rolls against its own degraded accuracy.
            var chance = ev.Projectile != null ? (int?)Math.Round(ev.Projectile.Accuracy) : null;
            // ProjectileImpactReport fires whenever the projectile hits ANYTHING (target,
            // intervening cover, ground via trajectory). Only RollHitOriginalTarget counts as
            // a real hit on the intended target for the [Ability] color; otherwise grey it.
            var hitTheTarget =
                ev.Projectile != null
                && ev.Projectile.Result
                    == Xenonauts
                        .GroundCombat
                        .Abilities
                        .Shoot
                        .Projectile
                        .Status
                        .RollHitOriginalTarget;
            var head = FormatHead(conflict, attacker, roll, chance, hit: hitTheTarget);
            var state = new ImpactState(target, head);
            _pending[ev] = state;
            var burstIndex = ev.Projectile?.IndexInBurst ?? 0;
            CombatLogFeed.Publish(
                new CombatEntry(
                    EntryKind.Hit,
                    BuildLine(state),
                    mergeKey: ev,
                    burstKey: conflict,
                    burstIndex: burstIndex
                )
            );
        }

        [Subscriber]
        public void OnProjectileMiss(ProjectileMissEvent ev)
        {
            var conflict = ev?.Projectile?.Conflict;
            if (conflict == null)
                return;
            var attacker = conflict.Attacker;
            var target = conflict.Target;
            if (attacker == null || target == null)
                return;
            if (!ShouldShowCombatants(attacker, target))
                return;
            var roll = ev!.Projectile != null ? (int?)Math.Round(ev.Projectile.Roll) : null;
            var chance = ev.Projectile != null ? (int?)Math.Round(ev.Projectile.Accuracy) : null;
            var burstIndex = ev.Projectile?.IndexInBurst ?? 0;
            CombatLogFeed.Publish(
                new CombatEntry(
                    EntryKind.Miss,
                    FormatMissLine(conflict, attacker, target, roll, chance),
                    burstKey: conflict,
                    burstIndex: burstIndex
                )
            );
        }

        [Subscriber]
        public void OnMeleeImpact(MeleeImpactReport ev)
        {
            if (ev == null)
                return;
            var conflict = ev.Conflict;
            if (conflict == null)
                return;
            var attacker = conflict.Attacker;
            var target = ev.Target ?? conflict.Target;
            if (attacker == null || target == null)
                return;
            if (!ShouldShowCombatants(attacker, target))
                return;
            // Melee's d100 is local to MeleeAbility.HitTarget and not stored on the event.
            var head = FormatHead(conflict, attacker, roll: null, chance: null, hit: true);
            var state = new ImpactState(target, head);
            _pending[ev] = state;
            CombatLogFeed.Publish(
                new CombatEntry(EntryKind.Hit, BuildLine(state), mergeKey: ev, burstKey: conflict)
            );
        }

        [Subscriber]
        public void OnMeleeMiss(MeleeMissEvent ev)
        {
            var conflict = ev?.Conflict;
            if (conflict == null)
                return;
            var attacker = conflict.Attacker;
            var target = conflict.Target;
            if (attacker == null || target == null)
                return;
            if (!ShouldShowCombatants(attacker, target))
                return;
            CombatLogFeed.Publish(
                new CombatEntry(
                    EntryKind.Miss,
                    FormatMissLine(conflict, attacker, target, roll: null, chance: null),
                    burstKey: conflict
                )
            );
        }

        [Subscriber]
        public void OnStatChange(StatChangeReport ev)
        {
            if (ev?.Target == null)
                return;
            // Corpses still take stat changes from area effects (for example smoke grenade).
            if (IsDead(ev.Target))
                return;
            var stat = ev.Stat;

            if (
                stat != StatChangeReport.StatType.HitPoint
                && stat != StatChangeReport.StatType.Stun
                && stat != StatChangeReport.StatType.Armour
            )
                return;

            var signedAmount = ev.Amount;
            int amount;
            string label;
            string color;
            int sortOrder;
            switch (stat)
            {
                case StatChangeReport.StatType.HitPoint:
                    if (signedAmount >= 0f) // skip heals
                        return;
                    amount = (int)Math.Round(Math.Abs(signedAmount));
                    label = "Body";
                    color = BodyColor;
                    sortOrder = 1;
                    break;
                case StatChangeReport.StatType.Stun:
                    if (signedAmount <= 0f) // 0 / negative = no stun applied
                        return;
                    amount = (int)Math.Round(signedAmount);
                    label = "Stun";
                    color = StunColor;
                    sortOrder = 2;
                    break;
                default: // Armour
                    if (signedAmount >= 0f)
                        return;
                    amount = (int)Math.Round(Math.Abs(signedAmount));
                    label = "Armour";
                    color = ArmourColor;
                    sortOrder = 0;
                    break;
            }
            if (amount <= 0)
                return;

            // Tied to a specific impact: append to that impact's state and re-publish.
            if (ev.EventTrigger.IsSet && ev.EventTrigger.Value is IImpactReport impact)
            {
                // Explosions reroute through ShockwaveImpactReport whose Cause is the original
                // projectile impact. Walk up the chain so the head we already published merges
                // with the explosion per-target damage (otherwise the damage line shows up
                // as a second indented entry).
                if (impact is ShockwaveImpactReport shock && shock.Cause != null)
                    impact = shock.Cause;

                if (!_pending.TryGetValue(impact, out var state))
                {
                    // Impact wasn't seen by us, maybe it was suppressed by the visibility filter
                    // or it's an impact type we don't subscribe to. Create a minimal state,
                    // but only if the wearer/target is visible; otherwise don't show.
                    Entity? wearer =
                        stat == StatChangeReport.StatType.Armour
                        && ev.Conflict.IsSet
                        && ev.Conflict.Value.Target != null
                            ? ev.Conflict.Value.Target
                            : ev.Target;
                    if (!IsVisibleNow(wearer))
                        return;
                    state = new ImpactState(wearer, head: null);
                    _pending[impact] = state;
                }
                state.AddOrAccumulate(label, amount, color, sortOrder);
                CombatLogFeed.Publish(
                    new CombatEntry(EntryKind.Hit, BuildLine(state), mergeKey: impact)
                );
                return;
            }

            // Standalone (bleeding wounds, environment).
            var standaloneTarget =
                stat == StatChangeReport.StatType.Armour
                && ev.Conflict.IsSet
                && ev.Conflict.Value.Target != null
                    ? ev.Conflict.Value.Target
                    : ev.Target;
            if (!IsVisibleNow(standaloneTarget))
                return;
            var single = new ImpactState(standaloneTarget, head: null);
            single.AddOrAccumulate(label, amount, color, sortOrder);
            CombatLogFeed.Publish(new CombatEntry(EntryKind.Damage, BuildLine(single)));
        }

        [Subscriber]
        public void OnDeath(CombatantDeathReport ev)
        {
            if (ev?.Actor == null)
                return;
            if (!IsVisibleNow(ev.Actor))
                return;
            var color = ColorFor(ev.Actor);
            var line =
                $"<color={color}>{NameOf(ev.Actor)}</color> "
                + $"<color={DeathColor}>killed</color>";
            CombatLogFeed.Publish(new CombatEntry(EntryKind.Death, line));
        }

        // Final line: "Attacker [Ability] (roll vs chance) >> Target X Body, Y Armour"
        private string BuildLine(ImpactState state)
        {
            var pieces = new List<string>(3);
            if (state.Head != null)
                pieces.Add(state.Head);
            var targetSpan = TargetSpan(state.Target);
            if (targetSpan != null)
                pieces.Add($"<color={MetaColor}>{Bullet}</color> {targetSpan}");
            var breakdown = BreakdownString(state.Items);
            if (breakdown.Length > 0)
                pieces.Add(breakdown);
            return string.Join(" ", pieces);
        }

        private string BreakdownString(List<DamageItem> items)
        {
            if (items.Count == 0)
                return string.Empty;
            items.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
            var parts = new List<string>(items.Count);
            foreach (var item in items)
                parts.Add($"<color={item.Color}>{item.Amount} {item.Label}</color>");
            return string.Join(", ", parts);
        }

        private string? TargetSpan(Entity? target)
        {
            if (!TargetIsNamed(target))
                return null;
            return $"<color={ColorFor(target)}>{NameOf(target)}</color>";
        }

        // Just the attacker + ability + roll-vs-chance prefix.
        private string FormatHead(
            BaseConflict conflict,
            Entity attacker,
            int? roll,
            int? chance,
            bool hit
        )
        {
            var aColor = ColorFor(attacker);
            var ability = AbilityName(conflict);
            // Action bracket is green on hit, grey on miss - lets the player skim outcomes.
            var abilityColor = hit ? AbilityHitColor : AbilityMissColor;
            var abilitySuffix =
                ability != null ? $" <color={abilityColor}>[{ability}]</color>" : "";

            var effectiveChance = chance ?? (int)Math.Round(conflict.RoundedTrueChanceToHit);
            var rollText = roll.HasValue
                ? $"<color={MetaColor}>({roll.Value} vs {effectiveChance})</color>"
                : $"<color={MetaColor}>({effectiveChance}%)</color>";

            return $"<color={aColor}>{NameOf(attacker)}</color>{abilitySuffix} {rollText}";
        }

        // Single-shot miss line: "Attacker [Ability] (roll vs chance) >> Target".
        private string FormatMissLine(
            BaseConflict conflict,
            Entity attacker,
            Entity target,
            int? roll,
            int? chance
        )
        {
            var head = FormatHead(conflict, attacker, roll, chance, hit: false);
            var span = TargetSpan(target);
            return span != null ? $"{head} <color={MetaColor}>{Bullet}</color> {span}" : head;
        }

        private static bool TargetIsNamed(Entity? entity)
        {
            if (entity == null)
                return false;
            try
            {
                return entity.HasName() && !string.IsNullOrEmpty(entity.Name().value);
            }
            catch
            {
                return false;
            }
        }

        private static bool ShouldShowCombatants(Entity? attacker, Entity? target)
        {
            return IsVisibleNow(attacker) || IsVisibleNow(target);
        }

        private static bool IsDead(Entity? entity)
        {
            if (entity == null)
                return false;
            try
            {
                return entity.HasLifeStatus() && entity.LifeStatus().IsDead();
            }
            catch
            {
                return false;
            }
        }

        private static bool IsVisibleNow(Entity? entity)
        {
            if (entity == null)
                return false;
            try
            {
                if (entity.HasSightingState())
                    return entity.SightingState().IsVisible();
            }
            catch { }
            // Fall back to the per-player query if the entity has no SightingState yet.
            try
            {
                return SightSystem.IsCombatantVisibleForAnyLocalPlayer(entity, currentOnly: true);
            }
            catch
            {
                return true;
            }
        }

        private static string? AbilityName(BaseConflict conflict)
        {
            try
            {
                var def = conflict.Ability?.Definition;
                if (def == null)
                    return null;
                var name = def.Name;
                return string.IsNullOrEmpty(name) ? null : name;
            }
            catch
            {
                return null;
            }
        }

        private string ColorFor(Entity? entity)
        {
            if (entity == null)
                return NeutralColor;
            try
            {
                if (entity.HasController())
                {
                    var controller = entity.Controller().Value;
                    if (controller != null && controller.HasName())
                    {
                        var name = controller.Name().value;
                        if (name == XenonautsConstants.Players.XENONAUT)
                            return PlayerColor;
                        if (name == XenonautsConstants.Players.ALIEN)
                            return EnemyColor;
                    }
                }
            }
            catch { }

            try
            {
                var player = base.World.GetPlayer(XenonautsConstants.Players.XENONAUT);
                if (player == null)
                    return NeutralColor;
                return entity.AlignmentTo(player) switch
                {
                    AlignmentComponent.Alignment.Friendly => PlayerColor,
                    AlignmentComponent.Alignment.Hostile => EnemyColor,
                    _ => NeutralColor,
                };
            }
            catch
            {
                return NeutralColor;
            }
        }

        private static string NameOf(Entity? entity)
        {
            if (entity == null)
                return "?";
            try
            {
                if (entity.HasName())
                    return entity.Name().value;
            }
            catch { }

            return $"#{entity.ID}";
        }

        private const string Bullet = "»";

        private sealed class ImpactState
        {
            public Entity? Target { get; }
            public string? Head { get; }
            public List<DamageItem> Items { get; } = new();

            public ImpactState(Entity? target, string? head)
            {
                Target = target;
                Head = head;
            }

            // Single Apply() can queue multiple HP reports (e.g. main + EMP, or splash) for the
            // same target; combine themso the line reads "30 Body" instead of "18 Body, 12 Body".
            public void AddOrAccumulate(string label, int amount, string color, int sortOrder)
            {
                for (var i = 0; i < Items.Count; i++)
                {
                    if (Items[i].Label == label)
                    {
                        var existing = Items[i];
                        Items[i] = new DamageItem(
                            existing.Amount + amount,
                            label,
                            color,
                            sortOrder
                        );
                        return;
                    }
                }
                Items.Add(new DamageItem(amount, label, color, sortOrder));
            }
        }

        private readonly struct DamageItem
        {
            public int Amount { get; }
            public string Label { get; }
            public string Color { get; }
            public int SortOrder { get; }

            public DamageItem(int amount, string label, string color, int sortOrder)
            {
                Amount = amount;
                Label = label;
                Color = color;
                SortOrder = sortOrder;
            }
        }
    }
}
