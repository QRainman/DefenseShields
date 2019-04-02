﻿using System;
using DefenseSystems.Support;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;

namespace DefenseSystems
{
    public partial class Emitters
    {
        #region Block Status
        private bool ControllerLink()
        {
            //if (DefenseBus?.MasterGrid != LocalGrid) LocalGrid.Components.TryGet(out DefenseBus);

            if (!_isServer)
            {
                var link = ClientEmitterReady();
                if (!link && !_blockReset) BlockReset(true);

                return link;
            }

            if (!_firstSync && _readyToSync) SaveAndSendAll();

            var linkWas = EmiState.State.Link;
            var losWas = EmiState.State.Los;
            var idWas = EmiState.State.ActiveEmitterId;
            if (!EmitterReady())
            {
                EmiState.State.Link = false;

                if (linkWas || losWas != EmiState.State.Los || idWas != EmiState.State.ActiveEmitterId)
                {
                    if (!_isDedicated && !_blockReset) BlockReset(true);
                    NeedUpdate();
                }
                return false;
            }

            EmiState.State.Link = true;

            if (!linkWas || losWas != EmiState.State.Los || idWas != EmiState.State.ActiveEmitterId) NeedUpdate();

            return true;
        }

        private bool EmitterReady()
        {
            if (Suspend() || !BlockWorking())
                return false;

            return true;
        }

        private bool ClientEmitterReady()
        {
            if (DefenseBus?.ActiveController == null) return false;

            if (!_compact)
            {
                if (IsFunctional) Entity.TryGetSubpart("Rotor", out SubpartRotor);
                if (SubpartRotor == null) return false;
            }

            if (!EmiState.State.Los) LosLogic();

            if (EmiState.State.Los && !_wasLosState)
            {
                _wasLosState = EmiState.State.Los;
                _updateLosState = false;
                LosScaledCloud.Clear();
            }
            return EmiState.State.Link;
        }

        private bool Suspend()
        {
            if (DefenseBus?.ActiveEmitter != this)
            {
                if (!_isDedicated && !_blockReset) BlockReset(true);
                return true;
            }
            return false;
        }

        /*
        private bool Suspend()
        {
            EmiState.State.ActiveEmitterId = 0;
            var functional = IsFunctional;
            if (!functional)
            {
                EmiState.State.Suspend = true;
                if (DefenseBus?.StationEmitter == this) DefenseBus.StationEmitter = null;
                else if (DefenseBus?.ShipEmitter == this) DefenseBus.ShipEmitter = null;
                return true;
            }
            if (!_compact && SubpartRotor == null)
            {
                Entity.TryGetSubpart("Rotor", out SubpartRotor);
                if (SubpartRotor == null)
                {
                    EmiState.State.Suspend = true;
                    return true;
                }
            }

            if (DefenseBus == null)
            {
                EmiState.State.Suspend = true;
                return true;
            }

            var working = IsWorking;
            var stationMode = EmitterMode == EmitterType.Station;
            var shipMode = EmitterMode != EmitterType.Station;
            var modes = (IsStatic && stationMode) || (!IsStatic && shipMode);
            var mySlotNull = (stationMode && DefenseBus.StationEmitter == null) || (shipMode && DefenseBus.ShipEmitter == null);
            var myComp = (stationMode && DefenseBus.StationEmitter == this) || (shipMode && DefenseBus.ShipEmitter == this);

            var myMode = working && modes;
            var mySlotOpen = working && mySlotNull;
            var myShield = myMode && myComp;
            var iStopped = !working && myComp && modes;
            if (mySlotOpen)
            {
                Session.Instance.BlockTagActive(Emitter);
                if (stationMode)
                {
                    EmiState.State.Backup = false;
                    DefenseBus.StationEmitter = this;
                    if (myMode)
                    {
                        TookControl = true;
                        DefenseBus.EmitterMode = (int)EmitterMode;
                        DefenseBus.EmitterEvent = true;
                        DefenseBus.EmittersSuspended = false;
                        EmiState.State.Suspend = false;
                        myShield = true;
                        EmiState.State.Backup = false;
                    }
                    else EmiState.State.Suspend = true;
                }
                else
                {
                    EmiState.State.Backup = false;
                    DefenseBus.ShipEmitter = this;

                    if (myMode)
                    {
                        TookControl = true;
                        DefenseBus.EmitterMode = (int)EmitterMode;
                        DefenseBus.EmitterEvent = true;
                        DefenseBus.EmittersSuspended = false;
                        EmiState.State.Suspend = false;
                        myShield = true;
                        EmiState.State.Backup = false;
                    }
                    else EmiState.State.Suspend = true;
                }
                if (Session.Enforced.Debug >= 3) Log.Line($"mySlotOpen: {Definition.Name} - myMode:{myMode} - MyShield:{myShield} - Mode:{EmitterMode} - Static:{IsStatic} - ELos:{DefenseBus.EmitterLos} - ES:{DefenseBus.EmittersSuspended} - ModeM:{(int)EmitterMode == DefenseBus.EmitterMode} - S:{EmiState.State.Suspend} - EmitterId [{Emitter.EntityId}]");
            }
            else if (!myMode)
            {
                var compMode = DefenseBus.EmitterMode;
                if ((!EmiState.State.Suspend && ((compMode == 0 && !IsStatic) || (compMode != 0 && IsStatic))) || (!EmiState.State.Suspend && iStopped))
                {
                    DefenseBus.EmittersSuspended = true;
                    DefenseBus.EmitterLos = false;
                    DefenseBus.EmitterEvent = true;
                    if (Session.Enforced.Debug >= 3) Log.Line($"!myMode: {Definition.Name} suspending - Match:{(int)EmitterMode == DefenseBus.EmitterMode} - ELos:{DefenseBus.EmitterLos} - ES:{DefenseBus.EmittersSuspended} - ModeEq:{(int)EmitterMode == DefenseBus?.EmitterMode} - S:{EmiState.State.Suspend} - Static:{IsStatic} - EmitterId [{Emitter.EntityId}]");
                }
                else if (!EmiState.State.Suspend)
                {
                    if (Session.Enforced.Debug >= 3) Log.Line($"!myMode: {Definition.Name} suspending - Match:{(int)EmitterMode == DefenseBus.EmitterMode} - ELos:{DefenseBus.EmitterLos} - ES:{DefenseBus.EmittersSuspended} - ModeEq:{(int)EmitterMode == DefenseBus?.EmitterMode} - S:{EmiState.State.Suspend} - Static:{IsStatic} - EmitterId [{Emitter.EntityId}]");
                }
                EmiState.State.Suspend = true;
            }
            if (iStopped)
            {
                return EmiState.State.Suspend;
            }

            if (!myShield)
            {
                if (!EmiState.State.Backup)
                {
                    Session.Instance.BlockTagBackup(Emitter);
                    EmiState.State.Backup = true;
                    if (Session.Enforced.Debug >= 3) Log.Line($"!myShield - !otherMode: {Definition.Name} - isStatic:{IsStatic} - myShield:{myShield} - myMode {myMode} - Mode:{EmitterMode} - CompMode: {DefenseBus.EmitterMode} - ELos:{DefenseBus.EmitterLos} - ES:{DefenseBus.EmittersSuspended} - EmitterId [{Emitter.EntityId}]");
                }
                EmiState.State.Suspend = true;
            }

            if (myShield && EmiState.State.Suspend)
            {
                DefenseBus.EmittersSuspended = false;
                DefenseBus.EmitterEvent = true;
                EmiState.State.Backup = false;
                EmiState.State.Suspend = false;
                if (Session.Enforced.Debug >= 3) Log.Line($"Unsuspend - !otherMode: {Definition.Name} - isStatic:{IsStatic} - myShield:{myShield} - myMode {myMode} - Mode:{EmitterMode} - CompMode: {DefenseBus.EmitterMode} - ELos:{DefenseBus.EmitterLos} - ES:{DefenseBus.EmittersSuspended} - EmitterId [{Emitter.EntityId}]");
            }
            else if (EmiState.State.Suspend) return true;

            EmiState.State.Suspend = false;
            return false;
        }
        */
        private bool BlockWorking()
        {
            EmiState.State.ActiveEmitterId = MyCube.EntityId;

            if (DefenseBus.EmitterMode != (int)EmitterMode) DefenseBus.EmitterMode = (int)EmitterMode;
            if (DefenseBus.EmittersSuspended) SuspendCollisionDetected();

            LosLogic();

            DefenseBus.EmitterLos = EmiState.State.Los;
            DefenseBus.ActiveEmitterId = EmiState.State.ActiveEmitterId;

            var bus = DefenseBus;
            var controller = bus.ActiveController;
            var nullController = controller == null;
            var shieldWaiting = !nullController && controller.DsState.State.EmitterLos != EmiState.State.Los;
            if (shieldWaiting) bus.EmitterEvent = true;

            if (!EmiState.State.Los || nullController || shieldWaiting || !controller.DsState.State.Online || !(_tick >= controller.ResetEntityTick))
            {
                if (!_isDedicated && !_blockReset) BlockReset(true);
                return false;
            }
            return true;
        }

        private void SuspendCollisionDetected()
        {
            DefenseBus.EmitterMode = (int)EmitterMode;
            DefenseBus.EmittersSuspended = false;
            DefenseBus.EmitterEvent = true;
            TookControl = true;
        }
        #endregion

        #region Block States
        internal void UpdateState(EmitterStateValues newState)
        {
            if (newState.MId > EmiState.State.MId)
            {
                if (Session.Enforced.Debug >= 3) Log.Line($"UpdateState - NewLink:{newState.Link} - OldLink:{EmiState.State.Link} - EmitterId [{Emitter.EntityId}]:\n{EmiState.State}");
                EmiState.State = newState;
            }
        }

        private void NeedUpdate()
        {
            EmiState.State.Mode = (int)EmitterMode;
            EmiState.State.BoundingRange = DefenseBus?.ActiveController?.BoundingRange ?? 0f;
            EmiState.State.Compatible = (IsStatic && EmitterMode == EmitterType.Station) || (!IsStatic && EmitterMode != EmitterType.Station);
            EmiState.SaveState();
            if (Session.Instance.MpActive) EmiState.NetworkUpdate();
        }

        private void CheckEmitter(IMyTerminalBlock myTerminalBlock)
        {
            try
            {
                if (myTerminalBlock.IsWorking && DefenseBus != null) DefenseBus.CheckEmitters = true;
            }
            catch (Exception ex) { Log.Line($"Exception in CheckEmitter: {ex}"); }
        }

        private void IsWorkingChanged(MyCubeBlock myCubeBlock)
        {
            IsFunctional = myCubeBlock.IsWorking;
            IsWorking = myCubeBlock.IsWorking;
        }

        private void SetEmitterType()
        {
            Definition = DefinitionManager.Get(Emitter.BlockDefinition.SubtypeId);
            switch (Definition.Name)
            {
                case "EmitterST":
                    EmitterMode = EmitterType.Station;
                    Entity.TryGetSubpart("Rotor", out SubpartRotor);
                    break;
                case "EmitterL":
                case "EmitterLA":
                    EmitterMode = EmitterType.Large;
                    if (Definition.Name == "EmitterLA") _compact = true;
                    else Entity.TryGetSubpart("Rotor", out SubpartRotor);
                    break;
                case "EmitterS":
                case "EmitterSA":
                    EmitterMode = EmitterType.Small;
                    if (Definition.Name == "EmitterSA") _compact = true;
                    else Entity.TryGetSubpart("Rotor", out SubpartRotor);
                    break;
            }
            Emitter.AppendingCustomInfo += AppendingCustomInfo;
        }
        #endregion

    }
}
