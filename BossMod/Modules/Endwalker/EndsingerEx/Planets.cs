﻿using System;
using System.Collections.Generic;
using System.Numerics;

namespace BossMod.Endwalker.EndsingerEx
{
    using static BossModule;

    // used both for single planets (elegeia) and successions (fatalism)
    class Planets : Component
    {
        private Actor? _head;
        private List<Vector3> _planetsFiery = new();
        private List<Vector3> _planetsAzure = new();

        private static AOEShapeCone _aoeHead = new(20, MathF.PI / 2);
        private static AOEShapeCircle _aoePlanet = new(30);
        private static float _knockbackDistance = 25;
        private static float _planetOffset = 19.8f; // == 14 * sqrt(2)

        public override void AddHints(BossModule module, int slot, Actor actor, TextHints hints, MovementHints? movementHints)
        {
            if (_aoeHead.Check(actor.Position, _head))
            {
                hints.Add("GTFO from head aoe!");
            }
            if (_planetsFiery.Count > 0 && _aoePlanet.Check(actor.Position, _planetsFiery[0], 0))
            {
                hints.Add("GTFO from planet aoe!");
            }
            if (_planetsAzure.Count > 0)
            {
                var offsetLocation = AdjustPositionForKnockback(actor.Position, _planetsAzure[0], _knockbackDistance);
                if (!module.Arena.InBounds(offsetLocation))
                {
                    hints.Add("About to be knocked into wall!");
                }
            }
        }

        public override void DrawArenaBackground(BossModule module, int pcSlot, Actor pc, MiniArena arena)
        {
            _aoeHead.Draw(arena, _head);
            if (_planetsFiery.Count > 0)
            {
                _aoePlanet.Draw(arena, _planetsFiery[0], 0);
            }
        }

        public override void DrawArenaForeground(BossModule module, int pcSlot, Actor pc, MiniArena arena)
        {
            if (_planetsAzure.Count > 0)
            {
                var offsetLocation = AdjustPositionForKnockback(pc.Position, _planetsAzure[0], _knockbackDistance);
                arena.AddLine(pc.Position, offsetLocation, arena.ColorDanger);
                arena.Actor(offsetLocation, 0, arena.ColorDanger);
            }
        }

        public override void OnCastStarted(BossModule module, Actor actor)
        {
            if (actor.CastInfo!.IsSpell(AID.DiairesisElegeia))
                _head = actor;
        }

        public override void OnCastFinished(BossModule module, Actor actor)
        {
            if (_head == actor)
                _head = null;
        }

        public override void OnEventCast(BossModule module, CastEvent info)
        {
            if (!info.IsSpell())
                return;
            switch ((AID)info.Action.ID)
            {
                case AID.FatalismFieryStar1:
                    AddPlanet(module, info.CasterID, false, true);
                    break;
                case AID.FatalismFieryStar2:
                case AID.FieryStarVisual:
                    AddPlanet(module, info.CasterID, false, false);
                    break;
                case AID.FatalismAzureStar1:
                    AddPlanet(module, info.CasterID, true, true);
                    break;
                case AID.FatalismAzureStar2:
                case AID.AzureStarVisual:
                    AddPlanet(module, info.CasterID, true, false);
                    break;
                case AID.RubistellarCollision:
                case AID.FatalismRubistallarCollisionAOE:
                    if (_planetsFiery.Count > 0)
                        _planetsFiery.RemoveAt(0);
                    else
                        module.ReportError(this, "Unexpected fiery cast, no casters available");
                    break;
                case AID.CaerustellarCollision:
                case AID.FatalismCaerustallarCollisionAOE:
                    if (_planetsAzure.Count > 0)
                        _planetsAzure.RemoveAt(0);
                    else
                        module.ReportError(this, "Unexpected azure cast, no casters available");
                    break;
            }
        }

        private void AddPlanet(BossModule module, uint casterID, bool azure, bool firstOfPair)
        {
            var caster = module.WorldState.Actors.Find(casterID);
            if (caster != null)
            {
                var origin = module.Arena.WorldCenter + _planetOffset * GeometryUtils.DirectionToVec3(caster.Rotation);
                var planets = azure ? _planetsAzure : _planetsFiery;
                int index = firstOfPair ? 0 : planets.Count;
                planets.Insert(index, origin);
            }
        }
    }
}
