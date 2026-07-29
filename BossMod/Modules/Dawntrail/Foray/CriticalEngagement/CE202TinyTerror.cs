namespace BossMod.Dawntrail.Foray.CriticalEngagement.CE202TinyTerror;

public enum OID : uint {
    TinyMage = 0x4C6D,
    Helper = 0x233C,
    TinyMageHelper = 0x4D55, // R1.000, x1
    TinyApprentice = 0x4C6E, // R1.000, x0 (spawn during fight)
    ArcaneSphereSmall = 0x4C74, // R1.000, x0 (spawn during fight)
    ArcaneSphereBig = 0x4C73, // R1.000, x0 (spawn during fight)
    FlareSphereGrow = 0x4C6F, // R0.700-1.904, x0 (spawn during fight)
    FlareSphere = 0x4C70, // R1.400, x0 (spawn during fight)
    HolySphere1Grow = 0x4C71, // R0.700-1.904, x0 (spawn during fight)
    HolySphere = 0x4C72, // R1.400, x0 (spawn during fight)
    _Gen_Actor1ea1a1 = 0x1EA1A1, // R2.000, x2, EventObj type
    _Gen_Actor1ec099 = 0x1EC099, // R0.500, x1, EventObj type
    _Gen_ = 0x4EBB, // R1.750, x0 (spawn during fight)
}

public enum AID : uint {
    AutoAttack = 48305, // TinyMage->player, no cast, single-target
    TinyWarp = 48331, // TinyMage->location, no cast, single-target
    TinyThunderIIIRaidwide = 48329, // TinyMage->self, 5.0s cast, single-target
    TinyThunderIII = 48330, // Helper->self, no cast, ???

    TinyQuakeIII = 48322, // TinyMage->self, 3.5+0.5s cast, single-target
    TinyQuakeIIIInner = 48323, // Helper->self, 4.0s cast, range 10 circle
    TinyQuakeIIIMiddle = 48324, // Helper->self, 4.0s cast, range 10-20 donut
    TinyQuakeIIIOuter = 48325, // Helper->self, 4.0s cast, range 20-30 donut

    DiminutiveDualcast = 48317, // TinyMage->self, 5.5+0.5s cast, single-target
    TinyBlizzardIII = 48319, // Helper->self, 6.0s cast, range 40 60.000-degree cone
    TinyFireIII = 48318, // Helper->self, 6.0s cast, range 14 circle

    TinyMeteorCast = 48320, // TinyMage->self, 5.0s cast, single-target
    TinyMeteor = 48321, // Helper->location, 4.0s cast, range 6 circle

    Comet = 48327, // 4C74->self, 60.0s cast, range 60 circle
    Comet1 = 49061, // Helper->self, no cast, ???
    Meteor = 48326, // 4C73->self, 130.0s cast, single-target

    SmallForOne = 48306, // TinyMage->self, 3.0s cast, single-target - Spawns actors in

    TinyFlare = 48313, // 4C6F/4C70->self, no cast, single-target
    TinyFlare1 = 48311, // Helper->self, 2.0s cast, range 18 circle
    TinyHoly = 48314, // 4C72/4C71->self, no cast, single-target
    TinyHoly1 = 48312, // Helper->self, 2.0s cast, range 50 circle
    TinyHoly2 = 49058, // Helper->self, no cast, ???

    _Spell_Recharge = 48309, // 4C6E->self, 1.5s cast, single-target
    _Spell_Recharge1 = 48310, // 4C6E->self, 1.5s cast, single-target
    _Ability_Recharge = 49059, // 4C6E/TinyMage->self, no cast, single-target

    _Ability_ = 49057, // 4D55->self, no cast, range ?-25 donut
    _Ability_1 = 50530, // 4C6E->self, no cast, single-target
    _Spell_ = 50638, // 4C6E->self, no cast, single-target

    _Spell_ArcaneAggregation = 48307, // 4C6E->self, 3.0s cast, single-target
    _Spell_ArcaneAggregation1 = 49718, // 4C6E->self, 5.5s cast, single-target
    _Spell_ArcaneAggregation2 = 49719, // 4C6E->self, 5.5s cast, single-target
    _Spell_ArcaneAggregation3 = 48308, // 4C6E->self, 3.0s cast, single-target

    _Ability_AllForOne = 50762, // TinyMage->self, 3.0s cast, single-target
}

public enum SID : uint {
    _Gen_1 = 2552, // none->4C6E, extra=0x198
    _Gen_2 = 3445, // none->4C74/4C73, extra=0x15/0xA/0x1E
}

public enum TetherID : uint {
    OrbPairs = 415, // 4C72/4C70->4C72/4C70 - change name its when the orbs fire / white merge together
    _Gen_Tether_chn_m0012af = 60, // 4C74->4EBB
    CometTethers = 422, // 4C6E/TinyMage->4C74/4EBB
}

sealed class TinyThunderIII(BossModule module) : Components.RaidwideCast(module, (uint)AID.TinyThunderIIIRaidwide);

sealed class TinyQuake(BossModule module) : Components.GenericAOEs(module) {
    private readonly List<AOEInstance> aoes = [];

    public override void OnCastStarted(Actor caster, ActorCastInfo spell) {
        if (spell.Action.ID == (uint)AID.TinyQuakeIIIInner) {
            aoes.Add(new(new AOEShapeCircle(10.0f), caster.Position, caster.Rotation, Module.CastFinishAt(spell)));
        }

        if (spell.Action.ID == (uint)AID.TinyQuakeIIIMiddle) {
            aoes.Add(new(new AOEShapeDonut(10.0f, 20.0f), caster.Position, caster.Rotation, Module.CastFinishAt(spell)));
        }

        if (spell.Action.ID == (uint)AID.TinyQuakeIIIOuter) {
            aoes.Add(new(new AOEShapeDonut(20.0f, 30.0f), caster.Position, caster.Rotation, Module.CastFinishAt(spell)));
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell) {
        if (spell.Action.ID is (uint)AID.TinyQuakeIIIInner or (uint)AID.TinyQuakeIIIMiddle or (uint)AID.TinyQuakeIIIOuter) {
            aoes.Sort((a, b) => a.Activation.CompareTo(b.Activation));
            if (aoes.Count > 0) {
                aoes.RemoveAt(0);
            }
        }
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) {
        int show = 0;
        var currentAOEs = aoes.OrderBy(a => a.Activation).Take(2).ToList();

        foreach (ref var aoe in CollectionsMarshal.AsSpan(currentAOEs)) {
            aoe.Color = show == 0 ? Colors.Danger : Colors.AOE;
            aoe.Risky = show == 0;
            show++;
        }

        return CollectionsMarshal.AsSpan(currentAOEs);
    }
}

sealed class DiminutiveDualcast(BossModule module) : Components.GenericAOEs(module) {
    private readonly List<AOEInstance> aoes = [];

    public override void OnCastStarted(Actor caster, ActorCastInfo spell) {
        if (spell.Action.ID == (uint)AID.TinyBlizzardIII) {
            aoes.Add(new(new AOEShapeCone(40.0f, 30.0f.Degrees()), caster.Position, caster.Rotation, Module.CastFinishAt(spell)));
        }

        if (spell.Action.ID == (uint)AID.TinyFireIII) {
            aoes.Add(new(new AOEShapeCircle(14.0f), caster.Position, caster.Rotation, Module.CastFinishAt(spell)));
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell) {
        if (spell.Action.ID is (uint)AID.TinyBlizzardIII or (uint)AID.TinyFireIII) {
            aoes.Sort((a, b) => a.Activation.CompareTo(b.Activation));
            if (aoes.Count > 0) {
                aoes.RemoveAt(0);
            }
        }
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) {
        var currentAOEs = aoes.OrderBy(a => a.Activation).Take(4).ToList();
        if (currentAOEs.Count == 0) {
            return [];
        }

        var waveTimer = currentAOEs[0].Activation.AddSeconds(0.2f);

        foreach (ref var aoe in CollectionsMarshal.AsSpan(currentAOEs)) {
            var imminent = aoe.Activation <= waveTimer;
            aoe.Color = imminent ? Colors.Danger : Colors.AOE;
            aoe.Risky = imminent;
        }

        return CollectionsMarshal.AsSpan(currentAOEs);
    }
}

sealed class TinyMeteor(BossModule module) : Components.GenericAOEs(module, (uint)AID.TinyMeteor) {
    private readonly List<AOEInstance> aoes = [];

    public override void OnCastStarted(Actor caster, ActorCastInfo spell) {
        if (spell.Action.ID == (uint)AID.TinyMeteor) {
            aoes.Add(new(new AOEShapeCircle(6.0f), spell.LocXZ, spell.Rotation, Module.CastFinishAt(spell)));
            aoes.Sort((a, b) => a.Activation.CompareTo(b.Activation));
        }
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell) {
        if (spell.Action.ID == (uint)AID.TinyMeteor) {
            ++NumCasts;
            aoes.RemoveAll(aoe => aoe.Origin.AlmostEqual(spell.TargetXZ, 0.5f));
        }
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) {
        if (aoes.Count == 0) {
            return [];
        }

        var waveTimer = aoes[0].Activation.AddSeconds(0.2f);

        foreach (ref var aoe in CollectionsMarshal.AsSpan(aoes)) {
            var imminent = aoe.Activation <= waveTimer;
            aoe.Color = imminent ? Colors.Danger : Colors.AOE;
            aoe.Risky = imminent;
        }

        return CollectionsMarshal.AsSpan(aoes);
    }
}

// TODO add timers - its not 60 seconds since it goes faster depending on the number of actors around it
// TODO _Gen_2 = 3445, // none->4C74/4C73, extra=0x15/0xA/0x1E
sealed class Comet(BossModule module) : BossComponent(module) {
    private readonly Dictionary<ulong, CometActor> comets = [];
    private readonly HashSet<TetherLink> activeTethers = [];

    private sealed class CometActor(Actor actor) {
        public readonly Actor Actor = actor;
        public int Tethers;
    }

    private readonly record struct TetherLink(ulong First, ulong Second);

    private static TetherLink MakeLink(ulong first, ulong second) {
        return first < second ? new(first, second) : new(second, first);
    }

    public override void OnActorCreated(Actor actor) {
        if (actor.OID == (uint)OID.ArcaneSphereSmall) {
            if (comets.Count == 0) {
                activeTethers.Clear();
            }
            comets.TryAdd(actor.InstanceID, new(actor));
        }
    }

    public override void OnActorDeath(Actor actor) {
        RemoveActor(actor.InstanceID);
    }

    public override void OnActorDestroyed(Actor actor) {
        RemoveActor(actor.InstanceID);
    }

    public override void OnTethered(Actor source, in ActorTetherInfo tether) {
        if (tether.ID != (uint)TetherID.CometTethers) {
            return;
        }

        var comet = FindComet(source.InstanceID, tether.Target);
        var link = MakeLink(source.InstanceID, tether.Target);
        if (comet != null && activeTethers.Add(link)) {
            ++comet.Tethers;
        }
    }

    public override void OnUntethered(Actor source, in ActorTetherInfo tether) {
        if (tether.ID != (uint)TetherID.CometTethers) {
            return;
        }

        var link = MakeLink(source.InstanceID, tether.Target);
        if (activeTethers.Remove(link)) {
            var comet = FindComet(source.InstanceID, tether.Target);
            if (comet != null) {
                comet.Tethers = Math.Max(0, comet.Tethers - 1);
            }
        }
    }

    public override void DrawArenaForeground(int pcSlot, Actor pc) {
        var maxTethers = MaxTetherCount();
        if (maxTethers <= 0) {
            return;
        }

        foreach (var comet in comets.Values) {
            if (comet.Tethers == maxTethers) {
                Arena.AddCircle(comet.Actor.Position, 2.0f, Colors.Safe, 2.0f);
            }
        }
    }

    public override void AddHints(int slot, Actor actor, TextHints hints) {
        if (MaxTetherCount() > 0) {
            hints.Add("Attack a comet with a green circle around it!");
        }
    }

    private CometActor? FindComet(ulong first, ulong second) {
        if (comets.TryGetValue(first, out var comet)) {
            return comet;
        }
        return comets.GetValueOrDefault(second);
    }

    private int MaxTetherCount() {
        var max = 0;
        foreach (var comet in comets.Values) {
            max = Math.Max(max, comet.Tethers);
        }
        return max;
    }

    private void RemoveActor(ulong instanceID) {
        activeTethers.RemoveWhere(link => {
            if (link.First != instanceID && link.Second != instanceID) {
                return false;
            }

            var other = link.First == instanceID ? link.Second : link.First;
            if (comets.TryGetValue(other, out var comet)) {
                comet.Tethers = Math.Max(0, comet.Tethers - 1);
            }
            return true;
        });
        comets.Remove(instanceID);
        if (comets.Count == 0) {
            activeTethers.Clear();
        }
    }
}

static class TinyMageMechanic {
    private const float DirectionThreshold = 0.05f;

    public static void AddMage(List<Actor> mages, Actor actor, WPos center) {
        if (mages.Any(mage => mage.InstanceID == actor.InstanceID)) {
            return;
        }

        // Small for One normally resets the wave; this is a fallback for a missed cast-start event.
        if (mages.Count >= 4) {
            mages.Clear();
        }

        mages.Add(actor);
        mages.Sort((left, right) => {
            var result = ClockwiseFromNorth(left.Position, center).CompareTo(ClockwiseFromNorth(right.Position, center));
            return result != 0 ? result : left.InstanceID.CompareTo(right.InstanceID);
        });
    }

    public static void RemoveMage(List<Actor> mages, ulong instanceID) {
        mages.RemoveAll(mage => mage.InstanceID == instanceID);
    }

    public static int ObserveDirection(Actor orb, WPos center, ref float previousAngle, ref float angularTravel) {
        var currentAngle = ClockwiseFromNorth(orb.Position, center);
        var delta = currentAngle - previousAngle;
        if (delta > MathF.PI) {
            delta -= Angle.DoublePI;
        } else if (delta < -MathF.PI) {
            delta += Angle.DoublePI;
        }

        previousAngle = currentAngle;
        angularTravel += delta;
        return angularTravel > DirectionThreshold ? 1 : angularTravel < -DirectionThreshold ? -1 : 0;
    }

    public static float AngleFromNorth(WPos position, WPos center) => ClockwiseFromNorth(position, center);

    private static float ClockwiseFromNorth(WPos position, WPos center) {
        var angle = (position - center).ToAngle().Normalized().Rad;
        var result = (MathF.PI - angle) % Angle.DoublePI;
        return result < 0.0f ? result + Angle.DoublePI : result;
    }
}

sealed class FlareGrowable(BossModule module) : Components.GenericAOEs(module) {
    private readonly List<Actor> mages = [];
    private Actor? orb;
    private WPos? start;
    private ulong startActorID;
    private float previousAngle;
    private float angularTravel;
    private int direction;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell) {
        if (spell.Action.ID == (uint)AID.SmallForOne) {
            ResetWave();
        }
    }

    public override void OnActorCreated(Actor actor) {
        if (actor.OID == (uint)OID.TinyApprentice) {
            TinyMageMechanic.AddMage(mages, actor, Arena.Center);
        } else if (actor.OID == (uint)OID.FlareSphereGrow) {
            orb = actor;
            start = actor.Position;
            startActorID = actor.InstanceID;
            previousAngle = TinyMageMechanic.AngleFromNorth(actor.Position, Arena.Center);
            angularTravel = default;
            direction = default;
        }
    }

    public override void Update() {
        if (orb != null && direction == 0) {
            direction = TinyMageMechanic.ObserveDirection(orb, Arena.Center, ref previousAngle, ref angularTravel);
        }
    }

    public override void OnActorDeath(Actor actor) {
        RemoveActor(actor);
    }

    public override void OnActorDestroyed(Actor actor) {
        RemoveActor(actor);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell) {
        if (spell.Action.ID == (uint)AID.TinyFlare1) {
            ClearStart();
        }
    }

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) {
        if (mages.Count != 4 || start == null || direction == 0) {
            return [];
        }

        var startAOE = mages.FindIndex(mage => mage.Position.AlmostEqual(start.Value, 0.5f));
        if (startAOE < 0) {
            return [];
        }

        var targetActor = mages[(startAOE + mages.Count - direction) % mages.Count];
        return new AOEInstance[1] { new(new AOEShapeCircle(18.0f), targetActor.Position) };
    }

    private void RemoveActor(Actor actor) {
        TinyMageMechanic.RemoveMage(mages, actor.InstanceID);
        if (actor.InstanceID == startActorID) {
            ClearStart();
        }
    }

    private void ClearStart() {
        orb = null;
        start = null;
        startActorID = default;
        previousAngle = default;
        angularTravel = default;
        direction = default;
    }

    private void ResetWave() {
        mages.Clear();
        ClearStart();
    }
}

sealed class HolyGrowable(BossModule module) : Components.GenericKnockback(module) {
    private readonly List<Actor> mages = [];
    private Actor? orb;
    private WPos? start;
    private ulong startActorID;
    private float previousAngle;
    private float angularTravel;
    private int direction;

    public override void OnCastStarted(Actor caster, ActorCastInfo spell) {
        if (spell.Action.ID == (uint)AID.SmallForOne) {
            ResetWave();
        }
    }

    public override void OnActorCreated(Actor actor) {
        if (actor.OID == (uint)OID.TinyApprentice) {
            TinyMageMechanic.AddMage(mages, actor, Arena.Center);
        } else if (actor.OID == (uint)OID.HolySphere1Grow) {
            orb = actor;
            start = actor.Position;
            startActorID = actor.InstanceID;
            previousAngle = TinyMageMechanic.AngleFromNorth(actor.Position, Arena.Center);
            angularTravel = default;
            direction = default;
        }
    }

    public override void Update() {
        if (orb != null && direction == 0) {
            direction = TinyMageMechanic.ObserveDirection(orb, Arena.Center, ref previousAngle, ref angularTravel);
        }
    }

    public override void OnActorDeath(Actor actor) {
        RemoveActor(actor);
    }

    public override void OnActorDestroyed(Actor actor) {
        RemoveActor(actor);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell) {
        if (spell.Action.ID == (uint)AID.TinyHoly1) {
            ClearStart();
        }
    }

    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor) {
        if (mages.Count != 4 || start == null || direction == 0) {
            return [];
        }

        var startAOE = mages.FindIndex(mage => mage.Position.AlmostEqual(start.Value, 0.5f));
        if (startAOE < 0) {
            return [];
        }

        var targetActor = mages[(startAOE + mages.Count - direction) % mages.Count];
        return new Knockback[1] { new(targetActor.Position, 15.0f) };
    }

    private void RemoveActor(Actor actor) {
        TinyMageMechanic.RemoveMage(mages, actor.InstanceID);
        if (actor.InstanceID == startActorID) {
            ClearStart();
        }
    }

    private void ClearStart() {
        orb = null;
        start = null;
        startActorID = default;
        previousAngle = default;
        angularTravel = default;
        direction = default;
    }

    private void ResetWave() {
        mages.Clear();
        ClearStart();
    }
}

static class OrbPairMechanic {
    // Replay timing is not available yet. Both orbs travel half of their separation and the
    // resolving helper cast is 2s; keep the fallback speed isolated here for later calibration.
    private const float FallbackOrbSpeed = 6.0f;
    private const double ResolveCastTime = 2.0d;

    public readonly record struct Key(ulong First, ulong Second);

    public static Key MakeKey(ulong first, ulong second) {
        return first < second ? new(first, second) : new(second, first);
    }

    public static DateTime EstimateActivation(WorldState worldState, float separation) {
        return worldState.FutureTime(ResolveCastTime + separation * 0.5f / FallbackOrbSpeed);
    }
}

// The four pair resolutions form one mixed flare/holy sequence. Track both types in one
// timeline so a later holy does not become the active knockback while an earlier flare is
// still pending (and vice versa).
sealed class OrbPairTimeline(BossModule module) : BossComponent(module) {
    private const double ExpireDelay = 10.0d;

    public enum Kind : byte {
        Flare,
        Holy
    }

    public sealed class Entry(OrbPairMechanic.Key key, Kind kind, WPos origin, float distance, DateTime activation) {
        public readonly OrbPairMechanic.Key Key = key;
        public readonly Kind Type = kind;
        public readonly WPos Origin = origin;
        public readonly float Distance = distance;
        public DateTime Activation = activation;
        public ulong ActorID;
    }

    private readonly List<Entry> entries = [];
    private readonly HashSet<OrbPairMechanic.Key> pairs = [];
    private readonly HashSet<uint> seenGlobalSequences = [];

    public List<Entry> Upcoming(int count) {
        PruneExpired();
        return entries
            .OrderBy(entry => entry.Activation)
            .ThenBy(entry => entry.Distance)
            .ThenBy(entry => entry.Key.First)
            .Take(count)
            .ToList();
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell) {
        if (spell.Action.ID == (uint)AID._Ability_AllForOne) {
            ResetWave();
            return;
        }

        var kind = KindForAction(spell.Action.ID);
        if (kind != null) {
            UpdateActivation(kind.Value, caster, Module.CastFinishAt(spell));
        }
    }

    public override void OnTethered(Actor source, in ActorTetherInfo tether) {
        if (tether.ID != (uint)TetherID.OrbPairs) {
            return;
        }

        var target = WorldState.Actors.Find(tether.Target);
        if (target == null) {
            return;
        }

        var kind = KindForPair(source, target);
        if (kind == null) {
            return;
        }

        var key = OrbPairMechanic.MakeKey(source.InstanceID, target.InstanceID);
        if (!pairs.Add(key)) {
            return;
        }

        var distance = (target.Position - source.Position).Length();
        entries.Add(new(key, kind.Value, WPos.Lerp(source.Position, target.Position, 0.5f), distance, OrbPairMechanic.EstimateActivation(WorldState, distance)));
    }

    public override void OnActorDeath(Actor actor) => RemoveActor(actor.InstanceID);
    public override void OnActorDestroyed(Actor actor) => RemoveActor(actor.InstanceID);

    public override void OnEventCast(Actor caster, ActorCastEvent spell) {
        var kind = KindForAction(spell.Action.ID);
        if (kind == null || spell.GlobalSequence != 0 && !seenGlobalSequences.Add(spell.GlobalSequence)) {
            return;
        }

        RemoveAt(kind.Value, caster);
    }

    public override void Update() => PruneExpired();

    private static Kind? KindForAction(uint actionID) => actionID switch {
        (uint)AID.TinyFlare1 => Kind.Flare,
        (uint)AID.TinyHoly1 => Kind.Holy,
        _ => null
    };

    private static Kind? KindForPair(Actor first, Actor second) {
        if (first.OID == (uint)OID.FlareSphere && second.OID == (uint)OID.FlareSphere) {
            return Kind.Flare;
        }
        if (first.OID == (uint)OID.HolySphere && second.OID == (uint)OID.HolySphere) {
            return Kind.Holy;
        }
        return null;
    }

    private void UpdateActivation(Kind kind, Actor caster, DateTime activation) {
        foreach (var entry in entries) {
            if (entry.Type == kind && entry.Origin.AlmostEqual(caster.Position, 1.5f)) {
                entry.Activation = activation;
                entry.ActorID = caster.InstanceID;
            }
        }
    }

    private void RemoveAt(Kind kind, Actor caster) {
        for (var i = entries.Count - 1; i >= 0; --i) {
            var entry = entries[i];
            if (entry.Type == kind && (entry.ActorID == caster.InstanceID || entry.Origin.AlmostEqual(caster.Position, 1.5f))) {
                pairs.Remove(entry.Key);
                entries.RemoveAt(i);
            }
        }
    }

    private void RemoveActor(ulong instanceID) {
        for (var i = entries.Count - 1; i >= 0; --i) {
            var key = entries[i].Key;
            if (key.First == instanceID || key.Second == instanceID) {
                pairs.Remove(key);
                entries.RemoveAt(i);
            }
        }
    }

    private void ResetWave() {
        entries.Clear();
        pairs.Clear();
        seenGlobalSequences.Clear();
    }

    private void PruneExpired() {
        var now = WorldState.CurrentTime;
        for (var i = entries.Count - 1; i >= 0; --i) {
            if (now > entries[i].Activation.AddSeconds(ExpireDelay)) {
                pairs.Remove(entries[i].Key);
                entries.RemoveAt(i);
            }
        }
    }
}

sealed class FlareCombo(BossModule module) : Components.GenericAOEs(module) {
    private static readonly AOEShapeCircle Shape = new(18.0f);
    private readonly OrbPairTimeline timeline = module.FindComponent<OrbPairTimeline>()!;
    private readonly List<AOEInstance> displayed = [with(2)];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor) {
        displayed.Clear();
        var upcoming = timeline.Upcoming(2);
        if (upcoming.Count == 0) {
            return [];
        }

        var imminentDeadline = upcoming[0].Activation.AddSeconds(0.2d);
        foreach (var entry in upcoming) {
            if (entry.Type == OrbPairTimeline.Kind.Flare) {
                var imminent = entry.Activation <= imminentDeadline;
                displayed.Add(new(Shape, entry.Origin, activation: entry.Activation, color: imminent ? Colors.Danger : Colors.AOE, risky: imminent, actorID: entry.ActorID));
            }
        }
        return CollectionsMarshal.AsSpan(displayed);
    }
}

sealed class HolyCombo(BossModule module) : Components.GenericKnockback(module) {
    private readonly OrbPairTimeline timeline = module.FindComponent<OrbPairTimeline>()!;

    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor) {
        var upcoming = timeline.Upcoming(1);
        if (upcoming.Count == 0 || upcoming[0].Type != OrbPairTimeline.Kind.Holy) {
            return [];
        }

        var next = upcoming[0];
        return new Knockback[1] { new(next.Origin, 15.0f, next.Activation, actorID: next.ActorID) };
    }
}

[SkipLocalsInit]
sealed class TinyMageStates : StateMachineBuilder {
    public TinyMageStates(BossModule module) : base(module) {
        TrivialPhase()
            .ActivateOnEnter<TinyThunderIII>()
            .ActivateOnEnter<TinyQuake>()
            .ActivateOnEnter<DiminutiveDualcast>()
            .ActivateOnEnter<TinyMeteor>()
            .ActivateOnEnter<Comet>()
            .ActivateOnEnter<HolyGrowable>()
            .ActivateOnEnter<FlareGrowable>()
            .ActivateOnEnter<OrbPairTimeline>()
            .ActivateOnEnter<FlareCombo>()
            .ActivateOnEnter<HolyCombo>();
    }
}

[ModuleInfo(BossModuleInfo.Maturity.WIP,
    StatesType = typeof(TinyMageStates),
    ConfigType = null, // replace null with typeof(TinyMageConfig) if applicable
    ObjectIDType = typeof(OID),
    ActionIDType = typeof(AID),
    StatusIDType = typeof(SID),
    TetherIDType = typeof(TetherID),
    IconIDType = null, // replace null with typeof(IconID) if applicable
    PrimaryActorOID = (uint)OID.TinyMage,
    Contributors = "Equilius",
    Expansion = BossModuleInfo.Expansion.Dawntrail,
    Category = BossModuleInfo.Category.Foray,
    GroupType = BossModuleInfo.GroupType.CriticalEngagement,
    GroupID = 1093u,
    NameID = 60u,
    SortOrder = 1,
    PlanLevel = 0)]
[SkipLocalsInit]
public sealed class TinyMage(WorldState ws, Actor primary) : BossModule(ws, primary, new(152.000f, 716.000f), new ArenaBoundsCircle(20f)) {
    protected override void DrawEnemies(int pcSlot, Actor pc) {
        Arena.Actor(PrimaryActor);
        Arena.Actors(Enemies((uint)OID.ArcaneSphereSmall));
        Arena.Actors(Enemies((uint)OID.ArcaneSphereBig));
    }
}
