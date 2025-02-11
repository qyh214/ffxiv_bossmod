﻿using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace BossMod.Endwalker.P1S
{
    using static BossModule;

    // we have two cube patterns (symmetrical and asymmetrical) and two explosion orders, which gives us three movement patterns (symmetrical, asymmetrical top-bottom and asymmetrical bottom-top)
    // each pattern has four distinct kinds of squares that have identical cubes (N, S, E/W and intercardinals); one of the intercardinals should do special movement to swap with N for asymmetrical patterns
    // we use player's position during first explosion to assign their square (and don't try to handle fuckups)
    // N position: symmetrical = stay in N during all mechanic; asymmetrical = either stay at N or move to S for second explosion, move to designated corner for last explosion
    // S position: symmetrical = move to N for second explosion, then return to S; asymmetrical = move to center for second explosion, then return to S
    // E/W positions: symmetrical = move to center for second explosion, then return; asymmetrical = move to S for second explosion, then return
    // normal corners: symmetrical = move to S for second explosion, then return; asymmetrical = move to center for second explosion, then return
    // designated corner: symmetrical = same as normal corner; asymmetrical = move to N or S for second explosion, move to N for last explosion
    class Intemperance : Component
    {
        public enum State { Unknown, TopToBottom, BottomToTop }
        public enum Pattern { Unknown, Symmetrical, Asymmetrical }
        public enum Cube { None, R, B, P }

        public int NumExplosions { get; private set; } = 0;
        private State _curState = State.Unknown;
        private Pattern _pattern = Pattern.Unknown;
        private bool _patternModified = false;
        private Cube[] _cubes = new Cube[24]; // [3*i+j] corresponds to cell i [NW N NE E SE S SW W], cube j [bottom center top]
        private int[]? _playerAssignment; // cell index assigned to player, null if not assigned yet

        private static AOEShapeRect _delimiterAOE = new(20, 1, 20);
        private static Vector4[] _delimiterCenters = { new(93, 0, 100, 0), new(107, 0, 100, 0), new(100, 0, 93, MathF.PI / 2), new(100, 0, 107, MathF.PI / 2) };

        private static Cube[] _patternSymm = {
            Cube.R, Cube.P, Cube.R,
            Cube.B, Cube.R, Cube.B,
            Cube.R, Cube.P, Cube.R,
            Cube.R, Cube.P, Cube.B,
            Cube.R, Cube.P, Cube.R,
            Cube.B, Cube.B, Cube.B,
            Cube.R, Cube.P, Cube.R,
            Cube.R, Cube.P, Cube.B,
        };
        private static Cube[] _patternAsymm = {
            Cube.B, Cube.P, Cube.R,
            Cube.R, Cube.R, Cube.B,
            Cube.B, Cube.P, Cube.R,
            Cube.R, Cube.P, Cube.R,
            Cube.B, Cube.P, Cube.R,
            Cube.R, Cube.B, Cube.B,
            Cube.B, Cube.P, Cube.R,
            Cube.R, Cube.P, Cube.R,
        };
        private static Vector2[] _offsets = { new(-1, -1), new(0, -1), new(1, -1), new(1, 0), new(1, 1), new(0, 1), new(-1, 1), new(-1, 0), new(0, 0) };

        public override void Update(BossModule module)
        {
            if (_patternModified)
            {
                _pattern = _cubes.SequenceEqual(_patternSymm) ? Pattern.Symmetrical : (_cubes.SequenceEqual(_patternAsymm) ? Pattern.Asymmetrical : Pattern.Unknown);
                _patternModified = false;
            }
        }

        public override void AddHints(BossModule module, int slot, Actor actor, TextHints hints, MovementHints? movementHints)
        {
            if (_delimiterCenters.Any(c => _delimiterAOE.Check(actor.Position, new(c.X, c.Y, c.Z), c.W)))
            {
                hints.Add("GTFO from delimiter!");
            }
            else
            {
                int actorCell = PositionFromCoords(module, actor.Position);
                if (_playerAssignment != null)
                {
                    int expectedCell = NumExplosions == 1 ? Position2(module, _playerAssignment[slot]) : Position3(module, _playerAssignment[slot]);
                    if (actorCell != expectedCell)
                    {
                        hints.Add("Go to assigned cell!");
                    }
                }
                else if (module.Raid.WithoutSlot().Exclude(actor).Any(other => PositionFromCoords(module, other.Position) == actorCell))
                {
                    hints.Add("Stand in own cell!");
                }
            }

            if (movementHints != null && _playerAssignment != null)
                foreach (var (from, to, color) in MovementHints(module, actor.Position, _playerAssignment[slot]))
                    movementHints.Add(from, to, color);
        }

        public override void AddGlobalHints(BossModule module, GlobalHints hints)
        {
            hints.Add($"Order: {_curState}, pattern: {_pattern}.");
        }

        public override void DrawArenaBackground(BossModule module, int pcSlot, Actor pc, MiniArena arena)
        {
            foreach (var c in _delimiterCenters)
                _delimiterAOE.Draw(arena, new(c.X, c.Y, c.Z), c.W);
        }

        public override void DrawArenaForeground(BossModule module, int pcSlot, Actor pc, MiniArena arena)
        {
            if (_playerAssignment != null)
                foreach (var (from, to, color) in MovementHints(module, pc.Position, _playerAssignment[pcSlot]))
                    arena.AddLine(from, to, color);
        }

        public override void OnCastStarted(BossModule module, Actor actor)
        {
            if (actor != module.PrimaryActor)
                return;

            if (actor.CastInfo!.IsSpell(AID.IntemperateTormentUp))
                _curState = State.BottomToTop;
            else if (actor.CastInfo!.IsSpell(AID.IntemperateTormentDown))
                _curState = State.TopToBottom;
        }

        public override void OnEventCast(BossModule module, CastEvent info)
        {
            if (info.IsSpell(AID.PainfulFlux)) // this is convenient to rely on, since exactly 1 cast happens right after every explosion
            {
                if (NumExplosions++ == 0 && _pattern != Pattern.Unknown && _curState != State.Unknown)
                {
                    // on first explosion, assign players to cubes
                    _playerAssignment = new int[PartyState.MaxSize];
                    int occupiedMask = 0;
                    foreach (var (slot, player) in module.Raid.WithSlot(true))
                    {
                        var pos = _playerAssignment[slot] = PositionFromCoords(module, player.Position);
                        occupiedMask |= 1 << pos;
                    }

                    if (occupiedMask != 0xFF)
                    {
                        module.ReportError(this, "Failed to determine player assignments");
                        _playerAssignment = null;
                    }
                }
            }
        }

        public override void OnEventEnvControl(BossModule module, uint featureID, byte index, uint state)
        {
            // we get the following env-control messages:
            // 1. ~2.8s after 26142 cast, we get 25 EnvControl messages with featureID 800375A0
            // 2. first 24 correspond to cubes, in groups of three (bottom->top), in order: NW N NE E SE S SW W
            //    the last one (index 26) can be ignored, probably corresponds to oneshot border
            //    state corresponds to cube type (00020001 for red, 00800040 for blue, 20001000 for purple)
            //    so asymmetrical pattern is: BPR RRB BPR RPR BPR RBB BPR RPR
            //    and symmetrical pattern is: RPR BRB RPR RPB RPR BBB RPR RPB
            // 3. on each explosion, we get 8 191s, with type 00080004 for exploded red, 04000004 for exploded blue, 08000004 for exploded purple
            // 4. 3 sec before second & third explosion, we get 8 191s, with type 00200020 for preparing red, 02000200 for preparing blue, 80008000 for preparing purple
            if (featureID == 0x800375A0 && index < 24)
            {
                var cube = state switch
                {
                    0x00020001 => Cube.R,
                    0x00800040 => Cube.B,
                    0x20001000 => Cube.P,
                    _ => Cube.None
                };
                if (cube != Cube.None)
                {
                    _cubes[index] = cube;
                    _patternModified = true;
                }
            }
        }

        private int PositionFromCoords(BossModule module, Vector3 coords)
        {
            return (coords - module.Arena.WorldCenter) switch
            {
                { X: < -7, Z: < -7 } => 0,
                { X: > +7, Z: < -7 } => 2,
                { Z: < -7 } => 1,
                { X: < -7, Z: > +7 } => 6,
                { X: > +7, Z: > +7 } => 4,
                { Z: > +7 } => 5,
                { X: < -7 } => 7,
                { X: > +7 } => 3,
                _ => 8
            };
        }

        private int SwapCorner(BossModule module)
        {
            return 2 * (int)((module.Config as P1SConfig)?.IntemperanceAsymmetricalSwapCorner ?? P1SConfig.Corner.NW);
        }

        private Vector3 PosCenter(BossModule module, int pos)
        {
            var off = 14 * _offsets[pos];
            return module.Arena.WorldCenter + new Vector3(off.X, 0, off.Y);
        }

        private int Position2(BossModule module, int pos1)
        {
            if (_pattern == Pattern.Symmetrical)
            {
                return pos1 switch
                {
                    1 => 1, // N stays
                    5 => 1, // S goes N
                    3 or 7 => 8, // E/W go center
                    _ => 5, // corners go S
                };
            }

            if (pos1 == SwapCorner(module))
                return _curState == State.TopToBottom ? 5 : 1; // swap-corner goes N or S

            return pos1 switch
            {
                1 => _curState == State.TopToBottom ? 1 : 5, // N either stays or goes S
                5 => 8, // S goes center
                3 or 7 => 5, // E/W go S
                _ => 8, // corners go center
            };
        }

        private int Position3(BossModule module, int pos1)
        {
            if (_pattern == Pattern.Symmetrical)
                return pos1; // everyone returns to initial spot

            int swapCorner = SwapCorner(module);
            if (pos1 == swapCorner)
                return 1; // swap-corner goes N
            else if (pos1 == 1)
                return swapCorner; // N goes to swap-corner
            else
                return pos1; // others return to initial spot
        }

        private IEnumerable<(Vector3, Vector3, uint)> MovementHints(BossModule module, Vector3 startingPosition, int assignment)
        {
            switch (NumExplosions)
            {
                case 1:
                    var mid = PosCenter(module, Position2(module, assignment));
                    yield return (mid, PosCenter(module, Position3(module, assignment)), module.Arena.ColorDanger);
                    yield return (startingPosition, mid, module.Arena.ColorSafe);
                    break;
                case 2:
                    yield return (startingPosition, PosCenter(module, Position3(module, assignment)), module.Arena.ColorSafe);
                    break;
            }
        }
    }
}
