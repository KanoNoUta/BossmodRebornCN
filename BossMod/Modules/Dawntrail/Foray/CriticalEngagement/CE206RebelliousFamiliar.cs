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

// BFD0 repeatedly deals lethal damage from the center controller. In the replay, 28/29 target
// samples were at least 19.8y from center; keep the nominal 20y arena edge instead of widening it
// around the one high-speed stale-position outlier at 14.3y.
sealed class LethalBoundary(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeDonut Shape = new(20f, 35f);
    private readonly AOEInstance[] _aoe = [new(Shape, module.Arena.Center)];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => _aoe;
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

// BCA0 telegraphs a 60y radial knockback; the actual hit (C162) lands roughly six seconds after the
// short telegraph finishes. Keep the arrow visible until the hit instead of dropping it at cast end.
sealed class CircularKnockback(BossModule module) : Components.GenericKnockback(module)
{
    private static readonly AOEShapeCircle Shape = new(60f);
    private const double HitDelay = 6d;
    private readonly List<Knockback> _casters = [];
    private readonly List<Knockback> _displayed = [with(4)];

    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor)
    {
        PruneExpired();
        _displayed.Clear();
        _displayed.AddRange(_casters);
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void Update() => PruneExpired();

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.CircularKnockbackTelegraph)
        {
            _casters.RemoveAll(k => k.ActorID == caster.InstanceID);
            _casters.Add(new(spell.LocXZ, 30f, Module.CastFinishAt(spell).AddSeconds(HitDelay), Shape, spell.Rotation, Kind.AwayFromOrigin, actorID: caster.InstanceID));
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
// BCA1 telegraphs the lateral knockback; the real hit (C163) lands roughly six seconds after the
// short telegraph finishes, so the arrow must stay visible until then.
sealed class KnockAside(BossModule module) : Components.GenericKnockback(module)
{
    private static readonly AOEShapeRect Shape = new(40f, 30f);
    private const double HitDelay = 6d;
    private readonly List<(WPos Origin, Angle Rotation, DateTime Activation, ulong ActorID)> _casters = [];
    private readonly List<Knockback> _displayed = [with(4)];

    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor)
    {
        PruneExpired();
        _displayed.Clear();
        foreach (var source in _casters)
        {
            var left = source.Rotation.ToDirection().OrthoL();
            var kind = (actor.Position - source.Origin).Dot(left) >= 0f ? Kind.DirLeft : Kind.DirRight;
            _displayed.Add(new(source.Origin, 15f, source.Activation, Shape, source.Rotation, kind, actorID: source.ActorID));
        }
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void Update() => PruneExpired();

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.KnockAsideTelegraph)
        {
            _casters.RemoveAll(source => source.ActorID == caster.InstanceID);
            _casters.Add((caster.Position, spell.Rotation, Module.CastFinishAt(spell).AddSeconds(HitDelay), caster.InstanceID));
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID.RavenousGodsAsideHit)
        {
            _casters.Clear();
            ++NumCasts;
        }
    }

    public override void OnActorDestroyed(Actor actor) => _casters.RemoveAll(source => source.ActorID == actor.InstanceID);

    private void PruneExpired()
    {
        var now = WorldState.CurrentTime;
        _casters.RemoveAll(source => now > source.Activation.AddSeconds(1d));
    }
}
sealed class GemstoneRaidwides(BossModule module) : Components.RaidwideCasts(module, [(uint)AID.RubyLight, (uint)AID.RavenousGods, (uint)AID.Howl]);
sealed class RubyReflectionHint(BossModule module) : Components.CastHint(module, (uint)AID.RubyLight, "Ruby reflection - watch the gemstone lines");

sealed class RebelliousFamiliarStates : StateMachineBuilder
{
    public RebelliousFamiliarStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<LethalBoundary>()
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
public sealed class RebelliousFamiliar(WorldState ws, Actor primary) : BossModule(ws, primary, new(238f, 352f), new ArenaBoundsCircle(20f));
