﻿using System.Linq;
using System.Numerics;

namespace BossMod.Endwalker.P2S
{
    using static BossModule;

    // state related to coherence mechanic
    // TODO: i'm not 100% sure how exactly it selects target for aoe ray, I assume it is closest player except tether target?..
    class Coherence : CommonComponents.CastCounter
    {
        private Actor? _tetherTarget;
        private Actor? _rayTarget;
        private AOEShapeRect _rayShape = new(50, 3);
        private ulong _inRay = 0;

        private static float _aoeRadius = 10; // not sure about this - actual range is 60, but it has some sort of falloff? i have very few data points < 15

        public Coherence() : base(ActionID.MakeSpell(AID.CoherenceRay)) { }

        public override void Update(BossModule module)
        {
            // if tether is still active, update tether target
            var head = module.Enemies(OID.CataractHead).FirstOrDefault();
            if (head != null && head.Tether.Target != 0 && head.Tether.Target != _tetherTarget?.InstanceID)
            {
                _tetherTarget = module.WorldState.Actors.Find(head.Tether.Target);
            }

            _inRay = 0;
            _rayTarget = module.Raid.WithoutSlot().Exclude(_tetherTarget).MinBy(a => (a.Position - module.PrimaryActor.Position).LengthSquared());
            if (_rayTarget != null)
            {
                _rayShape.DirectionOffset = GeometryUtils.DirectionFromVec3(_rayTarget.Position - module.PrimaryActor.Position);
                _inRay = module.Raid.WithSlot().InShape(_rayShape, module.PrimaryActor.Position, 0).Mask();
            }
        }

        public override void AddHints(BossModule module, int slot, Actor actor, TextHints hints, MovementHints? movementHints)
        {
            if (actor == _tetherTarget)
            {
                if (actor.Role != Role.Tank)
                {
                    hints.Add("Pass tether to tank!");
                }
                else if (module.Raid.WithoutSlot().InRadiusExcluding(actor, _aoeRadius).Any())
                {
                    hints.Add("GTFO from raid!");
                }
            }
            else if (actor == _rayTarget)
            {
                if (actor.Role != Role.Tank)
                {
                    hints.Add("Go behind tank!");
                }
                else if (BitOperations.PopCount(_inRay) < 7)
                {
                    hints.Add("Make sure ray hits everyone!");
                }
            }
            else
            {
                if (actor.Role == Role.Tank)
                {
                    hints.Add("Go in front of raid!");
                }
                else if (!BitVector.IsVector64BitSet(_inRay, slot))
                {
                    hints.Add("Go behind tank!");
                }
            }
        }

        public override void DrawArenaBackground(BossModule module, int pcSlot, Actor pc, MiniArena arena)
        {
            if (_rayTarget != null)
                _rayShape.Draw(arena, module.PrimaryActor.Position, 0);
        }

        public override void DrawArenaForeground(BossModule module, int pcSlot, Actor pc, MiniArena arena)
        {
            // TODO: i'm not sure what are the exact mechanics - flare is probably distance-based, and ray is probably shared damage cast at closest target?..
            var head = module.Enemies(OID.CataractHead).FirstOrDefault();
            foreach ((int i, var player) in module.Raid.WithSlot())
            {
                if (head?.Tether.Target == player.InstanceID)
                {
                    arena.AddLine(player.Position, module.PrimaryActor.Position, arena.ColorDanger);
                    arena.Actor(player, arena.ColorDanger);
                    arena.AddCircle(player.Position, _aoeRadius, arena.ColorDanger);
                }
                else if (player == _rayTarget)
                {
                    arena.Actor(player, arena.ColorDanger);
                }
                else if (player != _tetherTarget)
                {
                    arena.Actor(player, BitVector.IsVector64BitSet(_inRay, i) ? arena.ColorPlayerInteresting : arena.ColorPlayerGeneric);
                }
            }
        }
    }
}
