namespace BossMod.Shadowbringers.Foray.CriticalEngagement.CE42FromBeyondTheGrave;

public enum OID : uint
{
    Boss = 0x2E35, // R8.25
    Deathwall = 0x1EB173, // R0.5
    DeathwallHelper = 0x2EE8, // R0.50
    ShockSphere = 0x3232, // R1.0
    WarWraith = 0x3233, // R1.8
    HernaisTheTenacious = 0x3234, // R0.5
    DyunbuTheAccursed = 0x3235, // R0.5
    LlofiiTheForthright = 0x3236, // R0.5
    Monoceros = 0x3237, // R1.8
    LivingCorpseSpawn = 0x1EB07A, // R0.5
    Helper = 0x233C
}

public enum AID : uint
{
    AutoAttackBoss = 24692, // Boss->player, no cast, single-target
    AutoAttackHernais = 6497, // HernaisTheTenacious->player, no cast, single-target
    AutoAttackWraith = 6498, // WarWraith->player, no cast, single-target

    DevourSoul = 24093, // Boss->player, 5.0s cast, single-target, tankbuster
    Blight = 24094, // Boss->self, 5.0s cast, single-target, visual
    BlightAOE = 24095, // Helper->self, 5.0s cast, ???, raidwide
    GallowsMarch = 24096, // Boss->self, 3.0s cast, single-target, visual (applies doom and forced march)
    LivingCorpse = 24097, // Boss->self, 3.0s cast, single-target, visual
    ChainMagick = 24098, // Boss->self, 3.0s cast, single-target, applies dualcast for next soul purge
    SoulPurgeCircle = 24099, // Boss->self, 5.0s cast, range 10 circle
    SoulPurgeCircleDual = 24100, // Boss->self, no cast, range 10 circle
    SoulPurgeDonut = 24101, // Boss->self, 5.0s cast, range 10-30 donut
    SoulPurgeDonutDual = 24102, // Boss->self, no cast, range 10-30 donut
    CrimsonBlade = 24103, // HernaisTheTenacious->self, 8.0s cast, range 50 180-degree cone aoe
    BloodCyclone = 24104, // HernaisTheTenacious->self, 3.0s cast, range 5 circle
    Aethertide = 24105, // DyunbuTheAccursed->self, 8.0s cast, single-target, visual
    AethertideAOE = 24106, // Helper->players, 8.0s cast, range 8 circle spread
    MarchingBreath = 24107, // DyunbuTheAccursed->self, 8.0s cast, interruptible, heals all allies by 20% of max health (raidwide)
    TacticalStone = 24108, // DyunbuTheAccursed->player, 2.5s cast, single-target, autoattack
    TacticalAero = 24109, // DyunbuTheAccursed->self, 3.0s cast, range 40 width 8 rect
    Enrage = 24110, // DyunbuTheAccursed->self, 3.0s cast, applies Dmg up and haste to self
    EntropicFlame = 24111, // WarWraith->self, 4.0s cast, range 60 width 8 rect
    DarkFlare = 24112, // WarWraith->location, 5.0s cast, range 8 circle
    SoulSacrifice = 24113, // WarWraith->Boss, 6.0s cast, interruptible, WarWraith sacrifices to give Dmg Up to Boss

    DeadlyToxin = 24699, // DeathwallHelper->self, no cast, range 25-30 donut, deathwall
    Shock = 24114, // ShockSphere->self, no cast, range 7 circle aoe around sphere

    AutoAttackMonoceros = 871, // Monoceros->Boss, no cast, single-target
    PurifyingLight = 24115, // Monoceros->location, 11.0s cast, range 12 circle, visual
    PurifyingLightAOE = 24116, // Helper->location, no cast, range 12 circle, cleanse doom
    Ruin = 24119, // LlofiiTheForthright->Boss, 2.5s cast, single-target, autoattack
    Cleanse = 24969, // LlofiiTheForthright->location, 5.0s cast, range 6 circle, damages boss
    SoothingGlimmer = 24970 // LlofiiTheForthright->self, 2.5s cast, single-target, heal
}

public enum SID : uint
{
    Doom = 1970,
    ForwardMarch = 2161, // Boss->player, extra=0x0
    AboutFace = 2162, // Boss->player, extra=0x0
    LeftFace = 2163, // Boss->player, extra=0x0
    RightFace = 2164 // Boss->player, extra=0x0
}

sealed class ElectricFence(BossModule module) : Components.GenericAOEs(module)
{
    // Like CE53/CE62: retain the outer boundary and draw the persistent lethal floor inside it.
    private static readonly AOEShapeDonut fence = new(CE42FromBeyondTheGrave.SafeRadius, CE42FromBeyondTheGrave.ArenaRadius);
    private readonly List<Actor> _fences = module.Enemies((uint)OID.Deathwall);
    private readonly AOEInstance[] _aoe = [new(fence, module.Center)];
    private DateTime _activation;

    public bool Active => _fences.Any(f => !f.IsDestroyed);

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        if (!Active && _activation == default)
            return [];
        _aoe[0].Activation = Active ? default : _activation;
        return _aoe;
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.GallowsMarch && !Active)
            _activation = Module.CastFinishAt(spell, 0.9d);
    }

    public override void OnActorDestroyed(Actor actor)
    {
        if (actor.OID == (uint)OID.Deathwall)
            _activation = default;
    }

    public override void AddHints(int slot, Actor actor, TextHints hints) { }
}

class DevourSoul(BossModule module) : Components.SingleTargetCast(module, (uint)AID.DevourSoul);
class Blight(BossModule module) : Components.RaidwideCast(module, (uint)AID.Blight);

sealed class GallowsMarch : Components.StatusDrivenForcedMarch
{
    private readonly PurifyingLight _light;
    private readonly List<Actor> _spheres;
    private ShapeDistance[] _marchZones = [];
    private Angle _direction;
    private Angle _facing;
    private DateTime _activation;
    private ulong _plannedActor;

    public GallowsMarch(BossModule module) : base(module, 3f, (uint)SID.ForwardMarch, (uint)SID.AboutFace, (uint)SID.LeftFace, (uint)SID.RightFace)
    {
        // Both replay marches last 3s and travel about 18y, not the shared 6.6y/s default.
        MovementSpeed = 6f;
        _light = module.FindComponent<PurifyingLight>()!;
        _spheres = module.Enemies((uint)OID.ShockSphere);
    }

    public override bool DestinationUnsafe(int slot, Actor actor, WPos pos)
    {
        if (!pos.InCircle(Arena.Center, CE42FromBeyondTheGrave.SafeRadius) || _spheres.Any(s => !s.IsDeadOrDestroyed && pos.InCircle(s.Position, 7f)))
            return true;
        var aoes = _light.ActiveAOEs(slot, actor);
        return aoes.Length != 0 && !aoes[0].Check(pos);
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var aoes = _light.ActiveAOEs(slot, actor);
        if (actor.IsDeadOrDestroyed || aoes.Length == 0)
            return;
        ref readonly var light = ref aoes[0];
        var state = State.GetValueOrDefault(actor.InstanceID);
        if (state == null || state.PendingMoves.Count == 0)
        {
            // March ends ~0.7s before the cleansing hit: stay inside until it actually resolves.
            if (state?.ForcedEnd > WorldState.CurrentTime)
            {
                hints.DesiredFacing = actor.Rotation;
                hints.DesiredFacingExpire = WorldState.FutureTime(0.5d);
                hints.ForcedMarchImminent = true;
                hints.ForceCancelCastMechanic = true;
            }
            else if (_plannedActor == actor.InstanceID && _activation != default && WorldState.CurrentTime < _activation.AddSeconds(0.3d))
            {
                // Directional status loss precedes status 1257 by one replay frame.
                hints.DesiredFacing = _facing;
                hints.DesiredFacingExpire = WorldState.FutureTime(0.5d);
                hints.ForcedMarchImminent = true;
                hints.ForceCancelCastMechanic = true;
            }
            else if (actor.FindStatus((uint)SID.Doom) != null)
                hints.AddForbiddenZone(new SDInvertedCircle(light.Origin, PurifyingLight.Radius - 1f), light.Activation);
            return;
        }

        var move = state.PendingMoves[0];
        var distance = MovementSpeed * move.duration;
        if (_activation != move.activation || _plannedActor != actor.InstanceID)
        {
            _marchZones = [];
            if (!PrepareMarch(actor, light.Origin, distance))
                return;
            _activation = move.activation;
            _plannedActor = actor.InstanceID;
        }
        foreach (var zone in _marchZones)
            hints.AddForbiddenZone(zone, move.activation);

        // Reuse CE32/Brainstorm's prepositioning + forbidden-facing sector. Fixed-direction
        // circle projections and capsules also exclude paths through the shock spheres.
        var facing = _facing = (_direction - move.dir).Normalized();
        hints.ForbiddenDirections.Add(((facing + 180f.Degrees()).Normalized(), 175f.Degrees(), move.activation));
        if (move.activation <= WorldState.FutureTime(0.5d) && !_marchZones.Any(z => z.Contains(actor.Position)))
        {
            // The existing lock prevents autoattacks/instant actions undoing the final facing.
            hints.DesiredFacing = facing;
            hints.DesiredFacingExpire = WorldState.FutureTime(0.5d);
            hints.ForcedMarchImminent = true;
            hints.ForceCancelCastMechanic = true;
        }
    }

    private bool PrepareMarch(Actor actor, WPos light, float distance)
    {
        // As in Brainstorm, search in 5-degree steps. Prefer turning in place; if the
        // player is too close to the center, find the nearest viable staging position.
        var preferred = Angle.FromDirection(light - actor.Position);
        bool TryDirection(WPos start, out Angle direction)
        {
            for (var i = 0; i < 72; ++i)
            {
                direction = preferred + (i * 5f).Degrees();
                var dir = direction.ToDirection();
                var end = start + dir * distance;
                if (!start.InCircle(Arena.Center, CE42FromBeyondTheGrave.SafeRadius - 1f)
                    || !end.InCircle(Arena.Center, CE42FromBeyondTheGrave.SafeRadius - 1f)
                    || !end.InCircle(light, PurifyingLight.Radius - 2f))
                    continue;
                if (!_spheres.Any(s => !s.IsDeadOrDestroyed && Intersect.RayCircle(start - s.Position, dir, 9f, distance)))
                    return true;
            }
            direction = default;
            return false;
        }

        if (!TryDirection(actor.Position, out _direction))
        {
            var best = float.MaxValue;
            for (var x = -24f; x <= 24f; x += 2f)
                for (var z = -24f; z <= 24f; z += 2f)
                {
                    var start = Arena.Center + new WDir(x, z);
                    var cost = (start - actor.Position).LengthSq();
                    if (cost < best && TryDirection(start, out var direction))
                    {
                        best = cost;
                        _direction = direction;
                    }
                }
            if (best == float.MaxValue)
                return false;
        }

        var displacement = _direction.ToDirection() * distance;
        var zones = new List<ShapeDistance>
        {
            new SDInvertedCircle(light - displacement, PurifyingLight.Radius - 2f),
            new SDInvertedCircle(Arena.Center - displacement, CE42FromBeyondTheGrave.SafeRadius - 1f)
        };
        foreach (var sphere in _spheres)
            if (!sphere.IsDeadOrDestroyed)
                zones.Add(new SDCapsule(sphere.Position, -_direction.ToDirection(), distance, 9f));
        _marchZones = [.. zones];
        return true;
    }

    public override void AddHints(int slot, Actor actor, TextHints hints) { }
}

class ShockSphere(BossModule module) : Components.Voidzone(module, 7f, GetSpheres)
{
    private static List<Actor> GetSpheres(BossModule module) => module.Enemies((uint)OID.ShockSphere);
}

class SoulPurge(BossModule module) : Components.GenericAOEs(module)
{
    private bool dualCast;
    private readonly List<AOEInstance> _aoes = [with(2)];

    private static readonly AOEShapeCircle circle = new(10f);
    private static readonly AOEShapeDonut donut = new(10f, 30f);

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        var count = _aoes.Count;
        if (count == 0)
            return [];
        var aoes = CollectionsMarshal.AsSpan(_aoes);
        for (var i = 0; i < count; ++i)
        {
            ref var aoe = ref aoes[i];
            if (i == 0)
            {
                if (count > 1)
                    aoe.Color = Colors.Danger;
                aoe.Risky = true;
            }
            else
                aoe.Risky = false;
        }
        return aoes;
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        switch (spell.Action.ID)
        {
            case (uint)AID.ChainMagick:
                dualCast = true;
                break;
            case (uint)AID.SoulPurgeDonut:
                if (!dualCast)
                    AddAOE(donut);
                else
                    AddAOEs(donut, circle);
                break;
            case (uint)AID.SoulPurgeCircle:
                if (!dualCast)
                    AddAOE(circle);
                else
                    AddAOEs(circle, donut);
                break;
        }
        void AddAOE(AOEShape shape, float delay = default) => _aoes.Add(new(shape, spell.LocXZ, default, Module.CastFinishAt(spell, delay)));
        void AddAOEs(AOEShape shape1, AOEShape shape2)
        {
            AddAOE(shape1);
            AddAOE(shape2, 2.1f);
            dualCast = false;
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (_aoes.Count != 0 && spell.Action.ID is (uint)AID.SoulPurgeCircle or (uint)AID.SoulPurgeCircleDual or (uint)AID.SoulPurgeDonut or (uint)AID.SoulPurgeDonutDual)
            _aoes.RemoveAt(0);
    }
}

class CrimsonBlade(BossModule module) : Components.SimpleAOEs(module, (uint)AID.CrimsonBlade, new AOEShapeCone(50f, 90f.Degrees()));
class BloodCyclone(BossModule module) : Components.SimpleAOEs(module, (uint)AID.BloodCyclone, 5f);
sealed class Aethertide(BossModule module) : Components.SpreadFromCastTargets(module, (uint)AID.AethertideAOE, 8f)
{
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var spreads = ActiveSpreads;
        if (spreads.Count == 0)
            return;
        var hazards = new List<Components.GenericAOEs.AOEInstance>();
        foreach (var component in Module.Components.OfType<Components.GenericAOEs>())
            foreach (ref readonly var aoe in component.ActiveAOEs(slot, actor))
                if (aoe.Risky)
                {
                    hazards.Add(aoe);
                    // Spread inside the available safe area from the start of the cast;
                    // do not cross Crimson Blade's future danger zone to reach a wider gap.
                    hints.AddForbiddenZone(aoe.ShapeDistance ?? aoe.Shape.Distance(aoe.Origin, aoe.Rotation));
                }
        if (IsSpreadTarget(actor))
        {
            var others = spreads.Where(s => s.Target != actor).Select(s => s.Target.Position).ToArray();
            if (others.Length == 0)
                return;
            // Like CE201 Familiar Tactics, score only positions outside the other mechanics.
            // Separation is a preference: the 180-degree Crimson Blade can leave too little
            // room for three non-overlapping circles. Hard 17y exclusions erase that safe area.
            var unsafeAOEs = hazards.ToArray();
            var center = Arena.Center;
            var separation = 2f * SpreadRadius + 1f;
            hints.GoalZones.Add(position =>
            {
                if (!position.InCircle(center, CE42FromBeyondTheGrave.SafeRadius - 1f) || unsafeAOEs.Any(aoe => aoe.Check(position)))
                    return 0f;
                var clearance = separation;
                foreach (var other in others)
                    clearance = Math.Min(clearance, (position - other).Length());
                return 20f + 10f * clearance / separation;
            });
            return;
        }
        for (var i = 0; i < spreads.Count; ++i)
        {
            var first = spreads[i];
            for (var j = i + 1; j < spreads.Count; ++j)
            {
                var second = spreads[j];
                // Unmarked players may take one circle, but never the intersection of two.
                hints.AddForbiddenZone(new SDIntersection([new SDCircle(first.Target.Position, SpreadRadius),
                    new SDCircle(second.Target.Position, SpreadRadius)]));
            }
        }
    }

    public override void AddHints(int slot, Actor actor, TextHints hints) { }
}
class MarchingBreath(BossModule module) : Components.CastInterruptHint(module, (uint)AID.MarchingBreath, showNameInHint: true); // heals all allies by 20% of max health (raidwide)
class TacticalAero(BossModule module) : Components.SimpleAOEs(module, (uint)AID.TacticalAero, new AOEShapeRect(40f, 4f));
class EntropicFlame(BossModule module) : Components.SimpleAOEs(module, (uint)AID.EntropicFlame, new AOEShapeRect(60f, 4f));
class DarkFlare(BossModule module) : Components.SimpleAOEs(module, (uint)AID.DarkFlare, 8f);
class SoulSacrifice(BossModule module) : Components.CastInterruptHint(module, (uint)AID.SoulSacrifice, showNameInHint: true); // WarWraith sacrifices itself to give boss a damage buff

class PurifyingLight : Components.SimpleAOEs
{
    public const float Radius = 12f;

    public PurifyingLight(BossModule module) : base(module, (uint)AID.PurifyingLight, Radius)
    {
        Color = Colors.SafeFromAOE;
        Risky = false;
    }

    // The helper hit is ~0.1s after cast finish; retain the safe circle through that gap.
    public override void OnCastFinished(Actor caster, ActorCastInfo spell) { }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID.PurifyingLightAOE)
            Casters.Clear();
    }

    public override void Update() => Casters.RemoveAll(c => c.Activation.AddSeconds(1d) < WorldState.CurrentTime);
}

class CE42FromBeyondTheGraveStates : StateMachineBuilder
{
    public CE42FromBeyondTheGraveStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .OnEnter(((CE42FromBeyondTheGrave)module).DisableAutomaticSprint)
            .OnExit(((CE42FromBeyondTheGrave)module).RestoreSprint)
            .ActivateOnEnter<ElectricFence>()
            .ActivateOnEnter<DevourSoul>()
            .ActivateOnEnter<Blight>()
            .ActivateOnEnter<PurifyingLight>()
            .ActivateOnEnter<GallowsMarch>()
            .ActivateOnEnter<ShockSphere>()
            .ActivateOnEnter<SoulPurge>()
            .ActivateOnEnter<CrimsonBlade>()
            .ActivateOnEnter<BloodCyclone>()
            .ActivateOnEnter<Aethertide>()
            .ActivateOnEnter<MarchingBreath>()
            .ActivateOnEnter<TacticalAero>()
            .ActivateOnEnter<EntropicFlame>()
            .ActivateOnEnter<DarkFlare>()
            .ActivateOnEnter<SoulSacrifice>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Verified, Contributors = "Malediktus", GroupType = BossModuleInfo.GroupType.CriticalEngagement, GroupID = 778, NameID = 20)] // bnpcname=9931
public class CE42FromBeyondTheGrave(WorldState ws, Actor primary) : BossModule(ws, primary, ArenaCenter, new ArenaBoundsCircle(ArenaRadius))
{
    public static readonly WPos ArenaCenter = new(-60f, 800f);
    public const float ArenaRadius = 30f;
    public const float SafeRadius = 25f;
    private ActionDefinition? _sprint;
    private ActionDefinition.ConditionDelegate? _previousSprintCondition;
    private ActionDefinition.ConditionDelegate? _sprintCondition;

    // Reuse CE32's module-scoped sprint policy: automatic sprint would change march distance.
    internal void DisableAutomaticSprint()
    {
        if (_sprint != null || ActionDefinitions.Instance[ActionDefinitions.IDSprint] is not { } sprint)
            return;
        _sprint = sprint;
        var previous = _previousSprintCondition = sprint.AllowExecute;
        sprint.AllowExecute = _sprintCondition = (ws, player, action, hints)
            => (action.Manual || !ReferenceEquals(ws, WorldState) || StateMachine.ActivePhase == null)
                && (previous?.Invoke(ws, player, action, hints) ?? true);
    }

    internal void RestoreSprint()
    {
        if (_sprint != null && _sprint.AllowExecute == _sprintCondition)
            _sprint.AllowExecute = _previousSprintCondition;
        _sprint = null;
    }

    protected override void DrawEnemies(int pcSlot, Actor pc)
    {
        base.DrawEnemies(pcSlot, pc);
        Arena.Actors(Enemies((uint)OID.WarWraith));
        Arena.Actors(Enemies((uint)OID.HernaisTheTenacious));
        Arena.Actors(Enemies((uint)OID.DyunbuTheAccursed));
    }

    protected override bool CheckPull() => base.CheckPull() && Raid.Player()!.Position.InCircle(Arena.Center, 30f);
}
