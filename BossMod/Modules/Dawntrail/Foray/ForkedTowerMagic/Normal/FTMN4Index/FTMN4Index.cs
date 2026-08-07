using BossMod.Dawntrail.Foray.CriticalEngagement;

namespace BossMod.Dawntrail.Foray.ForkedTowerMagic.Normal.FTMN4Index;

// Normal 魔之塔 Boss4: Index. 封印武器、居合斩/风斩、预言现象、元素扇区与击退。
sealed class IndexAOEs(BossModule module) : ReplayValidatedCastAOEs(module)
{
    private static readonly AOEShapeCircle Harp = new(15f);
    private static readonly AOEShapeCircle Bow = new(11f);
    private static readonly AOEShapeCone Iainuki = new(30f, 30f.Degrees());
    private static readonly AOEShapeCircle Starfall = new(10f);
    private static readonly AOEShapeDonut Cleansing = new(5f, 15f);
    private static readonly AOEShapeRect WeaponRect = new(7.5f, 7.5f, 7.5f);

    protected override AOEConfig? ConfigFor(uint actionID) => actionID switch
    {
        (uint)AID.RomeosBallad => new(Harp),
        (uint)AID.Aim => new(Bow),
        (uint)AID.Iainuki or (uint)AID.WindSlash => new(Iainuki),
        (uint)AID.Starfall => new(Starfall),
        (uint)AID.Cleansing => new(Cleansing),
        (uint)AID.UnknownWeaponskill2 => new(WeaponRect),
        _ => null
    };
}

// Fire/Ice/Thunder sector EventObjects define one 60-degree sector and its opposite.
// Balls rotate clockwise to their element (7s base + 1s/30 degrees); ARR places ring hits 6.8s after spawn.
sealed class ElementalSectors(BossModule module) : Components.GenericAOEs(module)
{
    private enum Element { Fire, Ice, Thunder }
    private enum Mechanic { Ball, Ring }
    private sealed record Pending(Element Element, Mechanic Mechanic, ulong SourceID, DateTime Activation, AOEInstance First, AOEInstance Second);

    private static readonly AOEShapeCone Shape = new(30f, 30f.Degrees());
    private readonly Dictionary<Element, Angle> _sectorRotations = [];
    private readonly List<Pending> _pending = [with(12)];
    private readonly List<AOEInstance> _displayed = [with(24)];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        Prune();
        _displayed.Clear();
        var ordered = _pending.OrderBy(p => p.Activation).ToArray();
        var riskyDeadline = ordered.Length > 0 ? ordered[0].Activation.AddSeconds(0.5d) : DateTime.MinValue;
        foreach (var p in ordered)
        {
            var risky = p.Activation <= riskyDeadline;
            var first = p.First;
            var second = p.Second;
            first.Risky = second.Risky = risky;
            first.Color = second.Color = risky ? Colors.Danger : Colors.AOE;
            _displayed.Add(first);
            _displayed.Add(second);
        }
        return CollectionsMarshal.AsSpan(_displayed);
    }

    public override void OnActorCreated(Actor actor)
    {
        if (SectorElement(actor.OID) is { } sector)
        {
            _sectorRotations[sector] = actor.Rotation;
            return;
        }

        if (RingElement(actor.OID) is { } ring)
        {
            Schedule(ring, Mechanic.Ring, actor.InstanceID, WorldState.FutureTime(7.0d));
            return;
        }

        if (BallElement(actor.OID) is not { } ball || !_sectorRotations.TryGetValue(ball, out var destination))
            return;

        var source = Angle.FromDirection(actor.Position - Module.Arena.Center);
        var delta = (source - destination).Normalized().Rad;
        if (delta < 0f)
            delta += MathF.PI;
        if (delta >= MathF.PI)
            delta -= MathF.PI;
        Schedule(ball, Mechanic.Ball, actor.InstanceID, WorldState.FutureTime(7d + delta * Angle.RadToDeg / 30f));
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        var element = spell.Action.ID switch
        {
            (uint)AID.FireIV => Element.Fire,
            (uint)AID.BlizzardIV => Element.Ice,
            (uint)AID.ThunderIV => Element.Thunder,
            _ => (Element?)null
        };
        if (element is not { } e)
            return;

        var now = WorldState.CurrentTime;
        var next = _pending.Where(p => p.Element == e && p.Activation <= now.AddSeconds(1d)).MinBy(p => p.Activation);
        if (next != null)
            _pending.Remove(next);
    }

    public override void Update() => Prune();

    private void Schedule(Element element, Mechanic mechanic, ulong sourceID, DateTime activation)
    {
        if (!_sectorRotations.TryGetValue(element, out var rotation))
            return;
        if (_pending.Any(p => p.Element == element && p.Mechanic == mechanic && Math.Abs((p.Activation - activation).TotalSeconds) < 0.5d))
            return;
        _pending.RemoveAll(p => p.SourceID == sourceID);
        _pending.Add(new(element, mechanic, sourceID, activation,
            new(Shape, Module.Arena.Center, rotation, activation, actorID: sourceID),
            new(Shape, Module.Arena.Center, rotation + 180f.Degrees(), activation, actorID: sourceID)));
    }

    private void Prune() => _pending.RemoveAll(p => WorldState.CurrentTime > p.Activation.AddSeconds(1d));

    private static Element? SectorElement(uint oid) => oid switch
    {
        (uint)OID.FireSector => Element.Fire,
        (uint)OID.IceSector => Element.Ice,
        (uint)OID.ThunderSector => Element.Thunder,
        _ => null
    };

    private static Element? RingElement(uint oid) => oid switch
    {
        (uint)OID.FireRing => Element.Fire,
        (uint)OID.IceRing => Element.Ice,
        (uint)OID.ThunderRing => Element.Thunder,
        _ => null
    };

    private static Element? BallElement(uint oid) => oid switch
    {
        (uint)OID.BallOfFire => Element.Fire,
        (uint)OID.SwirlingOrb => Element.Ice,
        (uint)OID.BallOfLevin => Element.Thunder,
        _ => null
    };
}

// ARR has two helper casts per lance; deduplicate them by location so AI receives three real sources.
sealed class PropulsiveShockwave(BossModule module) : Components.GenericKnockback(module)
{
    private static readonly AOEShapeCircle AffectedArea = new(15f);
    private readonly List<Knockback> _sources = [with(3)];

    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor) => CollectionsMarshal.AsSpan(_sources);

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != (uint)AID.Shockwave || spell.EventHappened)
            return;
        var activation = Module.CastFinishAt(spell);
        if (_sources.Any(s => (s.Origin - caster.Position).LengthSq() < 1f && Math.Abs((s.Activation - activation).TotalSeconds) < 1d))
            return;
        _sources.Add(new(caster.Position, 10f, activation, AffectedArea));
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == (uint)AID.Shockwave)
            _sources.Clear();
    }

    public override void Update()
    {
        _sources.RemoveAll(s => WorldState.CurrentTime > s.Activation.AddSeconds(1d));
        base.Update();
    }
}

sealed class AllConsumingFlames(BossModule module) : Components.SpreadFromIcon(module,
    (uint)IconID.Icon_loc06sp_05ak1, (uint)AID.AllConsumingFlames, 6f, 5.1d);

[ModuleInfo(BossModuleInfo.Maturity.Contributed,
    Contributors = "KanoNoUta",
    PrimaryActorOID = (uint)OID.Index,
    GroupType = BossModuleInfo.GroupType.TheForkedTowerMagic,
    GroupID = 1017u,
    NameID = 0u,
    SortOrder = 4,
    Category = BossModuleInfo.Category.Foray,
    Expansion = BossModuleInfo.Expansion.Dawntrail)]
public sealed class Index : BossModule
{
    public Index(WorldState ws, Actor primary) : base(ws, primary, new(0f, -628f), new ArenaBoundsSquare(25f))
        => Service.Logger.Information($"[FT] {GetType().Name} created (oid={primary.OID:X})");

    protected override void DrawEnemies(int pcSlot, Actor pc) => Arena.Actor(PrimaryActor, allowDeadAndUntargetable: true);
}
