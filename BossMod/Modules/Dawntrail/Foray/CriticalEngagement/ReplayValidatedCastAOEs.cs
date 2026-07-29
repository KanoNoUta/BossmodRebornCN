namespace BossMod.Dawntrail.Foray.CriticalEngagement;

// Cast packets in accelerated/replayed encounters can be duplicated or resynchronized. This
// component keys warnings by action + caster and keeps short tombstones for already resolved casts,
// so a stale cast-start packet cannot resurrect an AOE from the previous wave.
abstract class ReplayValidatedCastAOEs(BossModule module) : Components.GenericAOEs(module)
{
    protected readonly record struct AOEConfig(AOEShape Shape, bool LocationTargeted = false);

    private const double EventResolveTolerance = 0.5d;
    private const double TombstoneWindow = 1d;
    private const double ExpireDelay = 2d;

    private sealed class PendingAOE(uint actionID, AOEInstance aoe)
    {
        public readonly uint ActionID = actionID;
        public AOEInstance AOE = aoe;
    }

    private readonly record struct ResolvedCast(uint ActionID, ulong ActorID, DateTime Activation, DateTime ExpiresAt);

    private readonly List<PendingAOE> _pending = [with(16)];
    private readonly List<AOEInstance> _displayed = [with(16)];
    private readonly List<ResolvedCast> _resolved = [with(16)];
    private readonly HashSet<uint> _seenGlobalSequences = [];

    protected abstract AOEConfig? ConfigFor(uint actionID);
    protected virtual int MaxDisplayed => int.MaxValue;
    protected virtual int MaxRisky => int.MaxValue;
    protected virtual double RiskyActivationWindow => double.PositiveInfinity;

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        PruneExpired();
        _displayed.Clear();
        var count = Math.Min(_pending.Count, MaxDisplayed);
        var useRiskLimit = MaxRisky != int.MaxValue || !double.IsPositiveInfinity(RiskyActivationWindow);
        var riskyDeadline = !double.IsPositiveInfinity(RiskyActivationWindow) && count > 0
            ? _pending[0].AOE.Activation.AddSeconds(RiskyActivationWindow)
            : DateTime.MaxValue;
        for (var i = 0; i < count; ++i)
        {
            var aoe = _pending[i].AOE;
            if (useRiskLimit)
            {
                var imminent = i < MaxRisky && aoe.Activation <= riskyDeadline;
                aoe.Color = imminent ? Colors.Danger : Colors.AOE;
                aoe.Risky = imminent;
            }
            _displayed.Add(aoe);
        }
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void Update() => PruneExpired();

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (ConfigFor(spell.Action.ID) is not { } config)
        {
            return;
        }

        PruneExpired();
        var activation = Module.CastFinishAt(spell);
        if (spell.EventHappened || activation <= WorldState.CurrentTime || WasRecentlyResolved(spell.Action.ID, caster.InstanceID, activation))
        {
            return;
        }

        var origin = config.LocationTargeted ? spell.LocXZ : caster.Position;
        AddOrRefresh(spell.Action.ID, config.Shape, caster.InstanceID, origin, spell.Rotation, activation);
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (ConfigFor(spell.Action.ID) == null)
        {
            return;
        }

        var now = WorldState.CurrentTime;
        var activation = Module.CastFinishAt(spell);
        RemoveAll(spell.Action.ID, caster.InstanceID);
        if (spell.EventHappened || activation <= now.AddSeconds(EventResolveTolerance))
        {
            RememberResolved(spell.Action.ID, caster.InstanceID, activation, now);
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (ConfigFor(spell.Action.ID) == null || spell.GlobalSequence != 0 && !_seenGlobalSequences.Add(spell.GlobalSequence))
        {
            return;
        }

        var now = WorldState.CurrentTime;
        ++NumCasts;
        var activation = RemoveResolvedByEvent(spell.Action.ID, caster.InstanceID, now) ?? now;
        RememberResolved(spell.Action.ID, caster.InstanceID, activation, now);
    }

    public override void OnActorDeath(Actor actor) => RemoveActor(actor.InstanceID);
    public override void OnActorDestroyed(Actor actor) => RemoveActor(actor.InstanceID);

    private void AddOrRefresh(uint actionID, AOEShape shape, ulong actorID, WPos origin, Angle rotation, DateTime activation)
    {
        RemoveAll(actionID, actorID);
        _pending.Add(new(actionID, new(shape, origin, rotation, activation, actorID: actorID, shapeDistance: shape.Distance(origin, rotation))));
        _pending.Sort((left, right) => left.AOE.Activation.CompareTo(right.AOE.Activation));
    }

    private DateTime? RemoveResolvedByEvent(uint actionID, ulong actorID, DateTime now)
    {
        DateTime? activation = null;
        for (var i = _pending.Count - 1; i >= 0; --i)
        {
            var entry = _pending[i];
            if (entry.ActionID == actionID && entry.AOE.ActorID == actorID && entry.AOE.Activation <= now.AddSeconds(EventResolveTolerance))
            {
                activation = activation == null || entry.AOE.Activation < activation ? entry.AOE.Activation : activation;
                _pending.RemoveAt(i);
            }
        }
        return activation;
    }

    private bool WasRecentlyResolved(uint actionID, ulong actorID, DateTime activation)
        => _resolved.Any(resolved => resolved.ActionID == actionID && resolved.ActorID == actorID && Math.Abs((resolved.Activation - activation).TotalSeconds) <= TombstoneWindow);

    private void RememberResolved(uint actionID, ulong actorID, DateTime activation, DateTime now)
    {
        _resolved.RemoveAll(resolved => resolved.ActionID == actionID && resolved.ActorID == actorID);
        _resolved.Add(new(actionID, actorID, activation, now.AddSeconds(TombstoneWindow)));
    }

    private void PruneExpired()
    {
        var now = WorldState.CurrentTime;
        _pending.RemoveAll(entry => now > entry.AOE.Activation.AddSeconds(ExpireDelay));
        _resolved.RemoveAll(resolved => now > resolved.ExpiresAt);
    }

    private void RemoveAll(uint actionID, ulong actorID) => _pending.RemoveAll(entry => entry.ActionID == actionID && entry.AOE.ActorID == actorID);

    private void RemoveActor(ulong actorID)
    {
        _pending.RemoveAll(entry => entry.AOE.ActorID == actorID);
        _resolved.RemoveAll(entry => entry.ActorID == actorID);
    }
}

// Two-step front/back attacks only expose a cast bar for the first hit. Keep both hits as one
// sequence and show just the imminent half; drawing both at once would incorrectly mark the
// entire arena unsafe. This also survives CastInfo re-sync packets from accelerated replays.
abstract class ReplayValidatedOppositeAOEs(BossModule module) : Components.GenericAOEs(module)
{
    protected readonly record struct SequenceConfig(AOEShape FirstShape, AOEShape SecondShape, uint SecondActionID, double SecondDelay, Angle FirstRotationOffset = default);

    private sealed class Sequence(uint firstActionID, uint secondActionID, ulong actorID, AOEInstance first, AOEInstance second)
    {
        public readonly uint FirstActionID = firstActionID;
        public readonly uint SecondActionID = secondActionID;
        public readonly ulong ActorID = actorID;
        public readonly AOEInstance First = first;
        public readonly AOEInstance Second = second;
        public bool FirstResolved;
    }

    private const double ExpireDelay = 2d;
    private readonly List<Sequence> _sequences = [with(4)];
    private readonly List<AOEInstance> _displayed = [with(4)];
    private readonly HashSet<uint> _seenGlobalSequences = [];

    protected abstract SequenceConfig? ConfigFor(uint firstActionID);

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        PruneExpired();
        _displayed.Clear();
        foreach (var sequence in _sequences.OrderBy(sequence => sequence.FirstResolved ? sequence.Second.Activation : sequence.First.Activation))
        {
            _displayed.Add(sequence.FirstResolved ? sequence.Second : sequence.First);
            break;
        }
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void Update() => PruneExpired();

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (ConfigFor(spell.Action.ID) is not { } config || spell.EventHappened)
            return;

        var activation = Module.CastFinishAt(spell);
        if (activation <= WorldState.CurrentTime || _sequences.Any(sequence => sequence.ActorID == caster.InstanceID && sequence.FirstActionID == spell.Action.ID && sequence.FirstResolved))
            return;

        _sequences.RemoveAll(sequence => sequence.ActorID == caster.InstanceID && sequence.FirstActionID == spell.Action.ID);
        var firstRotation = spell.Rotation + config.FirstRotationOffset;
        var secondRotation = firstRotation + 180f.Degrees();
        var secondActivation = activation.AddSeconds(config.SecondDelay);
        _sequences.Add(new(spell.Action.ID, config.SecondActionID, caster.InstanceID,
            new(config.FirstShape, caster.Position, firstRotation, activation, actorID: caster.InstanceID, shapeDistance: config.FirstShape.Distance(caster.Position, firstRotation)),
            new(config.SecondShape, caster.Position, secondRotation, secondActivation, actorID: caster.InstanceID, shapeDistance: config.SecondShape.Distance(caster.Position, secondRotation))));
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.GlobalSequence != 0 && !_seenGlobalSequences.Add(spell.GlobalSequence))
            return;

        for (var i = _sequences.Count - 1; i >= 0; --i)
        {
            var sequence = _sequences[i];
            if (sequence.ActorID != caster.InstanceID)
                continue;

            if (spell.Action.ID == sequence.FirstActionID && !sequence.FirstResolved)
            {
                sequence.FirstResolved = true;
                ++NumCasts;
                return;
            }
            if (spell.Action.ID == sequence.SecondActionID)
            {
                _sequences.RemoveAt(i);
                ++NumCasts;
                return;
            }
        }
    }

    public override void OnActorDeath(Actor actor) => RemoveActor(actor.InstanceID);
    public override void OnActorDestroyed(Actor actor) => RemoveActor(actor.InstanceID);

    private void PruneExpired()
    {
        var now = WorldState.CurrentTime;
        foreach (var sequence in _sequences)
            if (!sequence.FirstResolved && now > sequence.First.Activation.AddSeconds(0.5d))
                sequence.FirstResolved = true;
        _sequences.RemoveAll(sequence => now > sequence.Second.Activation.AddSeconds(ExpireDelay));
    }

    private void RemoveActor(ulong actorID) => _sequences.RemoveAll(sequence => sequence.ActorID == actorID);
}
