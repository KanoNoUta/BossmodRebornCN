using BossMod.Dawntrail.Foray.CriticalEngagement;

namespace BossMod.Dawntrail.Foray.CriticalEngagement.CE210AcceptNoImitators;

public enum OID : uint
{
    Boss = 0x4C77, // R3.0, BNpcName 14801, morphing mage
    BoundaryController = 0x4DFD, // non-targetable controller at arena center
    Helper = 0x233C
}

public enum AID : uint
{
    AutoAttack = 0xBCCE,
    BlackenedRainVisual = 0xBCCF, // boss->self, 4.0s cast, raidwide visual
    BlackenedRain = 0xBCD0, // three helpers->players, 5.0s cast, raidwide damage
    DarkDealing = 0xBCD1, // boss->player, 5.0s cast, tankbuster
    ChangeFire = 0xBCD2, // boss->self, 4.0s cast, visual
    ChangeWind = 0xBCD3, // boss->self, 4.0s cast, visual
    Revert = 0xBCD4, // boss->self, no cast, visual
    TongueOfFlame = 0xBCD5, // boss->self, 4.0s cast, range 15 circle
    HellfireFetchVisual = 0xBCD6,
    HellwardBound = 0xBCD7, // boss->location, 6.0s cast, visual
    HellwardBoundHit = 0xBCD8,
    HellfireFetch = 0xBCD9, // helper->location, 7.0s cast, range 6 circle
    HellishBreathVisual = 0xBCDA, // boss->self, 6.0s cast, visual
    HellishBreathShort = 0xBCDB, // helper->self, 2.0s cast, range 60 60-degree cone
    HellishBreathMedium = 0xBCDC, // helper->self, 4.0s cast, range 60 60-degree cone
    HellishBreathLong = 0xBCDD, // helper->self, 6.0s cast, range 60 60-degree cone
    HellishBreathHit1 = 0xBCDE,
    HellishBreathHit2 = 0xBCDF,
    HellishBreathHit3 = 0xBCE0,
    HellishBreathHit4 = 0xBCE1,
    HellishBreathQuickCenter = 0xBE16, // helper, 1.1s cast, range 60 60-degree cone
    HellishBreathQuickLeft = 0xBE17, // helper, 1.1s cast, range 60 60-degree cone
    HellishBreathQuickRight = 0xC5F5, // helper, 1.1s cast, range 60 60-degree cone
    CyclonicRing = 0xBCE2, // boss->self, 4.0s cast, range 10-30 donut
    ShapeshiftingSupercellVisual1 = 0xBCE3, // boss->self, 5.5s cast, visual
    ShapeshiftingSupercellVisual2 = 0xBCE4, // boss->self, 5.5s cast, visual
    ShapeshiftingSupercellConeLong = 0xBCE5, // helper->self, 6.0s cast, range 60 90-degree cone
    ShapeshiftingSupercellResolve = 0xBCE6,
    ShapeshiftingSupercellConeShort = 0xBCE7, // helper->self, 1.5s cast, range 60 90-degree cone
    ShapeshiftingSupercellCircle = 0xBCE8, // helper->self, 6.0s cast, range 8 circle
    ShapeshiftingSupercellDonutInner = 0xBCE9, // helper->self, 6.0s cast, range 10-16 donut
    ShapeshiftingSupercellDonutOuter = 0xBCEA, // helper->self, 6.0s cast, range 16-30 donut
    ShapeshiftingSupercellExtraCircle = 0xC64F, // helper->self, 6.0s cast, range 8 circle
    MadeMagicVisual = 0xBCEB, // boss->self, 4.0s cast, visual
    MadeMagic = 0xBCEC, // helper pulses; radius is modified by status 1909
    CycloneCrossingVisual = 0xBCED, // boss->self, 10.5s cast, visual
    CycloneCrossing = 0xBCEE, // helper->self, 11.5s cast, range 60 width 16 cross
    LethalBoundary = 0xBCEF, // controller, persistent 20-30 donut
    UnknownTarget1 = 0xBCF0,
    UnknownTarget2 = 0xBCF1,
    UnknownLocation = 0xC620
}

public enum SID : uint
{
    AreaOfInfluenceUp = 1909 // Made Magic helper, extra 1-7; circle radius = extra * 2.5y
}

// The real arena is a 25y circle (player p99.9 radius 24.8, boundary hit at 24.6, charge targets at
// 25), so mark the persistent electric fence with a thin ring at the edge instead of a 20-30 donut.
sealed class LethalBoundary(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeDonut Shape = new(24.5f, 25.5f);
    private readonly AOEInstance[] _aoe = [new(Shape, module.Arena.Center)];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => _aoe;
}

// Every avoidable AOE below has an authoritative cast-start packet from the actor that owns the
// shape. The helpers also carry their actual origin/rotation, so none of the patterns are inferred
// from the boss transformation visuals.
sealed class MorphingMageAOEs(BossModule module) : ReplayValidatedCastAOEs(module)
{
    private static readonly AOEShapeCircle Tongue = new(15f);
    private static readonly AOEShapeCircle Hellfire = new(6f);
    private static readonly AOEShapeCone HellishBreath = new(60f, 30f.Degrees());
    private static readonly AOEShapeDonut CyclonicRing = new(10f, 30f);
    private static readonly AOEShapeCone SupercellCone = new(60f, 45f.Degrees());
    private static readonly AOEShapeCircle SupercellCircle = new(8f);
    private static readonly AOEShapeDonut SupercellInner = new(10f, 16f);
    private static readonly AOEShapeDonut SupercellOuter = new(16f, 30f);
    private static readonly AOEShapeCross CycloneCross = new(60f, 8f);

    // Several patterns expose the full sequence at once (notably the 2/4/6s breath cones).
    // Keep simultaneous casts dangerous, but later preview steps must not block AI movement yet.
    protected override double RiskyActivationWindow => 0.5d;

    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.TongueOfFlame => new(Tongue),
        (uint)AID.HellfireFetch => new(Hellfire, true),
        (uint)AID.HellishBreathShort or (uint)AID.HellishBreathMedium or (uint)AID.HellishBreathLong
            or (uint)AID.HellishBreathQuickCenter or (uint)AID.HellishBreathQuickLeft or (uint)AID.HellishBreathQuickRight => new(HellishBreath),
        (uint)AID.CyclonicRing => new(CyclonicRing),
        (uint)AID.ShapeshiftingSupercellConeLong or (uint)AID.ShapeshiftingSupercellConeShort => new(SupercellCone),
        (uint)AID.ShapeshiftingSupercellCircle or (uint)AID.ShapeshiftingSupercellExtraCircle => new(SupercellCircle),
        (uint)AID.ShapeshiftingSupercellDonutInner => new(SupercellInner),
        (uint)AID.ShapeshiftingSupercellDonutOuter => new(SupercellOuter),
        (uint)AID.CycloneCrossing => new(CycloneCross),
        _ => null
    };
}

// Made Magic creates four fixed helpers 7.8y from center. They pulse every ~0.6s while status
// 1909 grows from extra 1 through 7. The pulse is an expanding wave: actors already crossed by
// an earlier pulse can stand inside the current radius without being hit again. Treating it as a
// filled circle makes extra 7 falsely cover the entire arena, so only the next 2.5y ring is risky.
sealed class MadeMagic(BossModule module) : Components.GenericAOEs(module)
{
    private readonly Dictionary<ulong, AOEInstance> _pending = [];
    private readonly List<AOEInstance> _displayed = new(4);
    private readonly HashSet<uint> _seenGlobalSequences = [];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _displayed.Clear();
        _displayed.AddRange(_pending.Values);
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void Update()
    {
        // The four helpers can persist after the mechanic without a status-loss packet (replays and
        // live packet loss both do this). If no pulse has refreshed a ring for a while, it is stale
        // and must not remain drawn until the next mechanic.
        var now = WorldState.CurrentTime;
        foreach (var key in _pending.Keys.Where(key => now > _pending[key].Activation.AddSeconds(1.5d)).ToArray())
            _pending.Remove(key);
    }

    public override void OnStatusGain(Actor actor, ref ActorStatus status)
    {
        if (actor.OID != (uint)OID.Helper || !actor.Position.InCircle(Arena.Center, 30f)
            || status.ID != (uint)SID.AreaOfInfluenceUp || status.Extra is < 1 or > 7)
            return;

        var outer = status.Extra * 2.5f;
        AOEShape shape = status.Extra == 1 ? new AOEShapeCircle(outer) : new AOEShapeDonut(outer - 2.5f, outer);
        _pending[actor.InstanceID] = new(shape, actor.Position, color: Colors.Danger, activation: WorldState.FutureTime(0.3f),
            actorID: actor.InstanceID, shapeDistance: shape.Distance(actor.Position, default));
    }

    public override void OnStatusLose(Actor actor, ref ActorStatus status)
    {
        if (status.ID == (uint)SID.AreaOfInfluenceUp)
            _pending.Remove(actor.InstanceID);
    }

    public override void OnActorDeath(Actor actor) => _pending.Remove(actor.InstanceID);
    public override void OnActorDestroyed(Actor actor) => _pending.Remove(actor.InstanceID);

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID != (uint)AID.MadeMagic
            || spell.GlobalSequence != 0 && !_seenGlobalSequences.Add(spell.GlobalSequence)
            || !_pending.TryGetValue(caster.InstanceID, out var current))
            return;

        // Every status step normally pulses twice (extra 7 pulses three times). The first event is
        // therefore not the end of the ring; move the same warning to the next observed cadence.
        // A new status replaces its geometry, and status loss performs the final cleanup.
        _pending[caster.InstanceID] = current with { Activation = WorldState.FutureTime(0.58d) };
    }
}

// The three BCD0 helpers split one raidwide across the participant list. The boss cast is the
// stable, non-duplicated warning and starts one second before the helper cast bars.
sealed class BlackenedRain(BossModule module) : Components.RaidwideCast(module, (uint)AID.BlackenedRainVisual);
sealed class DarkDealing(BossModule module) : Components.SingleTargetDelayableCast(module, (uint)AID.DarkDealing);
sealed class HellwardBound : Components.ChargeAOEs
{
    public HellwardBound(BossModule module) : base(module, (uint)AID.HellwardBound, 5f)
    {
        Color = Colors.Danger;
    }
}

// The charge cast telegraphs only the first dash. After it resolves the boss dashes repeatedly
// across the arena (replay: center -> SE corner -> west edge -> back east, each ~0.3s segment),
// and those later segments carry damage too. Track the boss's fast movement and draw every dash
// segment as a short-lived danger line so the whole multi-dash sequence is visible.
sealed class ChargeDashes(BossModule module) : Components.GenericAOEs(module)
{
    private const float HalfWidth = 5f;
    private const float MinDashStep = 0.25f; // ~100y/s at 100Hz replay / ~1.7y per 60Hz frame; walks stay well below
    private const double DashLifetime = 1.5d;
    private readonly List<AOEInstance> _segments = [];
    private readonly List<AOEInstance> _displayed = [with(32)];
    private WPos _lastPosition;
    private bool _hasLast;

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        PruneExpired();
        _displayed.Clear();
        _displayed.AddRange(_segments);
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void Update()
    {
        var boss = Module.PrimaryActor;
        if (boss.IsDeadOrDestroyed)
        {
            _segments.Clear();
            _hasLast = false;
            return;
        }

        var now = WorldState.CurrentTime;
        if (_hasLast)
        {
            var delta = boss.Position - _lastPosition;
            if (delta.LengthSq() > MinDashStep * MinDashStep)
            {
                var rotation = Angle.FromDirection(delta);
                var shape = new AOEShapeRect(delta.Length(), HalfWidth);
                _segments.Add(new(shape, _lastPosition, rotation, now, Colors.Danger, true, boss.InstanceID,
                    shapeDistance: shape.Distance(_lastPosition, rotation)));
            }
        }
        _lastPosition = boss.Position;
        _hasLast = true;
        PruneExpired();
    }

    private void PruneExpired()
    {
        var now = WorldState.CurrentTime;
        _segments.RemoveAll(entry => now > entry.Activation.AddSeconds(DashLifetime));
    }
}

sealed class AcceptNoImitatorsStates : StateMachineBuilder
{
    public AcceptNoImitatorsStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<LethalBoundary>()
            .ActivateOnEnter<MorphingMageAOEs>()
            .ActivateOnEnter<MadeMagic>()
            .ActivateOnEnter<BlackenedRain>()
            .ActivateOnEnter<DarkDealing>()
            .ActivateOnEnter<HellwardBound>()
            .ActivateOnEnter<ChargeDashes>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Contributed,
    StatesType = typeof(AcceptNoImitatorsStates),
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID),
    PrimaryActorOID = (uint)OID.Boss,
    Contributors = "KanoNoUta",
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Foray,
    GroupType = BossModuleInfo.GroupType.CriticalEngagement,
    GroupID = 1093u,
    NameID = 63u,
    SortOrder = 9)]
// Replay-verified 25y circular arena (players reach r24.8, the charge ends at r25 and the fence
// kills at 24.6); the old 20y circle clipped the charge and misplaced the boundary drawing.
public sealed class AcceptNoImitators(WorldState ws, Actor primary) : BossModule(ws, primary, new(500f, -310f), new ArenaBoundsCircle(25f));
