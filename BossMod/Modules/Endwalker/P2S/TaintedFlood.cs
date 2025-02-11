﻿using System.Linq;

namespace BossMod.Endwalker.P2S
{
    // note: if activated together with ChannelingFlow, it does not target next flow arrows
    class TaintedFlood : CommonComponents.CastCounter
    {
        private ulong _ignoredTargets;

        private static float _radius = 6;

        public TaintedFlood() : base(ActionID.MakeSpell(AID.TaintedFloodAOE)) { }

        public override void Init(BossModule module)
        {
            var flow = module.FindComponent<ChannelingFlow>();
            if (flow != null)
            {
                _ignoredTargets = module.Raid.WithSlot().WhereSlot(s => flow.SlotActive(module, s)).Mask();
            }
        }

        public override void AddHints(BossModule module, int slot, Actor actor, BossModule.TextHints hints, BossModule.MovementHints? movementHints)
        {
            if (NumCasts > 0)
                return;

            if (BitVector.IsVector64BitSet(_ignoredTargets, slot))
            {
                // player is not a target of flood, so just make sure he is not clipped by others
                if (module.Raid.WithSlot().ExcludedFromMask(_ignoredTargets).InRadius(actor.Position, _radius).Any())
                    hints.Add("GTFO from flood!");
            }
            else
            {
                // player is target of flood => make sure no one is in range
                if (module.Raid.WithoutSlot().InRadiusExcluding(actor, _radius).Any())
                    hints.Add("Spread!");
            }
        }

        public override void DrawArenaForeground(BossModule module, int pcSlot, Actor pc, MiniArena arena)
        {
            if (NumCasts > 0)
                return;

            if (BitVector.IsVector64BitSet(_ignoredTargets, pcSlot))
            {
                foreach ((_, var actor) in module.Raid.WithSlot().ExcludedFromMask(_ignoredTargets))
                {
                    arena.Actor(actor, arena.ColorDanger);
                    arena.AddCircle(actor.Position, _radius, arena.ColorDanger);
                }
            }
            else
            {
                arena.AddCircle(pc.Position, _radius, arena.ColorDanger);
                foreach (var player in module.Raid.WithoutSlot().Exclude(pc))
                    arena.Actor(player, GeometryUtils.PointInCircle(player.Position - pc.Position, _radius) ? arena.ColorPlayerInteresting : arena.ColorPlayerGeneric);
            }
        }
    }
}
