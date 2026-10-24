namespace BossMod.Shadowbringers.Foray.CriticalEngagement.CE21FinalFurlong;

public enum OID : uint
{
    Boss = 0x2DBB, // R3.375
    Monoceros = 0x2DB9, // R1.8
    LlofiiTheForthright = 0x2DBA, // R0.5
    GraspingRancor = 0x2DBC, // R1.6
    Helper = 0x233C
}

public enum AID : uint
{
    AutoAttackBoss = 6498, // Boss->player, no cast, single-target
    Teleport = 20135, // Monoceros->location, no cast, single-target, teleport

    GraspingRancor = 20118, // Boss->self, 4.0s cast, single-target, visual (spawn hands)
    SpitefulGuillotine = 20119, // GraspingRancor->player, no cast, single-target, debuffs or kills if reaches tethered player
    SpiteWave = 20124, // Boss->self, 3.0s cast, single-target, visual (stack + puddles)
    HatefulMiasma = 20125, // Helper->players, 5.0s cast, range 6 circle stack
    PoisonedWords = 20126, // Helper->location, 5.0s cast, range 6 circle puddles
    TalonedGaze = 20127, // Boss->self, 4.0s cast, single-target, visual (front/back then sides)
    TalonedWings = 20128, // Boss->self, 4.0s cast, single-target, visual, (sides then front/back)
    CoffinNails = 20129, // Helper->self, 4.7s cast, range 60 90-degree cone aoe
    Stab = 20130, // Boss->player, 4.0s cast, single-target, tankbuster
    GripOfPoison = 20131, // Boss->self, 4.0s cast, range 60 circle, raidwide
    StepsOfDestruction = 21018, // Boss->self, 4.0s cast, single-target, visual (puddles)
    StepsOfDestructionAOE = 21019, // Helper->location, 4.0s cast, range 6 circle puddle
    AutoAttackMonoceros = 871, // Monoceros->Boss, no cast, single-target
    PurifyingLight = 20132, // Monoceros->location, 12.0s cast, range 12 circle (destroys hands)
    FabledHope = 20134, // Monoceros->self, 8.0s cast, range 10 circle, damage up on players
    Ruin = 20142, // LlofiiTheForthright->Boss, 2.5s cast, single-target, autoattack
    Scupper = 21334 // LlofiiTheForthright->Boss, 2.0s cast, single-target, damage down on boss
}

public enum TetherID : uint
{
    Movable = 1, // GraspingRancor->player
    Frozen = 2, // GraspingRancor->player
    Unfreezable = 17 // GraspingRancor->player (appears if hand wasn't hit by aoe)
}

sealed class GraspingRancor : Components.SimpleAOEs
{
    private const float FreezeRadius = 10f; // Leave 2y inside the R12 cleanse for movement/network delay.
    private const float MinSeparation = 9f; // Guillotine can hit from 5y; turn before the hand reaches that range.
    private const float ResumeSeparation = 12f;
    private readonly List<Actor> _hands;
    private readonly HashSet<ulong> _cleansed = [];
    private readonly HashSet<ulong> _retreating = [];
    private readonly AIHints _navigationHints = new();
    private Pathfinding.NavigationDecision.Context? _navigation;
    private DateTime _nextNavigation;
    private ulong _lastHand;
    private bool _holding;
    private WPos? _waypoint;

    public GraspingRancor(BossModule module) : base(module, (uint)AID.PurifyingLight, 12f)
    {
        Color = Colors.SafeFromAOE;
        Risky = false;
        _hands = module.Enemies((uint)OID.GraspingRancor);
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        base.AddHints(slot, actor, hints);
        if (FindHand(actor) is { } hand)
        {
            if (hand.Tether.ID == (uint)TetherID.Unfreezable)
                hints.Add("Hand missed the cleanse: keep running, it can no longer be frozen!");
            else
                hints.Add(ShouldFreeze(actor, hand) ? "Face the hand; keep your distance until it is cleansed!" : "Look away from hand and kite into safezone!",
                    ShouldFreeze(actor, hand) != (hand.Tether.ID == (uint)TetherID.Frozen));
        }
    }

    private Actor? FindHand(Actor actor) => _hands.FirstOrDefault(h => !h.IsDeadOrDestroyed && !_cleansed.Contains(h.InstanceID)
        && h.Tether.Target == actor.InstanceID && h.Tether.ID is (uint)TetherID.Movable or (uint)TetherID.Frozen or (uint)TetherID.Unfreezable);

    private bool ShouldFreeze(Actor actor, Actor hand) => hand.Tether.ID != (uint)TetherID.Unfreezable
        && (Casters.Count == 0 || hand.Position.InCircle(Casters[0].Origin, FreezeRadius)
            || actor.Position.InCircle(hand.Position, MinSeparation) || _retreating.Contains(hand.InstanceID));

    private WPos KiteDestination(Actor hand)
    {
        var center = Casters.Count > 0 ? Casters[0].Origin : Arena.Center;
        var direction = center - hand.Position;
        return center + (direction.LengthSq() > 0.01f ? direction.Normalized() : new WDir(0, 1)) * 6f;
    }

    public override void AddMovementHints(int slot, Actor actor, MovementHints movementHints)
    {
        if (Casters.Count > 0 && FindHand(actor) is { } hand && !hand.Position.InCircle(Casters[0].Origin, FreezeRadius))
            movementHints.Add(actor.Position, KiteDestination(hand), Colors.Safe);
    }

    // Called after the other components have supplied their AOEs, so facing control
    // does not suppress the normal navigator and leave us standing in another mechanic.
    public void GuideHand(int slot, Actor actor, AIHints hints)
    {
        var hand = FindHand(actor);
        if (actor.IsDeadOrDestroyed || hand == null)
        {
            _lastHand = 0;
            _waypoint = null;
            return;
        }

        var separation = (actor.Position - hand.Position).Length();
        if (separation < MinSeparation)
            _retreating.Add(hand.InstanceID);
        else if (separation >= ResumeSeparation)
            _retreating.Remove(hand.InstanceID);

        var freeze = ShouldFreeze(actor, hand);
        var holding = freeze && Casters.Count > 0 && hand.Position.InCircle(Casters[0].Origin, FreezeRadius)
            && separation >= MinSeparation;
        var goal = holding ? actor.Position : KiteDestination(hand);
        if (hand.Tether.ID == (uint)TetherID.Unfreezable)
            goal = Arena.Center + (actor.Position - hand.Position).Normalized() * 24f;

        hints.AddForbiddenZone(new SDCircle(hand.Position, MinSeparation - 1f));
        hints.GoalZones.Add(AIHints.GoalSingleTarget(goal, holding ? 0.75f : 1f, 100f));
        hints.MaxCastTime = 0;
        hints.ForceCancelCastMechanic = true;
        // Use the existing facing lock to prevent autoattacks/instant actions from
        // turning toward the boss while luring, or away from the frozen hand.
        hints.DesiredFacing = Angle.FromDirection(freeze ? hand.Position - actor.Position : actor.Position - hand.Position);
        hints.DesiredFacingExpire = WorldState.FutureTime(0.5d);
        hints.ForcedMarchImminent = true;

        if (_lastHand != hand.InstanceID || _holding != holding || WorldState.CurrentTime >= _nextNavigation)
        {
            _navigationHints.Clear();
            _navigationHints.PathfindMapCenter = hints.PathfindMapCenter;
            _navigationHints.PathfindMapBounds = hints.PathfindMapBounds;
            _navigationHints.PathfindMapObstacles = hints.PathfindMapObstacles;
            _navigationHints.ForbiddenZones.AddRange(hints.ForbiddenZones);
            _navigationHints.TemporaryObstacles.AddRange(hints.TemporaryObstacles);
            _navigationHints.GoalZones.Add(AIHints.GoalSingleTarget(goal, holding ? 0.75f : 1f, 100f));
            _navigationHints.Normalize();
            var speed = WorldState.Client.MoveSpeed;
            var decision = Pathfinding.NavigationDecision.Build(_navigation ??= new(), WorldState.CurrentTime, _navigationHints, actor,
                speed > 0 && float.IsFinite(speed) ? Math.Min(speed, 6f) : 6f, 0.5f);
            _waypoint = decision.Destination;
            _nextNavigation = WorldState.FutureTime(0.1d);
            _lastHand = hand.InstanceID;
            _holding = holding;
        }
        // ForcedMarchImminent suspends ordinary AI navigation; supply the safe
        // waypoint ourselves while retaining the existing movement/facing hooks.
        hints.ForcedMovement = _waypoint is { } waypoint && !actor.Position.InCircle(waypoint, 0.2f)
            ? (waypoint - actor.Position).ToVec3() : Vector3.Zero;
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID.PurifyingLight)
            foreach (var target in spell.Targets)
                _cleansed.Add(target.ID);
    }

    public override void OnActorDestroyed(Actor actor)
    {
        _cleansed.Remove(actor.InstanceID);
        _retreating.Remove(actor.InstanceID);
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc)
    {
        var hand = FindHand(pc);
        if (hand != null)
        {
            var isFrozen = hand.Tether.ID == (uint)TetherID.Frozen;
            Arena.Actor(hand, Colors.Object, true);
            Arena.AddLine(hand.Position, pc.Position, isFrozen ? Colors.Safe : default);
        }
    }
}

sealed class HatefulMiasma(BossModule module) : Components.StackWithCastTargets(module, (uint)AID.HatefulMiasma, 6f);
sealed class PoisonedWordsStepsOfDestruction(BossModule module) : Components.SimpleAOEGroups(module, [(uint)AID.PoisonedWords, (uint)AID.StepsOfDestructionAOE], 6f);

sealed class CoffinNails : Components.SimpleAOEs
{
    public CoffinNails(BossModule module) : base(module, (uint)AID.CoffinNails, new AOEShapeCone(60f, 45f.Degrees()))
    {
        MaxDangerColor = 2;
        MaxRisky = 2;
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var count = Casters.Count;
        if (count == 0)
            return;
        base.AddAIHints(slot, actor, assignment, hints);
        // stay close to the middle if there is next a 2nd aoe set
        if (count > 2)
        {
            var aoe = Casters[0];
            hints.AddForbiddenZone(new SDInvertedCircle(aoe.Origin, 4f), aoe.Activation);
        }
    }
}

sealed class Stab(BossModule module) : Components.SingleTargetCast(module, (uint)AID.Stab);
sealed class GripOfPoison(BossModule module) : Components.RaidwideCast(module, (uint)AID.GripOfPoison);

sealed class CE21FinalFurlongStates : StateMachineBuilder
{
    public CE21FinalFurlongStates(BossModule module) : base(module)
    {
        TrivialPhase()
            .ActivateOnEnter<GraspingRancor>()
            .ActivateOnEnter<HatefulMiasma>()
            .ActivateOnEnter<PoisonedWordsStepsOfDestruction>()
            .ActivateOnEnter<CoffinNails>()
            .ActivateOnEnter<Stab>()
            .ActivateOnEnter<GripOfPoison>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.Verified, Contributors = "Malediktus", GroupType = BossModuleInfo.GroupType.CriticalEngagement, GroupID = 735, NameID = 6)] // bnpcname=9405
public sealed class CE21FinalFurlong(WorldState ws, Actor primary) : BossModule(ws, primary, arena.Center, arena)
{
    private static readonly ArenaBoundsCustom arena = new([new Polygon(new(644f, 228f), 29.5f, 32)]);

    protected override bool CheckPull() => base.CheckPull() && Raid.Player()!.Position.InCircle(Arena.Center, 30f);

    protected override void CalculateModuleAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        if (AI.AIManager.Instance?.Beh != null || Autorotation.MiscAI.NormalMovement.Instance != null)
            FindComponent<GraspingRancor>()?.GuideHand(slot, actor, hints);
    }
}
