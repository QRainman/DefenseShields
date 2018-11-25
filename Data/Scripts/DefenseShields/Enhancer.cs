﻿using System;
using System.Collections.Generic;
using System.Text;
using DefenseShields.Support;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace DefenseShields
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_UpgradeModule), false, "LargeEnhancer", "SmallEnhancer")]
    public class Enhancers : MyGameLogicComponent
    {
        private uint _tick;
        private int _count = -1;
        private int _lCount;
        internal int RotationTime;
        internal bool ContainerInited;
        internal bool IsFunctional;
        internal bool IsWorking;

        private bool _tick60;
        private bool _powered;
        private bool _isServer;
        private bool _isDedicated;

        private const float Power = 0.01f;

        private readonly Dictionary<long, Enhancers> _enhancers = new Dictionary<long, Enhancers>();
        public IMyUpgradeModule Enhancer;
        internal MyCubeGrid MyGrid;
        internal MyCubeBlock MyCube;

        internal ShieldGridComponent ShieldComp;
        internal EnhancerState EnhState;
        internal DSUtils Dsutil1 = new DSUtils();

        internal MyResourceSinkInfo ResourceInfo;
        internal MyResourceSinkComponent Sink;
        private MyEntitySubpart _subpartRotor;

        private static readonly MyDefinitionId GId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Electricity");

        public override void UpdateBeforeSimulation()
        {
            try
            {
                _tick = Session.Instance.Tick;
                _tick60 = _tick % 60 == 0;
                var wait = _isServer && !_tick60 && EnhState.State.Backup;

                IsFunctional = MyCube.IsFunctional;
                IsWorking = IsFunctional;
                MyGrid = MyCube.CubeGrid;
                if (wait || MyGrid?.Physics == null) return;

                Timing();
                if (!EnhancerReady()) return;

                if (!_isDedicated && UtilsStatic.DistanceCheck(Enhancer, 1000, 1))
                {
                    var blockCam = MyCube.PositionComp.WorldVolume;
                    if (MyAPIGateway.Session.Camera.IsInFrustum(ref blockCam) && IsWorking) BlockMoveAnimation();
                }
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateBeforeSimulation: {ex}"); }
        }

        private void Timing()
        {
            if (_count++ == 59)
            {
                _count = 0;
                _lCount++;
                if (_lCount == 10) _lCount = 0;
            }

            if (!_isDedicated && _count == 29 && MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel)
            {
                Enhancer.RefreshCustomInfo();
            }
        }

        private bool EnhancerReady()
        {
            if (_subpartRotor == null)
            {
                Entity.TryGetSubpart("Rotor", out _subpartRotor);
                if (_subpartRotor == null) return false;
            }

            if (ShieldComp?.DefenseShields?.MyGrid != MyGrid) MyGrid.Components.TryGet(out ShieldComp);
            if (_isServer)
            {
                if (!BlockWorking()) return false;
            }
            else
            {
                if (ShieldComp?.DefenseShields == null) return false;

                if (!EnhState.State.Backup && ShieldComp.Enhancer != this) ShieldComp.Enhancer = this;

                if (!EnhState.State.Online) return false;
            }
            return BlockMoveAnimationReset();
        }

        private bool BlockWorking()
        {
            if (_count <= 0) _powered = Sink.IsPowerAvailable(GId, 0.01f);
            if (!IsWorking || !_powered)
            {
                NeedUpdate(EnhState.State.Online, false);
                return false;
            }

            if (ShieldComp?.DefenseShields == null)
            {
                NeedUpdate(EnhState.State.Online, false);
                return false;
            }

            if (ShieldComp.Enhancer != this)
            {
                if (ShieldComp.Enhancer == null)
                {
                    ShieldComp.Enhancer = this;
                    EnhState.State.Backup = false;
                }
                else if (ShieldComp.Enhancer != this)
                {
                    EnhState.State.Backup = true;
                    EnhState.State.Online = false;
                }

                if (!_isDedicated && Enhancer != null && _count == 29)
                {
                    Enhancer.RefreshCustomInfo();
                }
            }

            if (!EnhState.State.Backup && ShieldComp.Enhancer == this && ShieldComp.DefenseShields.WasOnline)
            {
                NeedUpdate(EnhState.State.Online, true);
                return true;
            }

            NeedUpdate(EnhState.State.Online, false);
            return false;
        }

        private void NeedUpdate(bool onState, bool turnOn)
        {
            if (!onState && turnOn)
            {
                EnhState.State.Online = true;
                EnhState.SaveState();
                EnhState.NetworkUpdate();
            }
            else if (onState & !turnOn)
            {
                EnhState.State.Online = false;
                EnhState.SaveState();
                EnhState.NetworkUpdate();
            }
        }

        public void UpdateState(ProtoEnhancerState newState)
        {
            EnhState.State = newState;
            if (Session.Enforced.Debug >= 1) Log.Line($"UpdateState: EnhancerId [{Enhancer.EntityId}]");
        }

        public override void OnAddedToContainer()
        {
            if (!ContainerInited)
            {
                PowerPreInit();
                NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
                Enhancer = (IMyUpgradeModule)Entity;
                ContainerInited = true;
                if (Session.Enforced.Debug >= 1) Log.Line($"ContainerInited:  EmitterId [{Enhancer.EntityId}]");
            }
            if (Entity.InScene) OnAddedToScene();
        }


        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            try
            {
                base.Init(objectBuilder);
                StorageSetup();
            }
            catch (Exception ex) { Log.Line($"Exception in EntityInit: {ex}"); }
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            try
            {
                if (Enhancer.CubeGrid.Physics == null) return;
                _enhancers.Add(Entity.EntityId, this);
                Session.Instance.Enhancers.Add(this);
                PowerInit();
                Entity.TryGetSubpart("Rotor", out _subpartRotor);
                _isServer = Session.IsServer;
                _isDedicated = Session.DedicatedServer;
                Enhancer.AppendingCustomInfo += AppendingCustomInfo;
                Enhancer.RefreshCustomInfo();
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateOnceBeforeFrame: {ex}"); }
        }

        public override bool IsSerialized()
        {
            if (MyAPIGateway.Multiplayer.IsServer)
            {
                if (Enhancer.Storage != null) EnhState.SaveState();
            }
            return false;
        }

        public override void OnAddedToScene()
        {
            try
            {
                MyCube = Enhancer as MyCubeBlock;
                if (Session.Enforced.Debug >= 2) Log.Line($"OnAddedToScene: - EnhancerId [{Enhancer.EntityId}]");
            }
            catch (Exception ex) { Log.Line($"Exception in OnAddedToScene: {ex}"); }
        }

        private void StorageSetup()
        {
            if (EnhState == null) EnhState = new EnhancerState(Enhancer);
            EnhState.StorageInit();
            EnhState.LoadState();
        }

        private void PowerPreInit()
        {
            try
            {
                if (Sink == null)
                {
                    Sink = new MyResourceSinkComponent();
                }
                ResourceInfo = new MyResourceSinkInfo()
                {
                    ResourceTypeId = GId,
                    MaxRequiredInput = 0.02f,
                    RequiredInputFunc = () => Power
                };
                Sink.Init(MyStringHash.GetOrCompute("Utility"), ResourceInfo);
                Sink.AddType(ref ResourceInfo);
                Entity.Components.Add(Sink);
            }
            catch (Exception ex) { Log.Line($"Exception in PowerPreInit: {ex}"); }
        }

        private void PowerInit()
        {
            try
            {
                var enableState = Enhancer.Enabled;
                if (enableState)
                {
                    Enhancer.Enabled = false;
                    Enhancer.Enabled = true;
                }
                Sink.Update();
                if (Session.Enforced.Debug >= 2) Log.Line($"PowerInit: EnhancerId [{Enhancer.EntityId}]");
            }
            catch (Exception ex) { Log.Line($"Exception in AddResourceSourceComponent: {ex}"); }
        }

        private bool BlockMoveAnimationReset()
        {
            if (!IsFunctional) return false;
            if (_subpartRotor == null)
            {
                Entity.TryGetSubpart("Rotor", out _subpartRotor);
                if (_subpartRotor == null) return false;
            }
            if (_subpartRotor.Closed) _subpartRotor.Subparts.Clear();
            return true;
        }

        private void BlockMoveAnimation()
        {
            if (!BlockMoveAnimationReset()) return;
            RotationTime -= 1;
            var rotationMatrix = MatrixD.CreateRotationY(0.05f * RotationTime);
            _subpartRotor.PositionComp.LocalMatrix = rotationMatrix;
        }

        #region Create UI
        private void CreateUi()
        {
            //EnhUi.CreateUi(Enhancer);
        }
        #endregion

        private void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            if (ShieldComp?.DefenseShields == null)
            {
                stringBuilder.Append("[Controller Link]: False");
            }
            else if (!EnhState.State.Backup && ShieldComp.DefenseShields.ShieldMode == DefenseShields.ShieldType.Station)
            {
                stringBuilder.Append("[Online]: " + EnhState.State.Online +
                                     "\n" +
                                     "\n[Amplifying Shield]: " + EnhState.State.Online +
                                     "\n[Enhancer Mode]: Fortress" +
                                     "\n[Bonsus] MaxHP, Repel Grids");
            }
            else if (!EnhState.State.Backup)
            {
                stringBuilder.Append("[Online]: " + EnhState.State.Online +
                                     "\n" +
                                     "\n[Shield Detected]: " + EnhState.State.Online +
                                     "\n[Enhancer Mode]: EMP Option");
            }
            else
            {
                stringBuilder.Append("[Backup]: " + EnhState.State.Backup);
            }
        }

        public override void OnRemovedFromScene()
        {
            try
            {
                if (Session.Instance.Enhancers.Contains(this)) Session.Instance.Enhancers.Remove(this);
                if (ShieldComp?.Enhancer == this)
                {
                    ShieldComp.Enhancer = null;
                }
                IsWorking = false;
                IsFunctional = false;
                MyCube = null;
            }
            catch (Exception ex) { Log.Line($"Exception in OnRemovedFromScene: {ex}"); }
        }

        public override void OnBeforeRemovedFromContainer()
        {
            if (Entity.InScene) OnRemovedFromScene();
        }
        public override void Close()
        {
            try
            {
                base.Close();
                if (Session.Instance.Enhancers.Contains(this)) Session.Instance.Enhancers.Remove(this);
                if (ShieldComp?.Enhancer == this)
                {
                    ShieldComp.Enhancer = null;
                }
                Enhancer = null;
            }
            catch (Exception ex) { Log.Line($"Exception in Close: {ex}"); }
        }

        public override void MarkForClose()
        {
            try
            {
                base.MarkForClose();
            }
            catch (Exception ex) { Log.Line($"Exception in MarkForClose: {ex}"); }
        }
    }
}
