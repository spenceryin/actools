﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Tools.Helpers;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Data {
    /// <summary>
    /// Only for KunosCareerObject and KunosCareerEventObject
    /// </summary>
    public class KunosCareerProgress : NotifyPropertyChanged, IDisposable {
        private static KunosCareerProgress _instance;

        public static KunosCareerProgress Instance => _instance ??
                (_instance = new KunosCareerProgress(FileUtils.GetKunosCareerProgressFilename()));

        private readonly string _filename;
        private FileSystemWatcher _fsWatcher;

        private KunosCareerProgress(string filename) {
            _filename = filename;

            var directory = Path.GetDirectoryName(_filename);
            if (Directory.Exists(directory)) {
                _fsWatcher = new FileSystemWatcher {
                    Path = directory,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                    Filter = Path.GetFileName(_filename)
                };
                _fsWatcher.Changed += FsWatcher_Changed;
                _fsWatcher.Created += FsWatcher_Changed;
                _fsWatcher.Deleted += FsWatcher_Changed;
                _fsWatcher.Renamed += FsWatcher_Changed;
                _fsWatcher.EnableRaisingEvents = true;
            }

            if (!TryToLoad()) {
                Reset();
            }
        }

#if DEBUG
        internal static KunosCareerProgress CreateForTests(string filename) {
            return new KunosCareerProgress(filename);
        }
#endif

        private DateTime _ignoreChanges;

        private void FsWatcher_Changed(object sender, FileSystemEventArgs e) {
            if ((DateTime.Now - _ignoreChanges).TotalSeconds < 1d) return;
            LoadLater();
        }

        private bool _loadingInProgress;

        private async void LoadLater() {
            if (_loadingInProgress) return;
            _loadingInProgress = true;

            await Task.Delay(200);
            
            if (Application.Current == null) {
                await Reload();
            } else {
                await Application.Current.Dispatcher.Invoke(Reload);
            }

            _loadingInProgress = false;
        }

        private string _currentSeries;

        [CanBeNull]
        internal string CurrentSeries {
            get { return _currentSeries; }
            set {
                if (Equals(value, _currentSeries)) return;
                _currentSeries = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        private string[] _completed;

        internal string[] Completed {
            get { return _completed; }
            set {
                if (Equals(value, _completed)) return;
                _completed = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        private double _aiLevel;

        internal double AiLevel {
            get { return _aiLevel; }
            set {
                if (Equals(value, _aiLevel)) return;
                _aiLevel = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        private bool _isNew;

        internal bool IsNew {
            get { return _isNew; }
            set {
                if (Equals(value, _isNew)) return;
                _isNew = value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        private Dictionary<string, KunosCareerProgressEntry> _entries;

        internal IReadOnlyDictionary<string, KunosCareerProgressEntry> Entries {
            get { return _entries; }
            set {
                if (Equals(value, _entries)) return;
                _entries = (Dictionary<string, KunosCareerProgressEntry>)value;
                OnPropertyChanged();
                SaveLater();
            }
        }

        internal void UpdateEntry([NotNull] string id, [NotNull] KunosCareerProgressEntry entry, bool globalUpdate) {
            if (id == null) throw new ArgumentNullException(nameof(id));
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            id = id.ToLowerInvariant();
            var data = Entries.ToDictionary(x => x.Key.ToLowerInvariant(), x => x.Key.Equals(id, StringComparison.OrdinalIgnoreCase) ? entry : x.Value);
            if (!data.ContainsKey(id)) {
                data[id] = entry;
            }

            if (globalUpdate) {
                Entries = data;
            } else {
                _entries = data;
                SaveLater();
            }
        }

        private void Reset() {
            Completed = new string[0];
            CurrentSeries = null;
            AiLevel = 100;
            IsNew = true;
            Entries = new Dictionary<string, KunosCareerProgressEntry>(0);
        }

        private async Task Reload() {
            for (var i = 0; i < 3; i++) {
                if (TryToLoad()) return;
                await Task.Delay(300);
            }

            if (!TryToLoad()) {
                Reset();
            }
        }

        private bool TryToLoad() {
            _skipSaving = true;

            try {
                IniFile iniFile;
                try {
                    iniFile = new IniFile(_filename);
                } catch (Exception e) {
                    NonfatalError.Notify("Can’t load Kunos career progress", e);
                    return false;
                }

                Completed = iniFile["CAREER"].GetStrings("COMPLETE").Select(x => x.ToLowerInvariant()).ToArray();
                CurrentSeries = iniFile["CAREER"].GetNonEmpty("CURRENTSERIES");
                AiLevel = iniFile["CAREER"].GetDouble("AI_LEVEL", 95d);
                IsNew = iniFile["CAREER"].GetInt("INTRO", 0) != 2;
                Entries = iniFile.Where(x => x.Key.StartsWith(@"SERIES")).ToDictionary(
                        x => x.Key.ToLowerInvariant(),
                        x => new KunosCareerProgressEntry(
                                x.Value.GetInt("EVENT", 0),
                                x.Value.Select(y => new {
                                    Key = y.Key.StartsWith(@"EVENT") ? FlexibleParser.TryParseInt(y.Key.Substring(5)) : null as int?,
                                    y.Value
                                }).Where(y => y.Key.HasValue).ToDictionary(y => y.Key.Value, y => FlexibleParser.ParseInt(y.Value, 0)),
                                x.Value.GetIntNullable("POINTS"),
                                x.Value.Select(y => new {
                                    Key = y.Key.StartsWith(@"AI") ? FlexibleParser.TryParseInt(y.Key.Substring(2)) - 1 : null as int?,
                                    y.Value
                                }).Where(y => y.Key.HasValue).ToDictionary(y => y.Key.Value, y => FlexibleParser.ParseInt(y.Value, 0)),
                                x.Value.GetLong("LASTSELECTED", 0)));

                return true;
            } finally {
                _skipSaving = false;
            }
        }

        private void Save() {
            var iniFile = new IniFile(_filename) {
                ["CAREER"] = {
                    ["COMPLETE"] = Completed.JoinToString(','),
                    ["CURRENTSERIES"] = CurrentSeries,
                    ["AI_LEVEL"] = AiLevel,
                    ["INTRO"] = IsNew ? 0 : 2,
                }
            };

            foreach (var pair in Entries) {
                var section = iniFile[pair.Key.ToUpperInvariant()];
                section.Clear();

                section["LASTSELECTED"] = pair.Value.LastSelectedTimestamp;
                section["EVENT"] = pair.Value.SelectedEvent;

                if (pair.Value.Points.HasValue) {
                    section["POINTS"] = pair.Value.Points;
                } else {
                    section.Remove(@"POINTS");
                }

                foreach (var result in pair.Value.EventsResults.Where(x => x.Value != 0)) {
                    section[$"EVENT{result.Key}"] = result.Value;
                }

                foreach (var result in pair.Value.AiPoints.Where(x => x.Value != 0)) {
                    section[$"AI{result.Key + 1}"] = result.Value;
                }
            }

            if (!File.Exists(_filename + ".backup")) {
                File.Copy(_filename, _filename + ".backup");
            }

            _ignoreChanges = DateTime.Now;
            iniFile.Save();
        }

        private bool _skipSaving, _savingInProgress;

        private async void SaveLater() {
            if (_savingInProgress || _skipSaving) return;
            _savingInProgress = true;

            await Task.Delay(300);

            Save();
            _savingInProgress = false;
        }

        public static void SaveBeforeExit() {
            if (_instance == null || !_instance._savingInProgress) return;
            _instance.Save();
        }

        public void Dispose() {
            DisposeHelper.Dispose(ref _fsWatcher);
        }
    }
}
