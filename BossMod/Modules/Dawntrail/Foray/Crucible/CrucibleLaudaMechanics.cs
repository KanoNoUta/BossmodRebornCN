namespace BossMod.Dawntrail.Foray.Crucible;

// R40 explosions cover the giant's arena. They are damage checks, not safe-position puzzles.
sealed class CrucibleGiantExplosionHint(BossModule module) : Components.CastHints(module, [49373u, 49374u, 49375u, 49376u], "凝胶炸裂：全屏伤害，准备减伤！");

// ARR 22:48:06: timeline 4562 precedes the hit by 15s, 4576 by 8s.
// Four waves are 2s apart. The final helper cast alone gives only 1s of warning.
sealed class CrucibleLaudaBlades(BossModule module) : Components.GenericAOEs(module, 49483u)
{
    private static readonly AOEShapeRect Shape = new(50, 2.5f);
    private readonly List<AOEInstance> _pending = [];

    public override ReadOnlySpan<AOEInstance> ActiveAOEs(int slot, Actor actor)
    {
        _pending.RemoveAll(a => a.Activation.AddSeconds(2) < WorldState.CurrentTime);
        _pending.Sort((a, b) => a.Activation.CompareTo(b.Activation));
        // Feeding all four waves to navigation would falsely cover the whole floor.
        var horizon = _pending.Count > 0 ? _pending[0].Activation.AddSeconds(2.2) : default;
        var entries = CollectionsMarshal.AsSpan(_pending);
        for (var i = 0; i < entries.Length; ++i)
        {
            entries[i].Risky = entries[i].Activation <= horizon;
            entries[i].Color = entries[i].Activation <= horizon.AddSeconds(-1.7) ? Colors.Danger : Colors.AOE;
        }
        return entries;
    }

    public override void OnActorPlayActionTimelineEvent(Actor actor, ushort id)
    {
        if (actor.OID != 19745 || id is not (4562 or 4576) || actor.IsDeadOrDestroyed)
            return;
        // The first cue is not allowed to restart a cue already in progress.
        if (id == 4562 && _pending.Any(a => a.ActorID == actor.InstanceID))
            return;
        var offset = actor.Position - Module.Center;
        var direction = Math.Abs(offset.X) / 15f >= Math.Abs(offset.Z) / 25f
            ? new WDir(-Math.Sign(offset.X), 0) : new WDir(0, -Math.Sign(offset.Z));
        Replace(actor.InstanceID, actor.Position, Angle.FromDirection(direction), WorldState.FutureTime(id == 4562 ? 15 : 8));
    }

    private void Replace(ulong actorID, WPos origin, Angle rotation, DateTime activation)
    {
        _pending.RemoveAll(a => a.ActorID == actorID || a.Origin.AlmostEqual(origin, 0.25f));
        _pending.Add(new(Shape, origin, rotation, activation, actorID: actorID));
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction && !spell.EventHappened && Module.CastFinishAt(spell) > WorldState.CurrentTime)
            Replace(caster.InstanceID, spell.LocXZ, spell.Rotation, Module.CastFinishAt(spell));
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction)
            _pending.RemoveAll(a => a.ActorID == caster.InstanceID);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == WatchedAction)
        {
            ++NumCasts;
            _pending.RemoveAll(a => a.ActorID == caster.InstanceID || a.Origin.AlmostEqual(caster.Position, 0.25f));
        }
    }

    public override void OnActorDestroyed(Actor actor) => _pending.RemoveAll(a => a.ActorID == actor.InstanceID);
    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);
}

// Knockback row 234: 20y to source-right, confirmed by the recorded northward landing.
// Its 80x80 hit is unavoidable: constrain the landing instead of forbidding the whole floor.
sealed class CrucibleLaudaTorrent(BossModule module)
    : Components.SimpleKnockbacks(module, 49453u, 20, shape: new AOEShapeRect(80, 40), kind: Kind.DirRight)
{
    public override ReadOnlySpan<Knockback> ActiveKnockbacks(int slot, Actor actor)
    {
        Casters.RemoveAll(k => k.Activation.AddSeconds(2) < WorldState.CurrentTime);
        return base.ActiveKnockbacks(slot, actor);
    }

    public override bool DestinationUnsafe(int slot, Actor actor, WPos pos)
        => !CrucibleLaudaFloor.Contains(pos);

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        foreach (var k in ActiveKnockbacks(slot, actor))
            if (!IsImmune(slot, k.Activation))
            {
                var displacement = (k.Direction - 90f.Degrees()).ToDirection() * k.Distance;
                hints.AddForbiddenZone(new CrucibleLaudaFloor.LandingZone(displacement), k.Activation.AddSeconds(-0.5));
            }
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != WatchedAction || spell.EventHappened || Module.CastFinishAt(spell) <= WorldState.CurrentTime)
            return;
        Casters.RemoveAll(k => k.ActorID == caster.InstanceID);
        base.OnCastStarted(caster, spell);
    }

    public override void OnEventCast(Actor caster, ActorCastEvent spell)
    {
        if (spell.Action.ID == WatchedAction)
            Casters.RemoveAll(k => k.ActorID == caster.InstanceID);
        base.OnEventCast(caster, spell);
    }

    public override void OnActorDestroyed(Actor actor) => Casters.RemoveAll(k => k.ActorID == actor.InstanceID);
    public override void OnActorDeath(Actor actor) => OnActorDestroyed(actor);
}

// Thunder follows its cast target all the way through the hit (Icon471). The
// marked player cannot sidestep their own line; do not create a self-chasing zone.
sealed class CrucibleLaudaThunder(BossModule module) : Components.BaitAwayCast(module, 49470u, new AOEShapeRect(50, 3), endsOnCastEvent: true, tankbuster: true)
{
    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID != WatchedAction || spell.EventHappened || Module.CastFinishAt(spell) <= WorldState.CurrentTime)
            return;
        CurrentBaits.RemoveAll(b => b.Source == caster);
        base.OnCastStarted(caster, spell);
    }

    public override void OnCastFinished(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID == WatchedAction && !spell.EventHappened && spell.NPCRemainingTime > 0.5f)
            CurrentBaits.RemoveAll(b => b.Source == caster);
    }

    public override void Update() => CurrentBaits.RemoveAll(b => b.Source.IsDeadOrDestroyed || b.Target.IsDeadOrDestroyed || b.Activation.AddSeconds(2) < WorldState.CurrentTime);
}

// Three Tether1 sources aim at the player, then lock into 49464/50848 ground lines.
// Use baits while aiming and the actual helper cast once locked.
sealed class CrucibleLaudaBaits(BossModule module) : Components.GenericBaitAway(module)
{
    private static readonly WPos BaitPoint = new(520, -398);

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        base.AddAIHints(slot, actor, assignment, hints);
        if (!IsBaitTarget(actor))
            return;
        hints.GoalZones.Add(p => p.InCircle(BaitPoint, 1) ? 100 : 0);
        hints.AddForbiddenZone(new SDInvertedCircle(BaitPoint, 1.2f), CurrentBaits.Min(b => b.Activation).AddSeconds(-0.7));
        hints.MaxCastTime = 0;
    }

    public override void OnTethered(Actor source, in ActorTetherInfo tether)
    {
        if (tether.ID != 1 || source.OID is not (19742 or 19747) || WorldState.Actors.Find(tether.Target) is not { } target)
            return;
        CurrentBaits.RemoveAll(b => b.Source == source);
        CurrentBaits.Add(new(source, target, new AOEShapeRect(60, 3), WorldState.FutureTime(7.1)));
    }

    public override void OnUntethered(Actor source, in ActorTetherInfo tether) => CurrentBaits.RemoveAll(b => b.Source == source);

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is 49464 or 50848)
            CurrentBaits.RemoveAll(b => b.Source.Position.AlmostEqual(caster.Position, 0.5f));
    }

    public override void Update() => CurrentBaits.RemoveAll(b => b.Source.IsDeadOrDestroyed || b.Target.IsDeadOrDestroyed || b.Activation.AddSeconds(1) < WorldState.CurrentTime);

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        base.AddHints(slot, actor, hints);
        if (IsBaitTarget(actor))
            hints.Add("三剑引导到南侧中央凸台，锁定后躲开，避免击中石像！", false);
    }
}

sealed class CrucibleLaudaLaterHint(BossModule module) : Components.CastHints(module, [49473u, 49477u], "后续战斧：Boss落地朝Boss方向击退，跳起朝对侧击退，注意电网！");

sealed class CrucibleLaudaAddRaidwide(BossModule module) : Components.CastHints(module, [50541u], "地狱之痛：全屏伤害，准备减伤！");
