﻿using System.Collections.Generic;
using DefenseSystems.Support;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage;

namespace DefenseSystems
{
    public partial class Bus
    {
        public void SortAndAddBlock<T>(T block)
        {
            var controller = block as Controllers;
            var emitter = block as Emitters;
            var regen = block as BlockRegen;

            if (controller != null && !SortedControllers.Add(controller)) return;
            if (emitter != null && !SortedEmitters.Add(emitter)) return;
            if (regen != null && !SortedRegens.Add(regen)) return;

            UpdateLogicState(block, LogicState.Join);
        }

        public void RemoveBlock<T>(T block)
        {
            var controller = block as Controllers;
            var emitter = block as Emitters;
            var regen = block as BlockRegen;

            if (controller != null && !SortedControllers.Remove(controller)) return;
            if (emitter != null && !SortedEmitters.Remove(emitter)) return;
            if (regen != null && !SortedRegens.Remove(regen)) return;

            UpdateLogicState(block, LogicState.Leave);
        }

        public void RemoveSubBlocks<T>(SortedSet<T> list, MyCubeGrid grid)
        {
            var tmpArray = new T[list.Count];
            list.CopyTo(tmpArray);
            foreach (var b in tmpArray)
            {
                var controller = b as Controllers;
                if (controller != null && controller.MyCube.CubeGrid == grid)
                {
                    Log.Line($"[ControllerSubSplit] - cId:{controller.MyCube.EntityId} - left BusId:{Spine.EntityId} - iMaster:{controller == ActiveController}");
                    SortedControllers.Remove(controller);
                    UpdateNetworks(controller.MyCube, LogicState.Leave);
                }

                var emitter = b as Emitters;
                if (emitter != null && emitter.MyCube.CubeGrid == grid)
                {
                    Log.Line($"[EmitterSubSplit] - cId:{emitter.MyCube.EntityId} - left BusId:{Spine.EntityId} - iMaster:{emitter == ActiveEmitter}");
                    SortedEmitters.Remove(emitter);
                    UpdateNetworks(emitter.MyCube, LogicState.Leave);
                }

                var regen = b as BlockRegen;
                if (regen != null && regen.MyCube.CubeGrid == grid)
                {
                    Log.Line($"[EmitterSubSplit] - cId:{regen.MyCube.EntityId} - left BusId:{Spine.EntityId} - iMaster:{regen == ActiveRegen}");
                    SortedRegens.Remove(regen);
                    UpdateNetworks(regen.MyCube, LogicState.Leave);
                }
                /*
                var enhancer = b as Enhancers;
                if (enhancer != null) SortedEnhancers.Remove(enhancer);

                var modulator = b as Modulators;
                if (modulator != null) SortedModulators.Remove(modulator);

                var o2Generator = b as O2Generators;
                if (o2Generator != null) SortedO2Generators.Remove(o2Generator);
                */
            }
        }

        private void UpdateLogicState<T>(T type, LogicState state)
        {
            var controller = type as Controllers;
            if (controller != null)
            {
                switch (state)
                {
                    case LogicState.Join:
                        Log.Line($"[ControllerJoin-] - cId:{controller.MyCube.EntityId} - gMaster:{Spine != null} - iMaster:{controller == ActiveController}");
                        controller.RegisterEvents(controller.MyCube.CubeGrid, this, true);
                        controller.IsAfterInited = false;
                        break;
                    case LogicState.Leave:
                        Log.Line($"[ControllerLeave] - cId:{controller.MyCube.EntityId} - gMaster:{Spine != null} - iMaster:{controller == ActiveController}");
                        controller.RegisterEvents(controller.MyCube.CubeGrid, this,false);
                        break;
                }
            }
            var emitter = type as Emitters;
            if (emitter != null)
            {
                switch (state)
                {
                    case LogicState.Join:
                        Log.Line($"[EmitterJoin----] - eId:{emitter.MyCube.EntityId} - gMaster:{Spine != null} - iMaster:{emitter == ActiveEmitter}");

                        emitter.RegisterEvents(emitter.MyCube.CubeGrid, this, true);
                        emitter.IsAfterInited = false;
                        break;
                    case LogicState.Leave:
                        Log.Line($"[EmitterLeave---] - eId:{emitter.MyCube.EntityId} - gMaster:{Spine != null} - iMaster:{emitter == ActiveEmitter}");
                        emitter.RegisterEvents(emitter.MyCube.CubeGrid, this, false);
                        break;
                }
            }
            var regen = type as BlockRegen;
            if (regen != null)
            {
                switch (state)
                {
                    case LogicState.Join:
                        Log.Line($"[RegenJoin------] - rId:{regen.MyCube.EntityId} - gMaster:{Spine != null} - iMaster:{regen == ActiveRegen}");

                        regen.RegisterEvents(regen.MyCube.CubeGrid, this, true);
                        regen.IsAfterInited = false;
                        break;
                    case LogicState.Leave:
                        Log.Line($"[RegenLeave-----] - rId:{regen.MyCube.EntityId} - gMaster:{Spine != null} - iMaster:{regen == ActiveRegen}");
                        regen.RegisterEvents(regen.MyCube.CubeGrid, this, false);
                        break;
                }
            }
            if (Inited) UpdateLogicMasters(type, state);
        }

        internal void UpdateLogicMasters<T>(T type, LogicState state)
        {
            var controller = type as Controllers;
            var emitter = type as Emitters;
            var regen = type as BlockRegen;

            if (controller != null)
            {
                Controllers newMaster;
                switch (state)
                {
                    case LogicState.Join:
                        newMaster = SortedControllers.Max;
                        if (ActiveController == newMaster) return;
                        Log.Line($"[J-ControllerElect] - [iMaster {newMaster == controller}] - state:{state} - myId:{controller.MyCube.EntityId}");
                        ActiveController = newMaster;
                        break;
                    case LogicState.Leave:
                        var keepMaster = !(ActiveController == null || ActiveController.MyCube.Closed || !ActiveController.MyCube.InScene || ActiveController == controller);
                        if (keepMaster) return;

                        newMaster = SortedControllers.Max;
                        if (ActiveController == newMaster) return;
                        Log.Line($"[L-ControllerElect] - [iMaster {newMaster == controller}] - state:{state} - myId:{controller.MyCube.EntityId}");

                        ActiveController = newMaster;
                        break;
                }
            }
            else if (emitter != null)
            {
                Emitters newMaster;
                switch (state)
                {
                    case LogicState.Join:
                        newMaster = SortedEmitters.Max;
                        if (ActiveEmitter == newMaster) return;
                        Log.Line($"[J-Emitter Elect] - [iMaster {newMaster == emitter}] - state:{state} - myId:{emitter.MyCube.EntityId}");
                        ActiveEmitter = newMaster;
                        break;
                    case LogicState.Leave:
                        var keepMaster = !(ActiveEmitter == null || ActiveEmitter.MyCube.Closed || !ActiveEmitter.MyCube.InScene || ActiveEmitter == emitter);
                        if (keepMaster) return;

                        newMaster = SortedEmitters.Max;
                        if (ActiveEmitter == newMaster) return;
                        Log.Line($"[L-Emitter Elect] - [iMaster {newMaster == emitter}] - state:{state} - myId:{emitter.MyCube.EntityId}");
                        ActiveEmitter = newMaster;
                        break;
                }
                EmitterEvent = true;
                ActiveEmitterId = ActiveEmitter?.MyCube?.EntityId ?? 0;
            }
            else if (regen != null)
            {
                BlockRegen newMaster;
                switch (state)
                {
                    case LogicState.Join:
                        newMaster = SortedRegens.Max;
                        if (ActiveRegen == newMaster) return;
                        Log.Line($"[J-Regen Elect] - [iMaster {newMaster == regen}] - state:{state} - myId:{regen.MyCube.EntityId}");
                        ActiveRegen = newMaster;
                        break;
                    case LogicState.Leave:
                        var keepMaster = !(ActiveRegen == null || ActiveRegen.MyCube.Closed || !ActiveRegen.MyCube.InScene || ActiveRegen == regen);
                        if (keepMaster) return;

                        newMaster = SortedRegens.Max;
                        if (ActiveRegen == newMaster) return;
                        Log.Line($"[L-Regen Elect] - [iMaster {newMaster == regen}] - state:{state} - myId:{regen.MyCube.EntityId}");
                        ActiveRegen = newMaster;
                        break;
                }
            }
        }

        private void UpdateNetworks<T>(T type, LogicState state)
        {
            var cube = type as MyCubeBlock;
            var controller = cube?.GameLogic as Controllers;
            var emitter = cube?.GameLogic as Emitters;
            var regen = cube?.GameLogic as BlockRegen;

            var newSplit = false;

            if (controller != null)
            {
                if (state == LogicState.Leave)
                {
                    if (ActiveController == controller)
                    {
                        Log.Line($"[DeRegistering] - from spine [sId:{Spine.EntityId}] as active controller [cId:{controller.MyCube.EntityId}]");
                        newSplit = true;
                        ActiveController = null;
                        UpdateLogicMasters(type, state);
                    }
                    Log.Line($"[Join New Bus] - cId:{controller.MyCube.EntityId}]");
                    controller.Registry.RegisterWithBus(controller, controller.MyCube.CubeGrid, true, controller.Bus, out controller.Bus);
                }
            }
            else if (emitter != null)
            {
                if (state == LogicState.Leave)
                {
                    if (ActiveEmitter == emitter)
                    {
                        Log.Line($"[DeRegistering] - from spine [sId:{Spine.EntityId}] as active emitter [eId:{emitter.MyCube.EntityId}]");
                        newSplit = true;
                        ActiveEmitter = null;
                        UpdateLogicMasters(type, state);
                    }
                    Log.Line($"[Join New Bus] - eId:{emitter.MyCube.EntityId}]");
                    emitter.Registry.RegisterWithBus(emitter, emitter.MyCube.CubeGrid, true, emitter.Bus, out emitter.Bus);
                }
            }
            else if (regen != null)
            {
                if (state == LogicState.Leave)
                {
                    if (ActiveRegen == regen)
                    {
                        Log.Line($"[DeRegistering] - from spine [sId:{Spine.EntityId}] as active regen [eId:{regen.MyCube.EntityId}]");
                        newSplit = true;
                        ActiveRegen = null;
                        UpdateLogicMasters(type, state);
                    }
                    Log.Line($"[Join New Bus] - rId:{regen.MyCube.EntityId}]");
                    regen.Registry.RegisterWithBus(regen, regen.MyCube.CubeGrid, true, regen.Bus, out regen.Bus);
                }
            }
            if (!BusIsSplit && newSplit) Split(cube.CubeGrid, state);
        }


        internal void BlockMonitor()
        {
            if (BlockChanged)
            {
                var tick = Session.Instance.Tick;
                BlockEvent = true;
                ActiveController.ShapeEvent = true;

                LosCheckTick = tick + 1800;
                if (BlockAdded) ActiveController.ShapeTick = tick + 300;
                else ActiveController.ShapeTick = tick + 1800;
            }
            if (FunctionalChanged) FunctionalEvent = true;

            FunctionalAdded = false;
            FunctionalRemoved = false;
            FunctionalChanged = false;

            BlockChanged = false;
            BlockRemoved = false;
            BlockAdded = false;
        }

        internal void SomeBlockChanged(bool backGround)
        {
            if (BlockEvent)
            {
                var notReady = !FuncTask.IsComplete || ActiveController.DsState.State.Sleeping || ActiveController.DsState.State.Suspended;
                if (notReady) return;
                if (FunctionalEvent) FunctionalBlockChanged(backGround);
                BlockEvent = false;
                FuncTick = Session.Instance.Tick + 60;
            }
        }

        private void FunctionalBlockChanged(bool backGround)
        {
            if (backGround) FuncTask = MyAPIGateway.Parallel.StartBackground(BackGroundChecks);
            else BackGroundChecks();
            FunctionalEvent = false;
        }

        private void BackGroundChecks()
        {
            var gridDistNeedUpdate = UpdateGridDistributor || MyResourceDist?.SourcesEnabled == MyMultipleEnabledEnum.NoObjects;
            UpdateGridDistributor = false;
            lock (SubLock)
            {
                _powerSources.Clear();
                _functionalBlocks.Clear();
                _batteryBlocks.Clear();
                _displayBlocks.Clear();

                foreach (var grid in LinkedGrids.Keys)
                {
                    var mechanical = SubGrids.Contains(grid);
                    foreach (var block in grid.GetFatBlocks())
                    {
                        if (mechanical)
                        {
                            if (gridDistNeedUpdate)
                            {
                                var controller = block as MyShipController;
                                if (controller != null)
                                {
                                    var distributor = controller.GridResourceDistributor;
                                    if (distributor.SourcesEnabled != MyMultipleEnabledEnum.NoObjects)
                                    {
                                        MyResourceDist = controller.GridResourceDistributor;
                                        gridDistNeedUpdate = false;
                                    }
                                }
                            }
                        }

                        if (!Session.Instance.DedicatedServer)
                        {
                            _functionalBlocks.Add(block);
                            var display = block as IMyTextPanel;
                            if (display != null) _displayBlocks.Add(display);
                        }

                        var battery = block as IMyBatteryBlock;
                        if (battery != null) _batteryBlocks.Add(battery);

                        var source = block.Components.Get<MyResourceSourceComponent>();
                        if (source == null) continue;

                        foreach (var type in source.ResourceTypes)
                        {
                            if (type != MyResourceDistributorComponent.ElectricityId) continue;
                            _powerSources.Add(source);
                            break;
                        }
                    }
                }
            }
        }

    }
}