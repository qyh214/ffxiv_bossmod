﻿using System.Numerics;

namespace BossMod.Endwalker.P2S
{
    using static BossModule;

    // state related to kampeos harma mechanic
    // note that it relies on waymarks to determine safe spots...
    class KampeosHarma : CommonComponents.CastCounter
    {
        private Vector3 _startingOffset;
        private int[] _playerOrder = new int[8]; // 0 if unknown, then sq1 sq2 sq3 sq4 tri1 tri2 tri3 tri4

        public KampeosHarma() : base(ActionID.MakeSpell(AID.KampeosHarmaChargeBoss)) { }

        public override void AddHints(BossModule module, int slot, Actor actor, TextHints hints, MovementHints? movementHints)
        {
            var safePos = GetSafeZone(module, slot);
            if (safePos != null && !GeometryUtils.PointInCircle(actor.Position - safePos.Value, 2))
            {
                hints.Add("Go to safe zone!");
                if (movementHints != null)
                {
                    movementHints.Add(actor.Position, safePos.Value, module.Arena.ColorDanger);
                }
            }
        }

        public override void DrawArenaForeground(BossModule module, int pcSlot, Actor pc, MiniArena arena)
        {
            var pos = GetSafeZone(module, pcSlot);
            if (pos != null)
                arena.AddCircle(pos.Value, 1, arena.ColorSafe);
        }

        public override void OnEventIcon(BossModule module, uint actorID, uint iconID)
        {
            if (iconID >= 145 && iconID <= 152)
            {
                _startingOffset = module.PrimaryActor.Position - module.Arena.WorldCenter;

                int slot = module.Raid.FindSlot(actorID);
                if (slot >= 0)
                    _playerOrder[slot] = (int)(iconID - 144);
            }
        }

        private Vector3? GetSafeZone(BossModule module, int slot)
        {
            switch (slot >= 0 ? _playerOrder[slot] : 0)
            {
                case 1: // sq 1 - opposite corner, hide after first charge
                    return module.Arena.WorldCenter + (NumCasts < 1 ? -1.2f : -1.4f) * _startingOffset;
                case 2: // sq 2 - same corner, hide after second charge
                    return module.Arena.WorldCenter + (NumCasts < 2 ? +1.2f : +1.4f) * _startingOffset;
                case 3: // sq 3 - opposite corner, hide before first charge
                    return module.Arena.WorldCenter + (NumCasts < 1 ? -1.4f : -1.2f) * _startingOffset;
                case 4: // sq 4 - same corner, hide before second charge
                    return module.Arena.WorldCenter + (NumCasts < 2 ? +1.4f : +1.2f) * _startingOffset;
                case 5: // tri 1 - waymark 1
                    return module.WorldState.Waymarks[Waymark.N1];
                case 6: // tri 2 - waymark 2
                    return module.WorldState.Waymarks[Waymark.N2];
                case 7: // tri 3 - waymark 3
                    return module.WorldState.Waymarks[Waymark.N3];
                case 8: // tri 4 - waymark 4
                    return module.WorldState.Waymarks[Waymark.N4];
            }
            return null;
        }
    }
}
