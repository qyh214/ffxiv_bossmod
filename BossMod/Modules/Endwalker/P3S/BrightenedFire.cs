﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace BossMod.Endwalker.P3S
{
    using static BossModule;

    // state related to brightened fire mechanic
    // this helper relies on waymarks 1-4, and assumes they don't change during fight - this is of course quite an assumption, but whatever...
    class BrightenedFire : CommonComponents.CastCounter
    {
        private int[] _playerOrder = new int[8]; // 0 if unknown, 1-8 otherwise

        private static float _aoeRange = 7;

        public BrightenedFire() : base(ActionID.MakeSpell(AID.BrightenedFireAOE)) { }

        public override void AddHints(BossModule module, int slot, Actor actor, TextHints hints, MovementHints? movementHints)
        {
            if (_playerOrder[slot] <= NumCasts)
                return;

            var pos = PositionForOrder(module, _playerOrder[slot]);
            if (!GeometryUtils.PointInCircle(actor.Position - pos, 5))
            {
                hints.Add($"Get to correct position {_playerOrder[slot]}!");
            }

            int numHitAdds = module.Enemies(OID.DarkenedFire).InRadius(actor.Position, _aoeRange).Count();
            if (numHitAdds < 2)
            {
                hints.Add("Get closer to adds!");
            }
        }

        public override void DrawArenaForeground(BossModule module, int pcSlot, Actor pc, MiniArena arena)
        {
            if (_playerOrder[pcSlot] <= NumCasts)
                return;

            var pos = PositionForOrder(module, _playerOrder[pcSlot]);
            arena.AddCircle(pos, 1, arena.ColorSafe);

            // draw all adds
            int addIndex = 0;
            foreach (var fire in module.Enemies(OID.DarkenedFire).SortedByRange(pos))
            {
                arena.Actor(fire, addIndex++ < 2 ? arena.ColorDanger : arena.ColorPlayerGeneric);
            }

            // draw range circle
            arena.AddCircle(pc.Position, _aoeRange, arena.ColorDanger);
        }

        public override void OnEventIcon(BossModule module, uint actorID, uint iconID)
        {
            if (iconID >= 268 && iconID <= 275)
            {
                int slot = module.Raid.FindSlot(actorID);
                if (slot >= 0)
                    _playerOrder[slot] = (int)iconID - 267;
            }
        }

        private Vector3 PositionForOrder(BossModule module, int order)
        {
            // TODO: consider how this can be improved...
            var markID = (Waymark)((int)Waymark.N1 + (order - 1) % 4);
            return module.WorldState.Waymarks[markID] ?? module.Arena.WorldCenter;
        }
    }
}
