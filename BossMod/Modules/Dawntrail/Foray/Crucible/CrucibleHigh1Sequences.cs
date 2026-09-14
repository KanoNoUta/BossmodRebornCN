namespace BossMod.Dawntrail.Foray.Crucible;

// 48719 predicts both hits, including when loading before the helpers arrive.
// ARR: R13 steel, then the R30 ring two seconds later. Misses out to 6.15y
// establish a conservative R6 inner pocket, not the ring's exact inner edge.
sealed class CrucibleGargoyleInOut(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeCircle Steel = new(13);
    private static readonly AOEShapeDonut Ring = new(6, 30);
    private AOEInstance? _steel;
    private AOEInstance? _ring;
    private DateTime _steelResolved;
    private DateTime _ringResolved;
    private readonly List<AOEInstance> _display = [];

    public override void Update()
    {
        if (_steel is { } steel && steel.Activation.AddSeconds(0.25) < WorldState.CurrentTime)
            _steel = null;
        if (_ring is { } ring && ring.Activation.AddSeconds(1) < WorldState.CurrentTime)
            _ring = null;
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.EventHappened || spell.Action.ID is not (48719 or 48720 or 48721))
            return;
        Update();
        var finish = Module.CastFinishAt(spell);
        var steelTime = finish.AddSeconds(spell.Action.ID switch { 48719 => 0.6, 48721 => -2, _ => 0 });
        var ringTime = steelTime.AddSeconds(2);
        if (steelTime > WorldState.CurrentTime && _steelResolved.AddSeconds(1) < WorldState.CurrentTime
            && (spell.Action.ID == 48720 || _steel == null))
            _steel = new(Steel, spell.LocXZ, activation: steelTime, actorID: spell.Action.ID == 48720 ? caster.InstanceID : 0);
        if (ringTime > WorldState.CurrentTime && _ringResolved.AddSeconds(1) < WorldState.CurrentTime
            && (spell.Action.ID == 48721 || _ring == null))
            _ring = new(Ring, spell.LocXZ, activation: ringTime, actorID: spell.Action.ID == 48721 ? caster.InstanceID : 0);
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        Update();
        _display.Clear();
        if (_steel is { } steel)
        {
            steel.Color = Colors.Danger;
            _display.Add(steel);
        }
        if (_ring is { } ring)
        {
            ring.Risky = _steel == null;
            ring.Color = ring.Risky ? Colors.Danger : Colors.AOE;
            _display.Add(ring);
        }
        return CollectionsMarshal.AsSpan(_display);
    }

    public override void DrawArenaBackground(int pcSlot, Actor pc)
    {
        foreach (ref readonly var aoe in ActiveAOEs(pcSlot, pc))
            if (aoe.Risky)
                aoe.Shape.Draw(Arena, aoe.Origin, aoe.Rotation, aoe.Color);
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        foreach (ref readonly var aoe in ActiveAOEs(pcSlot, pc))
            if (!aoe.Risky)
                aoe.Shape.Outline(Arena, aoe.Origin, aoe.Rotation, aoe.Color);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        Update();
        if (_steel is { } steel)
        {
            hints.AddForbiddenZone(new SDCircle(steel.Origin, 13.25f), steel.Activation.AddSeconds(-0.5));
            if (_ring is { } next)
            {
                hints.AddForbiddenZone(new SDInvertedCircle(steel.Origin, 14.5f), steel.Activation.AddSeconds(-0.5));
                if (Module.FindComponent<CrucibleMiasma>()?.ArmedPositions().ToArray() is { Length: > 0 } mines)
                    hints.AddForbiddenZone(new RingApproach(next.Origin, mines), steel.Activation.AddSeconds(-0.5));
            }
        }
        else if (_ring is { } ring)
            hints.AddForbiddenZone(new SDInvertedCircle(ring.Origin, 5.5f), ring.Activation.AddSeconds(-0.3));
    }

    // Pick a staging angle with a straight path into the ring pocket. There
    // are only two seconds between hits; detouring around a ball after steel
    // is too slow once movement latency is included.
    private sealed class RingApproach(WPos origin, WPos[] mines) : ShapeDistance
    {
        public override float Distance(in WPos p)
        {
            var offset = origin - p;
            var length = offset.Length();
            if (length < 5.3f)
                return 1;
            var direction = offset / length;
            foreach (var mine in mines)
            {
                var toMine = mine - p;
                var along = Math.Clamp(toMine.X * direction.X + toMine.Z * direction.Z, 0, length - 5.3f);
                if (mine.InCircle(p + along * direction, CrucibleMiasma.ContactRadius + 0.5f))
                    return 0;
            }
            return 1;
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == 48720 && (_steel?.ActorID is null or 0 || _steel.Value.ActorID == caster.InstanceID))
        {
            _steel = null;
            _steelResolved = WorldState.CurrentTime;
        }
        if (spell.Action.ID == 48721 && (_ring?.ActorID is null or 0 || _ring.Value.ActorID == caster.InstanceID))
        {
            _ring = null;
            _ringResolved = WorldState.CurrentTime;
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (!spell.EventHappened && spell.NPCRemainingTime > 0.5f)
        {
            if (spell.Action.ID == 48719)
                (_steel, _ring) = (null, null);
            if (spell.Action.ID == 48720 && _steel?.ActorID == caster.InstanceID)
                _steel = null;
            if (spell.Action.ID == 48721 && _ring?.ActorID == caster.InstanceID)
                _ring = null;
        }
    }

    public override void OnActorDeath(Actor actor)
    {
        if (actor.OID == 19650)
            (_steel, _ring) = (null, null);
        else
        {
            if (_steel?.ActorID == actor.InstanceID)
                _steel = null;
            if (_ring?.ActorID == actor.InstanceID)
                _ring = null;
        }
    }
    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);
}

// 50932 supplies the actual dash endpoint ~2.8s before two uncast 48718 hits,
// 2.05s apart and facing opposite ways. ARR hits at -84 degrees and misses at
// +96 degrees bound the half-cleave; use the rear pocket while awaiting a tell.
sealed class CrucibleGargoyleCharge(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeCone Cleave = new(60, 90f.Degrees());
    private readonly List<AOEInstance> _sequence = [];
    private readonly HashSet<uint> _events = [];
    private ulong _boss;

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == 50932 && caster.OID == 19650 && spell.TargetXZ.InRect(Module.Center, 25, 25))
        {
            if (!_events.Add(spell.GlobalSequence))
                return;
            _boss = caster.InstanceID;
            _sequence.Clear();
            _sequence.Add(new(Cleave, spell.TargetXZ, spell.Rotation, WorldState.FutureTime(2.8)));
            _sequence.Add(new(Cleave, spell.TargetXZ, spell.Rotation + 180f.Degrees(), WorldState.FutureTime(4.85)));
        }
        else if (spell.Action.ID == 48718 && _events.Add(spell.GlobalSequence))
        {
            if (_sequence.Count == 2)
            {
                _sequence.RemoveAt(0);
                _sequence[0] = new(Cleave, caster.Position, spell.Rotation + 180f.Degrees(), WorldState.FutureTime(2.05));
            }
            else
                _sequence.Clear();
        }
    }
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _sequence.RemoveAll(a => a.Activation.AddSeconds(0.5) < WorldState.CurrentTime);
        return _sequence.Select((a, i) => a with { Risky = i == 0, Color = i == 0 ? Colors.Danger : Colors.AOE }).ToArray();
    }
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var aoes = ActiveAOEs(slot, actor);
        if (aoes.Length == 0)
            return;
        var next = aoes[0];
        CrucibleScorpionAI.Avoid(hints, next);
        // Stay near the landing to cross to the first cleave's safe side after
        // it resolves, even when the dash lands beside the electric boundary.
        var destination = next.Origin - 3 * next.Rotation.ToDirection();
        hints.GoalZones.Add(p => Math.Max(0, 20 - 3 * (p - destination).Length()));
    }
    public override void OnActorDeath(Actor actor)
    {
        if (actor.InstanceID == _boss)
            _sequence.Clear();
    }
    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);
}

// Every recorded six-pulse breath originates 20y BEHIND the visual's facing,
// ~5.4s after cast start. Guide toward its inner pocket early and hold through
// all six pulses; the inner radius is unknown, so only draw a conservative
// destination, not a damage donut. The field bounds still constrain navigation.
sealed class CrucibleDragonWaves(BossModule module) : Components.GenericAOEs(module, 48808u)
{
    private WPos _origin;
    private DateTime _first;
    private DateTime _activation;
    private DateTime _expires;
    private int _pulses;
    private readonly HashSet<uint> _events = [];
    private static readonly AOEShapeCircle Spot = new(7);
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != 48807 || spell.EventHappened)
            return;
        var first = Module.CastFinishAt(spell, 2.4);
        if (Math.Abs((first - _first).TotalSeconds) < 0.75)
            return;
        _first = _activation = first;
        _expires = first.AddSeconds(6.5);
        _origin = Module.Center - 20 * spell.Rotation.ToDirection();
        _pulses = 0;
    }
    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == WatchedAction && _events.Add(spell.GlobalSequence))
        {
            _origin = caster.Position;
            if (_expires < WorldState.CurrentTime)
            {
                _expires = WorldState.FutureTime(6);
                _pulses = 0;
            }
            _activation = WorldState.FutureTime(1.07);
            if (++_pulses >= 6)
                _expires = default;
        }
    }
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => _expires > WorldState.CurrentTime
        ? new[] { new AOEInstance(Spot, _origin, activation: _activation, color: Colors.SafeFromAOE, risky: false) } : [];
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (_expires > WorldState.CurrentTime)
            hints.AddForbiddenZone(new SDInvertedCircle(_origin, Spot.Radius), _activation.AddSeconds(-0.5));
    }
    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == 48807 && !spell.EventHappened && spell.NPCRemainingTime > 0.5f)
            _expires = default;
    }
    public override void OnActorDeath(Actor actor)
    {
        if (actor.OID == 19672)
            _expires = default;
    }
    public override void OnActorDestroyed(Actor actor) => OnActorDeath(actor);
}
