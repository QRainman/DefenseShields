﻿namespace DefenseShields
{
    using global::DefenseShields.Support;
    using Sandbox.Game.Entities;
    using VRage.Game.ModAPI;
    using VRage.Utils;
    using VRageMath;

    public partial class DefenseShields
    {
        #region Shield Support Blocks
        public void GetModulationInfo()
        {
            var update = false;
            if (ShieldComp.Modulator != null && ShieldComp.Modulator.ModState.State.Online)
            {
                var modEnergyRatio = ShieldComp.Modulator.ModState.State.ModulateEnergy * 0.01f;
                var modKineticRatio = ShieldComp.Modulator.ModState.State.ModulateKinetic * 0.01f;
                if (!DsState.State.ModulateEnergy.Equals(modEnergyRatio) || !DsState.State.ModulateKinetic.Equals(modKineticRatio) || !DsState.State.EmpProtection.Equals(ShieldComp.Modulator.ModSet.Settings.EmpEnabled) || !DsState.State.ReInforce.Equals(ShieldComp.Modulator.ModSet.Settings.ReInforceEnabled)) update = true;
                DsState.State.ModulateEnergy = modEnergyRatio;
                DsState.State.ModulateKinetic = modKineticRatio;
                if (DsState.State.Enhancer)
                {
                    DsState.State.EmpProtection = ShieldComp.Modulator.ModSet.Settings.EmpEnabled;
                    DsState.State.ReInforce = ShieldComp.Modulator.ModSet.Settings.ReInforceEnabled;
                }

                if (update) ShieldChangeState();
            }
            else
            {
                if (!DsState.State.ModulateEnergy.Equals(1f) || !DsState.State.ModulateKinetic.Equals(1f) || DsState.State.EmpProtection || DsState.State.ReInforce) update = true;
                DsState.State.ModulateEnergy = 1f;
                DsState.State.ModulateKinetic = 1f;
                DsState.State.EmpProtection = false;
                DsState.State.ReInforce = false;
                if (update) ShieldChangeState();

            }
        }

        public void GetEnhancernInfo()
        {
            var update = false;
            if (ShieldComp.Enhancer != null && ShieldComp.Enhancer.EnhState.State.Online)
            {
                if (!DsState.State.EnhancerPowerMulti.Equals(2) || !DsState.State.EnhancerProtMulti.Equals(1000) || !DsState.State.Enhancer) update = true;
                DsState.State.EnhancerPowerMulti = 2;
                DsState.State.EnhancerProtMulti = 1000;
                DsState.State.Enhancer = true;
                if (update) ShieldChangeState();
            }
            else
            {
                if (!DsState.State.EnhancerPowerMulti.Equals(1) || !DsState.State.EnhancerProtMulti.Equals(1) || DsState.State.Enhancer) update = true;
                DsState.State.EnhancerPowerMulti = 1;
                DsState.State.EnhancerProtMulti = 1;
                DsState.State.Enhancer = false;
                DsState.State.EmpProtection = false;
                if (!DsState.State.Overload) DsState.State.ReInforce = false;
                if (update) ShieldChangeState();
            }
        }
        #endregion

        public void ShieldDoDamage(float damage, long entityId, float shieldFractionLoss = 0f)
        {
            EmpSize = shieldFractionLoss;
            ImpactSize = damage;

            if (shieldFractionLoss > 0)
            {
                damage = shieldFractionLoss;
            }
            Shield.SlimBlock.DoDamage(damage, Session.Instance.MPdamage, true, null, entityId);
        }

        public void DamageBlock(IMySlimBlock block, float damage, long entityId, MyStringHash damageType)
        {
            block.DoDamage(damage, damageType, true, null, entityId);
        }

        public void DamageBlockEffects(IMySlimBlock block, float damage, long entityId, MyStringHash damageType)
        {
            block.DoDamage(damage, Session.Instance.MpDmgEffect, true, null, entityId);
        }

        internal void AddShieldHit(long attackerId, float amount, MyStringHash damageType, IMySlimBlock block)
        {
            ShieldHit.Amount += amount;
            ShieldHit.DamageType = damageType.String;
            if (ShieldHit.HitPos == Vector3D.Zero)
            {
                if (block.FatBlock != null) ShieldHit.HitPos = block.FatBlock.PositionComp.WorldAABB.Center;
                else block.ComputeWorldCenter(out ShieldHit.HitPos);
            }
            if (attackerId != 0) ShieldHit.AttackerId = attackerId;
            if (amount > 0) _lastSendDamageTick = _tick;
        }

        internal void SendShieldHits()
        {
            while (ProtoShieldHits.Count != 0)
                Session.Instance.PacketizeShieldHit(MyCube, ProtoShieldHits.Dequeue());
        }

        private void ShieldHitReset(bool enQueue)
        {
            if (enQueue)
            {
                if (Session.Enforced.Debug >= 2) Log.Line("enQueue hit");
                if (_isServer)
                {
                    if (_mpActive) ProtoShieldHits.Enqueue(CloneHit());
                    if (!_isDedicated) AddLocalHit();
                }
            }
            if (Session.Enforced.Debug >= 2) Log.Line($"ShieldHitReset - previous wasType:{ShieldHit.DamageType} - Amount:{ShieldHit.Amount} - hitPos:{ShieldHit.HitPos}");
            _lastSendDamageTick = uint.MaxValue;
            ShieldHit.AttackerId = 0;
            ShieldHit.Amount = 0;
            ShieldHit.DamageType = string.Empty;
            ShieldHit.HitPos = Vector3D.Zero;
        }

        private ProtoShieldHit CloneHit()
        {
            var hitClone = new ProtoShieldHit
            {
                Amount = ShieldHit.Amount,
                AttackerId = ShieldHit.AttackerId,
                HitPos = ShieldHit.HitPos,
                DamageType = ShieldHit.DamageType
            };

            return hitClone;
        }

        private void AddLocalHit()
        {
            ShieldHits.Add(new ShieldHit(MyEntities.GetEntityById(ShieldHit.AttackerId), ShieldHit.Amount, MyStringHash.GetOrCompute(ShieldHit.DamageType), ShieldHit.HitPos));
        }
    }
}