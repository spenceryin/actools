using System;
using System.Linq;
using System.Windows.Input;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Data.GameSpecific;
using AcManager.Tools.Managers;
using AcManager.Tools.Profile;
using AcManager.Tools.SemiGui;
using AcTools.DataFile;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public class SpecialEventObject : KunosEventObjectBase {
        public sealed class AiLevelEntry : Displayable, IWithId<int> {
            private static readonly string[] DisplayNames = {
               "Easy", "Medium", "Hard",  "Alien"
            };

            public AiLevelEntry(int index, int aiLevel) {
                Place = 4 - index;
                AiLevel = aiLevel;
                DisplayName = DisplayNames[index];
            }

            public int Place { get; }

            public int AiLevel { get; }

            public int Id => AiLevel;
        }

        private string _guid;

        [CanBeNull]
        public string Guid {
            get { return _guid; }
            set {
                if (Equals(value, _guid)) return;
                _guid = value;
                OnPropertyChanged();
            }
        }

        public SpecialEventObject(IFileAcManager manager, string id, bool enabled)
                : base(manager, id, enabled) {}

        private AiLevelEntry[] _aiLevels;

        public AiLevelEntry[] AiLevels {
            get { return _aiLevels; }
            set {
                if (Equals(value, _aiLevels)) return;
                _aiLevels = value;
                OnPropertyChanged();
            }
        }

        private AiLevelEntry _selectedLevel;

        public AiLevelEntry SelectedLevel {
            get { return _selectedLevel; }
            set {
                if (Equals(value, _selectedLevel)) return;
                _selectedLevel = value;
                OnPropertyChanged();
                SpecialEventsManager.ProgressStorage.Set(KeySelectedLevel, value.AiLevel);
                AiLevel = value.AiLevel;
            }
        }

        private string KeyTakenPlace => $"{Id}:TakenPlace";

        private string KeySelectedLevel => $"{Id}:SelectedLevel";

        private string _displayDescription;

        public string DisplayDescription {
            get { return _displayDescription; }
            set {
                if (Equals(value, _displayDescription)) return;
                _displayDescription = value;
                OnPropertyChanged();
            }
        }

        protected override void LoadObjects() {
            base.LoadObjects();
            DisplayDescription = string.Format("{0} at {1}.", CarObject?.DisplayName ?? CarId, TrackObject?.Name ?? TrackId);
        }

        protected override void LoadData(IniFile ini) {
            base.LoadData(ini);
            Guid = ini["SPECIAL_EVENT"].GetNonEmpty("GUID");
        }

        protected override void LoadConditions(IniFile ini) {
            if (string.Equals(ini["CONDITION_0"].GetNonEmpty("TYPE"), @"AI", StringComparison.OrdinalIgnoreCase)) {
                var aiLevels = ini.GetSections("CONDITION").Take(4).Select((x, i) => new AiLevelEntry(i, x.GetInt("OBJECTIVE", 100))).Reverse().ToArray();
                if (aiLevels.Length != 4) {
                    AddError(AcErrorType.Data_KunosCareerConditions, $"NOT {aiLevels.Length}");
                    AiLevels = null;
                } else {
                    RemoveError(AcErrorType.Data_KunosCareerConditions);
                    AiLevels = aiLevels;
                }

                ConditionType = null;
                FirstPlaceTarget = SecondPlaceTarget = ThirdPlaceTarget = null;
            } else {
                AiLevels = null;
                base.LoadConditions(ini);
            }
        }

        protected override void TakenPlaceChanged() {
            SpecialEventsManager.ProgressStorage.Set(KeyTakenPlace, TakenPlace);
        }

        public override void LoadProgress() {
            TakenPlace = SpecialEventsManager.ProgressStorage.GetInt(KeyTakenPlace, 5);
            if (AiLevels != null) {
                SelectedLevel = AiLevels.GetByIdOrDefault(SpecialEventsManager.ProgressStorage.GetInt(KeySelectedLevel)) ??
                        AiLevels.ElementAtOrDefault(1);
            }
        }

        protected override IniFile ConvertConfig(IniFile ini) {
            ini = base.ConvertConfig(ini);

            if (SelectedLevel != null) {
                ini["RACE"].Set("AI_LEVEL", SelectedLevel.AiLevel);
            }

            foreach (var section in ini.GetSections("CONDITION")) {
                section.Set("TYPE", section.GetPossiblyEmpty("TYPE")?.ToLowerInvariant());
                section.Set("ACHIEVED", false);
            }

            return ini;
        }

        private ICommand _goCommand;

        // TODO: async command
        public ICommand GoCommand => _goCommand ?? (_goCommand = new DelegateCommand<Game.AssistsProperties>(async o => {
            await GameWrapper.StartAsync(new Game.StartProperties {
                AdditionalPropertieses = {
                    ConditionType.HasValue ? new PlaceConditions {
                        Type = ConditionType.Value,
                        FirstPlaceTarget = FirstPlaceTarget,
                        SecondPlaceTarget = SecondPlaceTarget,
                        ThirdPlaceTarget = ThirdPlaceTarget
                    } : null,
                    new SpecialEventsManager.EventProperties { EventId = Id }
                },
                PreparedConfig = ConvertConfig(new IniFile(IniFilename)),
                AssistsProperties = o
            });
        }));
    }
}