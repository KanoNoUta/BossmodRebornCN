namespace BossMod.Shadowbringers.Foray.Skirmish.ImAMechanicalMan;

public enum OID : uint
{
    Boss = 0x2EA6 // Magitek laborer B, R4.2
}

public enum AID : uint
{
    AutoAttack = 21264,
    Brainstorm = 21539, // 4.0s cast, applies a 10s directional march debuff
    Pulverize = 21540, // 6.0s cast, range 15 circle
    Compress = 21541, // 3.0s cast, range 50 width 7 cross
    Accelerate = 21542, // 5.0s cast, range 6 circle stack on the cast target
    DisposeFirst = 21543, // 5.0s cast, range 40 cone, starts the rotating sequence
    DisposeRest = 21544, // 0.5s cast, alternating with DisposeRest2
    RotateCW = 21546,
    RotateCCW = 21547,
    DisposeSingle = 21548, // 5.0s cast, range 40 cone, no rotation
    DisposeRest2 = 21549
}

public enum SID : uint
{
    ForwardMarch = 2381,
    AboutFace = 2382,
    LeftFace = 2383,
    RightFace = 2384
}

public enum IconID : uint
{
    RotateCW = 167,
    RotateCCW = 168
}

sealed class Compress(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Compress, new AOEShapeCross(50f, 3.5f));
sealed class Accelerate(BossModule module) : Components.StackWithCastTargets(module, (uint)AID.Accelerate, 6f);
sealed class Pulverize(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Pulverize, 15f);
sealed class DisposeSingle(BossModule module) : Components.SimpleAOEs(module, (uint)AID.DisposeSingle, DisposeRotating.Cone);

sealed class DisposeRotating(BossModule module) : Components.GenericAOEs(module)
{
    // No angle is encoded in the replay or Action.Omen. The 2026-10-21 replay supports
    // a 60-degree cone (hits near 28 degrees, misses near 40 degrees from its axis).
    public static readonly AOEShapeCone Cone = new(40f, 30f.Degrees());
    public WPos Origin;
    public Angle FirstRotation;
    public Angle Increment;
    public int NumResolved;
    public int Remaining;
    public Angle Rotation;
    public DateTime NextActivation;
    private readonly List<AOEInstance> _aoes = new(2);

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _aoes.Clear();
        if (Remaining > 0)
        {
            if (Remaining > 1 && Increment != default)
                _aoes.Add(new(Cone, Origin, Rotation + Increment, NextActivation.AddSeconds(1.2d)));
            _aoes.Add(new(Cone, Origin, Rotation, NextActivation, Colors.Danger));
        }
        return CollectionsMarshal.AsSpan(_aoes);
    }

    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (actor != Module.PrimaryActor)
            return;
        if (iconID == (uint)IconID.RotateCW)
            Increment = -60f.Degrees();
        else if (iconID == (uint)IconID.RotateCCW)
            Increment = 60f.Degrees();
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster != Module.PrimaryActor)
            return;
        switch (spell.Action.ID)
        {
            case (uint)AID.RotateCW:
                Increment = -60f.Degrees();
                break;
            case (uint)AID.RotateCCW:
                Increment = 60f.Degrees();
                break;
            case (uint)AID.DisposeFirst:
                NumResolved = 0;
                Remaining = 7; // initial cone plus six 60-degree turns, ending at the initial heading
                FirstRotation = spell.Rotation;
                SetCast(caster, spell);
                break;
            case (uint)AID.DisposeRest:
            case (uint)AID.DisposeRest2:
                // Also show the current cast if the module was loaded partway through the sequence.
                if (Remaining == 0)
                    Remaining = 1;
                SetCast(caster, spell);
                break;
        }
    }

    private void SetCast(Actor caster, ActorCastInfo spell)
    {
        Origin = caster.Position;
        Rotation = spell.Rotation;
        NextActivation = Module.CastFinishAt(spell);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (caster == Module.PrimaryActor && spell.Action.ID is ((uint)AID.DisposeFirst or (uint)AID.DisposeRest or (uint)AID.DisposeRest2) && Remaining > 0)
        {
            ++NumCasts;
            ++NumResolved;
            --Remaining;
            Rotation = spell.Rotation + Increment;
            NextActivation = WorldState.FutureTime(1.2d);
            if (Remaining == 0)
                Increment = default;
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (caster == Module.PrimaryActor && !spell.EventHappened && spell.Action.ID is ((uint)AID.DisposeFirst or (uint)AID.DisposeRest or (uint)AID.DisposeRest2))
        {
            Remaining = 0;
            Increment = default;
        }
    }

    public override void Update()
    {
        // Drop interrupted/stale predictions without relying on an action effect that may never arrive.
        if (Module.PrimaryActor.IsDeadOrDestroyed || Remaining > 0 && WorldState.CurrentTime > NextActivation.AddSeconds(2d))
        {
            Remaining = 0;
            Increment = default;
        }
    }

    public override void AddGlobalHints(GlobalHints hints)
    {
        if (Remaining > 0 && Increment != default)
            hints.Add(Increment.Rad > 0 ? "执行清理：逆时针旋转，每次60°" : "执行清理：顺时针旋转，每次60°");
    }

    public bool MarchUnsafe(WPos from, WPos to, DateTime start, DateTime end)
    {
        var rotation = Rotation;
        var activation = NextActivation;
        for (var i = 0; i < Remaining; ++i)
        {
            if (activation >= start && activation <= end)
            {
                var fraction = (float)Math.Clamp((activation - start).TotalSeconds / Math.Max((end - start).TotalSeconds, 0.001d), 0d, 1d);
                if (Cone.Check(from + (to - from) * fraction, Origin, rotation))
                    return true;
            }
            if (Increment == default)
                break;
            rotation += Increment;
            activation = activation.AddSeconds(1.2d);
        }
        return false;
    }
}

sealed class Brainstorm : Components.StatusDrivenForcedMarch
{
    private readonly DisposeRotating _dispose;
    private static readonly AOEShapeCone clearedSector = new(40f, 25f.Degrees());
    private Angle? _preparedFacing;
    private DateTime _activation;

    public Brainstorm(BossModule module) : base(module, 3f, (uint)SID.ForwardMarch, (uint)SID.AboutFace, (uint)SID.LeftFace, (uint)SID.RightFace)
    {
        // Replay: 3 seconds at ~6y/s, about 18y total (override the shared 6.6y/s default).
        MovementSpeed = 6f;
        _dispose = module.FindComponent<DisposeRotating>()!;
    }

    public override bool DestinationUnsafe(int slot, Actor actor, WPos pos)
    {
        if (base.DestinationUnsafe(slot, actor, pos))
            return true;
        var moves = ForcedMovements(actor);
        if (moves.Count == 0 || !State.TryGetValue(actor.InstanceID, out var state))
            return false;
        var start = state.ForcedEnd > WorldState.CurrentTime ? WorldState.CurrentTime : state.PendingMoves[0].activation;
        var end = state.ForcedEnd > WorldState.CurrentTime ? state.ForcedEnd : start.AddSeconds(Duration);
        return _dispose.MarchUnsafe(moves[0].from, pos, start, end);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var state = State.GetValueOrDefault(actor.InstanceID);
        if (actor.IsDeadOrDestroyed || Module.PrimaryActor.IsDeadOrDestroyed || state == null || state.PendingMoves.Count == 0)
        {
            _preparedFacing = null;
            return;
        }
        if (_dispose.Remaining == 0 || _dispose.Increment == default)
            return;

        var move = state.PendingMoves[0];
        if (_activation != move.activation)
            _preparedFacing = null;
        _activation = move.activation;

        // Like CE32's OrderForcedMarch: preposition near the center and publish
        // a 350-degree forbidden facing sector at the directional debuff's expiry.
        // The standard smart-facing/AI/IPC consumers handle this; no private lock.
        hints.AddForbiddenZone(new SDInvertedCircle(_dispose.Origin, 7f), move.activation);
        var landing = _dispose.Origin + (_dispose.FirstRotation + _dispose.Increment * 1.5f).ToDirection() * (MovementSpeed * move.duration);
        var direction = landing - actor.Position;
        var preferred = _preparedFacing ?? (direction.LengthSq() > 0.01f ? Angle.FromDirection(direction) - move.dir : actor.Rotation);
        if (TrySafeFacing(actor, move.dir, move.duration, move.activation, preferred, out var facing))
        {
            _preparedFacing = facing;
            hints.ForbiddenDirections.Add(((facing + 180f.Degrees()).Normalized(), 175f.Degrees(), move.activation));
        }
    }

    private bool TrySafeFacing(Actor actor, Angle offset, float duration, DateTime activation, Angle preferred, out Angle facing)
    {
        bool Safe(Angle candidate)
        {
            // The allowed facing window is +/-5 degrees, so its edges must also
            // lead to safe paths, not just its center direction.
            for (var i = -1; i <= 1; ++i)
            {
                var destination = actor.Position + (candidate + offset + (i * 5f).Degrees()).ToDirection() * (MovementSpeed * duration);
                if (!Arena.InBounds(destination) || _dispose.MarchUnsafe(actor.Position, destination, activation, activation.AddSeconds(duration)))
                    return false;
            }
            return true;
        }

        if (Safe(preferred))
        {
            facing = preferred;
            return true;
        }
        for (var i = 1; i <= 36; ++i)
        {
            var delta = (i * 5f).Degrees();
            if (Safe(facing = preferred + delta) || Safe(facing = preferred - delta))
                return true;
        }
        facing = default;
        return false;
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        base.AddHints(slot, actor, hints);
        if (HasForcedMovements(actor) && _dispose.Remaining > 0)
            hints.Add("强制移动约18米：将落点对准第2/3次清理已扫过的区域", false);
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        base.DrawArenaForeground(pcSlot, pc);
        if (!HasForcedMovements(pc) || _dispose.Remaining == 0 || _dispose.Increment == default)
            return;
        // Outline only sectors that have actually resolved; leave a 5-degree margin at their edges.
        for (var i = 1; i <= 2; ++i)
            if (_dispose.NumResolved > i)
                clearedSector.Outline(Arena, _dispose.Origin, _dispose.FirstRotation + _dispose.Increment * i, Colors.Safe);
    }
}

sealed class ImAMechanicalManStates : StateMachineBuilder
{
    public ImAMechanicalManStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<Compress>()
            .ActivateOnEnter<Accelerate>()
            .ActivateOnEnter<Pulverize>()
            .ActivateOnEnter<DisposeSingle>()
            .ActivateOnEnter<DisposeRotating>()
            .ActivateOnEnter<Brainstorm>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Contributed, Contributors = "COXOIDA", GroupType = BossModuleInfo.GroupType.ForayFATE, GroupID = 735, NameID = 1620)]
public sealed class ImAMechanicalMan(WorldState ws, Actor primary) : OpenWorldFate(ws, primary);
