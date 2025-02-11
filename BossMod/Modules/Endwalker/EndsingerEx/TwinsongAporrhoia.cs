﻿using System;

namespace BossMod.Endwalker.EndsingerEx
{
    class TwinsongAporrhoia : BossModule.Component
    {
        private enum HeadID { Center, Danger1, Danger2, Safe1, Safe2, Count };

        private int _castsDone;
        private bool _ringsAssigned;
        private float _centerStartingRotation;
        private (Actor? Actor, int Rings)[] _heads = new (Actor?, int)[(int)HeadID.Count];

        private static AOEShapeCone _aoeCenter = new(20, MathF.PI / 2);
        private static AOEShapeCircle _aoeDanger = new(15);
        private static AOEShapeDonut _aoeSafe = new(5, 15);

        public override void AddHints(BossModule module, int slot, Actor actor, BossModule.TextHints hints, BossModule.MovementHints? movementHints)
        {
            if (_castsDone >= 3 && !_ringsAssigned)
                return;

            bool inAOE = false;

            var center = _heads[(int)HeadID.Center];
            if (center.Actor != null)
            {
                float rot = _centerStartingRotation - (_castsDone - center.Rings) * MathF.PI / 2;
                inAOE = _aoeCenter.Check(actor.Position, center.Actor.Position, rot);
            }

            for (var i = HeadID.Danger1; i < HeadID.Count && !inAOE; ++i)
            {
                var head = _heads[(int)i];
                if (head.Actor != null)
                {
                    int safeCounter = (i >= HeadID.Safe1 ? 1 : 0) + _castsDone - head.Rings;
                    AOEShape aoe = (safeCounter & 1) != 0 ? _aoeSafe : _aoeDanger;
                    inAOE |= aoe.Check(actor.Position, head.Actor);
                }
            }

            if (inAOE)
                hints.Add("GTFO from aoe!");
        }

        public override void DrawArenaBackground(BossModule module, int pcSlot, Actor pc, MiniArena arena)
        {
            if (_castsDone >= 3 && !_ringsAssigned)
                return;

            var center = _heads[(int)HeadID.Center];
            if (center.Actor != null)
            {
                float rot = _centerStartingRotation - (_castsDone - center.Rings) * MathF.PI / 2;
                _aoeCenter.Draw(arena, center.Actor.Position, rot);
            }

            for (var i = HeadID.Danger1; i < HeadID.Count; ++i)
            {
                var head = _heads[(int)i];
                if (head.Actor != null)
                {
                    int safeCounter = (i >= HeadID.Safe1 ? 1 : 0) + _castsDone - head.Rings;
                    AOEShape aoe = (safeCounter & 1) != 0 ? _aoeSafe : _aoeDanger;
                    aoe.Draw(arena, head.Actor);
                }
            }
        }

        public override void OnStatusGain(BossModule module, Actor actor, int index)
        {
            var s = actor.Statuses[index];
            if ((SID)s.ID == SID.RewindTwinsong)
            {
                int rings = s.Extra switch
                {
                    0x178 => 1,
                    0x179 => 2,
                    0x17A => 3,
                    _ => 0,
                };
                if (rings == 0)
                {
                    module.ReportError(this, $"Unexpected extra {s.Extra:X} for rewind status");
                    return;
                }

                int slot = Array.FindIndex(_heads, ar => ar.Actor == actor);
                if (slot == -1)
                {
                    module.ReportError(this, $"Unexpected actor for rewind status");
                    return;
                }

                _heads[slot].Rings = rings;
                _ringsAssigned = true;
            }
        }

        public override void OnCastStarted(BossModule module, Actor actor)
        {
            if (!actor.CastInfo!.IsSpell())
                return;
            switch ((AID)actor.CastInfo.Action.ID)
            {
                case AID.DiairesisTwinsong:
                    if (_heads[(int)HeadID.Center].Actor == null)
                    {
                        _heads[(int)HeadID.Center] = (actor, 0);
                        _centerStartingRotation = actor.Rotation;
                    }
                    break;
                case AID.NecroticFluid:
                    if (_heads[(int)HeadID.Danger1].Actor == null)
                        _heads[(int)HeadID.Danger1] = (actor, 0);
                    else if (_heads[(int)HeadID.Danger2].Actor == null)
                        _heads[(int)HeadID.Danger2] = (actor, 0);
                    break;
                case AID.WaveOfNausea:
                    if (_heads[(int)HeadID.Safe1].Actor == null)
                        _heads[(int)HeadID.Safe1] = (actor, 0);
                    else if (_heads[(int)HeadID.Safe2].Actor == null)
                        _heads[(int)HeadID.Safe2] = (actor, 0);
                    break;
            }
        }

        public override void OnCastFinished(BossModule module, Actor actor)
        {
            if (actor.CastInfo!.IsSpell(AID.AporrhoiaUnforgotten))
                ++_castsDone;
            else if (actor.CastInfo!.IsSpell(AID.FatalismDiairesis))
                _ringsAssigned = false;
        }
    }
}
