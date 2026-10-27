namespace BossMod;

// Resolve mechanic targeting before rotations and navigation consume it. A native
// target-write deduplicator alone still leaves those consumers following the add.
public sealed class MechanicTargetSelection
{
    private BossModule? _module;
    private Actor? _player, _recommended, _previousTarget;
    private bool _active;

    private const string CrucibleNamespace = "BossMod.Dawntrail.Foray.Crucible";

    // Only Crucible modules are written around one-shot mechanic targeting.
    // Called every frame; the active module type rarely changes, so remember the last answer.
    private static Type? _lastType;
    private static bool _lastSupported;

    public static bool Supports(BossModule module)
    {
        var type = module.GetType();
        if (type != _lastType)
        {
            _lastType = type;
            _lastSupported = type.Namespace?.StartsWith(CrucibleNamespace, StringComparison.Ordinal) == true;
        }
        return _lastSupported;
    }

    public void Apply(AIHints hints, BossModule? module, Actor? player, Func<Actor, bool>? canSelect = null)
    {
        if (_module != module || _player != player || player?.IsDeadOrDestroyed != false)
        {
            _module = module;
            _player = player;
            _recommended = _previousTarget = null;
            _active = false;
        }
        if (module == null || player == null || player.IsDeadOrDestroyed)
            return;

        var current = module.WorldState.Actors.Find(player.TargetID);
        var targetDied = !Available(current) && _previousTarget != null
            && (_previousTarget.IsDeadOrDestroyed || _previousTarget.HPMP.CurHP == 0);
        var requested = hints.ForcedTarget;
        if (requested == null && hints.HighestPotentialTargetPriority > 0)
            requested = FirstAvailablePriorityTarget(hints);

        // Keep a choice within the same priority group stable when distances or
        // the manually selected add change. Advance when it dies or leaves the group.
        if (requested != null && _recommended != null && Available(_recommended)
            && hints.FindEnemy(requested) is { Priority: > 0 } next
            && hints.FindEnemy(_recommended) is { Priority: > 0 } previous && previous.Priority == next.Priority)
            requested = _recommended;

        // A disappearing recommendation (e.g. a mushroom crossing the water
        // edge) must not rearm it. A genuinely unsafe target must rearm after
        // its dangerous phase, so the eye's next safe window can select again.
        if (_recommended != null && (hints.ClearTargetID == _recommended.InstanceID
            || hints.FindEnemy(_recommended)?.Priority == AIHints.Enemy.PriorityForbidden))
            _recommended = null;

        if (requested == null && _active && targetDied)
            requested = FirstAvailablePriorityTarget(hints);

        hints.ForcedTarget = null;
        if (requested != null)
        {
            _active = true;
            if ((requested != _recommended || targetDied) && Available(requested)
                && hints.FindEnemy(requested)?.Priority != AIHints.Enemy.PriorityForbidden
                && (requested == current || (canSelect?.Invoke(requested) ?? player.DistanceToPoint(requested.Position) < 50)))
            {
                _recommended = requested;
                if (requested != current)
                    hints.ForcedTarget = requested;
            }
        }

        if (_active)
        {
            // Subsequent rotation/AI writers cannot overwrite this frame's
            // one-shot request or restore a recommendation the player left.
            hints.PreserveTarget = true;
            var selected = hints.FindEnemy(hints.ForcedTarget ?? current);
            if (selected != null && Available(selected.Actor)
                && (selected.Priority >= 0 || selected.Priority == AIHints.Enemy.PriorityUndesirable))
            {
                // Priority is a recommendation, not a target lock. Keep the
                // actual selection eligible without removing other AOE targets.
                selected.Priority = Math.Max(0, selected.Priority);
                foreach (var enemy in hints.PotentialTargets)
                    if (enemy.Priority > selected.Priority)
                        enemy.Priority = selected.Priority;
                hints.Normalize();
            }
        }
        _previousTarget = hints.ForcedTarget ?? current;
    }

    private static Actor? FirstAvailablePriorityTarget(AIHints hints)
    {
        foreach (var e in hints.PriorityTargetsSpan)
            if (Available(e.Actor))
                return e.Actor;
        return null;
    }

    private static bool Available(Actor? actor) => actor is { IsDeadOrDestroyed: false, IsTargetable: true }
        && (actor.Type != ActorType.Enemy || actor.HPMP.CurHP > 0);
}
