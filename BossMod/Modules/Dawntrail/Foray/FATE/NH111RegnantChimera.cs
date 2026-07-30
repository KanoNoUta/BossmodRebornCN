namespace BossMod.Dawntrail.Foray.FATE.NH111RegnantChimera;

public enum OID : uint
{
    RegnantChimera = 0x4C7D, // R5.180
    FulmipotentOrb = 0x4C7F,
    ChaoticNoise = 0x4B71,
}

public enum AID : uint
{
    AutoAttack = 50856, // RegnantChimera->player, no cast, single-target
    DragonsBreathFirst = 48629, // RegnantChimera->self, 6.0s cast, range 30 120-degree cone; first of three clockwise hits
    DragonsBreathSecond = 48630, // RegnantChimera->self, no cast, range 30 120-degree cone; second hit, 120 degrees clockwise
    RamsVoice = 48633, // RegnantChimera->self, 4.0s cast, range 9 circle
    DragonsVoice = 48634, // RegnantChimera->self, 4.0s cast, range 8-30 donut
    DragonsVoiceOrb = 48636, // FulmipotentOrb->self, 4.0s cast, range 8-30 donut
    DragonsBreathThird = 49747, // RegnantChimera->self, no cast, range 30 120-degree cone; third hit, 240 degrees clockwise
    LeftDuobreath = 50111, // RegnantChimera->self, 5.0s cast, range 40 180-degree cone; left then right (dragon first)
    RightDuobreath = 50112, // RegnantChimera->self, 5.0s cast, range 40 180-degree cone; right then left (ram first)
    Cacophony = 50113, // RegnantChimera->self, 4.0s cast, single-target
    ChaoticChorus = 50114, // ChaoticNoise->self, 1.5s cast, range 6 circle
    DuobreathDragonFollowup = 50115, // RegnantChimera->self, no cast, range 40 180-degree cone; follow-up to RightDuobreath
    DuobreathRamFollowup = 50116, // RegnantChimera->self, no cast, range 40 180-degree cone; follow-up to LeftDuobreath
}

// ARR records a fully deterministic clockwise sequence: the cast resolves at t=0, followed by
// BDF6 at +2.709s and C253 at +5.446s. The packet rotations advance by -120 degrees each time.
sealed class DragonsBreathSequence(BossModule module) : Components.GenericAOEs(module)
{
    private readonly record struct Pending(uint ActionID, AOEInstance AOE);

    private static readonly AOEShapeCone Shape = new(30f, 60f.Degrees());
    private static readonly Angle Step = 120f.Degrees();
    private readonly List<Pending> _pending = [with(3)];
    private readonly List<AOEInstance> _displayed = [with(2)];
    private readonly HashSet<uint> _seenGlobalSequences = [];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        PruneExpired();
        _displayed.Clear();
        var count = Math.Min(_pending.Count, 2);
        for (var i = 0; i < count; ++i)
        {
            var aoe = _pending[i].AOE;
            if (i == 0 && count > 1)
                aoe.Color = Colors.Danger;
            _displayed.Add(aoe);
        }
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void Update() => PruneExpired();

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != (uint)AID.DragonsBreathFirst || spell.EventHappened)
            return;

        var firstActivation = Module.CastFinishAt(spell);
        if (firstActivation <= WorldState.CurrentTime)
            return;

        _pending.Clear();
        Add(AID.DragonsBreathFirst, caster, spell.Rotation, firstActivation);
        Add(AID.DragonsBreathSecond, caster, spell.Rotation - Step, firstActivation.AddSeconds(2.709d));
        Add(AID.DragonsBreathThird, caster, spell.Rotation - 2f * Step, firstActivation.AddSeconds(5.446d));
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is not ((uint)AID.DragonsBreathFirst or (uint)AID.DragonsBreathSecond or (uint)AID.DragonsBreathThird)
            || spell.GlobalSequence != 0 && !_seenGlobalSequences.Add(spell.GlobalSequence))
            return;

        ++NumCasts;
        _pending.RemoveAll(entry => entry.ActionID == spell.Action.ID && entry.AOE.ActorID == caster.InstanceID);
        PruneExpired();
    }

    public override void OnActorDeath(Actor actor) => RemoveActor(actor.InstanceID);
    public override void OnActorDestroyed(Actor actor) => RemoveActor(actor.InstanceID);

    private void Add(AID action, Actor caster, Angle rotation, DateTime activation)
        => _pending.Add(new((uint)action, new(Shape, caster.Position, rotation, activation, actorID: caster.InstanceID, shapeDistance: Shape.Distance(caster.Position, rotation))));

    private void PruneExpired()
        => _pending.RemoveAll(entry => WorldState.CurrentTime > entry.AOE.Activation.AddSeconds(0.75d));

    private void RemoveActor(ulong actorID)
        => _pending.RemoveAll(entry => entry.AOE.ActorID == actorID);
}

// The cast packet is authoritative for the first half. 50115/50116 are confirmed follow-up
// actions, but no recording currently establishes their packet rotation/timing, so do not invent
// an opposite-side prediction that could send the AI into the real AOE.
sealed class Duobreath(BossModule module) : Components.SimpleAOEGroups(module,
    [(uint)AID.LeftDuobreath, (uint)AID.RightDuobreath], new AOEShapeCone(40f, 90f.Degrees()));
sealed class ChaoticChorus(BossModule module) : Components.SimpleAOEs(module, (uint)AID.ChaoticChorus, new AOEShapeCircle(6f));
sealed class RamsVoice(BossModule module) : Components.SimpleAOEs(module, (uint)AID.RamsVoice, new AOEShapeCircle(9f));
sealed class DragonsVoice(BossModule module) : Components.SimpleAOEGroups(module,
    [(uint)AID.DragonsVoice, (uint)AID.DragonsVoiceOrb], new AOEShapeDonut(8f, 30f));

[SkipLocalsInit]
sealed class RegnantChimeraStates : StateMachineBuilder
{
    public RegnantChimeraStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<DragonsBreathSequence>()
            .ActivateOnEnter<Duobreath>()
            .ActivateOnEnter<ChaoticChorus>()
            .ActivateOnEnter<RamsVoice>()
            .ActivateOnEnter<DragonsVoice>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Contributed,
    StatesType = typeof(RegnantChimeraStates),
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID),
    PrimaryActorOID = (uint)OID.RegnantChimera,
    Contributors = "KanoNoUta",
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Foray,
    GroupType = BossModuleInfo.GroupType.ForayFATE,
    GroupID = 1093u,
    NameID = 2076u,
    SortOrder = 1)]
[SkipLocalsInit]
public sealed class RegnantChimera(WorldState ws, Actor primary) : OpenWorldFate(ws, primary);
