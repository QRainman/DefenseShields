﻿using Sandbox.Game.Entities;
using Sandbox.ModAPI;

namespace DefenseSystems
{
    public partial class Displays
    {
        internal DisplayState State { get; set; }
        internal DisplaySettings Set { get; set; }
        internal DefenseBus DefenseBus;
        internal IMyTextPanel Display { get; set; }
        internal MyCubeGrid MyGrid { get; set; }
        internal MyCubeBlock MyCube { get; set; }
        internal bool ContainerInited { get; set; }
        internal bool SettingsUpdated { get; set; }
        internal bool ClientUiUpdate { get; set; }
        internal bool ShieldEnabled { get; set; }

        internal uint Tick;

        private const int SyncCount = 60;

        private bool _imagesDetected;
        private bool _off;
        private bool _wasText;
        private bool _isDedicated;
        private bool _mpActive;
        private bool _isServer;
        private bool _myDisplay;
        private bool _firstLoop = true;
        private bool _readyToSync;
        private bool _firstSync;
        private bool _bInit;

        private int _bCount;
        private int _bTime;
        private int _waitCount;
        private int _pEventIdWas;

        private uint _mId;
    }
}
