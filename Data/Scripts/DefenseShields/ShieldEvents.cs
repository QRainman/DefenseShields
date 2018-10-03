﻿using System;
using System.Text;
using DefenseShields.Support;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game.ModAPI;

namespace DefenseShields
{
    public partial class DefenseShields
    {
        private void RegisterEvents(bool register = true)
        {
            if (register)
            {
                ((MyCubeGrid)Shield.CubeGrid).OnHierarchyUpdated += HierarchyChanged;
                ((MyCubeGrid)Shield.CubeGrid).OnBlockAdded += BlockAdded;
                ((MyCubeGrid)Shield.CubeGrid).OnBlockRemoved += BlockRemoved;
                ((MyCubeGrid)Shield.CubeGrid).OnFatBlockAdded += FatBlockAdded;
                ((MyCubeGrid)Shield.CubeGrid).OnFatBlockRemoved += FatBlockRemoved;
                ((MyCubeGrid)Shield.CubeGrid).OnGridSplit += GridSplit;
                Shield.AppendingCustomInfo += AppendingCustomInfo;
            }
            else
            {
                ((MyCubeGrid)Shield.CubeGrid).OnHierarchyUpdated -= HierarchyChanged;
                ((MyCubeGrid)Shield.CubeGrid).OnBlockAdded -= BlockAdded;
                ((MyCubeGrid)Shield.CubeGrid).OnBlockRemoved -= BlockRemoved;
                ((MyCubeGrid)Shield.CubeGrid).OnFatBlockAdded -= FatBlockAdded;
                ((MyCubeGrid)Shield.CubeGrid).OnFatBlockRemoved -= FatBlockRemoved;
                ((MyCubeGrid)Shield.CubeGrid).OnGridSplit -= GridSplit;
                Shield.AppendingCustomInfo -= AppendingCustomInfo;
            }
        }

        private void GridSplit(MyCubeGrid oldGrid, MyCubeGrid newGrid)
        {
            newGrid.RecalculateOwners();
            ShieldGridComponent oldComp;
            ShieldGridComponent newComp;
            oldGrid.Components.TryGet(out oldComp);
            newGrid.Components.TryGet(out newComp);
            if (oldComp == null) ResetComp();
            Log.Line($"oNull:{oldComp?.DefenseShields == null} - nNull:{newComp?.DefenseShields == null} - neweqGrid:{newComp?.DefenseShields?.Shield?.CubeGrid == newGrid}/{newComp?.DefenseShields?.Shield?.CubeGrid == oldGrid} - oldeqGrid:{oldComp?.DefenseShields?.Shield?.CubeGrid == oldGrid}/{oldComp?.DefenseShields?.Shield?.CubeGrid == newGrid}");
            if (oldComp?.DefenseShields != null)
            {
                //ShieldComp.DefenseShields = null;
                //ShieldComp.StationEmitter = null;
                //ShieldComp.DefenseShields = null;
            }
        }

        private void HierarchyChanged(MyCubeGrid myCubeGrid = null)
        {
            try
            {
                if (ShieldComp == null) return;
                _subUpdate = true;
            }
            catch (Exception ex) { Log.Line($"Exception in Controller HierarchyChanged: {ex}"); }
        }

        private void BlockAdded(IMySlimBlock mySlimBlock)
        {
            try
            {
                _blockAdded = true;
                _blockChanged = true;
                if (_isServer) DsState.State.GridIntegrity += mySlimBlock.MaxIntegrity;
            }
            catch (Exception ex) { Log.Line($"Exception in Controller BlockAdded: {ex}"); }
        }

        private void BlockRemoved(IMySlimBlock mySlimBlock)
        {
            try
            {
                _blockRemoved = true;
                _blockChanged = true;
                if (_isServer) DsState.State.GridIntegrity -= mySlimBlock.MaxIntegrity;
            }
            catch (Exception ex) { Log.Line($"Exception in Controller BlockRemoved: {ex}"); }
        }

        private void FatBlockAdded(MyCubeBlock myCubeBlock)
        {
            try
            {
                _functionalAdded = true;
                _functionalChanged = true;
                if (MyGridDistributor == null)
                {
                    var controller = myCubeBlock as MyShipController;
                    if (controller != null)
                        if (controller.GridResourceDistributor.SourcesEnabled != MyMultipleEnabledEnum.NoObjects) _updateGridDistributor = true;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in Controller FatBlockAdded: {ex}"); }
        }

        private void FatBlockRemoved(MyCubeBlock myCubeBlock)
        {
            try
            {
                _functionalRemoved = true;
                _functionalChanged = true;
            }
            catch (Exception ex) { Log.Line($"Exception in Controller FatBlockRemoved: {ex}"); }
        }

        private string GetShieldStatus()
        {
            if (!DsState.State.Online && (!Shield.IsWorking || !Shield.IsFunctional)) return "[Controller Failure]";
            if (!DsState.State.Online && DsState.State.NoPower) return "[Insufficient Power]";
            if (!DsState.State.Online && DsState.State.Overload) return "[Overloaded]";
            if (!DsState.State.ControllerGridAccess) return "[Invalid Owner]";
            if (DsState.State.Waking) return "[Coming Online]";
            if (DsState.State.Suspended || DsState.State.Mode == 4) return "[Controller Standby]";
            if (DsState.State.Lowered) return "[Shield Down]";
            if (!DsState.State.EmitterWorking) return "[Emitter Failure]";
            if (DsState.State.Sleeping) return "[Suspended]";
            if (!DsState.State.Online) return "[Shield Offline]";
            return "[Shield Up]";
        }

        private void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            try
            {
                var secToFull = 0;
                var shieldPercent = !DsState.State.Online ? 0f : 100f;

                if (DsState.State.Buffer < ShieldMaxBuffer) shieldPercent = DsState.State.Buffer / ShieldMaxBuffer * 100;
                if (_shieldChargeRate > 0)
                {
                    var toMax = ShieldMaxBuffer - DsState.State.Buffer;
                    var secs = toMax / _shieldChargeRate;
                    if (secs.Equals(1)) secToFull = 0;
                    else secToFull = (int)secs;
                }

                var shieldPowerNeeds = _powerNeeded;
                var powerUsage = shieldPowerNeeds;
                var otherPower = _otherPower;
                var gridMaxPower = _gridMaxPower;
                if (!DsSet.Settings.UseBatteries)
                {
                    powerUsage = powerUsage + _batteryCurrentPower;
                    otherPower = _otherPower + _batteryCurrentPower;
                    gridMaxPower = gridMaxPower + _batteryMaxPower;
                }
                var status = GetShieldStatus();
                if (status == "[Shield Up]" || status == "[Shield Down]" || status == "[Shield Offline]")
                {
                    stringBuilder.Append(status + " MaxHP: " + (ShieldMaxBuffer * Session.Enforced.Efficiency).ToString("N0") +
                                         "\n" +
                                         "\n[Shield HP__]: " + (DsState.State.Buffer * Session.Enforced.Efficiency).ToString("N0") + " (" + shieldPercent.ToString("0") + "%)" +
                                         "\n[HP Per Sec_]: " + (_shieldChargeRate * Session.Enforced.Efficiency).ToString("N0") +
                                         "\n[Damage In__]: " + _damageReadOut.ToString("N0") +
                                         "\n[Charge Rate]: " + _shieldChargeRate.ToString("0.0") + " Mw" +
                                         "\n[Full Charge_]: " + secToFull.ToString("N0") + "s" +
                                         "\n[Over Heated]: " + DsState.State.Heat.ToString("0") + "%" +
                                         "\n[Maintenance]: " + _shieldMaintaintPower.ToString("0.0") + " Mw" +
                                         "\n[Power Usage]: " + powerUsage.ToString("0.0") + " (" + gridMaxPower.ToString("0.0") + ")Mw" +
                                         "\n[Shield Power]: " + Sink.CurrentInputByType(GId).ToString("0.0") + " Mw");
                }
                else
                {
                    stringBuilder.Append("Shield Status " + status +
                                         "\n" +
                                         "\n[Maintenance]: " + _shieldMaintaintPower.ToString("0.0") + " Mw" +
                                         "\n[Other Power]: " + otherPower.ToString("0.0") + " Mw" +
                                         "\n[HP Stored]: " + (DsState.State.Buffer * Session.Enforced.Efficiency).ToString("N0") + " (" + shieldPercent.ToString("0") + "%)" +
                                         "\n[Needed Power]: " + shieldPowerNeeds.ToString("0.0") + " (" + gridMaxPower.ToString("0.0") + ") Mw" +
                                         "\n[Emitter Detected]: " + DsState.State.EmitterWorking +
                                         "\n" +
                                         "\n[Grid Owns Controller]: " + DsState.State.IsOwner +
                                         "\n[In Grid's Faction]: " + DsState.State.InFaction);
                }
            }
            catch (Exception ex) { Log.Line($"Exception in Controller AppendingCustomInfo: {ex}"); }
        }
    }
}
