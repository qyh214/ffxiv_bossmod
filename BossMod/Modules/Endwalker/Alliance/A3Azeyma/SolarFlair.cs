﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace BossMod.Endwalker.Alliance.A3Azeyma
{
    class SolarFlair : BossModule.Component
    {
        private Dictionary<uint, Vector3?> _sunstorms = new(); // null = cast finished, otherwise expected position

        private static float _kickDistance = 18;
        private static AOEShapeCircle _aoe = new(15);

        public override void AddHints(BossModule module, int slot, Actor actor, BossModule.TextHints hints, BossModule.MovementHints? movementHints)
        {
            if (ActiveSunstorms(module).Any(s => _aoe.Check(actor.Position, s, 0)))
                hints.Add("GTFO from aoe!");
        }

        public override void DrawArenaBackground(BossModule module, int pcSlot, Actor pc, MiniArena arena)
        {
            foreach (var s in ActiveSunstorms(module))
                _aoe.Draw(arena, s, 0);
        }

        public override void OnCastStarted(BossModule module, Actor actor)
        {
            if (actor.CastInfo!.IsSpell(AID.HauteAirWings))
            {
                var closestSunstorm = module.Enemies(OID.Sunstorm).MinBy(s => (s.Position - actor.Position).LengthSquared());
                if (closestSunstorm != null)
                {
                    _sunstorms[closestSunstorm.InstanceID] = closestSunstorm.Position + _kickDistance * GeometryUtils.DirectionToVec3(actor.Rotation);
                }
            }
        }

        public override void OnCastFinished(BossModule module, Actor actor)
        {
            if (actor.CastInfo!.IsSpell(AID.SolarFlair))
                _sunstorms[actor.InstanceID] = null;
        }

        private IEnumerable<Vector3> ActiveSunstorms(BossModule module)
        {
            foreach (var s in module.Enemies(OID.Sunstorm))
            {
                if (!_sunstorms.ContainsKey(s.InstanceID))
                    _sunstorms[s.InstanceID] = s.Position;
                var pos = _sunstorms[s.InstanceID];
                if (pos != null)
                    yield return pos.Value;
            }
        }
    }
}
