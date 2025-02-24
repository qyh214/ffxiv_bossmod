﻿using System;

namespace BossMod.Endwalker.Alliance.A4Naldthal
{
    class GoldenTenet : CommonComponents.SharedTankbuster
    {
        public GoldenTenet() : base(ActionID.MakeSpell(AID.GoldenTenetAOE), 6) { }
    }

    class StygianTenet : CommonComponents.SpreadFromCastTargets
    {
        public StygianTenet() : base(ActionID.MakeSpell(AID.StygianTenetAOE), 6) { }
    }

    class FlamesOfTheDead : CommonComponents.SelfTargetedAOE
    {
        public FlamesOfTheDead() : base(ActionID.MakeSpell(AID.FlamesOfTheDeadReal), new AOEShapeDonut(8, 30)) { }
    }

    class LivingHeat : CommonComponents.SelfTargetedAOE
    {
        public LivingHeat() : base(ActionID.MakeSpell(AID.LivingHeatReal), new AOEShapeCircle(8)) { }
    }

    class HellOfFireFront : CommonComponents.SelfTargetedAOE
    {
        public HellOfFireFront() : base(ActionID.MakeSpell(AID.HellOfFireFrontAOE), new AOEShapeCone(60, MathF.PI / 2)) { }
    }

    class HellOfFireBack : CommonComponents.SelfTargetedAOE
    {
        public HellOfFireBack() : base(ActionID.MakeSpell(AID.HellOfFireBackAOE), new AOEShapeCone(60, MathF.PI / 2)) { }
    }

    class WaywardSoul : CommonComponents.SelfTargetedAOE
    {
        public WaywardSoul() : base(ActionID.MakeSpell(AID.WaywardSoulAOE), new AOEShapeCircle(18), 3) { }
    }

    class Twingaze : CommonComponents.SelfTargetedAOE
    {
        public Twingaze() : base(ActionID.MakeSpell(AID.Twingaze), new AOEShapeCone(60, MathF.PI / 12)) { }
    }

    // TODO: balancing counter, magmatic spell raid stack
    public class A4Naldthal : BossModule
    {
        public A4Naldthal(BossModuleManager manager, Actor primary)
            : base(manager, primary, true)
        {
            Arena.IsCircle = true;
            Arena.WorldCenter = new(750, -932, -750);
            Arena.WorldHalfSize = 25;

            var sb = new StateMachineBuilder(this);
            var s = sb.Simple(0, 600, "Fight")
                .ActivateOnEnter<GoldenTenet>()
                .ActivateOnEnter<StygianTenet>()
                .ActivateOnEnter<FlamesOfTheDead>()
                .ActivateOnEnter<LivingHeat>()
                .ActivateOnEnter<HeavensTrial>()
                .ActivateOnEnter<DeepestPit>()
                .ActivateOnEnter<OnceAboveEverBelow>()
                .ActivateOnEnter<HellOfFireFront>()
                .ActivateOnEnter<HellOfFireBack>()
                .ActivateOnEnter<WaywardSoul>()
                .ActivateOnEnter<FortuneFlux>()
                .ActivateOnEnter<Twingaze>()
                .DeactivateOnExit<GoldenTenet>()
                .DeactivateOnExit<StygianTenet>()
                .DeactivateOnExit<FlamesOfTheDead>()
                .DeactivateOnExit<LivingHeat>()
                .DeactivateOnExit<HeavensTrial>()
                .DeactivateOnExit<DeepestPit>()
                .DeactivateOnExit<OnceAboveEverBelow>()
                .DeactivateOnExit<HellOfFireFront>()
                .DeactivateOnExit<HellOfFireBack>()
                .DeactivateOnExit<WaywardSoul>()
                .DeactivateOnExit<FortuneFlux>()
                .DeactivateOnExit<Twingaze>();
            s.Raw.Update = _ => PrimaryActor.IsDead ? s.Raw.Next : null;
            sb.Simple(1, 0, "???");
            InitStates(sb.Initial);
            //InitStates(new A4NaldthalStates(this).Initial);
        }

        protected override void DrawArenaForegroundPre(int pcSlot, Actor pc)
        {
            foreach (var p in WorldState.Actors)
                if (p.Type == ActorType.Player && !p.IsDead)
                    Arena.Actor(p, Arena.ColorPlayerGeneric);
        }

        protected override void DrawArenaForegroundPost(int pcSlot, Actor pc)
        {
            Arena.Actor(PrimaryActor, Arena.ColorEnemy);
            Arena.Actor(pc, Arena.ColorPC);
        }
    }
}
