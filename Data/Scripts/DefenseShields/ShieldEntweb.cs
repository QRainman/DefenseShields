﻿using System.Collections.Generic;
using DefenseShields.Support;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Weapons;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace DefenseShields
{
    public partial class DefenseShields
    {
        #region Web Entities
        private void WebEntities()
        {
            if (Session.Enforced.Debug == 1) Dsutil2.Sw.Restart();

            var pruneSphere = new BoundingSphereD(DetectionCenter, BoundingRange + 3000);
            var pruneSphere2 = new BoundingSphereD(DetectionCenter, BoundingRange + 5);
            var pruneList = new List<MyEntity>();

            MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref pruneSphere, pruneList, MyEntityQueryType.Dynamic);
            MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref pruneSphere2, pruneList, MyEntityQueryType.Static);

            foreach (var eShield in EnemyShields) pruneList.Add(eShield);

            var disableVoxels = Session.Enforced.DisableVoxelSupport == 1 || ModComp == null || ModComp.ModulateVoxels;
            var entChanged = false;

            _enablePhysics = false;
            for (int i = 0; i < pruneList.Count; i++)
            {
                var ent = pruneList[i];
                var voxel = ent as MyVoxelBase;

                if (ent == null || ent.MarkedForClose || !GridIsMobile && voxel != null || disableVoxels && voxel != null || voxel != null && voxel != voxel.RootVoxel || !(ent is MyVoxelBase) && ent.Physics == null) continue;

                var entCenter = ent.PositionComp.WorldVolume.Center;
                var missileCheck = !pruneSphere2.Intersects(ent.PositionComp.WorldVolume) && !((ent.Flags & EntityFlags.IsNotGamePrunningStructureObject) != 0 && ent.GetType().Name.Equals(MyMissile));
                if (voxel == null && missileCheck) continue;
                if (FriendlyCache.Contains(ent) || IgnoreCache.Contains(ent) || PartlyProtectedCache.Contains(ent) || AuthenticatedCache.Contains(ent) || ent is IMyFloatingObject || ent is IMyEngineerToolBase || double.IsNaN(entCenter.X) || ent.GetType().Name == "MyDebrisBase") continue;

                var relation = EntType(ent);
                switch (relation)
                {
                    case Ent.Authenticated:
                        continue;
                    case Ent.Ignore:
                    case Ent.Friend:
                        if (relation == Ent.Friend) 
                        {
                            var grid = ent as MyCubeGrid;
                            if (grid != null)
                            {
                                if (ShieldEnt.PositionComp.WorldVolume.Intersects(grid.PositionComp.WorldVolume))
                                {
                                    var cornersInShield = CustomCollision.NotAllCornersInShield(grid, DetectMatrixOutsideInv);
                                    if (cornersInShield > 0 && cornersInShield != 8) PartlyProtectedCache.Add(ent);
                                    else if (cornersInShield == 8) FriendlyCache.Add(ent);
                                }
                            }
                            else if (CustomCollision.PointInShield(ent.PositionComp.WorldVolume.Center, DetectMatrixOutsideInv))
                            {
                                FriendlyCache.Add(ent);
                                continue;
                            }
                            IgnoreCache.Add(ent);
                        }
                        continue;
                }
                lock (WebEnts)
                {
                    EntIntersectInfo entInfo;
                    WebEnts.TryGetValue(ent, out entInfo);
                    if (entInfo != null)
                    {
                        var interestingEnts = relation == Ent.LargeEnemyGrid || relation == Ent.LargeNobodyGrid || relation == Ent.SmallEnemyGrid || relation == Ent.SmallNobodyGrid;
                        if (ent.Physics != null && ent.Physics.IsMoving) entChanged = true;
                        else if (entInfo.Touched || _count == 0 && interestingEnts && !ent.PositionComp.LocalAABB.Equals(entInfo.Box))
                        {
                            entInfo.Box = ent.PositionComp.LocalAABB;
                            entChanged = true;
                        }

                        _enablePhysics = true;
                        entInfo.LastTick = _tick;
                    }
                    else
                    {
                        if (relation == Ent.Other && CustomCollision.PointInShield(ent.PositionComp.WorldVolume.Center, DetectMatrixOutsideInv))
                        {
                            IgnoreCache.Add(ent);
                            continue;
                        }
                        if ((relation == Ent.LargeNobodyGrid || relation == Ent.SmallNobodyGrid) && CustomCollision.AllAabbInShield(ent.PositionComp.WorldAABB, DetectMatrixOutsideInv))
                        {
                            FriendlyCache.Add(ent);
                            WebEnts.Remove(ent);
                            continue;
                        }
                        entChanged = true;
                        _enablePhysics = true;
                        WebEnts.Add(ent, new EntIntersectInfo(ent.EntityId, 0f, false, ent.PositionComp.LocalAABB, Vector3D.NegativeInfinity, _tick, _tick, relation, new List<IMySlimBlock>()));
                    }
                }
            }

            ShieldMatrix = ShieldEnt.PositionComp.WorldMatrix;
            if (_enablePhysics && !ShieldMatrix.EqualsFast(ref OldShieldMatrix))
            {
                OldShieldMatrix = ShieldMatrix;
                Icosphere.ReturnPhysicsVerts(DetectMatrixOutside, ShieldComp.PhysicsOutside);
                if (!disableVoxels) Icosphere.ReturnPhysicsVerts(DetectMatrixOutside, ShieldComp.PhysicsOutsideLow);
            }

            if (_enablePhysics && (ShieldComp.GridIsMoving || entChanged)) MyAPIGateway.Parallel.Start(WebDispatch);

            if (Session.Enforced.Debug == 1) Dsutil2.StopWatchReport($"Web: ShieldId [{Shield.EntityId}]", 3);
        }

        private void WebDispatch()
        {
            if (Session.Enforced.Debug == 1) Dsutil3.Sw.Restart();
            var ep = 0;
            var ns = 0;
            var nl = 0;
            var es = 0;
            var el = 0;
            var ss = 0;
            var oo = 0;
            var vv = 0;
            var xx = 0;
            lock (WebEnts)
            {
                foreach (var webent in WebEnts.Keys)
                {
                    var entCenter = webent.PositionComp.WorldVolume.Center;
                    var entInfo = WebEnts[webent];
                    if (entInfo.LastTick != _tick) continue;
                    if (entInfo.FirstTick == _tick && (WebEnts[webent].Relation == Ent.LargeNobodyGrid || WebEnts[webent].Relation == Ent.LargeEnemyGrid))
                        ((IMyCubeGrid)webent).GetBlocks(WebEnts[webent].CacheBlockList, CollectCollidableBlocks);
                    switch (WebEnts[webent].Relation)
                    {
                        case Ent.EnemyPlayer:
                            {
                                ep++;
                                if ((_count == 2 || _count == 17 || _count == 32 || _count == 47) && CustomCollision.PointInShield(entCenter, DetectMatrixOutsideInv))
                                {
                                    if (Session.Enforced.Debug >= 2) Log.Line($"Ent EnemyPlayer: {((MyEntity)webent).DebugName} - ShieldId [{Shield.EntityId}]");
                                    MyAPIGateway.Parallel.Start(() => PlayerIntersect(webent));
                                }
                                continue;
                            }
                        case Ent.SmallNobodyGrid:
                            {
                                ns++;
                                if (Session.Enforced.Debug >= 2) Log.Line($"Ent SmallNobodyGrid: {((MyEntity)webent).DebugName} - ShieldId [{Shield.EntityId}]");
                                MyAPIGateway.Parallel.Start(() => SmallGridIntersect(webent));
                                continue;
                            }
                        case Ent.LargeNobodyGrid:
                            {
                                nl++;
                                if (Session.Enforced.Debug >= 2) Log.Line($"Ent LargeNobodyGrid: {((MyEntity)webent).DebugName} - ShieldId [{Shield.EntityId}]");
                                MyAPIGateway.Parallel.Start(() => GridIntersect(webent));
                                continue;
                            }
                        case Ent.SmallEnemyGrid:
                            {
                                es++;
                                if (Session.Enforced.Debug >= 2) Log.Line($"Ent SmallEnemyGrid: {((MyEntity)webent).DebugName} - ShieldId [{Shield.EntityId}]");
                                MyAPIGateway.Parallel.Start(() => SmallGridIntersect(webent));
                                continue;
                            }
                        case Ent.LargeEnemyGrid:
                            {
                                el++;
                                if (Session.Enforced.Debug >= 2) Log.Line($"Ent LargeEnemyGrid: {((MyEntity)webent).DebugName} - ShieldId [{Shield.EntityId}]");
                                MyAPIGateway.Parallel.Start(() => GridIntersect(webent));
                                continue;
                            }
                        case Ent.Shielded:
                            {
                                ss++;
                                if (Session.Enforced.Debug >= 2) Log.Line($"Ent Shielded: {((MyEntity)webent).DebugName} - ShieldId [{Shield.EntityId}]");
                                MyAPIGateway.Parallel.Start(() => ShieldIntersect(webent as IMyCubeGrid));
                                continue;
                            }
                        case Ent.Other:
                            {
                                oo++;
                                if (Session.Enforced.Debug >= 2) Log.Line($"Ent Other: {((MyEntity)webent).DebugName} - ShieldId [{Shield.EntityId}]");
                                if (CustomCollision.PointInShield(entCenter, DetectMatrixOutsideInv))
                                {
                                    if (webent.MarkedForClose || webent.Closed) continue;
                                    if (webent is IMyMeteor) _meteorDmg.Enqueue(webent as IMyMeteor);
                                    else _missileDmg.Enqueue(webent);
                                }
                                continue;
                            }
                        case Ent.VoxelBase:
                            {
                                vv++;
                                if (Session.Enforced.Debug >= 2) Log.Line($"Ent VoxelBase: {((MyEntity)webent).DebugName} - ShieldId [{Shield.EntityId}]");
                                MyAPIGateway.Parallel.Start(() => VoxelIntersect(webent as MyVoxelBase));
                                continue;
                            }
                        default:
                            if (Session.Enforced.Debug >= 2) Log.Line($"Ent default: {((MyEntity)webent).DebugName} - ShieldId [{Shield.EntityId}]");
                            xx++;
                            continue;
                    }
                }
            }

            if (Session.Enforced.Debug == 1 && _lCount == 5 && _count == 5)
                lock (WebEnts) if (WebEnts.Count > 7 || FriendlyCache.Count > 15 || IgnoreCache.Count > 15) Log.Line($"Web: friend:{FriendlyCache.Count} - ignore:{IgnoreCache.Count} - total:{WebEnts.Count} ep:{ep} ns:{ns} nl:{nl} es:{es} el:{el} ss:{ss} oo:{oo} vv:{vv} xx:{xx} - ShieldId [{Shield.EntityId}]");
            if (Session.Enforced.Debug == 1) Dsutil3.StopWatchReport($"webDispatch: ShieldId [{Shield.EntityId}]:", 3);
        }
        #endregion

        #region Gather Entity Information
        public enum Ent
        {
            Ignore,
            Friend,
            EnemyPlayer,
            SmallNobodyGrid,
            LargeNobodyGrid,
            SmallEnemyGrid,
            LargeEnemyGrid,
            Shielded,
            Other,
            VoxelBase,
            Weapon,
            Authenticated
        };

        private Ent EntType(IMyEntity ent)
        {
            if (ent == null) return Ent.Ignore;
            if (ent is MyVoxelBase && (Session.Enforced.DisableVoxelSupport == 1 || ModComp == null || ModComp.ModulateVoxels || !GridIsMobile)) return Ent.Ignore;
            //if (ent is IMyGunBaseUser) return Ent.Weapon;

            if (ent is IMyCharacter)
            {
                var dude = MyAPIGateway.Players.GetPlayerControllingEntity(ent)?.IdentityId;
                if (dude == null) return Ent.Ignore;
                var playerrelationship = Shield.GetUserRelationToOwner((long)dude);
                if (playerrelationship == MyRelationsBetweenPlayerAndBlock.Owner || playerrelationship == MyRelationsBetweenPlayerAndBlock.FactionShare) return Ent.Friend;
                return (ent as IMyCharacter).IsDead ? Ent.Ignore : Ent.EnemyPlayer;
            }
            if (ent is IMyCubeGrid)
            {
                if (ModComp != null && ModComp.ModulateGrids || Session.Enforced.DisableGridDamageSupport == 1) return Ent.Ignore;

                var grid = ent as IMyCubeGrid;
                ModulatorGridComponent modComp;
                grid.Components.TryGet(out modComp);
                if (modComp?.ModulationPassword != null)
                {
                    if (modComp.ModulationPassword.Equals(Shield.CustomData))
                    {
                        foreach (var subGrid in modComp.GetSubGrids)
                        {
                            if (ShieldEnt.PositionComp.WorldVolume.Intersects(grid.PositionComp.WorldVolume))
                            {
                                var cornersInShield = CustomCollision.NotAllCornersInShield(grid, DetectMatrixOutsideInv);
                                if (cornersInShield > 0 && cornersInShield != 8) PartlyProtectedCache.Add(ent);
                                else if (cornersInShield == 8) FriendlyCache.Add(ent);
                            }
                            else AuthenticatedCache.Add(subGrid);
                        }
                        return Ent.Authenticated;
                    }
                }

                if (((MyCubeGrid)grid).BlocksCount < 3 && grid.BigOwners.Count == 0) return Ent.SmallNobodyGrid;
                if (grid.BigOwners.Count == 0) return Ent.LargeNobodyGrid;

                var enemy = GridEnemy(grid);
                if (enemy && ((MyCubeGrid)grid).BlocksCount < 3) return Ent.SmallEnemyGrid;

                ShieldGridComponent shieldComponent;
                grid.Components.TryGet(out shieldComponent);
                if (shieldComponent?.DefenseShields?.ShieldComp != null)
                {
                    var dsComp = shieldComponent.DefenseShields;
                    var shieldEntity = (MyEntity)Shield.Parent;
                    if (!enemy) return Ent.Friend;
                    if (!dsComp.ShieldComp.ShieldActive)
                    {
                        lock (WebEnts) if (WebEnts.Remove(ent))
                        return Ent.LargeEnemyGrid;
                    }
                    dsComp.EnemyShields.Add(shieldEntity);
                    return Ent.Shielded;
                }

                return enemy ? Ent.LargeEnemyGrid : Ent.Friend;
            }

            if (ent is IMyMeteor || ent.GetType().Name.Equals(MyMissile)) return Ent.Other;
            if (ent is MyVoxelBase && GridIsMobile) return Ent.VoxelBase;
            return 0;
        }

        private bool GridEnemy(IMyCubeGrid grid)
        {
            var owners = grid.BigOwners;
            if (owners.Count == 0) return true;
            var relationship = Shield.GetUserRelationToOwner(owners[0]);
            var enemy = relationship != MyRelationsBetweenPlayerAndBlock.Owner && relationship != MyRelationsBetweenPlayerAndBlock.FactionShare;
            return enemy;
        }

        private static bool CollectCollidableBlocks(IMySlimBlock mySlimBlock)
        {
            return mySlimBlock.BlockDefinition.Id.TypeId != typeof(MyObjectBuilder_TextPanel)
                   && mySlimBlock.BlockDefinition.Id.TypeId != typeof(MyObjectBuilder_ButtonPanel)
                   && mySlimBlock.BlockDefinition.Id.SubtypeId != MyStringHash.TryGet("SmallLight");
        }
        #endregion
    }
}