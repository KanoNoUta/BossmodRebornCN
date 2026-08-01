using BossMod.Dawntrail.Foray.CriticalEngagement;
using static BossMod.Components.GenericKnockback;

namespace BossMod.Dawntrail.Foray.CriticalEngagement.CE206RebelliousFamiliar;

public enum OID : uint
{
    Boss = 0x4C4F, // R3.8, BNpcName 14791, cornered gemstone
    YellowGem = 0x4C50,
    BoundaryController = 0x4D88, // non-targetable controller at arena center
    Helper = 0x233C
}

public enum AID : uint
{
    AutoAttack = 0xC6A4, // boss->player, no cast, single-target
    LethalBoundary = 0xBFD0, // controller, persistent out-of-bounds kill field
    YellowGemstones = 0xBC98,
    YellowGemActiveVisual = 0xBC99, // yellow gem->location, 3.0s cast, no damage event
    TopazRay = 0xBC9A, // yellow gem->location, 3.0s cast, range 4 circle
    RubyLight = 0xBC9C,
    RubyReflectionShort = 0xBC9D, // helper, 20y long, 20y wide rect
    RubyReflectionLong1 = 0xBC9E, // helper, 40y long, 40y wide rect
    RubyReflectionLong2 = 0xBC9F,
    CircularKnockbackTelegraph = 0xBCA0, // helper, 60y circle; resolves as 30y away knockback
    KnockAsideTelegraph = 0xBCA1, // helper, 40y long, 60y wide rect; resolves as 15y left knockback
    RavenousGods = 0xBCA3,
    RavenousGodsSecond = 0xBCA4,
    ClawThenTail = 0xBCA6, // 45y 180-degree cone
    TailThenClaw = 0xBCA7, // 40y 180-degree cone
    ClawThenTailSecond = 0xBCA8,
    TailThenClawSecond = 0xBCA9,
    Howl = 0xBCAA,
    ComboEndVisual = 0xBCAB, // boss, no targets/effects; animation-only combo terminator
    RubyOuterReflection = 0xC4F2,
    RevertModel = 0xC51D, // boss, model-state reset after the claw/tail sequence
    RubyGlowHit = 0xC5CD, // helpers, split packets for the Ruby Light raidwide
    HowlAlt = 0xC161,
    RavenousGodsCircleHit = 0xC162,
    RavenousGodsAsideHit = 0xC163
}


sealed class ClawTailCombo(BossModule module) : ReplayValidatedOppositeAOEs(module)
{
    private static readonly AOEShapeCone Claw = new(45f, 90f.Degrees());
    private static readonly AOEShapeCone Tail = new(40f, 90f.Degrees());

    protected override SequenceConfig? ConfigFor(uint firstActionID) => firstActionID switch
    {
        (uint)AID.ClawThenTail => new(Claw, Tail, (uint)AID.ClawThenTailSecond, 2d),
        // Replay-verified: even though the boss visually spins around before the first hit lands,
        // both first hits resolve centered on the cast-start rotation (hits within +-90 deg of it)
        // and both second hits resolve on the opposite half. No rotation offset for either combo.
        (uint)AID.TailThenClaw => new(Tail, Claw, (uint)AID.TailThenClawSecond, 2d),
        _ => null
    };
}

sealed class TopazRay(BossModule module) : ReplayValidatedCastAOEs(module)
{
    private static readonly AOEShapeCircle Shape = new(4f);

    protected override AOEConfig? ConfigFor(uint actionID) => actionID == (uint)AID.TopazRay ? new(Shape, true) : null;
}

// BCA0 telegraphs a 60y radial knockback that resolves as C162 when Ravenous Gods completes,
// ~6s after the short telegraph ends. The arrow must not sit on the radar for the whole cast:
// show it only in the final two seconds, and mark the square-wall forbidden zone so automation
// stands where the 30y push cannot slam it into the electric fence.
sealed class CircularKnockback(BossModule module) : Components.GenericKnockback(module)
{
    private static readonly AOEShapeCircle Shape = new(60f);
    private const float Distance = 30f;
    private const float ArenaHalfWidth = 24f;
    private const double HitDelay = 6.0d;
    private const double ShowBeforeHit = 2d;
    private readonly List<Knockback> _casters = [];
    private readonly List<Knockback> _displayed = [with(4)];

    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor)
    {
        PruneExpired();
        _displayed.Clear();
        var now = WorldState.CurrentTime;
        foreach (var kb in _casters)
            if (now >= kb.Activation.AddSeconds(-ShowBeforeHit))
                _displayed.Add(kb);
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void Update() => PruneExpired();

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        // The lateral (aside) shove resolves ~3.4s before this radial push, so the player is already
        // displaced by it when the circle resolves. If we evaluate the safe square from the current
        // position we get the wrong side of the origin (the aside can carry the player past the
        // circle center, flipping the radial escape direction into the fence). While the aside is
        // still pending, offset both the square and the push origin by that displacement so the
        // forbidden zone describes the post-aside push. Show it for the whole cast (no 2s gate) so
        // automation can pre-position for the combined knockback rather than react after the shove.
        var aside = Module.FindComponent<KnockAside>();
        foreach (var kb in _casters)
        {
            var center = Arena.Center;
            var origin = kb.Origin;
            if (aside != null && aside.TryGetPendingAsidePush(kb.Activation, out var a))
            {
                center -= a;
                origin -= a;
            }
            hints.AddForbiddenZone(new SDKnockbackInAABBSquareAwayFromOrigin(center, origin, kb.Distance, ArenaHalfWidth), kb.Activation);
        }
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.CircularKnockbackTelegraph)
        {
            _casters.RemoveAll(k => k.ActorID == caster.InstanceID);
            _casters.Add(new(spell.LocXZ, Distance, Module.CastFinishAt(spell).AddSeconds(HitDelay), Shape, spell.Rotation, Kind.AwayFromOrigin, actorID: caster.InstanceID));
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID.RavenousGodsCircleHit)
        {
            _casters.Clear();
            ++NumCasts;
        }
    }

    private void PruneExpired()
    {
        var now = WorldState.CurrentTime;
        _casters.RemoveAll(k => now > k.Activation.AddSeconds(1d));
    }
}

// Knockback rows 90/91 are selected per target: players on opposite sides of the center line are
// pushed in opposite directions. A fixed left/right arrow would therefore be wrong for half of
// the raid; derive the side from each player's position relative to the helper's cast direction.
// BCA1 telegraphs the lateral knockback; the real hit (C163) lands ~5.1s after the short telegraph
// ends. Its direction is perpendicular to the aside helper's radius, pointing toward the helper
// that telegraphs the following circular knockback (verified across all three recorded waves), not
// a per-player left/right split. Show the arrow only when the hit is imminent and add a
// square-wall forbidden zone so automation starts from a position that stays inside after the push.
sealed class KnockAside(BossModule module) : Components.GenericKnockback(module)
{
    private static readonly AOEShapeRect Shape = new(40f, 30f);
    private const float Distance = 15f;
    private const float ArenaHalfWidth = 24f;
    private const double HitDelay = 5.1d;
    private const double ShowBeforeHit = 2d;

    private sealed class AsideSource(WPos asidePos, WPos circlePos, DateTime activation, ulong actorID)
    {
        public readonly WPos AsidePos = asidePos;
        public readonly WPos CirclePos = circlePos;
        public readonly DateTime Activation = activation;
        public readonly ulong ActorID = actorID;

        public WDir PushDirection(WPos center)
        {
            var radial = AsidePos - center;
            var perp = radial.OrthoL();
            var towardCircle = CirclePos - center;
            return towardCircle.Dot(perp) >= 0f ? perp.Normalized() : (-perp).Normalized();
        }
    }

    private readonly List<AsideSource> _sources = [];
    private readonly List<(WPos AsidePos, DateTime Activation, ulong ActorID)> _pendingAside = [];
    private readonly List<Knockback> _displayed = [with(4)];

    // Exposes the lateral push displacement (15y * direction) that will resolve before the given
    // circular-knockback activation and has not yet been applied, so CircularKnockback can offset
    // its safe square by it. Returns false when no such aside is still pending.
    public bool TryGetPendingAsidePush(DateTime circleActivation, out WDir push)
    {
        var now = WorldState.CurrentTime;
        foreach (var source in _sources)
            if (source.Activation < circleActivation && now < source.Activation)
            {
                push = Distance * source.PushDirection(Arena.Center);
                return true;
            }
        push = default;
        return false;
    }

    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor)
    {
        PruneExpired();
        _displayed.Clear();
        var now = WorldState.CurrentTime;
        foreach (var source in _sources)
        {
            if (now < source.Activation.AddSeconds(-ShowBeforeHit))
                continue;
            var dir = source.PushDirection(Arena.Center);
            _displayed.Add(new(source.AsidePos, Distance, source.Activation, Shape, Angle.FromDirection(dir), Kind.DirForward, actorID: source.ActorID));
        }
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void Update() => PruneExpired();

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var now = WorldState.CurrentTime;
        foreach (var source in _sources)
        {
            if (now < source.Activation.AddSeconds(-ShowBeforeHit))
                continue;
            var dir = source.PushDirection(Arena.Center);
            hints.AddForbiddenZone(new SDKnockbackInAABBSquareFixedDirection(Arena.Center, Distance * dir, ArenaHalfWidth), source.Activation);
        }
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        switch (spell.Action.ID)
        {
            case (uint)AID.KnockAsideTelegraph:
                _pendingAside.RemoveAll(p => p.ActorID == caster.InstanceID);
                _pendingAside.Add((caster.Position, Module.CastFinishAt(spell).AddSeconds(HitDelay), caster.InstanceID));
                break;
            case (uint)AID.CircularKnockbackTelegraph:
                // The circle helper arrives a couple of seconds after the aside telegraph; pair the
                // latest pending aside with it to resolve the lateral push direction.
                for (var i = _pendingAside.Count - 1; i >= 0; --i)
                {
                    var p = _pendingAside[i];
                    _sources.RemoveAll(s => s.ActorID == p.ActorID);
                    _sources.Add(new(p.AsidePos, spell.LocXZ, p.Activation, p.ActorID));
                    _pendingAside.RemoveAt(i);
                }
                break;
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID.RavenousGodsAsideHit)
        {
            _sources.Clear();
            _pendingAside.Clear();
            ++NumCasts;
        }
    }

    public override void OnActorDestroyed(Actor actor)
    {
        _sources.RemoveAll(s => s.ActorID == actor.InstanceID);
        _pendingAside.RemoveAll(p => p.ActorID == actor.InstanceID);
    }

    private void PruneExpired()
    {
        var now = WorldState.CurrentTime;
        _sources.RemoveAll(s => now > s.Activation.AddSeconds(1d));
        _pendingAside.RemoveAll(p => now > p.Activation.AddSeconds(1d));
    }
}
sealed class GemstoneRaidwides(BossModule module) : Components.RaidwideCasts(module, [(uint)AID.RubyLight, (uint)AID.RavenousGods, (uint)AID.Howl]);
sealed class RubyReflectionHint(BossModule module) : Components.CastHint(module, (uint)AID.RubyLight, "Ruby reflection - watch the gemstone lines");

sealed class RebelliousFamiliarStates : StateMachineBuilder
{
    public RebelliousFamiliarStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<ClawTailCombo>()
            .ActivateOnEnter<TopazRay>()
            .ActivateOnEnter<CircularKnockback>()
            .ActivateOnEnter<KnockAside>()
            .ActivateOnEnter<GemstoneRaidwides>()
            .ActivateOnEnter<RubyReflectionHint>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Contributed,
    StatesType = typeof(RebelliousFamiliarStates),
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID),
    PrimaryActorOID = (uint)OID.Boss,
    Contributors = "KanoNoUta",
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Foray,
    GroupType = BossModuleInfo.GroupType.CriticalEngagement,
    GroupID = 1093u,
    NameID = 56u,
    SortOrder = 5)]
// The electric fence is a square: replay player positions reach the corners and BFD0 lethal hits
// cluster at |x|/|z| ~= 24 from center, so the arena and knockback safety checks use a 24y square.
public sealed class RebelliousFamiliar(WorldState ws, Actor primary) : BossModule(ws, primary, new(238f, 352f), new ArenaBoundsSquare(24f));
