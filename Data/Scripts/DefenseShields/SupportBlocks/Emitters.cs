﻿using System;
using System.Collections.Generic;
using System.Text;
using DefenseShields.Support;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using CollisionLayers = Sandbox.Engine.Physics.MyPhysics.CollisionLayers;

namespace DefenseShields
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_UpgradeModule), false, "EmitterL", "EmitterS", "EmitterST", "EmitterLA", "EmitterSA")]
    public class Emitters : MyGameLogicComponent
    {
        private uint _tick;
        private int _count = -1;
        private int _lCount;
        internal int RotationTime;
        internal int AnimationLoop;
        internal int TranslationTime;

        private float _power = 0.01f;
        internal float EmissiveIntensity;

        public bool ServerUpdate;
        internal bool IsStatic;
        internal bool TookControl;
        internal bool ContainerInited;
        internal bool IsFunctional;
        internal bool IsWorking;

        private bool _tick60;
        private bool _isServer;
        private bool _isDedicated;
        private bool _wasOnline;
        private bool _wasLink;
        private bool _wasBackup;
        private bool _wasSuspend;
        private bool _wasLos;
        private bool _wasCompact;
        private bool _wasCompatible;
        private int _wasMode;
        private double _wasBoundingRange;

        private const string PlasmaEmissive = "PlasmaEmissive";

        private Vector3D _sightPos;

        internal ShieldGridComponent ShieldComp;
        private MyEntitySubpart _subpartRotor;
        //private MyParticleEffect _effect = new MyParticleEffect();
        internal MyResourceSinkInfo ResourceInfo;
        internal MyResourceSinkComponent Sink;

        internal Definition Definition;
        internal DSUtils Dsutil1 = new DSUtils();
        internal EmitterState EmiState;

        public IMyUpgradeModule Emitter;
        public EmitterType EmitterMode;
        internal MyCubeGrid MyGrid;
        internal MyCubeBlock MyCube;

        private readonly MyConcurrentList<int> _vertsSighted = new MyConcurrentList<int>();
        private readonly MyConcurrentList<int> _noBlocksLos = new MyConcurrentList<int>();
        private readonly MyConcurrentHashSet<int> _blocksLos = new MyConcurrentHashSet<int>();

        private static readonly MyDefinitionId GId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Electricity");

        public enum EmitterType
        {
            Station,
            Large,
            Small,
        };

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            try
            {
                if (Emitter.CubeGrid.Physics == null) return;
                Session.Emitters.Add(this);
                PowerInit();
                _isServer = Session.IsServer;
                _isDedicated = Session.DedicatedServer;
                IsStatic = Emitter.CubeGrid.IsStatic;
                StateChange(true);
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateOnceBeforeFrame: {ex}"); }
        }

        public override void UpdateBeforeSimulation()
        {
            try
            {
                _tick = Session.Tick;
                _tick60 = _tick % 60 == 0;
                var wait = _isServer && !_tick60 && EmiState.State.Backup;

                MyGrid = MyCube.CubeGrid;
                if (wait || MyGrid?.Physics == null) return;
                IsStatic = MyGrid.IsStatic;

                Timing();
                if (!ControllerLink()) return;
                if (!_isDedicated && UtilsStatic.DistanceCheck(Emitter, 1000, EmiState.State.BoundingRange))
                {
                    //if (ShieldComp.GridIsMoving && !Compact) BlockParticleUpdate();
                    var blockCam = MyCube.PositionComp.WorldVolume;
                    var onCam = MyAPIGateway.Session.Camera.IsInFrustum(ref blockCam);
                    if (onCam)
                    {
                        //if (_effect == null && ShieldComp.ShieldPercent <= 97 && !Compact) BlockParticleStart();
                        //else if (_effect != null && ShieldComp.ShieldPercent > 97f && !Compact) BlockParticleStop();
                        BlockMoveAnimation();
                    }
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
            if (_count == 29 && !_isDedicated && MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel)
            {
                Emitter.RefreshCustomInfo();
            }
        }

        private bool StateChange(bool update = false)
        {
            if (update)
            {
                _wasOnline = EmiState.State.Online;
                _wasLink = EmiState.State.Link;
                _wasBackup = EmiState.State.Backup;
                _wasSuspend = EmiState.State.Suspend;
                _wasLos = EmiState.State.Los;
                _wasCompact = EmiState.State.Compact;
                _wasCompatible = EmiState.State.Compatible;
                _wasMode = EmiState.State.Mode;
                _wasBoundingRange = EmiState.State.BoundingRange;
                return true;
            }

            return _wasOnline != EmiState.State.Online || _wasLink != EmiState.State.Link ||
                   _wasBackup != EmiState.State.Backup || _wasSuspend != EmiState.State.Suspend ||
                   _wasLos != EmiState.State.Los || _wasCompact != EmiState.State.Compact ||
                   _wasCompatible != EmiState.State.Compatible || _wasMode != EmiState.State.Mode ||
                   !_wasBoundingRange.Equals(EmiState.State.BoundingRange);
        }

        private bool ControllerLink()
        {
            if (!EmitterReady())
            {
                if (_isServer) EmiState.State.Link = false;

                if (StateChange())
                {
                    if (_isServer)
                    {
                        BlockReset(true);
                        NeedUpdate();
                        StateChange(true);
                    }
                    else
                    {
                        BlockReset(true);
                        StateChange(true);
                    }
                }
                return false;
            }
            if (_isServer)
            {
                EmiState.State.Link = true;
                if (StateChange())
                {
                    NeedUpdate();
                    StateChange(true);
                }
            }
            else if (!EmiState.State.Link)
            {
                if (StateChange())
                {
                    BlockReset(true);
                    StateChange(true);
                }
                return false;
            }
            return true;
        }

        private bool EmitterReady()
        {
            if (ShieldComp?.DefenseShields?.MyGrid != MyGrid) MyGrid.Components.TryGet(out ShieldComp);
            if (_isServer)
            {
                if (Suspend() || !BlockWorking()) return false;
            }
            else
            {
                if (ShieldComp == null) return false;

                if (EmiState.State.Mode == 0 && EmiState.State.Link && ShieldComp.StationEmitter == null) ShieldComp.StationEmitter = this;
                else if (EmiState.State.Mode != 0 && EmiState.State.Link && ShieldComp.ShipEmitter == null) ShieldComp.ShipEmitter = this;

                if (ShieldComp.DefenseShields == null || !IsFunctional) return false;

                if (!EmiState.State.Compact && _subpartRotor == null)
                {
                    Entity.TryGetSubpart("Rotor", out _subpartRotor);
                    if (_subpartRotor == null) return false;
                }
                if (!EmiState.State.Link || !EmiState.State.Online) return false;
            }
            return true;
        }

        private void NeedUpdate()
        {
            EmiState.State.Mode = (int)EmitterMode;
            EmiState.State.BoundingRange = ShieldComp?.DefenseShields?.BoundingRange ?? 0f;
            EmiState.State.Compatible = IsStatic && EmitterMode == EmitterType.Station || !IsStatic && EmitterMode != EmitterType.Station;
            EmiState.SaveState();
            if (Session.MpActive) EmiState.NetworkUpdate();
        }

        public void UpdateState(ProtoEmitterState newState)
        {
            EmiState.State = newState;
            if (Session.Enforced.Debug <= 3) Log.Line($"UpdateState - EmitterId [{Emitter.EntityId}]:\n{EmiState.State}");
        }

        #region Block Animation
        private void BlockReset(bool force = false)
        {
            //if (_effect != null && !Session.DedicatedServer && !Compact) BlockParticleStop();
            if (!_isDedicated && !EmissiveIntensity.Equals(0) || !_isDedicated && force) BlockMoveAnimationReset(true);
        }

        private bool BlockMoveAnimationReset(bool clearAnimation)
        {
            if (!IsFunctional) return false;

            if (!EmiState.State.Compact && _subpartRotor == null)
            {
                Entity.TryGetSubpart("Rotor", out _subpartRotor);
                if (_subpartRotor == null) return false;
            }
            else if (!EmiState.State.Compact)
            {
                if (_subpartRotor.Closed) _subpartRotor.Subparts.Clear();
                Entity.TryGetSubpart("Rotor", out _subpartRotor);
            }

            if (clearAnimation)
            {
                RotationTime = 0;
                TranslationTime = 0;
                AnimationLoop = 0;
                EmissiveIntensity = 0;

                if (!EmiState.State.Compact)
                {
                    var rotationMatrix = MatrixD.CreateRotationY(0);
                    var matrix = rotationMatrix * MatrixD.CreateTranslation(0, 0, 0);
                    _subpartRotor.PositionComp.LocalMatrix = matrix;
                    _subpartRotor.SetEmissiveParts(PlasmaEmissive, Color.Transparent, 0);
                }
                else MyCube.SetEmissiveParts(PlasmaEmissive, Color.Transparent, 0);
            }

            if (Session.Enforced.Debug == 3) Log.Line($"EmitterAnimationReset: [EmitterType: {Definition.Name} - Compact({EmiState.State.Compact})] - Tick:{_tick.ToString()} - EmitterId [{Emitter.EntityId}]");
            return true;
        }

        private void BlockMoveAnimation()
        {
            var percent = ShieldComp.DefenseShields.DsState.State.ShieldPercent;
            if (EmiState.State.Compact)
            {
                if (_count == 0) EmissiveIntensity = 2;
                if (_count < 30) EmissiveIntensity += 1;
                else EmissiveIntensity -= 1;
                MyCube.SetEmissiveParts(PlasmaEmissive, UtilsStatic.GetShieldColorFromFloat(percent), 0.1f * EmissiveIntensity);
                return;
            }

            if (_subpartRotor.Closed.Equals(true)) BlockMoveAnimationReset(false);
            RotationTime -= 1;
            if (AnimationLoop == 0) TranslationTime = 0;
            if (AnimationLoop < 299) TranslationTime += 1;
            else TranslationTime -= 1;
            if (_count == 0) EmissiveIntensity = 2;
            if (_count < 30) EmissiveIntensity += 1;
            else EmissiveIntensity -= 1;

            var rotationMatrix = MatrixD.CreateRotationY(0.025f * RotationTime);
            var matrix = rotationMatrix * MatrixD.CreateTranslation(0, Definition.BlockMoveTranslation * TranslationTime, 0);

            _subpartRotor.PositionComp.LocalMatrix = matrix;
            _subpartRotor.SetEmissiveParts(PlasmaEmissive, UtilsStatic.GetShieldColorFromFloat(percent), 0.1f * EmissiveIntensity);

            if (AnimationLoop++ == 599) AnimationLoop = 0;
        }

        /*
        private void BlockParticleUpdate()
        {
            if (_effect == null) return;

            var testDist = Definition.ParticleDist;

            var spawnDir = _subpartRotor.PositionComp.WorldVolume.Center - Emitter.PositionComp.WorldVolume.Center;
            spawnDir.Normalize();
            var spawnPos = Emitter.PositionComp.WorldVolume.Center + spawnDir * testDist;

            var predictedMatrix = Emitter.PositionComp.WorldMatrix;

            predictedMatrix.Translation = spawnPos;
            if (ShieldComp.ShieldVelocitySqr > 4000) predictedMatrix.Translation = spawnPos + Emitter.CubeGrid.Physics.GetVelocityAtPoint(Emitter.PositionComp.WorldMatrix.Translation) * MyEngineConstants.UPDATE_STEP_SIZE_IN_SECONDS;
            _effect.WorldMatrix = predictedMatrix;
        }

        private void BlockParticleStop()
        {
            if (_effect == null) return;
            _effect?.Stop();
            _effect?.Close(false, true);
            _effect = null;
        }

        private void BlockParticleStart()
        {
            var scale = Definition.ParticleScale;
            var matrix = Emitter.WorldMatrix;
            var pos = Emitter.WorldVolume.Center;
            MyParticlesManager.TryCreateParticleEffect(6666, out _effect, ref matrix, ref pos, _myRenderId, true); // 15, 16, 17, 24, 25, 28, (31, 32) 211 215 53
            _effect.UserRadiusMultiplier = scale;
            _effect.Play();
            BlockParticleUpdate();
        }
        */
        #endregion

        private bool BlockWorking()
        {
            if (ShieldComp.EmitterMode != (int)EmitterMode) ShieldComp.EmitterMode = (int)EmitterMode;
            if (ShieldComp.EmittersSuspended) SuspendCollisionDetected();

            EmiState.State.Online = true;
            var online = EmiState.State.Online;
            var logic = ShieldComp.DefenseShields;
            var controllerReady = logic != null && logic.Warming && logic.IsWorking && logic.IsFunctional && !logic.DsState.State.Suspended && logic.DsState.State.ControllerGridAccess;
            var losCheckReq = online && controllerReady;
            if (losCheckReq && ShieldComp.CheckEmitters || controllerReady && TookControl) CheckShieldLineOfSight();
            //if (losCheckReq && !EmiState.State.Los && !Session.DedicatedServer) DrawHelper();
            ShieldComp.EmittersWorking = EmiState.State.Los && online;
            if (!ShieldComp.EmittersWorking || logic == null || !ShieldComp.DefenseShields.DsState.State.Online || !(_tick >= logic.UnsuspendTick))
            {
                BlockReset();
                return false;
            }
            return true;
        }

        private void SuspendCollisionDetected()
        {
            ShieldComp.EmitterMode = (int)EmitterMode;
            ShieldComp.EmittersSuspended = false;
            ShieldComp.EmitterEvent = true;
            TookControl = true;
        }

        private bool Suspend()
        {
            EmiState.State.Online = false;
            var functional = IsFunctional;
            if (!functional)
            {
                EmiState.State.Suspend = true;
                if (ShieldComp?.StationEmitter == this) ShieldComp.StationEmitter = null;
                else if (ShieldComp?.ShipEmitter == this) ShieldComp.ShipEmitter = null;
                return true;
            }
            if (!EmiState.State.Compact && _subpartRotor == null)
            {
                Entity.TryGetSubpart("Rotor", out _subpartRotor);
                if (_subpartRotor == null)
                {
                    EmiState.State.Suspend = true;
                    return true;
                }
            }

            if (ShieldComp == null)
            {
                EmiState.State.Suspend = true;
                return true;
            }

            var working = IsWorking;
            var stationMode = EmitterMode == EmitterType.Station;
            var shipMode = EmitterMode != EmitterType.Station;
            var modes = IsStatic && stationMode || !IsStatic && shipMode;
            var mySlotNull = stationMode && ShieldComp.StationEmitter == null || shipMode && ShieldComp.ShipEmitter == null;
            var myComp = stationMode && ShieldComp.StationEmitter == this || shipMode && ShieldComp.ShipEmitter == this;

            var myMode = working && modes;
            var mySlotOpen = working && mySlotNull;
            var myShield = myMode && myComp;
            var iStopped = !working && myComp && modes;
            if (mySlotOpen)
            {
                if (stationMode)
                {
                    EmiState.State.Backup = false;
                    ShieldComp.StationEmitter = this;
                    if (myMode)
                    {
                        TookControl = true;
                        ShieldComp.EmitterMode = (int)EmitterMode;
                        ShieldComp.EmitterEvent = true;
                        ShieldComp.EmittersSuspended = false;
                        EmiState.State.Suspend = false;
                        myShield = true;
                        EmiState.State.Backup = false;
                    }
                    else EmiState.State.Suspend = true;
                }
                else
                {
                    EmiState.State.Backup = false;
                    ShieldComp.ShipEmitter = this;

                    if (myMode)
                    {
                        TookControl = true;
                        ShieldComp.EmitterMode = (int)EmitterMode;
                        ShieldComp.EmitterEvent = true;
                        ShieldComp.EmittersSuspended = false;
                        EmiState.State.Suspend = false;
                        myShield = true;
                        EmiState.State.Backup = false;
                    }
                    else EmiState.State.Suspend = true;
                }
                if (Session.Enforced.Debug == 3) Log.Line($"mySlotOpen: {Definition.Name} - myMode:{myMode} - MyShield:{myShield} - Mode:{EmitterMode} - Static:{IsStatic} - EW:{ShieldComp.EmittersWorking} - ES:{ShieldComp.EmittersSuspended} - ModeM:{(int)EmitterMode == ShieldComp.EmitterMode} - S:{EmiState.State.Suspend} - EmitterId [{Emitter.EntityId}]");
            }
            else if (!myMode)
            {
                var compMode = ShieldComp.EmitterMode;
                if (!EmiState.State.Suspend && (compMode == 0 && !IsStatic || compMode != 0 && IsStatic) || !EmiState.State.Suspend && iStopped)
                {
                    ShieldComp.EmittersSuspended = true;
                    ShieldComp.EmittersWorking = false;
                    ShieldComp.EmitterEvent = true;
                    if (Session.Enforced.Debug == 3) Log.Line($"!myMode: {Definition.Name} suspending - myMode: {myMode} - myShield: {myShield} - Match:{(int)EmitterMode == ShieldComp.EmitterMode} - EW:{ShieldComp.EmittersWorking} - ES:{ShieldComp.EmittersSuspended} - ModeEq:{(int)EmitterMode == ShieldComp?.EmitterMode} - S:{EmiState.State.Suspend} - Static:{IsStatic} - EmitterId [{Emitter.EntityId}]");
                }
                else if (!EmiState.State.Suspend)
                {
                    if (Session.Enforced.Debug == 3) Log.Line($"!myMode: {Definition.Name} suspending - myMode: {myMode} - myShield: {myShield} - Match:{(int)EmitterMode == ShieldComp.EmitterMode} - EW:{ShieldComp.EmittersWorking} - ES:{ShieldComp.EmittersSuspended} - ModeEq:{(int)EmitterMode == ShieldComp?.EmitterMode} - S:{EmiState.State.Suspend} - Static:{IsStatic} - EmitterId [{Emitter.EntityId}]");
                }
                EmiState.State.Suspend = true;
            }
            if (iStopped)
            {
                return EmiState.State.Suspend;
            }

            if (!myShield)
            {
                if (!EmiState.State.Suspend)
                {
                    EmiState.State.Backup = true;
                }
                EmiState.State.Suspend = true;
            }

            if (myShield && EmiState.State.Suspend)
            {
                ShieldComp.EmittersSuspended = false;
                ShieldComp.EmitterEvent = true;
                EmiState.State.Backup = false;
                EmiState.State.Suspend = false;
                if (Session.Enforced.Debug == 3) Log.Line($"Unsuspend - !otherMode: {Definition.Name} - isStatic:{IsStatic} - myShield:{myShield} - myMode {myMode} - Mode:{EmitterMode} - CompMode: {ShieldComp.EmitterMode} - EW:{ShieldComp.EmittersWorking} - ES:{ShieldComp.EmittersSuspended} - EmitterId [{Emitter.EntityId}]");
            }
            else if (EmiState.State.Suspend) return true;

            EmiState.State.Suspend = false;
            return false;
        }

        private void CheckShieldLineOfSight()
        {
            if (!EmiState.State.Compact && _subpartRotor.Closed.Equals(true)) BlockMoveAnimationReset(false);
            TookControl = false;
            _blocksLos.Clear();
            _noBlocksLos.Clear();
            _vertsSighted.Clear();
            var testDist = Definition.FieldDist;
            var testDir = MyCube.PositionComp.WorldMatrix.Up;
            if (!EmiState.State.Compact) testDir = _subpartRotor.PositionComp.WorldVolume.Center - MyCube.PositionComp.WorldVolume.Center;
            testDir.Normalize();
            var testPos = MyCube.PositionComp.WorldVolume.Center + testDir * testDist;
            _sightPos = testPos;
            ShieldComp.DefenseShields.ResetShape(false, false);
            if (EmitterMode == EmitterType.Station)
            {
                EmiState.State.Los = true;
                ShieldComp.CheckEmitters = false;
            }
            else
            {
                ShieldComp.DefenseShields.Icosphere.ReturnPhysicsVerts(ShieldComp.DefenseShields.DetectionMatrix, ShieldComp.PhysicsOutside);
                MyAPIGateway.Parallel.For(0, ShieldComp.PhysicsOutside.Length, i =>
                {
                    var hit = MyGrid.RayCastBlocks(testPos, ShieldComp.PhysicsOutside[i]);
                    if (hit.HasValue)
                    {
                        _blocksLos.Add(i);
                        return;
                    }
                    _noBlocksLos.Add(i);
                });
                for (int i = 0; i < ShieldComp.PhysicsOutside.Length; i++) if (!_blocksLos.Contains(i)) _vertsSighted.Add(i);
                EmiState.State.Los = _blocksLos.Count < 552;
                if (!EmiState.State.Los) ShieldComp.EmitterEvent = true;
                ShieldComp.CheckEmitters = false;
            }
            if (Session.Enforced.Debug == 3 && !EmiState.State.Los) Log.Line($"LOS: Mode: {EmitterMode} - blocked verts {_blocksLos.Count.ToString()} - visable verts: {_vertsSighted.Count.ToString()} - LoS: {EmiState.State.Los.ToString()} - EmitterId [{Emitter.EntityId}]");
        }

        private void DrawHelper()
        {
            const float lineWidth = 0.025f;
            var lineDist = Definition.HelperDist;

            foreach (var blocking in _blocksLos)
            {
                var blockedDir = ShieldComp.PhysicsOutside[blocking] - _sightPos;
                blockedDir.Normalize();
                var blockedPos = _sightPos + blockedDir * lineDist;
                DsDebugDraw.DrawLineToVec(_sightPos, blockedPos, Color.Black, lineWidth);
            }

            foreach (var sighted in _vertsSighted)
            {
                var sightedDir = ShieldComp.PhysicsOutside[sighted] - _sightPos;
                sightedDir.Normalize();
                var sightedPos = _sightPos + sightedDir * lineDist;
                DsDebugDraw.DrawLineToVec(_sightPos, sightedPos, Color.Blue, lineWidth);
            }
            if (_count == 0) MyVisualScriptLogicProvider.ShowNotification("The shield emitter DOES NOT have a CLEAR ENOUGH LINE OF SIGHT to the shield, SHUTTING DOWN.", 960, "Red", Emitter.OwnerId);
            if (_count == 0) MyVisualScriptLogicProvider.ShowNotification("Blue means clear line of sight, black means blocked......................................................................", 960, "Red", Emitter.OwnerId);
        }

        public override void OnAddedToContainer()
        {
            if (!ContainerInited)
            {
                PowerPreInit();
                NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
                Emitter = (IMyUpgradeModule)Entity;
                ContainerInited = true;
                if (Session.Enforced.Debug == 3) Log.Line($"ContainerInited: EmitterId [{Emitter.EntityId}]");
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

        public override bool IsSerialized()
        {
            if (MyAPIGateway.Multiplayer.IsServer)
            {
                if (Emitter.Storage != null) EmiState.SaveState();
            }
            return false;
        }

        public override void OnAddedToScene()
        {
            try
            {
                MyGrid = (MyCubeGrid)Emitter.CubeGrid;
                MyCube = Emitter as MyCubeBlock;
                SetEmitterType();
                RegisterEvents();
                if (Session.Enforced.Debug == 3) Log.Line($"OnAddedToScene: {EmitterMode} - EmitterId [{Emitter.EntityId}]");
            }
            catch (Exception ex) { Log.Line($"Exception in OnAddedToScene: {ex}"); }
        }

        private void CheckEmitter(IMyTerminalBlock myTerminalBlock)
        {
            try
            {
                if (myTerminalBlock.IsWorking && ShieldComp != null) ShieldComp.CheckEmitters = true;
            }
            catch (Exception ex) { Log.Line($"Exception in CheckEmitter: {ex}"); }
        }

        private void StorageSetup()
        {
            if (EmiState == null) EmiState = new EmitterState(Emitter);
            EmiState.StorageInit();
            EmiState.LoadState();
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
                    MaxRequiredInput = 0f,
                    RequiredInputFunc = () => _power
                };
                Sink.Init(MyStringHash.GetOrCompute("Utility"), ResourceInfo);
                Sink.AddType(ref ResourceInfo);
                Entity.Components.Add(Sink);
                Sink.Update();
            }
            catch (Exception ex) { Log.Line($"Exception in PowerPreInit: {ex}"); }
        }

        private void PowerInit()
        {
            try
            {
                var enableState = Emitter.Enabled;
                if (enableState)
                {
                    Emitter.Enabled = false;
                    Emitter.Enabled = true;
                }
                Sink.Update();
                IsWorking = MyCube.IsWorking;
                if (Session.Enforced.Debug == 3) Log.Line($"PowerInit: EmitterId [{Emitter.EntityId}]");
            }
            catch (Exception ex) { Log.Line($"Exception in AddResourceSourceComponent: {ex}"); }
        }

        private void SetEmitterType()
        {
            Definition = DefinitionManager.Get(Emitter.BlockDefinition.SubtypeId);
            switch (Definition.Name)
            {
                case "EmitterST":
                    EmitterMode = EmitterType.Station;
                    Entity.TryGetSubpart("Rotor", out _subpartRotor);
                    break;
                case "EmitterL":
                case "EmitterLA":
                    EmitterMode = EmitterType.Large;
                    if (Definition.Name == "EmitterLA") EmiState.State.Compact = true;
                    else Entity.TryGetSubpart("Rotor", out _subpartRotor);
                    break;
                case "EmitterS":
                case "EmitterSA":
                    EmitterMode = EmitterType.Small;
                    if (Definition.Name == "EmitterSA") EmiState.State.Compact = true;
                    else Entity.TryGetSubpart("Rotor", out _subpartRotor);
                    break;
            }
            Emitter.AppendingCustomInfo += AppendingCustomInfo;
        }

        private void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            try
            {
                var mode = Enum.GetName(typeof(EmitterType), EmiState.State.Mode);
                if (!EmiState.State.Link)
                {
                    stringBuilder.Append("[ No Valid Controller ]" +
                                         "\n" +
                                         "\n[Emitter Type]: " + mode +
                                         "\n[Grid Compatible]: " + EmiState.State.Compatible +
                                         "\n[Controller Link]: " + EmiState.State.Link +
                                         "\n[Controller Bus]: " + (ShieldComp?.DefenseShields != null) +
                                         "\n[Line of Sight]: " + EmiState.State.Los +
                                         "\n[Is Suspended]: " + EmiState.State.Suspend +
                                         "\n[Is a Backup]: " + EmiState.State.Backup);
                }
                else if (!EmiState.State.Online)
                {
                    stringBuilder.Append("[ Emitter Offline ]" +
                                         "\n" +
                                         "\n[Emitter Type]: " + mode +
                                         "\n[Grid Compatible]: " + EmiState.State.Compatible +
                                         "\n[Controller Link]: " + EmiState.State.Link +
                                         "\n[Line of Sight]: " + EmiState.State.Los +
                                         "\n[Is Suspended]: " + EmiState.State.Suspend +
                                         "\n[Is a Backup]: " + EmiState.State.Backup);
                }
                else
                {
                    stringBuilder.Append("[ Emitter Online ]" +
                                         "\n" +
                                         "\n[Emitter Type]: " + mode +
                                         "\n[Grid Compatible]: " + EmiState.State.Compatible +
                                         "\n[Controller Link]: " + EmiState.State.Link +
                                         "\n[Line of Sight]: " + EmiState.State.Los +
                                         "\n[Is Suspended]: " + EmiState.State.Suspend +
                                         "\n[Is a Backup]: " + EmiState.State.Backup);
                }
            }
            catch (Exception ex) { Log.Line($"Exception in AppendingCustomInfo: {ex}"); }
        }

        public override void OnRemovedFromScene()
        {
            try
            {
                if (Session.Enforced.Debug == 3) Log.Line($"OnRemovedFromScene: {EmitterMode} - EmitterId [{Emitter.EntityId}]");
                //BlockParticleStop();
                RegisterEvents(false);
                IsWorking = false;
                IsFunctional = false;
            }
            catch (Exception ex) { Log.Line($"Exception in OnRemovedFromScene: {ex}"); }
        }

        private void RegisterEvents(bool register = true)
        {
            if (register)
            {
                Emitter.EnabledChanged += CheckEmitter;
                MyCube.IsWorkingChanged += IsWorkingChanged;
                IsWorkingChanged(MyCube);
            }
            else
            {
                Emitter.AppendingCustomInfo -= AppendingCustomInfo;
                Emitter.EnabledChanged -= CheckEmitter;
                MyCube.IsWorkingChanged -= IsWorkingChanged;
            }
        }

        private void IsWorkingChanged(MyCubeBlock myCubeBlock)
        {
            IsFunctional = myCubeBlock.IsWorking;
            IsWorking = myCubeBlock.IsWorking;
        }

        public override void OnBeforeRemovedFromContainer() { if (Entity.InScene) OnRemovedFromScene(); }
        public override void Close()
        {
            try
            {
                base.Close();
                if (Session.Enforced.Debug == 3) Log.Line($"Close: {EmitterMode} - EmitterId [{Entity.EntityId}]");
                if (Session.Emitters.Contains(this)) Session.Emitters.Remove(this);
                if (ShieldComp?.StationEmitter == this)
                {
                    if ((int)EmitterMode == ShieldComp.EmitterMode)
                    {
                        ShieldComp.EmittersWorking = false;
                        ShieldComp.EmitterEvent = true;
                    }
                    ShieldComp.StationEmitter = null;
                }
                else if (ShieldComp?.ShipEmitter == this)
                {
                    if ((int)EmitterMode == ShieldComp.EmitterMode)
                    {
                        ShieldComp.EmittersWorking = false;
                        ShieldComp.EmitterEvent = true;
                    }
                    ShieldComp.ShipEmitter = null;
                }
                //BlockParticleStop();
            }
            catch (Exception ex) { Log.Line($"Exception in Close: {ex}"); }
        }

        public override void MarkForClose()
        {
            try
            {
                base.MarkForClose();
                if (Session.Enforced.Debug == 3) Log.Line($"MarkForClose: {EmitterMode} - EmitterId [{Entity.EntityId}]");
            }
            catch (Exception ex) { Log.Line($"Exception in MarkForClose: {ex}"); }
        }
    }
}