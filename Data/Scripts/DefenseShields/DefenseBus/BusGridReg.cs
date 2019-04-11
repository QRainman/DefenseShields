﻿using System;
using DefenseSystems.Support;
using Sandbox.Game.Entities;
using VRage;
using VRage.Game.ModAPI;

namespace DefenseSystems
{
    public partial class Bus
    {
        public void RegisterGridEvents(MyCubeGrid grid, bool register = true)
        {
            if (register)
            {
                if (Session.Instance.IsServer) grid.OnBlockOwnershipChanged += OwnerChangedEvent;
                grid.OnHierarchyUpdated += HierarchyChangedEvent;
                grid.OnBlockAdded += BlockAddedEvent;
                grid.OnBlockRemoved += BlockRemovedEvent;
                grid.OnFatBlockAdded += FatBlockAddedEvent;
                grid.OnFatBlockRemoved += FatBlockRemovedEvent;
                grid.OnBlockIntegrityChanged += BlockIntegrityEvent;
                grid.OnGridSplit += GridSplitEvent;
            }
            else
            {
                if (Session.Instance.IsServer) grid.OnBlockOwnershipChanged -= OwnerChangedEvent;
                grid.OnHierarchyUpdated -= HierarchyChangedEvent;
                grid.OnBlockAdded -= BlockAddedEvent;
                grid.OnBlockRemoved -= BlockRemovedEvent;
                grid.OnFatBlockAdded -= FatBlockAddedEvent;
                grid.OnFatBlockRemoved -= FatBlockRemovedEvent;
                grid.OnBlockIntegrityChanged += BlockIntegrityEvent;
                grid.OnGridSplit -= GridSplitEvent;
            }
        }

        private void OwnerChangedEvent(MyCubeGrid myCubeGrid)
        {
            try
            {
                //if (MyCube == null || LocalGrid == null || Spine == null || MyCube.OwnerId == _controllerOwnerId && LocalGrid.BigOwners.Count != 0 && LocalGrid.BigOwners[0] == _gridOwnerId) return;
                //GridOwnsController();
            }
            catch (Exception ex) { Log.Line($"Exception in Controller OwnerChanged: {ex}"); }
        }


        private void GridSplitEvent(MyCubeGrid oldGrid, MyCubeGrid newGrid)
        {
            newGrid.RecalculateOwners();
        }

        private void HierarchyChangedEvent(MyCubeGrid grid = null)
        {
            try
            {
                SubUpdate = true;
                if (grid != null && SubTick < Session.Instance.Tick + 10) SubGridDetect(grid);
            }
            catch (Exception ex) { Log.Line($"Exception in Controller HierarchyChanged: {ex}"); }
        }

        private void BlockAddedEvent(IMySlimBlock block)
        {
            try
            {
                BlockAdded = true;
                BlockChanged = true;
                if (Session.Instance.IsServer) SpineIntegrity += block.MaxIntegrity;
                if (!CheckIntegrity) ActiveRegen?.BlockChanged(block);
            }
            catch (Exception ex) { Log.Line($"Exception in Controller BlockAdded: {ex}"); }
        }

        private void BlockRemovedEvent(IMySlimBlock block)
        {
            try
            {
                BlockRemoved = true;
                BlockChanged = true;
                if (Session.Instance.IsServer) SpineIntegrity -= block.MaxIntegrity;
                if (!CheckIntegrity) ActiveRegen?.BlockChanged(block);
            }
            catch (Exception ex) { Log.Line($"Exception in Controller BlockRemoved: {ex}"); }
        }

        private void BlockIntegrityEvent(IMySlimBlock block)
        {
            try
            {
                if (!CheckIntegrity) ActiveRegen?.BlockChanged(block);
            }
            catch (Exception ex) { Log.Line($"Exception in Controller BlockRemoved: {ex}"); }
        }

        private void FatBlockAddedEvent(MyCubeBlock myCubeBlock)
        {
            try
            {
                FunctionalAdded = true;
                FunctionalChanged = true;
                if (MyResourceDist == null)
                {
                    var controller = myCubeBlock as MyShipController;
                    if (controller != null)
                        if (controller.GridResourceDistributor.SourcesEnabled != MyMultipleEnabledEnum.NoObjects) UpdateGridDistributor = true;
                }
            }
            catch (Exception ex) { Log.Line($"Exception in Controller FatBlockAdded: {ex}"); }
        }

        private void FatBlockRemovedEvent(MyCubeBlock myCubeBlock)
        {
            try
            {
                FunctionalRemoved = true;
                FunctionalChanged = true;
            }
            catch (Exception ex) { Log.Line($"Exception in Controller FatBlockRemoved: {ex}"); }
        }
    }
}