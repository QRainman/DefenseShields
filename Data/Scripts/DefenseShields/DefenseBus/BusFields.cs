﻿using System;
using System.Collections.Generic;
using DefenseSystems.Support;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using VRage.Game.Entity;
using VRageMath;

namespace DefenseSystems
{
    public partial class DefenseBus 
    {
        public enum LogicState
        {
            Join,
            Leave,
            Close,
            Offline,
            Online,
            Active,
            Suspend
        }

        internal readonly object SubLock = new object();
        internal readonly object SubUpdateLock = new object();
        internal SortedSet<MyCubeGrid> SortedGrids = new SortedSet<MyCubeGrid>(new GridPriority());
        internal SortedSet<Controllers> SortedControllers = new SortedSet<Controllers>(new ControlPriority());
        internal SortedSet<Emitters> SortedEmitters = new SortedSet<Emitters>(new EmitterPriority());

        internal HashSet<MyCubeGrid> NewTmp1 { get; set; } = new HashSet<MyCubeGrid>();
        internal HashSet<MyCubeGrid> AddSubs { get; set; } = new HashSet<MyCubeGrid>();
        internal HashSet<MyCubeGrid> RemSubs { get; set; } = new HashSet<MyCubeGrid>();
        internal HashSet<MyCubeGrid> SubGrids { get; set; } = new HashSet<MyCubeGrid>();
        internal Dictionary<MyCubeGrid, SubGridInfo> LinkedGrids { get; set; } = new Dictionary<MyCubeGrid, SubGridInfo>();

        internal MyResourceDistributorComponent MyResourceDist { get; set; }
        internal MyCubeGrid MasterGrid;

        internal float GridIntegrity { get; set; }

        internal bool SubUpdate { get; set; }
        internal bool FunctionalAdded { get; set; }
        internal bool FunctionalRemoved { get; set; }
        internal bool FunctionalChanged { get; set; }
        internal bool BlockAdded { get; set; }
        internal bool BlockRemoved { get; set; }
        internal bool BlockChanged { get; set; }
        internal bool UpdateGridDistributor { get; set; }
        internal bool CheckForDistributor { get; set; }

        internal Vector3D[] PhysicsOutside { get; set; } = new Vector3D[642];

        internal Vector3D[] PhysicsOutsideLow { get; set; } = new Vector3D[162];

        internal Controllers ActiveController { get; set; }

        internal Enhancers ActiveEnhancer { get; set; }

        internal Modulators ActiveModulator { get; set; }

        internal int EmitterMode { get; set; } = -1;
        internal long ActiveEmitterId { get; set; }

        internal Emitters ActiveEmitter { get; set; }

        internal O2Generators ActiveO2Generator { get; set; }

        internal string ModulationPassword { get; set; }

        internal bool EmitterLos { get; set; }

        internal bool EmittersSuspended { get; set; }

        internal bool O2Updated { get; set; }

        internal float DefaultO2 { get; set; }

        internal bool CheckEmitters { get; set; }

        internal bool GridIsMoving { get; set; }

        internal bool EmitterEvent { get; set; }

        internal double ShieldVolume { get; set; }
    }
}
