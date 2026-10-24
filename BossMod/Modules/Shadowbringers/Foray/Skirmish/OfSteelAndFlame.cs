namespace BossMod.Shadowbringers.Foray.Skirmish.OfSteelAndFlame;

public enum OID : uint
{
    Boss = 0x2FAE, // Cacus, R4.0
    Sartauvoir = 0x2FAD, // R0.5; initially allied/untargetable, becomes hostile after Thermal Shock
    Helper = 0x2EA1
}

public enum AID : uint
{
    AutoAttack = 21263,
    Exhaust = 21556, // Cacus->self, 4.0s cast, range 40 width 10 rect
    MagitekRay = 21558, // Cacus->location, 3.5s cast, range 6 circle
    GrandSwordFirst = 21553, // Cacus->self, 9.0s cast, range 27 120-degree cone
    GrandSwordFirstAlt = 21554, // alternate first cast, same shape
    GrandSwordRest = 21555, // Cacus->self, 0.5s observed cast, repeats every ~2.77s
    Overheat = 21557, // Cacus->self, 4.0s cast; subsequent sword sequences have six hits instead of three
    AtomicRay = 21559, // Cacus->self, 8.0s interruptible cast, range 40 circle

    ThermalShock = 21022, // Helper->self, 7.0s cast, range 30 proximity circle; Sartauvoir's entrance
    TridirectionalFlameVisual = 20933, // Sartauvoir->self, 3.0s cast, visual only
    TridirectionalFlame = 20927, // Helper->self, 3.0s cast, range 60 width 8 rect, three directions
    PyreticEruption = 20929, // Helper->location, 3.0s cast, range 8 circle, follows the three lines
    Pyroscatter = 20930, // Helper->location, 3.0s cast, range 8 circle, staggered pairs
    PyroburstVisual = 20937, // Sartauvoir->self, 4.0s cast, visual only
    Pyroburst = 20931, // Helper->self, 4.0s cast, range 10 circle
    GrandCrossflameVisual = 20934, // Sartauvoir->self, 4.5s cast, visual only
    GrandCrossflame = 20928 // Helper->self, 4.5s cast, range 40 width 18 cross
}

public enum SID : uint
{
    Overheat = 65
}

public enum IconID : uint
{
    RotateCW = 156,
    RotateCCW = 157
}

sealed class Exhaust(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Exhaust, new AOEShapeRect(40f, 5f));
sealed class MagitekRay(BossModule module) : Components.SimpleAOEs(module, (uint)AID.MagitekRay, 6f);
sealed class TridirectionalFlame(BossModule module) : Components.SimpleAOEs(module, (uint)AID.TridirectionalFlame, new AOEShapeRect(60f, 4f));
sealed class PyreticEruption(BossModule module) : Components.SimpleAOEs(module, (uint)AID.PyreticEruption, 8f);
sealed class Pyroscatter(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Pyroscatter, 8f);
sealed class Pyroburst(BossModule module) : Components.SimpleAOEs(module, (uint)AID.Pyroburst, 10f);
sealed class GrandCrossflame(BossModule module) : Components.SimpleAOEs(module, (uint)AID.GrandCrossflame, new AOEShapeCross(40f, 9f));

sealed class AtomicRay(BossModule module) : Components.SimpleAOEs(module, (uint)AID.AtomicRay, 40f)
{
    private bool MustEvade(ulong casterID) => WorldState.Actors.Find(casterID)?.CastInfo is { EventHappened: false } cast
        && cast.Action.ID == WatchedAction && cast.ElapsedTime >= cast.TotalTime * 0.7f;

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        var active = base.ActiveAOEs(slot, actor);
        var aoes = CollectionsMarshal.AsSpan(Casters);
        for (var i = 0; i < active.Length; ++i)
            aoes[i].Risky = MustEvade(aoes[i].ActorID);
        return active;
    }

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (Casters.Count != 0)
            hints.Add(Casters.Any(aoe => MustEvade(aoe.ActorID)) ? "原子射线未被打断：远离卡库斯" : "原子射线：可打断");
    }
}

sealed class ThermalShock(BossModule module) : Components.ProximityAOEs(module, (uint)AID.ThermalShock, 30f)
{
    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (Casters.Count != 0)
            hints.Add("炙热冲击：远离萨托瓦尔登场位置，距离越远伤害越低");
    }
}

sealed class GrandSword(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeCone cone = new(27f, 60f.Degrees());
    private readonly List<AOEInstance> _aoes = new(2);
    private Actor? _caster;
    private bool _overheated = module.Enemies((uint)OID.Boss).Any(a => a.FindStatus((uint)SID.Overheat) != null);
    private int _remaining;
    private Angle _rotation;
    private Angle _increment = -120f.Degrees();
    private DateTime _activation;

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _aoes.Clear();
        if (_remaining > 0 && _caster is { IsDeadOrDestroyed: false } caster)
        {
            // Preview the following sector without treating both sectors as simultaneous hazards.
            if (_remaining > 1)
                _aoes.Add(new(cone, caster.Position, _rotation + _increment, _activation.AddSeconds(2.77d), risky: false));
            _aoes.Add(new(cone, caster.Position, _rotation, _activation, Colors.Danger));
        }
        return CollectionsMarshal.AsSpan(_aoes);
    }

    public override void OnEventIcon(Actor actor, uint iconID, ulong targetID)
    {
        if (actor.OID != (uint)OID.Boss)
            return;
        if (iconID == (uint)IconID.RotateCW)
            _increment = -120f.Degrees();
        else if (iconID == (uint)IconID.RotateCCW)
            _increment = 120f.Degrees();
    }

    public override void OnStatusGain(Actor actor, ref ActorStatus status)
    {
        if (actor.OID == (uint)OID.Boss && status.ID == (uint)SID.Overheat)
            _overheated = true;
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (caster.OID != (uint)OID.Boss)
            return;
        switch (spell.Action.ID)
        {
            case (uint)AID.GrandSwordFirst:
            case (uint)AID.GrandSwordFirstAlt:
                _remaining = _overheated ? 6 : 3;
                SetCast(caster, spell);
                break;
            case (uint)AID.GrandSwordRest:
                // Resynchronize to the real cast, including when loading halfway through a sequence.
                if (_remaining == 0)
                    _remaining = 1;
                SetCast(caster, spell);
                break;
        }
    }

    private void SetCast(Actor caster, ActorCastInfo spell)
    {
        _caster = caster;
        _rotation = spell.Rotation;
        _activation = Module.CastFinishAt(spell);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (caster.OID != (uint)OID.Boss)
            return;
        if (spell.Action.ID == (uint)AID.Overheat)
            _overheated = true;
        else if (spell.Action.ID is (uint)AID.GrandSwordFirst or (uint)AID.GrandSwordFirstAlt or (uint)AID.GrandSwordRest && _remaining > 0)
        {
            ++NumCasts;
            --_remaining;
            _rotation = spell.Rotation + _increment;
            _activation = WorldState.FutureTime(2.77d);
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (caster == _caster && !spell.EventHappened && spell.Action.ID is (uint)AID.GrandSwordFirst or (uint)AID.GrandSwordFirstAlt or (uint)AID.GrandSwordRest)
            _remaining = 0;
    }

    public override void Update()
    {
        if (_caster is { IsDeadOrDestroyed: true } || _remaining > 0 && WorldState.CurrentTime > _activation.AddSeconds(1d))
            _remaining = 0;
    }

    public override void AddGlobalHints(Actor actor, GlobalHints hints)
    {
        if (_remaining > 0)
            hints.Add(_increment.Rad < 0f ? "巨剑：顺时针旋转，每次120°" : "巨剑：逆时针旋转，每次120°");
    }
}

sealed class OfSteelAndFlameStates : StateMachineBuilder
{
    public OfSteelAndFlameStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<Exhaust>()
            .ActivateOnEnter<MagitekRay>()
            .ActivateOnEnter<GrandSword>()
            .ActivateOnEnter<AtomicRay>()
            .ActivateOnEnter<ThermalShock>()
            .ActivateOnEnter<TridirectionalFlame>()
            .ActivateOnEnter<PyreticEruption>()
            .ActivateOnEnter<Pyroscatter>()
            .ActivateOnEnter<Pyroburst>()
            .ActivateOnEnter<GrandCrossflame>();
    }
}

// Both entry points draw the entire encounter: either boss can die first, or be the only one
// present when the plugin is reloaded. The surviving boss keeps the combined radar available.
[ModuleInfo(BossModuleInfo.Maturity.Contributed, Contributors = "COXOIDA", GroupType = BossModuleInfo.GroupType.ForayFATE, GroupID = 735, NameID = 1626, SortOrder = 1)]
public class OfSteelAndFlame(WorldState ws, Actor primary) : OpenWorldFate(ws, primary)
{
    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        Arena.Actors(Enemies((uint)OID.Boss));
        Arena.Actors(Enemies((uint)OID.Sartauvoir));
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Contributed, Contributors = "COXOIDA", PrimaryActorOID = (uint)OID.Sartauvoir, StatesType = typeof(OfSteelAndFlameStates), GroupType = BossModuleInfo.GroupType.ForayFATE, GroupID = 735, NameID = 1626, SortOrder = 2)]
public sealed class OfSteelAndFlameSartauvoir(WorldState ws, Actor primary) : OfSteelAndFlame(ws, primary);
