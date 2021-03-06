﻿using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Timers;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.SharedMemory {
    public class AcSharedMemory : NotifyPropertyChanged, IDisposable {
        public static AcSharedMemory Instance { get; private set; }

        public static void Initialize() {
            Debug.Assert(Instance == null, @"Already initialized");
            Instance = new AcSharedMemory();
        }

        private Timer _timer;

        private AcSharedMemory() {
            SettingsHolder.Drive.PropertyChanged += Drive_PropertyChanged;

            _timer = new Timer { AutoReset = true };
            _timer.Elapsed += Update;

            Status = SettingsHolder.Drive.WatchForSharedMemory ? AcSharedMemoryStatus.Disconnected :
                    AcSharedMemoryStatus.Disabled;
        }

        private void Drive_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(SettingsHolder.Drive.WatchForSharedMemory)) {
                Status = SettingsHolder.Drive.WatchForSharedMemory ? AcSharedMemoryStatus.Disconnected :
                        AcSharedMemoryStatus.Disabled;
            }
        }

        private AcSharedMemoryStatus? _statusValue;

        internal AcSharedMemoryStatus Status {
            get { return _statusValue ?? AcSharedMemoryStatus.Disconnected; }
            private set {
                if (Equals(value, _statusValue)) return;

                if (_statusValue == AcSharedMemoryStatus.Live) {
                    Finish?.Invoke(this, EventArgs.Empty);
                }

                if (_statusValue == AcSharedMemoryStatus.Connected) {
                    DisposeHelper.Dispose(ref _gameProcess);
                }

                _statusValue = value;
                OnPropertyChanged();

                switch (_statusValue) {
                    case AcSharedMemoryStatus.Disabled:
                        _timer.Enabled = false;
                        break;
                    case AcSharedMemoryStatus.Connected:
                        _timer.Interval = 200d;
                        _timer.Enabled = true;
                        break;
                    case AcSharedMemoryStatus.Connecting:
                        _timer.Enabled = false;
                        break;
                    case AcSharedMemoryStatus.Live:
                        _timer.Interval = 50d;
                        _timer.Enabled = true;
                        Start?.Invoke(this, EventArgs.Empty);
                        break;
                    case AcSharedMemoryStatus.Disconnected:
                        _timer.Interval = 2000d;
                        _timer.Enabled = true;
                        _previousPacketId = 0;
                        break;
                }
            }
        }

        public bool IsLive => Status == AcSharedMemoryStatus.Live;

        private bool _isPaused;

        public bool IsPaused {
            get { return _isPaused; }
            set {
                if (Equals(value, _isPaused)) return;
                _isPaused = value;
                OnPropertyChanged();
                PauseTime = DateTime.Now;
                (value ? Pause : Resume)?.Invoke(this, EventArgs.Empty);
            }
        }

        private DateTime _pauseTime;

        public DateTime PauseTime {
            get { return _pauseTime; }
            private set {
                if (Equals(value, _pauseTime)) return;
                _pauseTime = value;
                OnPropertyChanged();
            }
        }

        private AcShared _shared;
        private DateTime _sharedTime;

        [CanBeNull]
        public AcShared Shared {
            get { return _shared; }
            set {
                if (Equals(value, _shared)) return;
                var prev = _shared;
                var prevTime = _sharedTime;
                _shared = value;
                _sharedTime = DateTime.Now;
                OnPropertyChanged();
                Updated?.Invoke(this, new AcSharedEventArgs(prev, _shared, prev == null ? TimeSpan.Zero : _sharedTime - prevTime));
            }
        }

        public bool KnownProcess { get; private set; }

        private int _previousPacketId;
        private DateTime _previousPacketTime;
        private MemoryMappedFile _physicsFile;
        private MemoryMappedFile _graphicsFile;
        private MemoryMappedFile _staticInfoFile;

        private Process _gameProcess;

        private static bool IsGameProcess(Process process) {
            var filename = process.GetFilenameSafe();
            return filename == null || AcRootDirectory.CheckDirectory(Path.GetDirectoryName(filename));
        }

        [CanBeNull]
        private Process TryToFindGameProcess() {
            var processes = Process.GetProcesses();
            return processes.FirstOrDefault(x => (x.ProcessName == "acs" || x.ProcessName == "acs_x86") && IsGameProcess(x)) ??
                    processes.FirstOrDefault(x => x.ProcessName.IndexOf(@"acs", StringComparison.OrdinalIgnoreCase) != -1 && IsGameProcess(x));
        }

        private void UpdatePhysics() {
            if (_physicsFile == null) return;

            try {
                var physics = AcSharedPhysics.FromFile(_physicsFile);
                if (physics.PacketId != _previousPacketId) {
                    IsPaused = false;

                    _previousPacketId = physics.PacketId;
                    _previousPacketTime = DateTime.Now;

                    if (Status != AcSharedMemoryStatus.Live) {
                        Status = AcSharedMemoryStatus.Live;
                        _gameProcess = TryToFindGameProcess();
                        KnownProcess = _gameProcess != null;
                    }

                    var graphics = AcSharedGraphics.FromFile(_graphicsFile);
                    var staticInfo = AcSharedStaticInfo.FromFile(_staticInfoFile);
                    Shared = new AcShared(physics, graphics, staticInfo);
                } else if (_gameProcess?.HasExitedSafe() ?? (DateTime.Now - _previousPacketTime).TotalSeconds > 1d) {
                    IsPaused = false;
                    Status = AcSharedMemoryStatus.Connected;
                    Shared = null;
                } else {
                    IsPaused = Shared != null && Shared.Graphics.Status == AcGameStatus.AcPause;
                }
            } catch (Exception ex) {
                Logging.Error(ex);
                Status = AcSharedMemoryStatus.Disabled;
            }
        }

        private bool Reconnect() {
            Status = AcSharedMemoryStatus.Connecting;

            try {
                var sw = Stopwatch.StartNew();
                DisposeHelper.Dispose(ref _physicsFile);
                DisposeHelper.Dispose(ref _graphicsFile);
                DisposeHelper.Dispose(ref _staticInfoFile);

                _physicsFile = MemoryMappedFile.OpenExisting(@"Local\acpmf_physics");
                _graphicsFile = MemoryMappedFile.OpenExisting(@"Local\acpmf_graphics");
                _staticInfoFile = MemoryMappedFile.OpenExisting(@"Local\acpmf_static");

                Status = AcSharedMemoryStatus.Connected;
                Logging.Debug($"{sw.ElapsedMilliseconds:F2} ms");
                return true;
            } catch (FileNotFoundException) {
                Status = AcSharedMemoryStatus.Disconnected;
                return false;
            }
        }

        private void Update(object sender, ElapsedEventArgs e) {
            switch (Status) {
                case AcSharedMemoryStatus.Disconnected:
                    if (Reconnect()) {
                        UpdatePhysics();
                    }
                    break;

                case AcSharedMemoryStatus.Connected:
                case AcSharedMemoryStatus.Live:
                    UpdatePhysics();
                    break;

                case AcSharedMemoryStatus.Connecting:
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// Game started.
        /// </summary>
        public event EventHandler Start;

        /// <summary>
        /// Game paused.
        /// </summary>
        public event EventHandler Pause;

        /// <summary>
        /// Game resumed.
        /// </summary>
        public event EventHandler Resume;

        /// <summary>
        /// Game finished.
        /// </summary>
        public event EventHandler Finish;

        /// <summary>
        /// New physics data arrived.
        /// </summary>
        public event EventHandler<AcSharedEventArgs> Updated;

        public void Dispose() {
            DisposeHelper.Dispose(ref _timer);
            DisposeHelper.Dispose(ref _gameProcess);
            DisposeHelper.Dispose(ref _physicsFile);
            DisposeHelper.Dispose(ref _graphicsFile);
            DisposeHelper.Dispose(ref _staticInfoFile);
        }
    }
}
