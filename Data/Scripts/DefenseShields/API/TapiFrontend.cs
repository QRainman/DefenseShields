﻿using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace DefenseShields
{
    internal class TapiFrontend
    {
        private readonly IMyTerminalBlock _block;

        private readonly Func<IMyTerminalBlock, RayD, long, float, bool, Vector3D?> _rayAttackShield;
        private readonly Func<IMyTerminalBlock, Vector3D, long, float, bool, bool> _pointAttackShield;
        private readonly Func<IMyTerminalBlock, RayD, Vector3D?> _rayIntersectShield;
        private readonly Func<IMyTerminalBlock, Vector3D, bool> _pointInShield;
        private readonly Func<IMyTerminalBlock, float> _getShieldPercent;
        private readonly Func<IMyTerminalBlock, int> _getShieldHeat;
        private readonly Action<IMyTerminalBlock, int> _setShieldHeat;
        private readonly Action<IMyTerminalBlock> _overLoad;
        private readonly Func<IMyTerminalBlock, float> _getChargeRate;
        private readonly Func<IMyTerminalBlock, int> _hpToChargeRatio;
        private readonly Func<IMyTerminalBlock, float> _getMaxCharge;
        private readonly Func<IMyTerminalBlock, float> _getCharge;
        private readonly Action<IMyTerminalBlock, float> _setCharge;
        private readonly Func<IMyTerminalBlock, float> _getPowerUsed;
        private readonly Func<IMyTerminalBlock, float> _getPowerCap;
        private readonly Func<IMyTerminalBlock, float> _getMaxHpCap;
        private readonly Func<IMyTerminalBlock, bool> _isShieldUp;
        private readonly Func<IMyTerminalBlock, string> _shieldStatus;
        private readonly Func<IMyCubeGrid, bool> _gridHasShield;
        private readonly Func<IMyCubeGrid, bool> _gridShieldOnline;
        private readonly Func<IMyEntity, bool> _protectedByShield;
        private readonly Func<IMyEntity, IMyTerminalBlock> _getShieldBlock;

        public TapiFrontend(IMyTerminalBlock block)
        {
            _block = block;
            var delegates = _block.GetProperty("DefenseSystemsAPI")?.As<Dictionary<string, Delegate>>().GetValue(_block);
            if (delegates == null) return;

            _rayAttackShield = (Func<IMyTerminalBlock, RayD, long, float, bool, Vector3D?>)delegates["RayAttackShield"];
            _pointAttackShield = (Func<IMyTerminalBlock, Vector3D, long, float, bool, bool>)delegates["PointAttackShield"];
            _rayIntersectShield = (Func<IMyTerminalBlock, RayD, Vector3D?>)delegates["RayIntersectShield"];
            _pointInShield = (Func<IMyTerminalBlock, Vector3D, bool>)delegates["PointInShield"];
            _getShieldPercent = (Func<IMyTerminalBlock, float>)delegates["GetShieldPercent"];
            _getShieldHeat = (Func<IMyTerminalBlock, int>)delegates["GetShieldHeat"];
            _setShieldHeat = (Action<IMyTerminalBlock, int>)delegates["SetShieldHeat"];
            _overLoad = (Action<IMyTerminalBlock>)delegates["OverLoadShield"];
            _getChargeRate = (Func<IMyTerminalBlock, float>)delegates["GetChargeRate"];
            _hpToChargeRatio = (Func<IMyTerminalBlock, int>)delegates["HpToChargeRatio"];
            _getMaxCharge = (Func<IMyTerminalBlock, float>)delegates["GetMaxCharge"];
            _getCharge = (Func<IMyTerminalBlock, float>)delegates["GetCharge"];
            _setCharge = (Action<IMyTerminalBlock, float>)delegates["SetCharge"];
            _getPowerUsed = (Func<IMyTerminalBlock, float>)delegates["GetPowerUsed"];
            _getPowerCap = (Func<IMyTerminalBlock, float>)delegates["GetPowerCap"];
            _getMaxHpCap = (Func<IMyTerminalBlock, float>)delegates["GetMaxHpCap"];
            _isShieldUp = (Func<IMyTerminalBlock, bool>)delegates["IsShieldUp"];
            _shieldStatus = (Func<IMyTerminalBlock, string>)delegates["ShieldStatus"];
            _gridHasShield = (Func<IMyCubeGrid, bool>)delegates["GridHasShield"];
            _gridShieldOnline = (Func<IMyCubeGrid, bool>)delegates["GridShieldOnline"];
            _protectedByShield = (Func<IMyEntity, bool>)delegates["ProtectedByShield"];
            _getShieldBlock = (Func<IMyEntity, IMyTerminalBlock>)delegates["GetShieldBlock"];
        }

        public Vector3D? RayAttackShield(RayD ray, long attackerId, float damage, bool energy = false) =>
            _rayAttackShield?.Invoke(_block, ray, attackerId, damage, energy) ?? null;
        public bool PointAttackShield(Vector3D pos, long attackerId, float damage, bool energy = false) =>
            _pointAttackShield?.Invoke(_block, pos, attackerId, damage, energy) ?? false;
        public Vector3D? RayIntersectShield(RayD ray) => _rayIntersectShield?.Invoke(_block, ray) ?? null;
        public bool PointInShield(Vector3D pos) => _pointInShield?.Invoke(_block, pos) ?? false;
        public float GetShieldPercent() => _getShieldPercent?.Invoke(_block) ?? -1;
        public float GetShieldHeat() => _getShieldHeat?.Invoke(_block) ?? -1;
        public void SetShieldHeat(int value) => _setShieldHeat?.Invoke(_block, value);
        public void OverLoadShield() => _overLoad?.Invoke(_block);
        public float GetChargeRate() => _getChargeRate?.Invoke(_block) ?? -1;
        public float HpToChargeRatio() => _hpToChargeRatio?.Invoke(_block) ?? -1;
        public float GetMaxCharge() => _getMaxCharge?.Invoke(_block) ?? -1;
        public float GetCharge() => _getCharge?.Invoke(_block) ?? -1;
        public void SetCharge(float value) => _setCharge.Invoke(_block, value);
        public float GetPowerUsed() => _getPowerUsed?.Invoke(_block) ?? -1;
        public float GetPowerCap() => _getPowerCap?.Invoke(_block) ?? -1;
        public float GetMaxHpCap() => _getMaxHpCap?.Invoke(_block) ?? -1;
        public bool IsShieldUp() => _isShieldUp?.Invoke(_block) ?? false;
        public string ShieldStatus() => _shieldStatus?.Invoke(_block) ?? string.Empty;
        public bool GridHasShield(IMyCubeGrid grid) => _gridHasShield?.Invoke(grid) ?? false;
        public bool GridShieldOnline(IMyCubeGrid grid) => _gridShieldOnline?.Invoke(grid) ?? false;
        public bool ProtectedByShield(IMyEntity entity) => _protectedByShield?.Invoke(entity) ?? false;
        public IMyTerminalBlock GetShieldBlock(IMyEntity entity) => _getShieldBlock?.Invoke(entity) ?? null;
    }
}