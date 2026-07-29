using BossMod.Dawntrail.Foray.CriticalEngagement;
using static BossMod.Components.GenericKnockback;

namespace BossMod.Dawntrail.Foray.CriticalEngagement.CE205GluttonousCursefiend;

public enum OID : uint
{
    Boss = 0x4C4B, // R3.0, BNpcName 14790, Algol
    Helper = 0x233C
}

public enum AID : uint
{
    Cursevoice = 0xBBE4,
    DrawInVisual1 = 0xBBE5,
    DrawInVisual2 = 0xBBE6,
    DrawIn = 0xBBE7,
    DrawInCone = 0xBBE8, // 60y 30-degree cone
    Devour = 0xBBE9,
    SpitTomato = 0xBBEA,
    SpitOnion = 0xBBEB,
    TomatoMiasma1 = 0xBBED, // 50y long, 6y wide rect
    OnionMiasma1 = 0xBBEE, // 60y 30-degree cone
    TomatoMiasma2 = 0xBBEF,
    OnionMiasma2 = 0xBBF0,
    SpinningDrawInCone = 0xBBF1, // 30y 30-degree cone
    SpinningDrawIn = 0xBBF2,
    GreatMiasmaCannon1 = 0xBBF4, // 40y long, 50y wide rect
    CorruptMiasma1 = 0xBBF5, // 12y circle
    CursevoiceAlt = 0xBF4B,
    DevourAlt1 = 0xC4F6, // 12y 120-degree cone
    GreatMiasmaCannonVisual = 0xC4F7,
    GreatMiasmaCannon2 = 0xC4F8,
    CorruptMiasma2 = 0xC4F9, // 11y circle
    PiercingScream = 0xC4FA,
    PiercingScreamAlt = 0xC4FB,
    DevourAlt2 = 0xC523,
    DevourShort = 0xC525, // 8y 120-degree cone
    SpinningDrawInAlt = 0xC6FE
}

sealed class AlgolAOEs(BossModule module) : ReplayValidatedCastAOEs(module)
{
    private static readonly AOEShapeRect Tomato = new(50f, 3f);
    private static readonly AOEShapeCone Onion = new(60f, 15f.Degrees());
    private static readonly AOEShapeRect Cannon = new(40f, 25f);
    private static readonly AOEShapeCircle Corrupt12 = new(12f);
    private static readonly AOEShapeCircle Corrupt11 = new(11f);
    private static readonly AOEShapeCone Devour12 = new(12f, 60f.Degrees());
    private static readonly AOEShapeCone Devour8 = new(8f, 60f.Degrees());

    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.TomatoMiasma1 or (uint)AID.TomatoMiasma2 => new(Tomato),
        (uint)AID.OnionMiasma1 or (uint)AID.OnionMiasma2 => new(Onion),
        (uint)AID.GreatMiasmaCannon1 or (uint)AID.GreatMiasmaCannon2 => new(Cannon),
        (uint)AID.CorruptMiasma1 => new(Corrupt12, true),
        (uint)AID.CorruptMiasma2 => new(Corrupt11, true),
        (uint)AID.DevourAlt1 or (uint)AID.DevourAlt2 => new(Devour12),
        (uint)AID.DevourShort => new(Devour8),
        _ => null
    };
}

// The damaging pull packets have no cast bar. Their preceding helper cones provide the stable
// origin, direction and activation used for movement hints.
sealed class AlgolDrawIn(BossModule module) : Components.GenericKnockback(module)
{
    private static readonly AOEShapeCone LongCone = new(60f, 15f.Degrees());
    private static readonly AOEShapeCone ShortCone = new(30f, 15f.Degrees());
    private readonly List<Knockback> _sources = [];

    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor) => CollectionsMarshal.AsSpan(_sources);

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        var shape = spell.Action.ID switch
        {
            (uint)AID.DrawInCone => LongCone,
            (uint)AID.SpinningDrawInCone => ShortCone,
            _ => null
        };
        if (shape != null)
        {
            _sources.RemoveAll(source => source.ActorID == caster.InstanceID);
            _sources.Add(new(caster.Position, 50f, Module.CastFinishAt(spell), shape, spell.Rotation, Kind.TowardsOrigin, actorID: caster.InstanceID));
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is (uint)AID.DrawInCone or (uint)AID.SpinningDrawInCone)
            _sources.RemoveAll(source => source.ActorID == caster.InstanceID);
    }

    public override void OnActorDestroyed(Actor actor) => _sources.RemoveAll(source => source.ActorID == actor.InstanceID);
}

sealed class AlgolRaidwides(BossModule module) : Components.RaidwideCasts(module, [(uint)AID.Cursevoice, (uint)AID.CursevoiceAlt, (uint)AID.PiercingScream, (uint)AID.PiercingScreamAlt]);

sealed class GluttonousCursefiendStates : StateMachineBuilder
{
    public GluttonousCursefiendStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<AlgolAOEs>()
            .ActivateOnEnter<AlgolDrawIn>()
            .ActivateOnEnter<AlgolRaidwides>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Contributed,
    StatesType = typeof(GluttonousCursefiendStates),
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID),
    PrimaryActorOID = (uint)OID.Boss,
    Contributors = "KanoNoUta",
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Foray,
    GroupType = BossModuleInfo.GroupType.CriticalEngagement,
    GroupID = 1093u,
    NameID = 54u,
    SortOrder = 4)]
public sealed class GluttonousCursefiend(WorldState ws, Actor primary) : BossModule(ws, primary, new(765f, 0f), new ArenaBoundsCircle(20f));
