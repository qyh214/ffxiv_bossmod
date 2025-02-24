﻿using System;
using System.Collections.Generic;

namespace BossMod
{
    public class CastEvent
    {
        public struct Target
        {
            public uint ID;
            public ActionEffects Effects;
        }

        public uint CasterID;
        public uint MainTargetID; // note that actual affected targets could be completely different
        public ActionID Action;
        public float AnimationLockTime;
        public uint MaxTargets;
        public List<Target> Targets = new();
        public uint SourceSequence;

        public bool IsSpell() => Action.Type == ActionType.Spell;
        public bool IsSpell<AID>(AID aid) where AID : Enum => Action == ActionID.MakeSpell(aid);
    }

    // dispatcher for instant world events; part of the world state structure
    public class WorldEvents
    {
        public event EventHandler<(uint actorID, uint iconID)>? Icon; // TODO: this should really be an actor field, but I have no idea what triggers icon clear...
        public void DispatchIcon((uint actorID, uint iconID) args)
        {
            Icon?.Invoke(this, args);
        }

        public event EventHandler<CastEvent>? Cast;
        public void DispatchCast(CastEvent info)
        {
            Cast?.Invoke(this, info);
        }

        public event EventHandler<(uint featureID, byte index, uint state)>? EnvControl;
        public void DispatchEnvControl((uint featureID, byte index, uint state) args)
        {
            EnvControl?.Invoke(this, args);
        }
    }
}
