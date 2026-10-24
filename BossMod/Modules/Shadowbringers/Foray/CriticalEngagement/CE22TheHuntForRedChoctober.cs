namespace BossMod.Shadowbringers.Foray.CriticalEngagement.CE22TheHuntForRedChoctober;

public enum OID : uint
{
    Boss = 0x2E68, // R1.98
    RedChocobo1 = 0x2E3A, // R1.32
    RedChocobo2 = 0x2E69, // R1.32
    Helper = 0x233C
}

public enum AID : uint
{
    AutoAttack = 872, // Boss->player, no cast, single-target

    ChocoSlash = 20588, // Boss->player, 5.0s cast, single-target, tankbuster

    ChocoMeteorStrikeVisual1 = 20571, // Boss->self, 6.0s cast, single-target
    ChocoMeteorStrikeVisual2 = 20572, // RedChocobo2->self, no cast, single-target
    ChocoMeteorStrikeFirst = 20575, // Helper->self, 5.0s cast, range 8 circle
    ChocoMeteorStrikeRest = 20576, // Helper->self, no cast, range 8 circle

    ChocoBeakTeleport = 20573, // RedChocobo2->location, 5.0s cast, ???
    ChocoBeak1 = 20574, // Helper->self, 5.0s cast, range 40 width 20 rect
    ChocoBeak2 = 20582, // Helper->self, 5.0s cast, range 40 width 16 rect

    ChocoMeteoruptionVisual = 20567, // Boss->self, 4.0s cast, single-target
    ChocoMeteoruption = 20568, // Helper->location, 4.0s cast, range 6 circle

    ChocoCometVisual = 20579, // Boss->self, 5.0s cast, single-target, towers
    ChocoComet = 20580, // Helper->location, 5.0s cast, range 5 circle
    ChocoCometBurst = 20581, // Helper->self, no cast, range 40 circle, tower fail

    ChocoMeteorStreamVisual1 = 20583, // Boss->self, 6.0s cast, single-target
    ChocoMeteorStreamVisual2 = 20585, // Boss->self, 7.0s cast, single-target
    ChocoMeteorStreamVisual3 = 20584, // RedChocobo2->self, no cast, single-target
    ChocoMeteorStreamFirst = 20586, // Helper->location, 7.0s cast, range 8 circle
    ChocoMeteorStreamRest = 20587, // Helper->location, 1.0s cast, range 8 circle

    ChocoMeteorImpactVisual = 20569, // Boss->self, 4.9s cast, single-target, stack
    ChocoMeteorImpact = 20570, // Boss->players, no cast, range 5 circle

    ChocoMeteorainVisual1 = 20564, // Boss->self, 6.0s cast, single-target
    ChocoMeteorainVisual2 = 20565, // RedChocobo2->self, no cast, single-target
    ChocoMeteorainVisual3 = 20566, // RedChocobo2->location, 9.5s cast, single-target
    ChocoMeteorain = 21424 // Helper->self, 11.0s cast, range 40 circle, damage fall off aoe, approximately 21 optimal radius
}

public enum IconID : uint
{
    ChocoMeteorImpact = 161 // player->self
}

sealed class ChocoMeteorStrike(BossModule module) : Components.Exaflare(module, 8f)
{
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.ChocoMeteorStrikeFirst)
            Lines.Add(new(caster.Position, 4f * spell.Rotation.ToDirection(), Module.CastFinishAt(spell), 1.1d, 8, 4));
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID is (uint)AID.ChocoMeteorStrikeFirst or (uint)AID.ChocoMeteorStrikeRest)
        {
            var count = Lines.Count;
            var pos = caster.Position;
            for (var i = 0; i < count; ++i)
            {
                var line = Lines[i];
                if (line.Next.AlmostEqual(pos, 1f))
                {
                    AdvanceLine(line, pos);
                    if (line.ExplosionsLeft == 0)
                        Lines.RemoveAt(i);
                    break;
                }
            }
        }
    }
}

sealed class ChocoBeak1(BossModule module) : Components.SimpleAOEs(module, (uint)AID.ChocoBeak1, new AOEShapeRect(40f, 10f))
{
    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        // Towers and the central charge resolve together. Do not use the charge lane
        // as a shortcut between towers while it is still technically safe to cross.
        var soaking = Module.FindComponent<ChocoComet>()?.Towers.Count > 0;
        foreach (ref readonly var aoe in ActiveAOEs(slot, actor))
            hints.AddForbiddenZone(aoe.ShapeDistance ?? aoe.Shape.Distance(aoe.Origin, aoe.Rotation), soaking ? default : aoe.Activation);
    }
}
sealed class ChocoBeak2(BossModule module) : Components.SimpleAOEs(module, (uint)AID.ChocoBeak2, new AOEShapeRect(40f, 8f));
sealed class ChocoSlash(BossModule module) : Components.SingleTargetCast(module, (uint)AID.ChocoSlash);
sealed class ChocoMeteoruption(BossModule module) : Components.SimpleAOEs(module, (uint)AID.ChocoMeteoruption, 6f);
sealed class ChocoComet(BossModule module) : Components.CastTowersOpenWorld(module, (uint)AID.ChocoComet, 5f, maxSoakers: int.MaxValue)
{
    private readonly HashSet<Actor> _soakers = [];
    private readonly Dictionary<ulong, Tower> _targets = [];

    public override void Update()
    {
        if (Towers.Count == 0)
        {
            _soakers.Clear();
            _targets.Clear();
            return;
        }
        // Count live players, including non-party members and players who arrived
        // after the towers spawned. More than one soaker does not fail this mechanic.
        _soakers.Clear();
        foreach (var actor in WorldState.Actors.Actors.Values)
            if (actor.Type == ActorType.Player && !actor.IsDeadOrDestroyed)
                _soakers.Add(actor);
        foreach (var tower in Towers)
            tower.AllowedSoakers = _soakers;
    }

    private int OtherSoakers(Tower tower, Actor actor)
    {
        var count = 0;
        foreach (var soaker in _soakers)
            if (soaker != actor && tower.IsInside(soaker))
                ++count;
        return count;
    }

    private WPos SafePosition(Tower tower) => tower.Position + (tower.Position - Arena.Center).Normalized() * 1.5f;

    private bool SameSide(Tower tower, Actor actor)
    {
        if (Module.FindComponent<ChocoBeak1>() is { } charge)
        {
            foreach (ref readonly var aoe in charge.ActiveAOEs(0, actor))
            {
                var lateral = aoe.Rotation.ToDirection().OrthoL();
                var side = (actor.Position - aoe.Origin).Dot(lateral);
                if (Math.Abs(side) > 0.5f && side * (tower.Position - aoe.Origin).Dot(lateral) < 0f)
                    return false;
            }
        }
        return true;
    }

    private Tower? SelectTower(Actor actor)
    {
        Tower? current = null;
        foreach (var tower in Towers)
        {
            if (!tower.IsInside(actor))
                continue;
            current = tower;
            // Do not turn one empty tower into another by abandoning our own.
            if (OtherSoakers(tower, actor) == 0)
                return tower;
        }

        bool CanFill(Tower tower) => OtherSoakers(tower, actor) == 0 && SameSide(tower, actor)
            && (SafePosition(tower) - actor.Position).Length() / 6f + 0.75f < (tower.Activation - WorldState.CurrentTime).TotalSeconds;

        if (_targets.TryGetValue(actor.InstanceID, out var target) && Towers.Contains(target) && CanFill(target))
            return target;

        Tower? empty = null;
        var distance = float.MaxValue;
        foreach (var tower in Towers)
        {
            if (!CanFill(tower))
                continue;
            var d = (SafePosition(tower) - actor.Position).LengthSq();
            if (d < distance)
            {
                distance = d;
                empty = tower;
            }
        }
        if (empty != null || current != null)
            return empty ?? current;

        // If no empty tower is reachable in time, share a nearby tower rather than
        // declaring every occupied tower dangerous and returning to the boss.
        Tower? nearest = null;
        distance = float.MaxValue;
        foreach (var tower in Towers)
        {
            if (!SameSide(tower, actor))
                continue;
            var d = (SafePosition(tower) - actor.Position).LengthSq();
            if (d < distance)
            {
                distance = d;
                nearest = tower;
            }
        }
        return nearest;
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (SelectTower(actor) is { } tower)
            hints.Add(tower.IsInside(actor) ? "Stay in the tower until it resolves!" : OtherSoakers(tower, actor) == 0 ? "Soak an empty tower on your side!" : "Soak the tower!", !tower.IsInside(actor));
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (SelectTower(actor) is not { } tower)
            return;
        _targets[actor.InstanceID] = tower;
        // Keep a margin inside the tower and prefer its outer side, away from the
        // central charge. Navigation still respects the other components' AOEs.
        hints.AddForbiddenZone(new SDInvertedCircle(tower.Position, 4f), tower.Activation);
        hints.GoalZones.Add(AIHints.GoalSingleTarget(SafePosition(tower), 1f, 100f));
        BitMask soakers = default;
        foreach (var member in Raid.WithSlot())
            foreach (var active in Towers)
                if (active.IsInside(member.Item2))
                {
                    soakers.Set(member.Item1);
                    break;
                }
        hints.AddPredictedDamage(soakers, tower.Activation, DamageType);
    }
}
sealed class ChocoMeteorain(BossModule module) : Components.SimpleAOEs(module, (uint)AID.ChocoMeteorain, 21f, 2);
sealed class ChocoMeteorImpact(BossModule module) : Components.StackWithIcon(module, (uint)IconID.ChocoMeteorImpact, (uint)AID.ChocoMeteorImpact, 5f, 5.1f, 8);

sealed class ChocoMeteorStream(BossModule module) : Components.GenericAOEs(module)
{
    private readonly List<AOEInstance> _aoes = [with(28)];
    private static readonly AOEShapeCircle circle = new(8f);
    private readonly List<WPos> initialPositions = [with(2)];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        var count = _aoes.Count;
        if (count == 0)
            return [];
        var max = count > 4 ? 4 : count;
        var aoes = CollectionsMarshal.AsSpan(_aoes);
        var maxC = Math.Min(max, count - NumCasts);
        var maxI = NumCasts + maxC;
        var numCasts2 = NumCasts + 2;
        for (var i = NumCasts; i < maxI; ++i)
        {
            ref var aoe = ref aoes[i];
            if (i < numCasts2 && i < 26)
            {
                aoe.Color = Colors.Danger;
            }
        }
        return aoes.Slice(NumCasts, maxC);
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == (uint)AID.ChocoMeteorStreamFirst)
        {
            var loc = spell.LocXZ;
            _aoes.Add(new(circle, loc, default, Module.CastFinishAt(spell)));
            initialPositions.Add(loc);
        }
        else if (spell.Action.ID == (uint)AID.ChocoMeteorStreamRest)
        {
            var loc = spell.LocXZ;
            if (_aoes.Count < 28)
            {
                var center = Arena.Center;
                var dir = loc - center;

                // identify the closest initial position
                var a = initialPositions[0];
                var b = initialPositions[1];
                var distA = (a - loc).LengthSq();
                var distB = (b - loc).LengthSq();
                var closest = distA < distB ? a : b;
                var dirClosest = closest - center;

                var angle = (dirClosest.Cross(dir) > 0f ? -1f : 1f) * 45f.Degrees(); // cross product to detect rotation direction
                for (var i = 0; i < 13; ++i)
                {
                    var pos = (i == 0 ? loc : center + dir.Rotate(i * angle)).Quantized();
                    _aoes.Add(new(circle, pos, default, Module.CastFinishAt(spell, i * 2d)));
                }

                if (_aoes.Count == 28)
                    _aoes.Sort((x, y) => x.Activation.CompareTo(y.Activation));
            }
            else
            {
                var aoes = CollectionsMarshal.AsSpan(_aoes);
                var maxC = Math.Min(2, 28 - NumCasts);
                var maxI = NumCasts + maxC;

                for (var i = NumCasts; i < maxI; ++i)
                {
                    ref var aoe = ref aoes[i];
                    if (loc.AlmostEqual(aoe.Origin, 1f))
                    {
                        aoe.Origin = loc;
                        return;
                    }
                }
            }
        }
        if (spell.Action.ID == (uint)AID.ChocoMeteorStreamRest) // update location to replace prediction with actual location to ensure pixel perfectness
        {
            var count = _aoes.Count;
            var max = count > 2 ? 2 : count;
            var aoes = CollectionsMarshal.AsSpan(_aoes);
            var maxC = Math.Min(max, count - NumCasts);
            var maxI = NumCasts + maxC;
            var loc = spell.LocXZ;
            for (var i = NumCasts; i < maxI; ++i)
            {
                ref var aoe = ref aoes[i];
                if (loc.AlmostEqual(aoe.Origin, 1f))
                {
                    aoe.Origin = loc;
                    return;
                }
            }
        }
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is (uint)AID.ChocoMeteorStreamFirst or (uint)AID.ChocoMeteorStreamRest)
        {
            if (++NumCasts == 28)
            {
                _aoes.Clear();
                initialPositions.Clear();
                NumCasts = 0;
            }
        }
    }
}

sealed class CE22TheHuntForRedChoctoberStates : StateMachineBuilder
{
    public CE22TheHuntForRedChoctoberStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<ChocoBeak1>()
            .ActivateOnEnter<ChocoBeak2>()
            .ActivateOnEnter<ChocoSlash>()
            .ActivateOnEnter<ChocoMeteoruption>()
            .ActivateOnEnter<ChocoComet>()
            .ActivateOnEnter<ChocoMeteorain>()
            .ActivateOnEnter<ChocoMeteorStrike>()
            .ActivateOnEnter<ChocoMeteorImpact>()
            .ActivateOnEnter<ChocoMeteorStream>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Verified, Contributors = "The Combat Reborn Team (Malediktus)", GroupType = BossModuleInfo.GroupType.CriticalEngagement, GroupID = 735, NameID = 7)]
public sealed class CE22TheHuntForRedChoctober(WorldState ws, Actor primary) : BossModule(ws, primary, arena.Center, arena)
{
    private static readonly ArenaBoundsCustom arena = new([new Polygon(new(393f, 268f), 19.5f, 32)]);

    protected override bool CheckPull() => base.CheckPull() && Raid.Player()!.Position.InCircle(Arena.Center, 20f);
}
