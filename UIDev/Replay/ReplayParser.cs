﻿using BossMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace UIDev
{
    public class ReplayParser
    {
        protected Replay _res = new();
        protected WorldState _ws = new();
        private Dictionary<uint, Replay.Encounter> _encounters = new();
        private Dictionary<uint, Replay.Participant> _participants = new();
        private Dictionary<(uint, int), Replay.Status> _statuses = new();
        private Dictionary<uint, Replay.Tether> _tethers = new();

        protected ReplayParser()
        {
            _ws.Actors.Added += ActorAdded;
            _ws.Actors.Removed += ActorRemoved;
            _ws.Actors.InCombatChanged += ActorCombat;
            _ws.Actors.IsTargetableChanged += ActorTargetable;
            _ws.Actors.IsDeadChanged += ActorDead;
            _ws.Actors.Moved += ActorMoved;
            _ws.Actors.HPChanged += ActorHP;
            _ws.Actors.CastStarted += CastStart;
            _ws.Actors.CastFinished += CastFinish;
            _ws.Actors.Tethered += TetherAdd;
            _ws.Actors.Untethered += TetherRemove;
            _ws.Actors.StatusGain += StatusGain;
            _ws.Actors.StatusLose += StatusLose;
            _ws.Actors.StatusChange += StatusChange;
            _ws.Events.Icon += EventIcon;
            _ws.Events.Cast += EventCast;
            _ws.Events.EnvControl += EventEnvControl;
        }

        protected void AddOp(DateTime timestamp, ReplayOps.Operation op)
        {
            _ws.CurrentTime = op.Timestamp = timestamp;
            op.Redo(_ws);
            _res.Ops.Add(op);
        }

        protected Replay Finish(string path = "")
        {
            //foreach (var a in _res.Actions)
            //{
            //    Service.Log($"{a.ID} {ReplayUtils.ParticipantString(a.Source)} -> {ReplayUtils.ParticipantString(a.MainTarget)}");
            //    foreach (var t in a.Targets)
            //    {
            //        Service.Log($"- {ReplayUtils.ParticipantString(t.Target)}");
            //        foreach (var e in t.Effects)
            //        {
            //            Service.Log($"-- {ReplayUtils.ActionEffectString(e)} ({ActionEffectParser.DescribeUnknown(e)})");
            //        }
            //    }
            //}

            _res.Path = path;
            foreach (var enc in _encounters.Values)
            {
                enc.Time.End = _ws.CurrentTime;
            }
            foreach (var p in _participants.Values)
            {
                p.Existence.End = _ws.CurrentTime;
                if (p.Casts.LastOrDefault()?.Time.End == new DateTime())
                    p.Casts.Last().Time.End = _ws.CurrentTime;
                if (p.TargetableHistory.LastOrDefault().Value)
                {
                    if (p.TargetableHistory.Last().Key < _ws.CurrentTime)
                        p.TargetableHistory.Add(_ws.CurrentTime, false);
                    else
                        p.TargetableHistory.RemoveAt(p.TargetableHistory.Count - 1);
                }
            }
            foreach (var s in _statuses.Values)
            {
                s.Time.End = _ws.CurrentTime;
            }
            foreach (var t in _tethers.Values)
            {
                t.Time.End = _ws.CurrentTime;
            }
            return _res;
        }

        private void StartEncounter(Actor actor)
        {
            if (_encounters.ContainsKey(actor.InstanceID))
                return;

            var m = ModuleRegistry.TypeForOID(actor.OID);
            if (m == null)
                return;

            var e = _encounters[actor.InstanceID] = new()
            {
                InstanceID = actor.InstanceID,
                OID = actor.OID,
                Time = new(_ws.CurrentTime),
                Zone = _ws.CurrentZone,
                FirstAction = _res.Actions.Count,
                FirstStatus = _res.Statuses.Count,
                FirstTether = _res.Tethers.Count,
                FirstIcon = _res.Icons.Count,
                FirstEnvControl = _res.EnvControls.Count
            };
            foreach (var p in _participants.Values)
                e.Participants.GetOrAdd(p.OID).Add(p);
            foreach (var p in _ws.Party.WithoutSlot(true))
                e.PartyMembers.Add((_participants[p.InstanceID], p.Class));
            _res.Encounters.Add(e);
        }

        private void FinishEncounter(Actor actor)
        {
            var e = _encounters.GetValueOrDefault(actor.InstanceID);
            if (e == null)
                return;

            e.Time.End = _ws.CurrentTime;
            _encounters.Remove(actor.InstanceID);
        }

        private void ActorAdded(object? sender, Actor actor)
        {
            var p = _participants[actor.InstanceID] = new() { InstanceID = actor.InstanceID, OID = actor.OID, Type = actor.Type, Name = actor.Name, Existence = new(_ws.CurrentTime) };
            if (actor.IsTargetable)
                p.TargetableHistory.Add(_ws.CurrentTime, true);
            p.PosRotHistory.Add(_ws.CurrentTime, actor.PosRot);
            p.HPHistory.Add(_ws.CurrentTime, (actor.HPCur, actor.HPMax));
            _res.Participants.Add(p);
            foreach (var e in _encounters.Values)
                e.Participants.GetOrAdd(p.OID).Add(p);
        }

        private void ActorRemoved(object? sender, Actor actor)
        {
            var p = _participants[actor.InstanceID];
            p.Existence.End = _ws.CurrentTime;
            if (actor.IsTargetable)
                p.TargetableHistory.Add(_ws.CurrentTime, false);
            _participants.Remove(actor.InstanceID);
        }

        private void ActorCombat(object? sender, Actor actor)
        {
            if (!actor.InCombat)
                FinishEncounter(actor);
            else if (actor.IsTargetable)
                StartEncounter(actor);
        }

        private void ActorTargetable(object? sender, Actor actor)
        {
            _participants[actor.InstanceID].TargetableHistory.Add(_ws.CurrentTime, actor.IsTargetable);
            if (actor.InCombat && actor.IsTargetable)
                StartEncounter(actor);
        }

        private void ActorDead(object? sender, Actor actor)
        {
            _participants[actor.InstanceID].DeadHistory.Add(_ws.CurrentTime, actor.IsDead);
        }

        private void ActorMoved(object? sender, (Actor actor, Vector4 prevPosRot) args)
        {
            _participants[args.actor.InstanceID].PosRotHistory.Add(_ws.CurrentTime, args.actor.PosRot);
        }

        private void ActorHP(object? sender, (Actor actor, uint prevCur, uint prevMax) args)
        {
            _participants[args.actor.InstanceID].HPHistory.Add(_ws.CurrentTime, (args.actor.HPCur, args.actor.HPMax));
        }

        private void CastStart(object? sender, Actor actor)
        {
            var c = actor.CastInfo!;
            var target = _participants.GetValueOrDefault(c.TargetID);
            _participants[actor.InstanceID].Casts.Add(new() { ID = c.Action, ExpectedCastTime = c.TotalTime, Time = new(_ws.CurrentTime), Target = target, Location = c.TargetID == 0 ? c.Location : (_ws.Actors.Find(c.TargetID)?.Position ?? new()) });
        }

        private void CastFinish(object? sender, Actor actor)
        {
            _participants[actor.InstanceID].Casts.Last().Time.End = _ws.CurrentTime;
        }

        private void TetherAdd(object? sender, Actor actor)
        {
            var t = _tethers[actor.InstanceID] = new() { ID = actor.Tether.ID, Source = _participants[actor.InstanceID], Target = _participants.GetValueOrDefault(actor.Tether.Target), Time = new(_ws.CurrentTime) };
            _res.Tethers.Add(t);
        }

        private void TetherRemove(object? sender, Actor actor)
        {
            _tethers[actor.InstanceID].Time.End = _ws.CurrentTime;
            _tethers.Remove(actor.InstanceID);
        }

        private void StatusGain(object? sender, (Actor actor, int index) args)
        {
            var p = _participants[args.actor.InstanceID];
            var s = args.actor.Statuses[args.index];
            var src = _participants.GetValueOrDefault(s.SourceID);
            var r = _statuses[(args.actor.InstanceID, args.index)] = new() { ID = s.ID, Index = args.index, Target = p, Source = src, InitialDuration = (float)(s.ExpireAt - _ws.CurrentTime).TotalSeconds, Time = new(_ws.CurrentTime), StartingExtra = s.Extra };
            p.HasAnyStatuses = true;
            _res.Statuses.Add(r);
        }

        private void StatusLose(object? sender, (Actor actor, int index) args)
        {
            var s = args.actor.Statuses[args.index];
            var r = _statuses.GetValueOrDefault((args.actor.InstanceID, args.index));
            if (r == null)
                return;

            r.Time.End = _ws.CurrentTime;
            _statuses.Remove((args.actor.InstanceID, args.index));
        }

        private void StatusChange(object? sender, (Actor actor, int index, ushort, DateTime) args)
        {
            StatusLose(sender, (args.actor, args.index));
            StatusGain(sender, (args.actor, args.index));
        }

        private void EventIcon(object? sender, (uint actorID, uint iconID) args)
        {
            _res.Icons.Add(new() { ID = args.iconID, Target = _participants.GetValueOrDefault(args.actorID), Timestamp = _ws.CurrentTime });
        }

        private void EventCast(object? sender, CastEvent info)
        {
            var p = _participants.GetValueOrDefault(info.CasterID);
            if (p == null)
            {
                Service.Log($"Skipping {info.Action} cast from unknown actor {info.CasterID:X}");
                return;
            }

            var srcActor = _ws.Actors.Find(info.CasterID);
            var tgtActor = _ws.Actors.Find(info.MainTargetID);

            Vector3 targetPos = new();
            if (tgtActor != null)
                targetPos = tgtActor.Position;
            else if (srcActor?.CastInfo != null)
                targetPos = srcActor.CastInfo.Location;
            else if (info.Targets.Count > 0)
                targetPos = _ws.Actors.Find(info.Targets[0].ID)?.Position ?? new();

            var a = new Replay.Action() { ID = info.Action, Timestamp = _ws.CurrentTime, Source = p,
                MainTarget = _participants.GetValueOrDefault(info.MainTargetID), TargetPos = targetPos };
            foreach (var t in info.Targets)
            {
                var target = _participants.GetValueOrDefault(t.ID);
                if (target != null)
                    target.IsTargetOfAnyActions = true;
                a.Targets.Add(new() { Target = target, Effects = t.Effects });
            }
            p.HasAnyActions = true;
            _res.Actions.Add(a);
        }

        private void EventEnvControl(object? sender, (uint feature, byte index, uint state) args)
        {
            _res.EnvControls.Add(new() { Feature = args.feature, Index = args.index, State = args.state, Timestamp = _ws.CurrentTime });
        }
    }
}
