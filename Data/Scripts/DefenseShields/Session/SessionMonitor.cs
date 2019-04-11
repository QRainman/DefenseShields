﻿namespace DefenseSystems
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Support;
    using Sandbox.Game.Entities;
    using Sandbox.ModAPI;
    using Sandbox.ModAPI.Weapons;
    using VRage.Game.Entity;
    using VRage.Game.ModAPI;
    using VRageMath;

    public partial class Session
    {
        #region WebMonitor
        internal void WebMonitor()
        {
            try
            {
                while (Monitor)
                {
                    _autoResetEvent.WaitOne();
                    if (!Monitor) break;
                    _newFrame = false;
                    _workData.DoIt(new List<Controllers>(FunctionalShields.Keys), Tick);
                    MinScaler = _workData.MinScaler;
                    MyAPIGateway.Parallel.For(0, _workData.ShieldCnt, x =>
                    {
                        var s = _workData.ShieldList[x];
                        var tick = _workData.Tick;
                        if (_newFrame || s.MarkedForClose || !s.Warming) return;
                        var notBubble = s.DsState.State.ProtectMode > 0;
                        if (!IsServer)
                        {
                            if (notBubble != s.NotBubble)
                            {
                                lock (s.Bus.SubLock) foreach (var sub in s.Bus.SubGrids) _entRefreshQueue.Enqueue(sub);
                                s.NotBubble = notBubble;
                            }

                            if (EntSlotTick && RefreshCycle == s.MonitorSlot)
                            {
                                List<MyEntity> monitorListClient = null;
                                var newSubClient = false;
                                if (!notBubble) monitorListClient = new List<MyEntity>();
                                MonitorRefreshTasks(x, ref monitorListClient, notBubble, ref newSubClient);
                            }
                            s.TicksWithNoActivity = 0;
                            s.LastWokenTick = tick;
                            s.Asleep = false;
                            return;
                        }

                        bool shieldActive;
                        lock (ActiveShields) shieldActive = ActiveShields.Contains(s);

                        if (s.LostPings > 59)
                        {
                            if (shieldActive)
                            {
                                if (Enforced.Debug >= 2) Log.Line("Logic Paused by lost pings");
                                lock (ActiveShields) ActiveShields.Remove(s);
                                s.WasPaused = true;
                            }
                            s.Asleep = false;
                            return;
                        }
                        if (Enforced.Debug >= 2 && s.LostPings > 0) Log.Line($"Lost Logic Pings:{s.LostPings}");
                        if (shieldActive) s.LostPings++;

                        if (s.Asleep && EmpStore.Count != 0 && Vector3D.DistanceSquared(s.DetectionCenter, EmpWork.EpiCenter) <= SyncDistSqr)
                        {
                            s.TicksWithNoActivity = 0;
                            s.LastWokenTick = tick;
                            s.Asleep = false;
                            return;
                        }

                        if (!shieldActive && s.LostPings > 59)
                        {
                            s.Asleep = true;
                            return;
                        }

                        List<MyEntity> monitorList = null;
                        var newSub = false;
                        if (!notBubble) monitorList = new List<MyEntity>();
                        if (EntSlotTick && RefreshCycle == s.MonitorSlot) MonitorRefreshTasks(x, ref monitorList, notBubble, ref newSub);

                        if (notBubble) return;
                        if (tick < s.LastWokenTick + 400 || s.Missiles.Count > 0)
                        {
                            s.Asleep = false;
                            return;
                        }

                        if (s.ShieldIsMobile && s.Bus.Spine.Physics.IsMoving)
                        {
                            s.LastWokenTick = tick;
                            s.Asleep = false;
                            return;
                        }

                        if (!s.PlayerByShield && !s.MoverByShield && !s.NewEntByShield)
                        {
                            if (s.TicksWithNoActivity++ % EntCleanCycle == 0) s.EntCleanUpTime = true;
                            if (shieldActive && !s.WasPaused && tick > 1200)
                            {
                                if (Enforced.Debug >= 2) Log.Line($"Logic Paused by monitor");
                                lock (ActiveShields) ActiveShields.Remove(s);
                                s.WasPaused = true;
                                s.Asleep = false;
                                s.TicksWithNoActivity = 0;
                                s.LastWokenTick = tick;
                            }
                            else s.Asleep = true;
                            return;
                        }

                        var intersect = false;
                        if (!(EntSlotTick && RefreshCycle == s.MonitorSlot)) MyGamePruningStructure.GetTopmostEntitiesInBox(ref s.WebBox, monitorList, MyEntityQueryType.Dynamic);
                        for (int i = 0; i < monitorList.Count; i++)
                        {
                            var ent = monitorList[i];

                            if (ent.Physics == null || !(ent is MyCubeGrid || ent is IMyCharacter || ent is IMyMeteor)) continue;
                            if (ent.Physics.IsMoving)
                            {
                                if (s.WebBox.Intersects(ent.PositionComp.WorldAABB))
                                {
                                    intersect = true;
                                    break;
                                }
                            }
                        }

                        if (!intersect)
                        {
                            s.Asleep = true;
                            return;
                        }
                        s.TicksWithNoActivity = 0;
                        s.LastWokenTick = tick;
                        s.Asleep = false;
                    });

                    if (_workData.Tick % 180 == 0 && _workData.Tick > 1199)
                    {
                        _entRefreshTmpList.Clear();
                        _entRefreshTmpList.AddRange(_globalEntTmp.Where(info => _workData.Tick - 540 > info.Value));
                        foreach (var dict in _entRefreshTmpList)
                        {
                            var ent = dict.Key;
                            _entRefreshQueue.Enqueue(ent);
                            uint value;
                            _globalEntTmp.TryRemove(ent, out value);
                        }
                    }
                }
            }
            catch (Exception ex) { Log.Line($"Exception in WebMonitor: {ex}"); }
        }

        internal void MonitorRefreshTasks(int x, ref List<MyEntity> monitorList, bool reInforce, ref bool newSub)
        {
            var s = _workData.ShieldList[x];

            if (reInforce)
            {
                HashSet<MyCubeGrid> subs;
                lock (s.Bus.SubLock) subs = new HashSet<MyCubeGrid>(s.Bus.SubGrids);
                var newMode = !s.NotBubble;
                if (!newMode) return;
                foreach (var sub in subs)
                {
                    if (!_globalEntTmp.ContainsKey(sub)) newSub = true;
                    _entRefreshQueue.Enqueue(sub);
                    if (!s.WasPaused) _globalEntTmp[sub] = _workData.Tick;
                }

                s.NotBubble = true;
                s.TicksWithNoActivity = 0;
                s.LastWokenTick = _workData.Tick;
                s.Asleep = false;
            }
            else
            {
                var newMode = false;
                if (s.NotBubble)
                {
                    HashSet<MyCubeGrid> subs;
                    lock (s.Bus.SubLock) subs = new HashSet<MyCubeGrid>(s.Bus.SubGrids); 
                    foreach (var sub in subs)
                    {
                        _entRefreshQueue.Enqueue(sub);
                        if (!s.WasPaused) _globalEntTmp[sub] = _workData.Tick;
                    }
                    //if (Enforced.Debug >= 2) Log.Line($"found Reinforce");
                    s.NotBubble = false;
                    s.TicksWithNoActivity = 0;
                    s.LastWokenTick = _workData.Tick;
                    s.Asleep = false;
                    newMode = true;
                }

                if (!newMode)
                {
                    // var testMat = s.DetectMatrixOutside;
                    // var shape1 = new Sphere(Vector3D.Zero, 1.0).Transformed(testMat);
                    var foundNewEnt = false;
                    var disableVoxels = Enforced.DisableVoxelSupport == 1 || s.Bus.ActiveModulator == null || s.Bus.ActiveModulator.ModSet.Settings.ModulateVoxels;
                    MyGamePruningStructure.GetTopmostEntitiesInBox(ref s.WebBox, monitorList);
                    if (!s.WasPaused)
                    {
                        foreach (var ent in monitorList)
                        {
                            var voxel = ent as MyVoxelBase;
                            if (ent == null || ent.MarkedForClose || (voxel == null && (ent.Physics == null || ent.DefinitionId == null)) || (!s.ShieldIsMobile && voxel != null) || (disableVoxels && voxel != null) || (voxel != null && voxel != voxel.RootVoxel))
                            {
                                continue;
                            }

                            if (ent is IMyFloatingObject || ent is IMyEngineerToolBase || !s.WebSphere.Intersects(ent.PositionComp.WorldVolume)) continue;

                            // var halfExtents = ent.PositionComp.LocalAABB.HalfExtents;
                            // if (halfExtents.X < 1) halfExtents.X = 10;
                            // if (halfExtents.Y < 1) halfExtents.Y = 10;
                            // if (halfExtents.Z < 1) halfExtents.Z = 10;
                            // var shape2 = new Box(-halfExtents, halfExtents).Transformed(ent.WorldMatrix);
                            // var test = Gjk.Intersects(ref shape1, ref shape2);
                            // Log.Line($"{ent.DebugName} - {test}");
                            if (CustomCollision.NewObbPointsInShield(ent, s.DetectMatrixOutsideInv) > 0)
                            {
                                if (!_globalEntTmp.ContainsKey(ent))
                                {
                                    foundNewEnt = true;
                                    s.Asleep = false;
                                }

                                _globalEntTmp[ent] = _workData.Tick;
                            }
                            s.NewEntByShield = foundNewEnt;
                        }
                    }
                    else s.NewEntByShield = false;

                    if (!s.NewEntByShield)
                    {
                        var foundPlayer = false;
                        foreach (var player in Players.Values)
                        {
                            var character = player.Character;
                            if (character == null) continue;

                            if (Vector3D.DistanceSquared(character.PositionComp.WorldMatrix.Translation, s.DetectionCenter) < SyncDistSqr)
                            {
                                foundPlayer = true;
                                break;
                            }
                        }
                        s.PlayerByShield = foundPlayer;
                    }
                    if (!s.PlayerByShield)
                    {
                        s.MoverByShield = false;
                        var newMover = false;
                        var moverList = new List<MyEntity>();

                        MyGamePruningStructure.GetTopMostEntitiesInBox(ref s.ShieldBox3K, moverList, MyEntityQueryType.Dynamic);
                        for (int i = 0; i < moverList.Count; i++)
                        {
                            var ent = moverList[i];

                            var meteor = ent as IMyMeteor;
                            if (meteor != null)
                            {
                                if (CustomCollision.FutureIntersect(s, ent, s.DetectMatrixOutside, s.DetectMatrixOutsideInv))
                                {
                                    if (Enforced.Debug >= 2) Log.Line($"[Future Intersecting Meteor] distance from shieldCenter: {Vector3D.Distance(s.DetectionCenter, ent.WorldMatrix.Translation)} - waking:");
                                    newMover = true;
                                    break;
                                }
                                continue;
                            }

                            if (!(ent.Physics == null || ent is MyCubeGrid || ent is IMyCharacter)) continue;
                            var entPos = ent.PositionComp.WorldAABB.Center;

                            var keyFound = s.EntsByMe.ContainsKey(ent);
                            if (keyFound)
                            {
                                if (!s.EntsByMe[ent].Pos.Equals(entPos, 1e-3))
                                {
                                    MoverInfo moverInfo;
                                    s.EntsByMe.TryRemove(ent, out moverInfo);
                                    s.EntsByMe.TryAdd(ent, new MoverInfo(entPos, _workData.Tick));
                                    if (moverInfo.CreationTick == _workData.Tick - 1)
                                    {
                                        if (Enforced.Debug >= 3 && s.WasPaused) Log.Line($"[Moved] Ent:{ent.DebugName} - howMuch:{Vector3D.Distance(entPos, s.EntsByMe[ent].Pos)} - ShieldId [{s.Shield.EntityId}]");
                                        newMover = true;
                                    }
                                    break;
                                }
                            }
                            else
                            {
                                if (Enforced.Debug >= 3) Log.Line($"[NewMover] Ent:{ent.DebugName} - ShieldId [{s.Shield.EntityId}]");
                                s.EntsByMe.TryAdd(ent, new MoverInfo(entPos, _workData.Tick));
                            }
                        }
                        s.MoverByShield = newMover;
                    }

                    if (_workData.Tick < s.LastWokenTick + 400)
                    {
                        s.Asleep = false;
                        return;
                    }
                }

                if (s.EntCleanUpTime)
                {
                    s.EntCleanUpTime = false;
                    if (!s.EntsByMe.IsEmpty)
                    {
                        var entsByMeTmp = new List<KeyValuePair<MyEntity, MoverInfo>>();
                        entsByMeTmp.AddRange(s.EntsByMe.Where(info => !info.Key.InScene || _workData.Tick - info.Value.CreationTick > EntMaxTickAge));
                        for (int i = 0; i < entsByMeTmp.Count; i++)
                        {
                            MoverInfo mInfo;
                            s.EntsByMe.TryRemove(entsByMeTmp[i].Key, out mInfo);
                        }
                    }
                }
            }
        }
        #endregion

        #region Timings / LoadBalancer
        private void Timings()
        {
            _newFrame = true;
            Tick = (uint)(Session.ElapsedPlayTime.TotalMilliseconds * TickTimeDiv);
            Tick20 = Tick % 20 == 0;
            Tick60 = Tick % 60 == 0;
            Tick60 = Tick % 60 == 0;
            Tick180 = Tick % 180 == 0;
            Tick300 = Tick % 300 == 0;
            Tick600 = Tick % 600 == 0;
            Tick1800 = Tick % 1800 == 0;
            if (Tick1800 && AuthorPlayerId != 0) AuthorDebug();
            if (LogStats && (IsServer && LogServer || !IsServer && !LogServer))
            {
                Perf.Ticker(Tick, LogTime, LogFullReport, LogColumn);
            }
            if (_count++ == 59)
            {
                _count = 0;
                _lCount++;
                if (_lCount == 10)
                {
                    _lCount = 0;
                    _eCount++;
                    if (_eCount == 10)
                    {
                        _eCount = 0;
                        _previousEntId = -1;
                    }
                }
            }
            if (!GameLoaded && Tick > 100)
            {
                if (FirstLoop && Tick > 100)
                {
                    if (!WarHeadLoaded && WarTerminalReset != null)
                    {
                        WarTerminalReset.ShowInTerminal = true;
                        WarTerminalReset = null;
                        WarHeadLoaded = true;
                    }

                    if (!MiscLoaded)
                    {
                        MiscLoaded = true;
                        UtilsStatic.GetDefinitons();
                        if (!IsServer) PlayerConnected(MyAPIGateway.Session.Player.IdentityId);
                    }
                    GameLoaded = true;
                }
                else if (!FirstLoop)
                {
                    FirstLoop = true;
                    _bTapi.Init();
                }
            }
            if (EmpWork.EventRunning && EmpWork.Computed) EmpWork.EventComplete();

            if (Tick20)
            {
                Scale();
                EntSlotTick = Tick % (180 / EntSlotScaler) == 0;
                if (EntSlotTick) LoadBalancer();
            }
            else EntSlotTick = false;
        }

        internal static int GetSlot()
        {
            if (++_entSlotAssigner >= Instance.EntSlotScaler) _entSlotAssigner = 0;
            return _entSlotAssigner;
        }

        private void Scale()
        {
            if (Tick < 600) return;
            var oldScaler = EntSlotScaler;
            var globalProtCnt = GlobalProtect.Count;

            if (globalProtCnt <= 25) EntSlotScaler = 1;
            else if (globalProtCnt <= 50) EntSlotScaler = 2;
            else if (globalProtCnt <= 75) EntSlotScaler = 3;
            else if (globalProtCnt <= 100) EntSlotScaler = 4;
            else if (globalProtCnt <= 150) EntSlotScaler = 5;
            else if (globalProtCnt <= 200) EntSlotScaler = 6;
            else EntSlotScaler = 9;

            if (EntSlotScaler < MinScaler) EntSlotScaler = MinScaler;

            if (oldScaler != EntSlotScaler)
            {
                GlobalProtect.Clear();
                ProtSets.Clean();
                foreach (var s in FunctionalShields.Keys)
                {
                    s.AssignSlots();
                    s.Asleep = false;
                }
                foreach (var c in AllControllers)
                {
                    if (FunctionalShields.ContainsKey(c)) continue;
                    c.AssignSlots();
                    c.Asleep = false;
                }
                ScalerChanged = true;
            }
            else ScalerChanged = false;
        }

        private void LoadBalancer()
        {

            if (++RefreshCycle >= EntSlotScaler) RefreshCycle = 0;
            MyEntity ent;
            while (_entRefreshQueue.TryDequeue(out ent))
            {
                MyProtectors myProtector;
                if (!GlobalProtect.TryGetValue(ent, out myProtector)) continue;

                var entShields = myProtector.Shields;
                var refreshCount = 0;
                Controllers notBubble = null;
                var removeIShield = false;
                foreach (var s in entShields)
                {
                    if (s.WasPaused) continue;
                    if (s.DsState.State.ProtectMode > 0 && s.Bus.SubGrids.Contains(ent))
                    {
                        notBubble = s;
                        refreshCount++;
                    }
                    else if (!ent.InScene || !s.ResetEnts(ent, Tick))
                    {
                        myProtector.Shields.Remove(s);
                    }
                    else refreshCount++;

                    if (notBubble == null && myProtector.NotBubble == s)
                    {
                        removeIShield = true;
                        myProtector.NotBubble = null;
                    }

                    var detectedStates = s.PlayerByShield || s.MoverByShield || Tick <= s.LastWokenTick + 580 || notBubble != null || removeIShield;
                    if (ScalerChanged || detectedStates)
                    {
                        s.Asleep = false;
                    }
                }

                if (notBubble != null)
                {
                    myProtector.Shields.Remove(notBubble);
                    myProtector.NotBubble = notBubble;
                }

                myProtector.Shields.ApplyChanges();

                if (refreshCount == 0)
                {
                    GlobalProtect.Remove(ent);
                    ProtSets.Return(myProtector);
                }
            }
        }
        #endregion

        #region LogicUpdates
        private void LogicUpdates()
        {
            if (!Dispatched)
            {
                lock (ActiveShields)
                {
                    if (LogStats)
                    {
                        Perf.Active(ActiveShields.Count);
                        Perf.Paused(AllControllers.Count - FunctionalShields.Count);
                        Perf.Emitters(Emitters.Count);
                        Perf.Modulators(Modulators.Count);
                        Perf.Displays(Displays.Count);
                        Perf.Enhancers(Enhancers.Count);
                        Perf.O2Generators(O2Generators.Count);
                        Perf.Protected(GlobalProtect.Count);
                    }

                    foreach (var s in ActiveShields)
                    {
                        if (s.Asleep)
                        {
                            if (LogStats) Perf.Asleep();
                            continue;
                        }
                        if (LogStats) Perf.Awake();
                        var protMode = s.DsState.State.ProtectMode;
                        if (protMode > 0)
                        {
                            if (protMode == 1) s.DeformEnabled = true;
                            s.ProtectSubs(Tick);
                            continue;
                        }

                        if (!DedicatedServer && Tick20 && s.Bus.EffectsDirty) s.Bus.ResetDamageEffects();
                        if (Tick600) s.CleanWebEnts();
                        s.WebEntities();
                    }
                }
                if (WebWrapperOn)
                {
                    Dispatched = true;
                    MyAPIGateway.Parallel.Start(WebDispatch, WebDispatchDone);
                    WebWrapperOn = false;
                }
            }
        }

        private void WebDispatch()
        {
            Controllers controller;
            while (WebWrapper.TryDequeue(out controller))
            {
                if (controller == null || controller.MarkedForClose) continue;
                if (!controller.VoxelsToIntersect.IsEmpty) MyAPIGateway.Parallel.Start(controller.VoxelIntersect);
                if (!controller.WebEnts.IsEmpty) MyAPIGateway.Parallel.ForEach(controller.WebEnts, controller.EntIntersectSelector);
            }
        }

        private void WebDispatchDone()
        {
            Dispatched = false;
        }
        #endregion
    }
}
