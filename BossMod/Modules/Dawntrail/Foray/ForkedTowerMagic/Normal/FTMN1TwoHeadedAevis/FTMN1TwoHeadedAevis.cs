using BossMod.Dawntrail.Foray.CriticalEngagement;

namespace BossMod.Dawntrail.Foray.ForkedTowerMagic.Normal.FTMN1TwoHeadedAevis;

// Normal 魔之塔 Boss1: Two-Headed Aevis. 蓝头剧毒吐息 18m 圈、双头恐惧 40x10 直条、
// 雷/冰簇 15m 圈、雷霜暴风雨把场上剩余球全部 15m 圈、魔法阵信标 60x5 直条、冰焰凝环。
sealed class TwoHeadedAevisAOEs(BossModule module) : ReplayValidatedCastAOEs(module)
{
    private static readonly AOEShapeCircle PoisonBreath = new(18f);
    private static readonly AOEShapeRect TwoTerrors = new(40f, 5f);
    private static readonly AOEShapeCircle OrbBurst = new(15f);
    private static readonly AOEShapeRect ArcaneBeacon = new(30f, 2.5f, 30f);
    private static readonly AOEShapeCircle Blaze = new(5f);
    private static readonly AOEShapeDonut BlazeLoop = new(5f, 60f);

    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.Ability_PoisonBreath => new(PoisonBreath, true),
        (uint)AID.Ability_TwoTerrors1 => new(TwoTerrors, true),
        (uint)AID.Ability_Shock => new(OrbBurst),
        (uint)AID.Ability_HypothermalCombustion => new(OrbBurst),
        (uint)AID.Ability_ArcaneBeacon => new(ArcaneBeacon),
        (uint)AID.Ability_Blaze1 or (uint)AID.Ability_Blaze3 or (uint)AID.Ability_Blaze5 => new(Blaze, true),
        (uint)AID.Ability_Blazeloop1 => new(BlazeLoop),
        _ => null
    };
}

// 50697/50698 是选择球的 8s 预兆；真正的球在选择判定后才开始短读条。
// ARR 显示 selector 的落点贴着目标球，因此在长读条开始时就把对应球标成危险。
sealed class SelectedOrbAOEs(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeCircle Shape = new(15f);
    private readonly List<AOEInstance> _displayed = [with(4)];
    private readonly Dictionary<ulong, (uint OID, WPos Pos)> _balls = [];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => CollectionsMarshal.AsSpan(_displayed);

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID is (uint)OID.BallLightning or (uint)OID.SwirlingOrb)
            _balls[actor.InstanceID] = (actor.OID, actor.Position);
    }

    public override void OnActorDestroyed(Actor actor) => _balls.Remove(actor.InstanceID);

    public override void OnActorDeath(Actor actor) => _balls.Remove(actor.InstanceID);

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        var wanted = spell.Action.ID switch
        {
            (uint)AID.Ability_LightningCluster => (uint)OID.BallLightning,
            (uint)AID.Ability_IceCluster1 => (uint)OID.SwirlingOrb,
            _ => 0u
        };
        if (wanted == 0 || spell.EventHappened)
            return;

        WPos? best = null;
        var bestSq = float.MaxValue;
        foreach (var (_, ball) in _balls)
        {
            if (ball.OID != wanted)
                continue;
            var sq = (ball.Pos - spell.LocXZ).LengthSq();
            if (sq < bestSq)
            {
                bestSq = sq;
                best = ball.Pos;
            }
        }
        if (best is not { } pos || bestSq > 15f * 15f)
            return;

        _displayed.RemoveAll(a => (a.Origin - pos).LengthSq() < 1f);
        _displayed.Add(new(Shape, pos, activation: Module.CastFinishAt(spell, 2d)));
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is (uint)AID.Ability_Shock or (uint)AID.Ability_HypothermalCombustion)
            _displayed.RemoveAll(a => (a.Origin - caster.Position).LengthSq() < 4f);
    }

    public override void Update()
    {
        _displayed.RemoveAll(a => WorldState.CurrentTime > a.Activation.AddSeconds(1d));
        base.Update();
    }
}
// 雷霜暴风雨: 施法时把场上所有存活的雷球/冰球位置画 15m 危险圈。
// 球的实机位置/追踪不可靠，改为 OnActorCreated 记录球位（与可达鸭 AddCombatant 记录一致），
// 避免 Enemies 列表缓存/位置缺失导致漏画。
sealed class ThunderfrostTempest(BossModule module) : Components.GenericAOEs(module)
{
    private static readonly AOEShapeCircle Shape = new(15f);
    private readonly List<AOEInstance> _displayed = [with(12)];
    private readonly Dictionary<ulong, WPos> _balls = [];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) => CollectionsMarshal.AsSpan(_displayed);

    public override void OnActorCreated(Actor actor)
    {
        if (actor.OID is (uint)OID.BallLightning or (uint)OID.SwirlingOrb)
            _balls[actor.InstanceID] = actor.Position;
    }

    public override void OnActorDestroyed(Actor actor) => _balls.Remove(actor.InstanceID);

    public override void OnActorDeath(Actor actor) => _balls.Remove(actor.InstanceID);

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != (uint)AID.Ability_ThunderfrostTempest || spell.EventHappened)
            return;

        _displayed.Clear();
        var activation = Module.CastFinishAt(spell);
        foreach (var (_, pos) in _balls)
            _displayed.Add(new(Shape, pos, activation: activation));
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.Ability_ThunderfrostTempest)
            _displayed.Clear();
    }
}

// 定时诅咒（5403 东风 / 5404 西风）：中诅咒后 13s 沿固定方向击退 15y。
// StatusAdd 立即画击退箭头，并在落点画绿色圆环安全区，方便提前站位。
sealed class TimedCurseKnockback(BossModule module) : Components.GenericKnockback(module)
{
    private const float Distance = 15f;
    private const double Delay = 13d;
    private readonly List<Knockback> _knockbacks = [with(8)];
    private readonly List<Knockback> _filtered = [with(2)];

    // 定时诅咒是全员同时中的状态：若不按 ActorID 过滤，GenericKnockback 会把全队每个人的
    // 击退线都从本地玩家脚下画出，24 条红线铺满全场（可达鸭脚本用 TargetId==Me + Owner=Me 过滤）。
    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor)
    {
        _filtered.Clear();
        var span = CollectionsMarshal.AsSpan(_knockbacks);
        for (var i = 0; i < span.Length; ++i)
            if (span[i].ActorID == actor.InstanceID)
                _filtered.Add(span[i]);
        return CollectionsMarshal.AsSpan(_filtered);
    }

    public override void OnStatusGain(Actor actor, ref ActorStatus status)
    {
        var direction = status.ID switch
        {
            (uint)SID.EasterlyReprise => -90f.Degrees(), // 东风 -X
            (uint)SID.WesterlyReprise => 90f.Degrees(), // 西风 +X
            _ => (Angle?)null
        };
        if (direction is not { } dir)
            return;

        _knockbacks.RemoveAll(kb => kb.ActorID == actor.InstanceID);
        _knockbacks.Add(new(actor.Position, Distance, WorldState.FutureTime(Delay), default, dir, Kind.DirForward, actorID: actor.InstanceID));
    }

    public override void OnStatusLose(Actor actor, ref ActorStatus status)
    {
        if (status.ID is (uint)SID.EasterlyReprise or (uint)SID.WesterlyReprise)
            _knockbacks.RemoveAll(kb => kb.ActorID == actor.InstanceID);
    }

    public override void Update()
    {
        var now = WorldState.CurrentTime;
        _knockbacks.RemoveAll(kb => now > kb.Activation.AddSeconds(1d));
        base.Update();
    }
}


// 风暴吐息: 全场从场中击退 15m（旧版视为击退而非 30m 全屏圈）。
// 画击退线 + 场中绿色安全区；不加全屏禁区避免"全屏伤害"。
sealed class StormsBreathKnockback(BossModule module) : Components.GenericKnockback(module)
{
    private const float Distance = 15f;
    private readonly List<Knockback> _knockbacks = [with(2)];

    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor) => CollectionsMarshal.AsSpan(_knockbacks);

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != (uint)AID.Ability_StormsBreathAOE || spell.EventHappened)
            return;
        _knockbacks.Clear();
        _knockbacks.Add(new(Module.Arena.Center, Distance, Module.CastFinishAt(spell)));
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.Ability_StormsBreathAOE)
            _knockbacks.Clear();
    }

    public override void Update()
    {
        var now = WorldState.CurrentTime;
        _knockbacks.RemoveAll(kb => now > kb.Activation.AddSeconds(1d));
        base.Update();
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        base.DrawArenaForeground(pcSlot, pc);
        // 场中绿色安全区: 距中心 5m 内被击退 15m 后仍留在 20m 半宽的场内
        if (_knockbacks.Count != 0)
            Arena.ZoneCircle(Module.Arena.Center, 5f, Colors.Safe);
    }
}
[ModuleInfo(BossModuleInfo.Maturity.Contributed,
    Contributors = "KanoNoUta",
    PrimaryActorOID = (uint)OID.TwoHeadedAevis,
    GroupType = BossModuleInfo.GroupType.TheForkedTowerMagic,
    GroupID = 1017u,
    NameID = 0u,
    SortOrder = 1,
    Category = BossModuleInfo.Category.Foray,
    Expansion = BossModuleInfo.Expansion.Dawntrail)]
public sealed class TwoHeadedAevis : BossModule
{
    public TwoHeadedAevis(WorldState ws, Actor primary) : base(ws, primary, new(-900f, 700f), new ArenaBoundsSquare(20f))
        => Service.Logger.Information($"[FT] {GetType().Name} created (oid={primary.OID:X})");

    protected override bool CheckPull()
        => base.CheckPull()
        || Enemies((uint)OID.GreenHead1).Any(h => h.IsTargetable && h.InCombat)
        || Enemies((uint)OID.BlueHead1).Any(h => h.IsTargetable && h.InCombat);

    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        foreach (var head in Enemies((uint)OID.GreenHead1))
            Arena.Actor(head, allowDeadAndUntargetable: true);
        foreach (var head in Enemies((uint)OID.BlueHead1))
            Arena.Actor(head, allowDeadAndUntargetable: true);
    }
}