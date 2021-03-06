using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using SlimDX.DirectInput;

namespace AcManager.Tools.Helpers.DirectInput {
    public interface IDirectInputDevice : IWithId {
        bool IsVirtual { get; }

        string DisplayName { get; }

        int Index { get; }

        IList<int> OriginalIniIds { get; }

        bool Same(IDirectInputDevice other);

        [CanBeNull]
        DirectInputAxle GetAxle(int id);

        [CanBeNull]
        DirectInputButton GetButton(int id);
    }

    public class PlaceholderInputDevice : IDirectInputDevice {
        public PlaceholderInputDevice([CanBeNull] string id, string displayName, int index) {
            Id = id;
            DisplayName = displayName;
            Index = index;
            OriginalIniIds = new List<int> { index };
        }

        [CanBeNull]
        public string Id { get; }

        public bool IsVirtual => true;

        public string DisplayName { get; }

        public int Index { get; set; }

        public IList<int> OriginalIniIds { get; }

        private readonly Dictionary<int, DirectInputAxle> _axles = new Dictionary<int, DirectInputAxle>(); 
        private readonly Dictionary<int, DirectInputButton> _buttons = new Dictionary<int, DirectInputButton>();

        public bool Same(IDirectInputDevice other) {
            return other != null && (Id == other.Id || DisplayName == other.DisplayName);
        }

        public DirectInputAxle GetAxle(int id) {
            DirectInputAxle result;
            if (_axles.TryGetValue(id, out result)) return result;
            result = new DirectInputAxle(this, id);
            return _axles[id] = result;
        }

        public DirectInputButton GetButton(int id) {
            DirectInputButton result;
            if (_buttons.TryGetValue(id, out result)) return result;
            result = new DirectInputButton(this, id);
            return _buttons[id] = result;
        }

        public override string ToString() {
            return $"PlaceholderInputDevice({Id}:{DisplayName}, Ini={Index})";
        }
    }

    public sealed class DirectInputDevice : Displayable, IDirectInputDevice, IDisposable {
        [NotNull]
        public DeviceInstance Device { get; }

        public string Id { get; }

        public bool IsVirtual => false;

        public int Index { get; }

        public IList<int> OriginalIniIds { get; }

        public bool Same(IDirectInputDevice other) {
            return other != null && (Id == other.Id || DisplayName == other.DisplayName);
        }

        public bool Same(DeviceInstance other) {
            return other != null && (Id == GuidToString(other.ProductGuid) || DisplayName == other.InstanceName);
        }

        public DirectInputAxle GetAxle(int id) {
            return Axles.ElementAtOrDefault(id);
        }

        public DirectInputButton GetButton(int id) {
            return Buttons.ElementAtOrDefault(id);
        }

        public DirectInputButton[] Buttons { get; } 

        public DirectInputAxle[] Axles { get; } 

        private Joystick _joystick;
        private readonly int _buttonsCount;

        public override string ToString() {
            return $"DirectInputDevice({Id}:{DisplayName}, Ini={Index})";
        }

        private static string GuidToString(Guid guid) {
            return guid.ToString().ToUpperInvariant();
        }

        private DirectInputDevice([NotNull] SlimDX.DirectInput.DirectInput directInput, [NotNull] DeviceInstance device, int index) {
            Device = device;
            DisplayName = device.InstanceName;

            Id = GuidToString(device.ProductGuid);
            Index = index;
            OriginalIniIds = new List<int>();

            _joystick = new Joystick(directInput, Device.InstanceGuid);
            if (Application.Current.MainWindow != null) {
                try {
                    _joystick.SetCooperativeLevel(new WindowInteropHelper(Application.Current.MainWindow).Handle,
                            CooperativeLevel.Background | CooperativeLevel.Nonexclusive);
                } catch (Exception e) {
                    Logging.Warning("Can’t set cooperative level: " + e);
                }
            }

            var capabilities = _joystick.Capabilities;
            _buttonsCount = capabilities.ButtonCount;

            Buttons = Enumerable.Range(0, _buttonsCount).Select(x => new DirectInputButton(this, x)).ToArray();
            Axles = Enumerable.Range(0, 8).Select(x => new DirectInputAxle(this, x)).ToArray();
        }

        [CanBeNull]
        public static DirectInputDevice Create(SlimDX.DirectInput.DirectInput directInput, DeviceInstance device, int iniId) {
            try {
                return new DirectInputDevice(directInput, device, iniId);
            } catch (DirectInputException) {
                return null;
            }
        }

        public bool Error { get; private set; }

        public bool Unplugged { get; private set; }

        public void OnTick() {
            try {
                if (_joystick.Acquire().IsFailure || _joystick.Poll().IsFailure || SlimDX.Result.Last.IsFailure) {
                    return;
                }

                var state = _joystick.GetCurrentState();

                var i = 0;
                foreach (var source in state.GetButtons().Take(_buttonsCount)) {
                    Buttons[i++].Value = source;
                }

                i = 0;
                var sliders = state.GetSliders();
                foreach (var source in new [] {
                    state.X, state.Y, state.Z,
                    state.RotationX, state.RotationY, state.RotationZ,
                    sliders[0], sliders[1]
                }) {
                    Axles[i++].Value = source / 65535d;
                }
            } catch (DirectInputException e) when(e.Message.Contains(@"DIERR_UNPLUGGED")) {
                Unplugged = true;
            } catch (DirectInputException e) {
                if (!Error){
                    Logging.Warning("Exception: " + e);
                    Error = true;
                }
            }
        }

        public void Dispose() {
            DisposeHelper.Dispose(ref _joystick);
        }
    }
}