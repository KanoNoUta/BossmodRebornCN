namespace BossMod.Dawntrail.Foray.Crucible;

sealed class CrucibleMedusaAdds(BossModule module) : BossComponent(module)
{
    public static bool IsLivingAdd(Actor actor) => actor.OID is 19706u or 19707u
        && !actor.IsDeadOrDestroyed && actor.IsTargetable && actor.HPMP.CurHP > 0;

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        var adds = WorldState.Actors.Where(IsLivingAdd).ToArray();
        foreach (var add in adds)
            hints.SetPriority(add, 10);
        if (adds.Length > 0)
            hints.ForcedTarget = adds.FirstOrDefault(a => a.InstanceID == actor.TargetID)
                ?? adds.MinBy(a => (a.Position - actor.Position).LengthSq());
        else if (Module.Enemies(19705u).FirstOrDefault(a => !a.IsDeadOrDestroyed && a.IsTargetable && a.HPMP.CurHP > 0) is { } boss)
            hints.ForcedTarget = boss;
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        if (WorldState.Actors.Any(IsLivingAdd))
            hints.Add("优先击杀美杜莎小怪，清完切回Boss！", false);
    }
}

// ARR 20:51:08: tether1/icon23 precede 49277/49278 by 5.8s. The helper
// rotation is fixed at cast start, leaving 3s to dodge. 49278 applies 2550
// to Lamia and 437 to Cyclops; aiming is useful only before that snapshot.
sealed class CrucibleMedusaRay(BossModule module) : Components.GenericBaitAway(module)
{
    private static readonly AOEShapeCone Ray = new(60, 22.5f.Degrees());
    private Actor? _add;

    public override void OnTethered(Actor source, in ActorTetherInfo tether)
    {
        if (source.OID != 19705 || tether.ID != 1 || source.CastInfo?.Action.ID is 49277 or 49278 or 49279 or 49281
            || WorldState.Actors.Find(tether.Target) is not { } target)
            return;
        if (CurrentBaits.Any(b => b.Source == source && b.Target == target))
            return;
        CurrentBaits.Clear();
        CurrentBaits.Add(new(source, target, Ray, WorldState.FutureTime(8.8)));
        _add = null;
    }

    public override void OnUntethered(Actor source, in ActorTetherInfo tether)
    {
        CurrentBaits.RemoveAll(b => b.Source == source);
        if (CurrentBaits.Count == 0)
            _add = null;
    }

    public override void OnCastStarted(Actor caster, ActorCastInfo spell)
    {
        if (spell.Action.ID is 49277 or 49278 or 49279 or 49281)
        {
            CurrentBaits.Clear();
            _add = null;
        }
    }

    public override void Update()
    {
        // Do not keep trying to bait if a lock/untether packet was lost.
        CurrentBaits.RemoveAll(b => b.Source.IsDeadOrDestroyed || b.Target.IsDeadOrDestroyed
            || b.Activation.AddSeconds(-2.3) < WorldState.CurrentTime);
        if (CurrentBaits.Count == 0)
            _add = null;
    }

    private WPos? Aim(Actor player, AIHints hints)
    {
        if (!IsBaitTarget(player))
            return null;
        var boss = CurrentBaits.First(b => b.Target == player).Source;
        if (_add == null || !CrucibleMedusaAdds.IsLivingAdd(_add))
            _add = WorldState.Actors.Where(CrucibleMedusaAdds.IsLivingAdd)
                .OrderBy(a => a == hints.ForcedTarget ? 0 : 1)
                .ThenBy(a => (a.Position - player.Position).LengthSq()).FirstOrDefault();
        if (_add == null)
            return null;
        var offset = _add.Position - boss.Position;
        var distance = offset.Length();
        if (distance < 1)
            return null; // No reliable ray direction when the actors overlap.
        var direction = offset / distance;
        // Stand on the boss-facing side of the add: the ray still hits it,
        // while the shorter sidestep after lock leaves enough time to dodge.
        var point = boss.Position + direction * MathF.Max(2, distance - _add.HitboxRadius - 2);
        return Arena.InBounds(point) ? point : null;
    }

    public override void AddAIHints(int slot, Actor actor, PartyRolesConfig.Assignment assignment, AIHints hints)
    {
        base.AddAIHints(slot, actor, assignment, hints);
        if (Aim(actor, hints) is { } point)
        {
            // A goal, not a mandatory zone: existing hazards must stay avoidable.
            hints.GoalZones.Add(p => MathF.Max(0, 100 - 5 * (p - point).Length()));
            hints.MaxCastTime = 0;
        }
    }

    public override void AddHints(int slot, Actor actor, TextHints hints)
    {
        base.AddHints(slot, actor, hints);
        if (IsBaitTarget(actor) && WorldState.Actors.Any(CrucibleMedusaAdds.IsLivingAdd))
            hints.Add("射线引导命中小怪，读条锁定后躲开！", false);
    }
}
