﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DefenseShields.Support;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace DefenseShields
{
    public partial class Emitters
    {
        internal ShieldGridComponent ShieldComp;
        internal MyResourceSinkInfo ResourceInfo;
        internal List<Vector3D> LosScaledCloud = new List<Vector3D>(2000);
        internal MyEntitySubpart SubpartRotor;

        private const string PlasmaEmissive = "PlasmaEmissive";


        private readonly List<int> _vertsSighted = new List<int>();
        private readonly ConcurrentDictionary<int, bool> _blocksLos = new ConcurrentDictionary<int, bool>();
        private readonly MyDefinitionId _gId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Electricity");

        private DSUtils _dsUtil = new DSUtils();

        private uint _tick;
        private int _count = -1;
        private int _lCount;
        private int _wasMode;
        private int _unitSpherePoints = 2000;
        private bool _updateLosState = true;

        private float _power = 0.01f;
        private bool _tick60;
        private bool _isServer;
        private bool _isDedicated;
        private bool _compact;
        private bool _wasLink;
        private bool _wasBackup;
        private bool _wasSuspend;
        private bool _wasLos;
        private bool _wasLosState;
        private bool _disableLos;
        private bool _wasCompatible;
        private double _wasBoundingRange;
        private long _wasActiveEmitterId;

        public enum EmitterType
        {
            Station,
            Large,
            Small,
        }

        internal Definition Definition { get; set; }
        internal EmitterState EmiState { get; set; }

        internal IMyUpgradeModule Emitter { get; set; }
        internal EmitterType EmitterMode { get; set; }
        internal MyCubeGrid MyGrid { get; set; }
        internal MyCubeBlock MyCube { get; set; }

        internal MyResourceSinkComponent Sink { get; set; }

        internal int RotationTime { get; set; }
        internal int AnimationLoop { get; set; }
        internal int TranslationTime { get; set; }

        internal float EmissiveIntensity { get; set; }

        internal bool ServerUpdate { get; set; }
        internal bool IsStatic { get; set; }
        internal bool TookControl { get; set; }
        internal bool ContainerInited { get; set; }
        internal bool IsFunctional { get; set; }
        internal bool IsWorking { get; set; }

    }
}
