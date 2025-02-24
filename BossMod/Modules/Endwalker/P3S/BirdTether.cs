﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace BossMod.Endwalker.P3S
{
    using static BossModule;

    // state related to large bird tethers
    // TODO: simplify and make more robust, e.g. in case something goes wrong and bird dies without tether update
    class BirdTether : Component
    {
        public int NumFinishedChains { get; private set; } = 0;
        private (Actor?, Actor?, int)[] _chains = new (Actor?, Actor?, int)[4]; // actor1, actor2, num-charges
        private ulong _playersInAOE = 0;

        private static float _chargeHalfWidth = 3;
        private static float _chargeMinSafeDistance = 30;

        public override void Update(BossModule module)
        {
            _playersInAOE = 0;
            var birdsLarge = module.Enemies(OID.SunbirdLarge);
            for (int i = 0; i < Math.Min(birdsLarge.Count, _chains.Length); ++i)
            {
                if (_chains[i].Item3 == 2)
                    continue; // this is finished

                var bird = birdsLarge[i];
                if (_chains[i].Item1 == null && bird.Tether.Target != 0)
                {
                    _chains[i].Item1 = module.WorldState.Actors.Find(bird.Tether.Target); // first target found
                }
                if (_chains[i].Item2 == null && (_chains[i].Item1?.Tether.Target ?? 0) != 0)
                {
                    _chains[i].Item2 = module.WorldState.Actors.Find(_chains[i].Item1!.Tether.Target); // second target found
                }
                if (_chains[i].Item3 == 0 && _chains[i].Item1 != null && bird.Tether.Target == 0)
                {
                    _chains[i].Item3 = 1; // first charge (bird is no longer tethered to anyone)
                }
                if (_chains[i].Item3 == 1 && (_chains[i].Item1?.Tether.Target ?? 0) == 0)
                {
                    _chains[i].Item3 = 2;
                    ++NumFinishedChains;
                    continue;
                }

                // find players hit by next bird charge
                var nextTarget = _chains[i].Item3 > 0 ? _chains[i].Item2 : _chains[i].Item1;
                if (nextTarget != null && nextTarget.Position != bird.Position)
                {
                    var fromTo = nextTarget.Position - bird.Position;
                    float len = fromTo.Length();
                    fromTo /= len;
                    foreach ((int j, var player) in module.Raid.WithSlot().Exclude(nextTarget))
                    {
                        if (GeometryUtils.PointInRect(player.Position - bird.Position, fromTo, len, 0, _chargeHalfWidth))
                        {
                            BitVector.SetVector64Bit(ref _playersInAOE, j);
                        }
                    }
                }
            }
        }

        public override void AddHints(BossModule module, int slot, Actor actor, TextHints hints, MovementHints? movementHints)
        {
            var birdsLarge = module.Enemies(OID.SunbirdLarge);
            foreach ((var bird, (var p1, var p2, int numCharges)) in birdsLarge.Zip(_chains))
            {
                if (numCharges == 2)
                    continue;

                var nextTarget = numCharges > 0 ? p2 : p1;
                if (actor == nextTarget)
                {
                    // check that tether is 'safe'
                    var tetherSource = numCharges > 0 ? p1 : bird;
                    if (tetherSource?.Tether.ID != (uint)TetherID.LargeBirdFar)
                    {
                        hints.Add("Too close!");
                    }
                }
            }

            if (BitVector.IsVector64BitSet(_playersInAOE, slot))
            {
                hints.Add("GTFO from charge zone!");
            }
        }

        public override void DrawArenaBackground(BossModule module, int pcSlot, Actor pc, MiniArena arena)
        {
            // draw aoe zones for imminent charges, except one towards player
            var birdsLarge = module.Enemies(OID.SunbirdLarge);
            foreach ((var bird, (var p1, var p2, int numCharges)) in birdsLarge.Zip(_chains))
            {
                if (numCharges == 2)
                    continue;

                var nextTarget = numCharges > 0 ? p2 : p1;
                if (nextTarget != null && nextTarget != pc && nextTarget.Position != bird.Position)
                {
                    var fromTo = nextTarget.Position - bird.Position;
                    float len = fromTo.Length();
                    arena.ZoneQuad(bird.Position, fromTo / len, len, 0, _chargeHalfWidth, arena.ColorAOE);
                }
            }
        }

        public override void DrawArenaForeground(BossModule module, int pcSlot, Actor pc, MiniArena arena)
        {
            // draw all birds and all players
            var birdsLarge = module.Enemies(OID.SunbirdLarge);
            foreach (var bird in birdsLarge)
                arena.Actor(bird, arena.ColorEnemy);
            foreach ((int i, var player) in module.Raid.WithSlot())
                arena.Actor(player, BitVector.IsVector64BitSet(_playersInAOE, i) ? arena.ColorPlayerInteresting : arena.ColorPlayerGeneric);

            // draw chains containing player
            foreach ((var bird, (var p1, var p2, int numCharges)) in birdsLarge.Zip(_chains))
            {
                if (numCharges == 2)
                    continue;

                if (p1 == pc)
                {
                    // bird -> pc -> other
                    if (numCharges == 0)
                    {
                        arena.AddLine(bird.Position, pc.Position, (bird.Tether.ID == (uint)TetherID.LargeBirdFar) ? arena.ColorSafe : arena.ColorDanger);
                        if (p2 != null)
                        {
                            arena.AddLine(pc.Position, p2.Position, (pc.Tether.ID == (uint)TetherID.LargeBirdFar) ? arena.ColorSafe : arena.ColorDanger);
                        }

                        if (bird.Position != arena.WorldCenter)
                        {
                            var safespot = bird.Position + Vector3.Normalize(arena.WorldCenter - bird.Position) * _chargeMinSafeDistance;
                            arena.AddCircle(safespot, 1, arena.ColorSafe);
                        }
                    }
                    // else: don't care, charge to pc already happened
                }
                else if (p2 == pc && p1 != null)
                {
                    // bird -> other -> pc
                    if (numCharges == 0)
                    {
                        arena.AddLine(bird.Position, p1.Position, (bird.Tether.ID == (uint)TetherID.LargeBirdFar) ? arena.ColorSafe : arena.ColorDanger);
                        arena.AddLine(p1.Position, pc.Position, (p1.Tether.ID == (uint)TetherID.LargeBirdFar) ? arena.ColorSafe : arena.ColorDanger);

                        arena.AddCircle(bird.Position, 1, arena.ColorSafe); // draw safespot near bird
                    }
                    else
                    {
                        arena.AddLine(bird.Position, pc.Position, (p1.Tether.ID == (uint)TetherID.LargeBirdFar) ? arena.ColorSafe : arena.ColorDanger);

                        if (bird.Position != arena.WorldCenter)
                        {
                            var safespot = bird.Position + Vector3.Normalize(arena.WorldCenter - bird.Position) * _chargeMinSafeDistance;
                            arena.AddCircle(safespot, 1, arena.ColorSafe);
                        }
                    }
                }
            }
        }
    }
}
