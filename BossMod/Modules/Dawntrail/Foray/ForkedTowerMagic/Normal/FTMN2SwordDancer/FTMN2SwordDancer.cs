using BossMod.Dawntrail.Foray.CriticalEngagement;

namespace BossMod.Dawntrail.Foray.ForkedTowerMagic.Normal.FTMN2SwordDancer;

// Normal 魔之塔 Boss2: Sword Dancer. 秘法剑 96y 半圆、突进 30x6、旋转月环/钢铁、
// 剑舞直条 60x20。剑刃矩形（ObjectEffect 2015283 四连）会在真实读条前预绘。
sealed class SwordDancerAOEs(BossModule module) : ReplayValidatedCastAOEs(module)
{
    private static readonly AOEShapeCone MartialMystique = new(96f, 90f.Degrees());
    private static readonly AOEShapeRect Rush = new(30f, 3f);
    private static readonly AOEShapeDonut SpinDonut = new(5f, 60f);
    private static readonly AOEShapeCircle SpinSmall = new(15f);
    private static readonly AOEShapeCircle SpinLarge = new(20f);
    private static readonly AOEShapeDonutSector TurnInnerWide = new(9f, 14f, 45f.Degrees());
    private static readonly AOEShapeDonutSector TurnOuterWide = new(19f, 24f, 45f.Degrees());
    private static readonly AOEShapeDonutSector TurnInnerNarrow = new(9f, 14f, 32.5f.Degrees());
    private static readonly AOEShapeDonutSector TurnOuterNarrow = new(19f, 24f, 27f.Degrees());
    private static readonly AOEShapeRect SwordDance = new(60f, 10f);
    private static readonly AOEShapeCircle Pierce = new(5f);

    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.MartialMystique2 => new(MartialMystique),
        (uint)AID.Rush2 => new(Rush),
        (uint)AID.Spin => new(SpinDonut),
        (uint)AID.Spin1 => new(SpinSmall),
        (uint)AID.Spin2 => new(SpinLarge),
        (uint)AID.Turn1 => new(TurnInnerWide),
        (uint)AID.Turn2 => new(TurnOuterWide),
        (uint)AID.Turn5 => new(TurnInnerNarrow),
        (uint)AID.Turnabout => new(TurnOuterNarrow),
        (uint)AID.SwordDance6 => new(SwordDance),
        (uint)AID.Pierce => new(Pierce),
        _ => null
    };
}

sealed class SwordRush(BossModule module) : Components.GenericAOEs(module)
{
    private readonly List<AOEInstance> _aoes = [with(8)];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => CollectionsMarshal.AsSpan(_aoes);

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is not ((uint)AID.Rush or (uint)AID.Rush1) || spell.EventHappened)
            return;
        var direction = spell.LocXZ - caster.Position;
        var shape = new AOEShapeRect(direction.Length(), 3.5f);
        var rotation = Angle.FromDirection(direction);
        _aoes.Add(new(shape, caster.Position, rotation, Module.CastFinishAt(spell), actorID: caster.InstanceID,
            shapeDistance: shape.Distance(caster.Position, rotation)));
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is (uint)AID.Rush or (uint)AID.Rush1)
            _aoes.RemoveAll(a => a.ActorID == caster.InstanceID);
    }

    public override void Update() => _aoes.RemoveAll(a => WorldState.CurrentTime > a.Activation.AddSeconds(1d));
}

// 普通剑舞: 0x1EC033 事件物件发 EAnim(1,2)，按顺序刷出四条 60x20 剑刃矩形。
sealed class SwordBladeRects(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeRect Shape = new(0.5f, 10f, 60f);
    private readonly List<(ulong ActorID, WPos Position, Angle Rotation)> _warnings = [with(4)];
    private readonly List<AOEInstance> _displayed = [with(8)];
    private DateTime _firstWarningAt;

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => CollectionsMarshal.AsSpan(_displayed);

    public override void OnActorEAnim(Actor actor, uint state)
    {
        if (state != 0x00010002 || actor.OID != (uint)OID.Actor1ec033)
            return;

        if (_warnings.Any(w => w.ActorID == actor.InstanceID))
            return;

        if (_warnings.Count == 0)
            _firstWarningAt = WorldState.CurrentTime;
        _warnings.Add((actor.InstanceID, actor.Position, actor.Rotation));
        if (_warnings.Count < 4)
            return;

        // ARR: the four warnings arrive within 2.5s, then resolve in warning order at
        // +6.4s from the final warning and every 2.5s thereafter.
        for (var i = 0; i < 4; ++i)
        {
            var warning = _warnings[i];
            _displayed.Add(new(Shape, warning.Position, warning.Rotation,
                activation: WorldState.FutureTime(6.4d + 2.5d * i), actorID: warning.ActorID));
        }
        _warnings.Clear();
        _firstWarningAt = default;
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID != (uint)AID.SwordDance6 || _displayed.Count == 0)
            return;

        var now = WorldState.CurrentTime;
        var resolved = _displayed.FindIndex(aoe => aoe.Activation <= now.AddSeconds(0.75d));
        if (resolved >= 0)
            _displayed.RemoveAt(resolved);
    }

    public override void Update()
    {
        var now = WorldState.CurrentTime;
        if (_warnings.Count > 0 && now > _firstWarningAt.AddSeconds(5d))
        {
            _warnings.Clear();
            _firstWarningAt = default;
        }
        _displayed.RemoveAll(aoe => now > aoe.Activation.AddSeconds(1d));
    }
}


// 场地电网: 圆形场地边缘的电网，红色圆环标出（用户实测直径 ~47.4m）。
sealed class ElectricBoundary(BossModule module) : Components.GenericAOEs(module)
{
    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => [];
    public override void DrawArenaForeground(int pcSlot, Actor pc)
        => Arena.ZoneCircleOutlineUnclipped(Arena.Center, 23.7f, Colors.Danger, 3f);
}
[ModuleInfo(BossModuleInfo.Maturity.Contributed,
    Contributors = "KanoNoUta",
    PrimaryActorOID = (uint)OID.SwordDancer,
    GroupType = BossModuleInfo.GroupType.TheForkedTowerMagic,
    GroupID = 1017u,
    NameID = 0u,
    SortOrder = 2,
    Category = BossModuleInfo.Category.Foray,
    Expansion = BossModuleInfo.Expansion.Dawntrail)]
public sealed class SwordDancer : BossModule
{
    public SwordDancer(WorldState ws, Actor primary) : base(ws, primary, new(600f, 704f), new ArenaBoundsCircle(23.7f))
        => Service.Logger.Information($"[FT] {GetType().Name} created (oid={primary.OID:X})");

    protected override void DrawEnemies(int pcSlot, Actor pc) => Arena.Actor(PrimaryActor, allowDeadAndUntargetable: true);
}