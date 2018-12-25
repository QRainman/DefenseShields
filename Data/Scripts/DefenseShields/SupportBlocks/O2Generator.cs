﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DefenseShields.Support;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace DefenseShields
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_OxygenGenerator), false, "DSSupergen")]
    public class O2Generators : MyGameLogicComponent
    {
        private int _airIPercent = -1;
        private int _count = -1;
        private int _lCount = -1;
        internal int RotationTime;
        internal int AnimationLoop;
        internal int TranslationTime;
        private double _shieldVolFilled;
        private double _oldShieldVol;
        internal float EmissiveIntensity;

        private bool _isServer;
        private bool _isDedicated;

        internal bool IsFunctional;
        internal bool IsWorking;
        internal bool AllInited;
        internal bool Suspended;
        internal bool IsStatic;
        internal bool BlockIsWorking;
        internal bool BlockWasWorking;
        internal bool ContainerInited;

        internal ShieldGridComponent ShieldComp;
        internal MyResourceSourceComponent Source;
        internal O2GeneratorState O2State;
        private static readonly MyDefinitionId GId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Electricity");

        internal DSUtils Dsutil1 = new DSUtils();

        public IMyGasGenerator O2Generator;
        internal MyCubeGrid MyGrid;
        internal MyCubeBlock MyCube;
        private IMyInventory _inventory;


        public override void OnAddedToContainer()
        {
            if (!ContainerInited)
            {
                NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;
                O2Generator = (IMyGasGenerator)Entity;
                ContainerInited = true;
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

        public override void OnAddedToScene()
        {
            try
            {
                MyGrid = (MyCubeGrid)O2Generator.CubeGrid;
                MyCube = O2Generator as MyCubeBlock;
                RegisterEvents();
                if (Session.Enforced.Debug == 3) Log.Line($"OnAddedToScene: - O2GeneatorId [{O2Generator.EntityId}]");
            }
            catch (Exception ex) { Log.Line($"Exception in OnAddedToScene: {ex}"); }
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            try
            {
                Session.Instance.O2Generators.Add(this);
                Source = O2Generator.Components.Get<MyResourceSourceComponent>();
                _isServer = Session.Instance.IsServer;
                _isDedicated = Session.Instance.DedicatedServer;
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateOnceBeforeFrame: {ex}"); }
        }

        public override void UpdateBeforeSimulation10()
        {
            try
            {
                if (_count++ == 5)
                {
                    _count = 0;
                    _lCount++;
                    if (_lCount == 10) _lCount = 0;
                }

                var wait = _isServer && _count != 0 && O2State.State.Backup;

                MyGrid = MyCube.CubeGrid;
                if (wait || MyGrid?.Physics == null) return;
                if (!_isDedicated && _count == 0) Timing();

                if (!O2GeneratorReady()) return;

                if (_count > 0) return;

                if (_isServer)
                {
                    Pressurize();
                    NeedUpdate(O2State.State.Pressurized, true);
                }
            }
            catch (Exception ex) { Log.Line($"Exception in UpdateBeforeSimulation: {ex}"); }
        }

        private void Timing()
        {
            if (MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel && Session.Instance.LastTerminalId == O2Generator.EntityId)
            {
                Log.Line($"test");
                O2Generator.RefreshCustomInfo();
                MyCube.UpdateTerminal();
            }
        }

        private bool InitO2Generator()
        {
            if (!AllInited)
            {
                if (_isServer)
                {
                    if (ShieldComp == null) MyGrid.Components.TryGet(out ShieldComp);

                    if (ShieldComp?.DefenseShields == null || ShieldComp?.ActiveO2Generator != null || !ShieldComp.DefenseShields.Warming || ShieldComp.ShieldVolume <= 0) return false;
                    ShieldComp.ActiveO2Generator = this;
                    _oldShieldVol = ShieldComp.ShieldVolume;
                    _inventory = MyCube.GetInventory();
                }
                else
                {
                    if (ShieldComp == null) MyGrid.Components.TryGet(out ShieldComp);

                    if (ShieldComp?.DefenseShields == null) return false;
                    if (ShieldComp.ActiveO2Generator == null) ShieldComp.ActiveO2Generator = this;
                }

                RemoveControls();
                O2Generator.AppendingCustomInfo += AppendingCustomInfo;
                Source.Enabled = false;
                O2Generator.AutoRefill = false;

                ResetAirEmissives(-1);
                BlockWasWorking = true;
                AllInited = true;
                return true;
            }
            return true;
        }

        private bool O2GeneratorReady()
        {
            if (ShieldComp?.DefenseShields?.MyGrid != MyGrid) MyGrid.Components.TryGet(out ShieldComp);
            if (_isServer)
            {
                if (!AllInited && !InitO2Generator() || !BlockWorking()) return false;
            }
            else
            {
                if (!AllInited && !InitO2Generator()) return false;

                if (ShieldComp?.DefenseShields == null) return false;

                if (!O2State.State.Backup && ShieldComp.ActiveO2Generator != this) ShieldComp.ActiveO2Generator = this;

                if (!O2State.State.Pressurized) return false;
            }
            return true;
        }

        private bool BlockWorking()
        {
            if (_count <= 0) IsStatic = MyGrid.Physics.IsStatic;

            if (!O2Generator.Enabled || !IsFunctional || !IsStatic || !IsWorking)
            {
                if (O2State.State.Pressurized) UpdateAirEmissives(0f);
                NeedUpdate(O2State.State.Pressurized, false);
                return false;
            }

            if (ShieldComp?.DefenseShields == null)
            {
                NeedUpdate(O2State.State.Pressurized, false);
                return false;
            }

            if (ShieldComp.ActiveO2Generator != this)
            {
                if (ShieldComp.ActiveO2Generator == null)
                {
                    ShieldComp.ActiveO2Generator = this;
                    O2State.State.Backup = false;
                }
                else if (ShieldComp.ActiveO2Generator != this)
                {
                    O2State.State.Backup = true;
                    O2State.State.Pressurized = false;
                }
            }

            if (!O2State.State.Backup && ShieldComp.ActiveO2Generator == this)
            {
                NeedUpdate(O2State.State.Pressurized, true);
                return true;
            }
            NeedUpdate(O2State.State.Pressurized, false);
            return false;
        }

        private void Pressurize()
        {
            var sc = ShieldComp;
            var shieldFullVol = sc.ShieldVolume;
            var startingO2Fpercent = sc.DefaultO2 + sc.DefenseShields.DsState.State.IncreaseO2ByFPercent;

            if (shieldFullVol < _oldShieldVol)
            {
                var ratio = _oldShieldVol / shieldFullVol;
                if (startingO2Fpercent * ratio > 1) startingO2Fpercent = 1d;
                else startingO2Fpercent = startingO2Fpercent * ratio;
            }
            else if (shieldFullVol > _oldShieldVol)
            {
                var ratio = _oldShieldVol / shieldFullVol;
                startingO2Fpercent = startingO2Fpercent * ratio;
            }
            _oldShieldVol = shieldFullVol;

            _shieldVolFilled = shieldFullVol * startingO2Fpercent;
            if (!_isDedicated) UpdateAirEmissives(startingO2Fpercent);

            var shieldVolStillEmpty = shieldFullVol - _shieldVolFilled;
            if (!(shieldVolStillEmpty > 0)) return;

            var amount = _inventory.CurrentVolume.RawValue;
            if (amount <= 0) return;
            if (amount - 10.3316326531 > 0)
            {
                _inventory.RemoveItems(0, 2700);
                _shieldVolFilled += 10.3316326531 * 261.333333333;
            }
            else
            {
                _inventory.RemoveItems(0, _inventory.CurrentVolume);
                _shieldVolFilled += amount * 261.333333333;
            }
            if (_shieldVolFilled > shieldFullVol) _shieldVolFilled = shieldFullVol;

            var shieldVolPercentFull = _shieldVolFilled * 100.0;
            var fPercentToAddToDefaultO2Level = shieldVolPercentFull / shieldFullVol * 0.01 - sc.DefaultO2;

            sc.DefenseShields.DsState.State.IncreaseO2ByFPercent = fPercentToAddToDefaultO2Level;
            sc.O2Updated = true;
            if (Session.Enforced.Debug == 3) Log.Line($"default:{ShieldComp.DefaultO2} - Filled/(Max):{O2State.State.VolFilled}/({shieldFullVol}) - ShieldO2Level:{sc.DefenseShields.DsState.State.IncreaseO2ByFPercent} - O2Before:{MyAPIGateway.Session.OxygenProviderSystem.GetOxygenInPoint(O2Generator.PositionComp.WorldVolume.Center)}");
        }

        private void UpdateVisuals()
        {
            UpdateAirEmissives(O2State.State.O2Level);
        }

        private void NeedUpdate(bool onState, bool turnOn)
        {
            var o2State = O2State.State;
            if (ShieldComp?.DefenseShields == null)
            {
                if (O2State.State.Pressurized)
                {
                    o2State.Pressurized = false;
                    o2State.VolFilled = 0;
                    o2State.DefaultO2 = 0;
                    o2State.O2Level = 0;
                    o2State.ShieldVolume = 0;
                    O2State.SaveState();
                    O2State.NetworkUpdate();
                }
                return;
            }

            var conState = ShieldComp.DefenseShields.DsState.State;
            var o2Level = conState.IncreaseO2ByFPercent + ShieldComp.DefaultO2;
            var o2Change = !o2State.VolFilled.Equals(_shieldVolFilled) || !o2State.DefaultO2.Equals(ShieldComp.DefaultO2) || !o2State.ShieldVolume.Equals(ShieldComp.ShieldVolume) || !o2State.O2Level.Equals(o2Level);
            if (!onState && turnOn)
            {
                o2State.Pressurized = true;
                o2State.VolFilled = _shieldVolFilled;
                o2State.DefaultO2 = ShieldComp.DefaultO2;
                o2State.O2Level = o2Level;
                o2State.ShieldVolume = ShieldComp.ShieldVolume;
                O2State.SaveState();
                O2State.NetworkUpdate();
            }
            else if (onState & !turnOn)
            {
                o2State.Pressurized = false;
                o2State.VolFilled = _shieldVolFilled;
                o2State.DefaultO2 = ShieldComp.DefaultO2;
                o2State.O2Level = o2Level;
                o2State.ShieldVolume = ShieldComp.ShieldVolume;
                O2State.SaveState();
                O2State.NetworkUpdate();
            }
            else if (o2Change)
            {
                o2State.VolFilled = _shieldVolFilled;
                o2State.DefaultO2 = ShieldComp.DefaultO2;
                o2State.O2Level = o2Level;
                o2State.ShieldVolume = ShieldComp.ShieldVolume;
                O2State.SaveState();
                O2State.NetworkUpdate();
            }
        }

        private void UpdateAirEmissives(double fPercent)
        {
            var tenPercent = fPercent * 10;
            if ((int)tenPercent != _airIPercent) _airIPercent = (int)tenPercent;
            else return;
            if (tenPercent > 9) tenPercent = 9;
            ResetAirEmissives(tenPercent);
        }

        private void ResetAirEmissives(double tenPercent)
        {
            for (int i = 0; i < 10; i++)
            {
                if (tenPercent < 0 || i > tenPercent)
                {
                    O2Generator.SetEmissiveParts("Emissive" + i, Color.Transparent, 0f);
                }
                else
                {
                    O2Generator.SetEmissiveParts("Emissive" + i, UtilsStatic.GetAirEmissiveColorFromDouble(i * 10), 1f);
                }
            }
        }

        private void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder stringBuilder)
        {
            if (!O2State.State.Pressurized)
            {
                stringBuilder.Append("\n" +
                                     "\n[ Generator Standby ]");
            }
            else
            {
                stringBuilder.Append("\n" +
                                     "\n[Ice-to-Air volumetric ratio]: 261.3" +
                                     "\n[Shield Volume]: " + O2State.State.ShieldVolume.ToString("N0") +
                                     "\n[Volume Filled]: " + O2State.State.VolFilled.ToString("N0") +
                                     "\n[Backup Generator]: " + O2State.State.Backup +
                                     "\n[Internal O2 Lvl]: " + ((O2State.State.O2Level + O2State.State.DefaultO2) * 100).ToString("0") + "%" +
                                     "\n[External O2 Lvl]: " + (O2State.State.DefaultO2 * 100).ToString("0") + "%");
            }
        }

        public void UpdateState(ProtoO2GeneratorState newState)
        {
            O2State.State = newState;
            if (!_isDedicated) UpdateVisuals();
            if (Session.Enforced.Debug == 3) Log.Line($"UpdateState - O2GenId [{O2Generator.EntityId}]:\n{newState}");
        }

        private void StorageSetup()
        {
            if (O2State == null) O2State = new O2GeneratorState(O2Generator);
            O2State.StorageInit();
            O2State.LoadState();
        }

        public override bool IsSerialized()
        {
            if (MyAPIGateway.Multiplayer.IsServer)
            {
                if (O2Generator.Storage != null) O2State.SaveState();
            }
            return false;
        }

        public override void OnRemovedFromScene()
        {
            try
            {
                if (!Entity.MarkedForClose)
                {
                    return;
                }
                if (Session.Instance.O2Generators.Contains(this)) Session.Instance.O2Generators.Remove(this);
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
                MyCube.IsWorkingChanged += IsWorkingChanged;
                IsWorkingChanged(MyCube);
            }
            else
            {
                O2Generator.AppendingCustomInfo -= AppendingCustomInfo;
                MyCube.IsWorkingChanged -= IsWorkingChanged;
            }
        }

        private void IsWorkingChanged(MyCubeBlock myCubeBlock)
        {
            IsFunctional = myCubeBlock.IsFunctional;
            IsWorking = myCubeBlock.IsWorking;
        }

        public override void OnBeforeRemovedFromContainer() { if (Entity.InScene) OnRemovedFromScene(); }
        public override void Close()
        {
            base.Close();
            try
            {
                if (Session.Instance.O2Generators.Contains(this)) Session.Instance.O2Generators.Remove(this);
            }
            catch (Exception ex) { Log.Line($"Exception in Close: {ex}"); }
        }

        public override void MarkForClose()
        {
            try
            {
            }
            catch (Exception ex) { Log.Line($"Exception in MarkForClose: {ex}"); }
            base.MarkForClose();
        }

        public static void RemoveControls()
        {
            var actions = new List<IMyTerminalAction>();
            MyAPIGateway.TerminalControls.GetActions<Sandbox.ModAPI.Ingame.IMyGasGenerator>(out actions);
            var aRefill = actions.First((x) => x.Id.ToString() == "Refill");
            aRefill.Enabled = block => false;
            var aAutoRefill = actions.First((x) => x.Id.ToString() == "Auto-Refill");
            aAutoRefill.Enabled = block => false;

            var controls = new List<IMyTerminalControl>();
            MyAPIGateway.TerminalControls.GetControls<Sandbox.ModAPI.Ingame.IMyGasGenerator>(out controls);
            var cRefill = controls.First((x) => x.Id.ToString() == "Refill");
            cRefill.Enabled = block => false;
            cRefill.Visible = block => false;
            cRefill.RedrawControl();

            var cAutoRefill = controls.First((x) => x.Id.ToString() == "Auto-Refill");
            cAutoRefill.Enabled = block => false;
            cAutoRefill.Visible = block => false;
            cAutoRefill.RedrawControl();
        }
    }
}