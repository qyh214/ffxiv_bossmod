﻿using Dalamud.Game.ClientState.JobGauge.Types;
using Dalamud.Game.ClientState.Objects.Enums;
using ImGuiNET;
using System.Linq;

namespace BossMod
{
    class WARActions : CommonActions
    {
        private WARConfig _config;
        private WARRotation.State _state;
        private WARRotation.Strategy _strategy;
        private ActionID _nextBestSTAction = ActionID.MakeSpell(WARRotation.AID.HeavySwing);
        private ActionID _nextBestAOEAction = ActionID.MakeSpell(WARRotation.AID.Overpower);

        public WARActions(Autorotation autorot)
            : base(autorot)
        {
            _config = autorot.Config.Get<WARConfig>();
            _state = BuildState();
            _strategy = new()
            {
                FirstChargeIn = 0.01f, // by default, always preserve 1 onslaught charge
                SecondChargeIn = 10000, // ... but don't preserve second
            };

            SmartQueueRegisterSpell(WARRotation.AID.Rampart);
            SmartQueueRegisterSpell(WARRotation.AID.Vengeance);
            SmartQueueRegisterSpell(WARRotation.AID.ThrillOfBattle);
            SmartQueueRegisterSpell(WARRotation.AID.Holmgang);
            SmartQueueRegisterSpell(WARRotation.AID.Equilibrium);
            SmartQueueRegisterSpell(WARRotation.AID.Reprisal);
            SmartQueueRegisterSpell(WARRotation.AID.ShakeItOff);
            SmartQueueRegisterSpell(WARRotation.AID.RawIntuition);
            SmartQueueRegisterSpell(WARRotation.AID.NascentFlash);
            SmartQueueRegisterSpell(WARRotation.AID.Bloodwhetting);
            SmartQueueRegisterSpell(WARRotation.AID.ArmsLength);
            SmartQueueRegisterSpell(WARRotation.AID.Provoke);
            SmartQueueRegisterSpell(WARRotation.AID.Shirk);
            SmartQueueRegister(CommonRotation.IDSprint);
            SmartQueueRegister(WARRotation.IDStatPotion);
        }

        protected override void OnCastSucceeded(ActionID actionID)
        {
            string comment = "";
            if (actionID.Type == ActionType.Spell)
            {
                switch ((WARRotation.AID)actionID.ID)
                {
                    case WARRotation.AID.HeavySwing:
                        if (_state.ComboLastMove == WARRotation.AID.HeavySwing || _state.ComboLastMove == WARRotation.AID.Maim)
                            comment += $", mistake=wrong-combo({_state.ComboLastMove})";
                        if (_state.InnerReleaseStacks > 0)
                            comment += $", mistake=wasted-ir-stack";
                        break;
                    case WARRotation.AID.Maim:
                        if (_state.ComboLastMove != WARRotation.AID.HeavySwing)
                            comment += $", mistake=wrong-combo({_state.ComboLastMove})";
                        if (_state.InnerReleaseStacks > 0)
                            comment += $", mistake=wasted-ir-stack";
                        if (_state.Gauge > 90)
                            comment += $", mistake=overcap-gauge";
                        break;
                    case WARRotation.AID.StormPath:
                        if (_state.ComboLastMove != WARRotation.AID.Maim)
                            comment += $", mistake=wrong-combo({_state.ComboLastMove})";
                        if (_state.InnerReleaseStacks > 0)
                            comment += $", mistake=wasted-ir-stack";
                        if (_state.Gauge > 80)
                            comment += $", mistake=overcap-gauge";
                        if (_state.SurgingTempestLeft <= 0)
                            comment += $", mistake=no-st";
                        break;
                    case WARRotation.AID.StormEye:
                        if (_state.ComboLastMove != WARRotation.AID.Maim)
                            comment += $", mistake=wrong-combo({_state.ComboLastMove})";
                        if (_state.InnerReleaseStacks > 0)
                            comment += $", mistake=wasted-ir-stack";
                        if (_state.Gauge > 90)
                            comment += $", mistake=overcap-gauge";
                        if (_state.SurgingTempestLeft > 30)
                            comment += $", mistake=overcap-st";
                        break;
                    case WARRotation.AID.FellCleave:
                        comment += _state.InnerReleaseStacks > 0 ? ", spent IR stack" : ", spent gauge";
                        if (_state.InfuriateCD < 5)
                            comment += $", mistake=overcap-infuriate";
                        if (_state.SurgingTempestLeft <= 0)
                            comment += $", mistake=no-st";
                        break;
                    case WARRotation.AID.InnerChaos:
                        if (_state.InnerReleaseStacks > 0)
                            comment += $", mistake=wasted-ir-stack";
                        if (_state.InfuriateCD < 5)
                            comment += $", mistake=overcap-infuriate";
                        if (_state.SurgingTempestLeft <= 0)
                            comment += $", mistake=no-st";
                        break;
                    case WARRotation.AID.Overpower:
                        if (_state.ComboLastMove == WARRotation.AID.Overpower)
                            comment += $", mistake=wrong-combo({_state.ComboLastMove})";
                        if (_state.InnerReleaseStacks > 0)
                            comment += $", mistake=wasted-ir-stack";
                        break;
                    case WARRotation.AID.MythrilTempest:
                        if (_state.ComboLastMove != WARRotation.AID.Overpower)
                            comment += $", mistake=wrong-combo({_state.ComboLastMove})";
                        if (_state.InnerReleaseStacks > 0)
                            comment += $", mistake=wasted-ir-stack";
                        if (_state.Gauge > 80)
                            comment += $", mistake=overcap-gauge";
                        break;
                    case WARRotation.AID.Infuriate:
                        if (_state.Gauge > 50)
                            comment += $", mistake=overcap-gauge";
                        if (_state.NascentChaosLeft > 0)
                            comment += $", mistake=overwrite-nc";
                        if (_state.InnerReleaseStacks > 0)
                            comment += $", mistake=infuriate-under-ir";
                        break;
                    case WARRotation.AID.Onslaught:
                        // note: onslaught without ST is not really a mistake...
                        break;
                    case WARRotation.AID.Upheaval:
                        if (_state.SurgingTempestLeft <= 0)
                            comment += $", mistake=no-st";
                        break;
                    case WARRotation.AID.InnerRelease:
                        if (_state.SurgingTempestLeft <= 0)
                            comment += $", mistake=no-st";
                        if (_state.NascentChaosLeft > 0)
                            comment += $", mistake=ir-under-nc";
                        if (_state.SurgingTempestLeft > 50)
                            comment += $", mistake=overcap-st";
                        break;
                    case WARRotation.AID.Tomahawk:
                        if (_state.InnerReleaseStacks > 0)
                            comment += $", mistake=wasted-ir-stack";
                        break;
                }
            }
            Log($"Cast {actionID}, next-best={_nextBestSTAction}/{_nextBestAOEAction}{comment} [{_state}]");
        }

        protected override CommonRotation.State OnUpdate()
        {
            var currState = BuildState();
            LogStateChange(_state, currState);
            _state = currState;

            FillCommonStrategy(_strategy, WARRotation.IDStatPotion);

            // cooldown execution
            _strategy.ExecuteRampart = SmartQueueActiveSpell(WARRotation.AID.Rampart);
            _strategy.ExecuteVengeance = SmartQueueActiveSpell(WARRotation.AID.Vengeance);
            _strategy.ExecuteThrillOfBattle = SmartQueueActiveSpell(WARRotation.AID.ThrillOfBattle);
            _strategy.ExecuteHolmgang = SmartQueueActiveSpell(WARRotation.AID.Holmgang);
            _strategy.ExecuteEquilibrium = SmartQueueActiveSpell(WARRotation.AID.Equilibrium) && Service.ClientState.LocalPlayer?.CurrentHp < Service.ClientState.LocalPlayer?.MaxHp;
            _strategy.ExecuteReprisal = SmartQueueActiveSpell(WARRotation.AID.Reprisal) && AllowReprisal();
            _strategy.ExecuteShakeItOff = SmartQueueActiveSpell(WARRotation.AID.ShakeItOff); // TODO: check that raid is in range?...
            _strategy.ExecuteBloodwhetting = SmartQueueActiveSpell(WARRotation.AID.RawIntuition) || SmartQueueActiveSpell(WARRotation.AID.Bloodwhetting); // TODO: consider auto-use?..
            _strategy.ExecuteNascentFlash = SmartQueueActiveSpell(WARRotation.AID.NascentFlash);
            _strategy.ExecuteArmsLength = SmartQueueActiveSpell(WARRotation.AID.ArmsLength);
            _strategy.ExecuteProvoke = SmartQueueActiveSpell(WARRotation.AID.Provoke); // TODO: check that not MT already
            _strategy.ExecuteShirk = SmartQueueActiveSpell(WARRotation.AID.Shirk); // TODO: check that hate is close to MT...

            var nextBestST = _config.FullRotation ? WARRotation.GetNextBestAction(_state, _strategy, false) : ActionID.MakeSpell(WARRotation.AID.HeavySwing);
            var nextBestAOE = _config.FullRotation ? WARRotation.GetNextBestAction(_state, _strategy, true) : ActionID.MakeSpell(WARRotation.AID.Overpower);
            if (_nextBestSTAction != nextBestST || _nextBestAOEAction != nextBestAOE)
            {
                Log($"Next-best changed: ST={_nextBestSTAction}->{nextBestST}, AOE={_nextBestAOEAction}->{nextBestAOE} [{_state}]");
                _nextBestSTAction = nextBestST;
                _nextBestAOEAction = nextBestAOE;
            }
            return _state;
        }

        protected override (ActionID, uint) DoReplaceActionAndTarget(ActionID actionID, Targets targets)
        {
            if (actionID.Type == ActionType.Spell)
            {
                actionID = (WARRotation.AID)actionID.ID switch
                {
                    WARRotation.AID.HeavySwing => _config.FullRotation ? _nextBestSTAction : actionID,
                    WARRotation.AID.Overpower => _config.FullRotation ? _nextBestAOEAction : actionID,
                    WARRotation.AID.Maim => _config.STCombos ? ActionID.MakeSpell(WARRotation.GetNextMaimComboAction(_state.ComboLastMove)) : actionID,
                    WARRotation.AID.StormEye => _config.STCombos ? ActionID.MakeSpell(WARRotation.GetNextSTComboAction(_state.ComboLastMove, WARRotation.AID.StormEye)) : actionID,
                    WARRotation.AID.StormPath => _config.STCombos ? ActionID.MakeSpell(WARRotation.GetNextSTComboAction(_state.ComboLastMove, WARRotation.AID.StormPath)) : actionID,
                    WARRotation.AID.MythrilTempest => _config.AOECombos ? ActionID.MakeSpell(WARRotation.GetNextAOEComboAction(_state.ComboLastMove)) : actionID,
                    _ => actionID
                };
            }
            uint targetID = actionID.Type == ActionType.Spell ? (WARRotation.AID)actionID.ID switch
            {
                WARRotation.AID.NascentFlash or WARRotation.AID.Shirk => SmartTargetNascentFlashShirk(actionID, targets),
                WARRotation.AID.Holmgang => _config.HolmgangSelf ? Service.ClientState.LocalPlayer?.ObjectId ?? targets.MainTarget : targets.MainTarget,
                _ => targets.MainTarget
            } : targets.MainTarget;
            return (actionID, targetID);
        }

        public override void DrawOverlay()
        {
            ImGui.TextUnformatted($"Next: {WARRotation.ActionShortString(_nextBestSTAction)} / {WARRotation.ActionShortString(_nextBestAOEAction)}");
            ImGui.TextUnformatted(_strategy.ToString());
            ImGui.TextUnformatted($"Raidbuffs: {_state.RaidBuffsLeft:f2}s left, next in {_strategy.RaidBuffsIn:f2}s");
            ImGui.TextUnformatted($"Downtime: {_strategy.FightEndIn:f2}s, pos-lock: {_strategy.PositionLockIn:f2}");
            ImGui.TextUnformatted($"GCD={_state.GCD:f3}, AnimLock={_state.AnimationLock:f3}+{_state.AnimationLockDelay:f3}");
        }

        private WARRotation.State BuildState()
        {
            WARRotation.State s = new();
            FillCommonState(s, WARRotation.AID.HeavySwing, WARRotation.IDStatPotion);
            if (Service.ClientState.LocalPlayer != null)
            {
                s.Gauge = Service.JobGauges.Get<WARGauge>().BeastGauge;

                foreach (var status in Service.ClientState.LocalPlayer.StatusList)
                {
                    switch ((WARRotation.SID)status.StatusId)
                    {
                        case WARRotation.SID.SurgingTempest:
                            s.SurgingTempestLeft = StatusDuration(status.RemainingTime);
                            break;
                        case WARRotation.SID.NascentChaos:
                            s.NascentChaosLeft = StatusDuration(status.RemainingTime);
                            break;
                        case WARRotation.SID.InnerRelease:
                            s.InnerReleaseLeft = StatusDuration(status.RemainingTime);
                            s.InnerReleaseStacks = status.StackCount;
                            break;
                        case WARRotation.SID.PrimalRend:
                            s.PrimalRendLeft = StatusDuration(status.RemainingTime);
                            break;
                    }
                }

                s.InfuriateCD = SpellCooldown(WARRotation.AID.Infuriate);
                s.UpheavalCD = SpellCooldown(WARRotation.AID.Upheaval);
                s.InnerReleaseCD = SpellCooldown(WARRotation.AID.InnerRelease);
                s.OnslaughtCD = SpellCooldown(WARRotation.AID.Onslaught);
                s.RampartCD = SpellCooldown(WARRotation.AID.Rampart);
                s.VengeanceCD = SpellCooldown(WARRotation.AID.Vengeance);
                s.ThrillOfBattleCD = SpellCooldown(WARRotation.AID.ThrillOfBattle);
                s.HolmgangCD = SpellCooldown(WARRotation.AID.Holmgang);
                s.EquilibriumCD = SpellCooldown(WARRotation.AID.Equilibrium);
                s.ReprisalCD = SpellCooldown(WARRotation.AID.Reprisal);
                s.ShakeItOffCD = SpellCooldown(WARRotation.AID.ShakeItOff);
                s.BloodwhettingCD = SpellCooldown(WARRotation.AID.Bloodwhetting);
                s.ArmsLengthCD = SpellCooldown(WARRotation.AID.ArmsLength);
                s.ProvokeCD = SpellCooldown(WARRotation.AID.Provoke);
                s.ShirkCD = SpellCooldown(WARRotation.AID.Shirk);
            }
            return s;
        }

        private void LogStateChange(WARRotation.State prev, WARRotation.State curr)
        {
            // do nothing if not in combat
            if (Service.ClientState.LocalPlayer == null || !Service.ClientState.LocalPlayer.StatusFlags.HasFlag(StatusFlags.InCombat))
                return;

            // detect expired buffs
            if (curr.InnerReleaseLeft == 0 && prev.InnerReleaseLeft != 0 && prev.InnerReleaseLeft < 1)
                Log($"Expired IR [{curr}]");
            if (curr.NascentChaosLeft == 0 && prev.NascentChaosLeft != 0 && prev.NascentChaosLeft < 1)
                Log($"Expired NC [{curr}]");
            if (curr.PrimalRendLeft == 0 && prev.PrimalRendLeft != 0 && prev.PrimalRendLeft < 1)
                Log($"Expired PR [{curr}]");
            if (curr.SurgingTempestLeft == 0 && prev.SurgingTempestLeft != 0 && prev.SurgingTempestLeft < 1)
                Log($"Expired ST [{curr}]");
            if (curr.ComboTimeLeft == 0 && prev.ComboTimeLeft != 0 && prev.ComboTimeLeft < 1)
                Log($"Expired combo [{curr}]");
        }

        // shirk/nascent flash smart targeting: target if friendly > mouseover if friendly > other tank
        private uint SmartTargetNascentFlashShirk(ActionID action, Targets targets)
        {
            targets = SmartQueueTarget(action, targets);
            var target = SmartTargetFriendly(targets, _config.SmartNascentFlashShirkTarget);
            if (target != null)
                return target.InstanceID;

            if (_config.SmartNascentFlashShirkTarget)
            {
                target = Autorot.Bossmods.WorldState.Party.WithoutSlot().FirstOrDefault(a => a.InstanceID != Service.ClientState.LocalPlayer?.ObjectId && a.Role == Role.Tank);
                if (target != null)
                    return target.InstanceID;
            }

            // can't find good target, deactivate smart-queue entry to prevent silly spam
            Log($"Smart-target failed, removing from queue");
            SmartQueueDeactivate(action);
            return targets.MainTarget;
        }

        // check whether any targetable enemies are in reprisal range (TODO: consider checking only target?..)
        private bool AllowReprisal()
        {
            var playerPos = Service.ClientState.LocalPlayer?.Position ?? new();
            return Service.ObjectTable.Any(o => o.ObjectKind == ObjectKind.BattleNpc && (BattleNpcSubKind)o.SubKind == BattleNpcSubKind.Enemy && Utils.GameObjectIsTargetable(o) && GeometryUtils.PointInCircle(o.Position - playerPos, 5 + o.HitboxRadius));
        }
    }
}
