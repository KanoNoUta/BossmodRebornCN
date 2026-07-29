using BossMod.Dawntrail.Foray.CriticalEngagement;
using static BossMod.Components.GenericKnockback;

namespace BossMod.Dawntrail.Foray.CriticalEngagement.CE206RebelliousFamiliar;

public enum OID : uint
{
    Boss = 0x4C4F, // R3.8, BNpcName 14791, cornered gemstone
    YellowGem = 0x4C50,
    Helper = 0x233C
}

public enum AID : uint
{
    YellowGemstones = 0xBC98,
    YellowGemActive = 0xBC99,
    YellowGemInactive = 0xBC9A,
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
    RubyOuterReflection = 0xC4F2,
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
        (uint)AID.TailThenClaw => new(Tail, Claw, (uint)AID.TailThenClawSecond, 2d),
        _ => null
    };
}

sealed class CircularKnockback(BossModule module) : Components.SimpleKnockbacks(module, (uint)AID.CircularKnockbackTelegraph, 30f, shape: new AOEShapeCircle(60f));

// Knockback rows 90/91 are selected per target: players on opposite sides of the center line are
// pushed in opposite directions. A fixed left/right arrow would therefore be wrong for half of
// the raid; derive the side from each player's position relative to the helper's cast direction.
sealed class KnockAside(BossModule module) : Components.GenericKnockback(module)
{
    private static readonly AOEShapeRect Shape = new(40f, 30f);
    private readonly List<(WPos Origin, Angle Rotation, DateTime Activation, ulong ActorID)> _casters = [];
    private readonly List<Knockback> _displayed = [with(4)];

    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor)
    {
        _displayed.Clear();
        foreach (var source in _casters)
        {
            var left = source.Rotation.ToDirection().OrthoL();
            var kind = (actor.Position - source.Origin).Dot(left) >= 0f ? Kind.DirLeft : Kind.DirRight;
            _displayed.Add(new(source.Origin, 15f, source.Activation, Shape, source.Rotation, kind, actorID: source.ActorID));
        }
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.KnockAsideTelegraph)
        {
            _casters.RemoveAll(source => source.ActorID == caster.InstanceID);
            _casters.Add((caster.Position, spell.Rotation, Module.CastFinishAt(spell), caster.InstanceID));
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.KnockAsideTelegraph)
            _casters.RemoveAll(source => source.ActorID == caster.InstanceID);
    }

    public override void OnActorDestroyed(Actor actor) => _casters.RemoveAll(source => source.ActorID == actor.InstanceID);
}
sealed class GemstoneRaidwides(BossModule module) : Components.RaidwideCasts(module, [(uint)AID.RavenousGods, (uint)AID.Howl]);
sealed class RubyReflectionHint(BossModule module) : Components.CastHint(module, (uint)AID.RubyLight, "Ruby reflection - watch the gemstone lines");

sealed class RebelliousFamiliarStates : StateMachineBuilder
{
    public RebelliousFamiliarStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<ClawTailCombo>()
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
