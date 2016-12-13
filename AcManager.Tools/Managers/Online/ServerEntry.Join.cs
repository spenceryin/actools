﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Tools.GameProperties;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Helpers.Api.Kunos;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Online {
    public partial class ServerEntry {
        private IReadOnlyList<string> _naReasons;

        [CanBeNull]
        public IReadOnlyList<string> NonAvailableReasons {
            get { return _naReasons; }
            set {
                if (Equals(value, _naReasons)) return;
                _naReasons = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Non available reasons, already joined to one string, for optimization purposes.
        /// </summary>
        [CanBeNull]
        public string NonAvailableReasonsString => _naReasons == null ? null : (_naReasonsString ?? (_naReasonsString = _naReasons.JoinToString('\n')));
        private string _naReasonsString;

        private IEnumerable<string> GetNonAvailableReasons() {
            if (!IsFullyLoaded || Sessions == null) {
                yield return "Can’t get any information";
                yield break;
            }

            if (Status != ServerStatus.Ready) {
                yield return "CM isn’t ready";
            }

            if (PasswordRequired) {
                if (string.IsNullOrEmpty(Password)) {
                    yield return ToolsStrings.ArchiveInstallator_PasswordIsRequired;
                } else if (WrongPassword) {
                    yield return ToolsStrings.ArchiveInstallator_PasswordIsInvalid;
                }
            }

            if (!IsBookedForPlayer) {
                if (SelectedCarEntry == null) {
                    yield return "Car isn’t selected";
                }

                if (CurrentDriversCount == Capacity) {
                    yield return "No available places left";
                }

                if (BookingMode && Sessions.FirstOrDefault(x => x.IsActive)?.Type != Game.SessionType.Booking) {
                    yield return "Wait for the next booking";
                }
            }
            
            if (!BookingMode && SelectedCarEntry?.IsAvailable != true) {
                yield return "Selected car isn’t available";
            }
        }

        private void AvailableUpdate() {
            NonAvailableReasons = GetNonAvailableReasons().ToList();
            IsAvailable = NonAvailableReasons.Count == 0;
        }

        private bool _isBooked;

        public bool IsBooked {
            get { return _isBooked; }
            set {
                if (Equals(value, _isBooked)) return;
                _isBooked = value;
                OnPropertyChanged();
                _cancelBookingCommand?.RaiseCanExecuteChanged();
            }
        }

        private DateTime _startTime;

        public DateTime StartTime {
            get { return _startTime; }
            set {
                if (Equals(value, _startTime)) return;
                _startTime = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BookingTimeLeft));
            }
        }

        private string _bookingErrorMessage;

        public string BookingErrorMessage {
            get { return _bookingErrorMessage; }
            set {
                if (Equals(value, _bookingErrorMessage)) return;
                _bookingErrorMessage = value;
                OnPropertyChanged();
            }
        }

        public TimeSpan BookingTimeLeft {
            get {
                var result = StartTime - DateTime.Now;
                return result <= TimeSpan.Zero ? TimeSpan.Zero : result;
            }
        }

        private async Task CancelBooking() {
            DisposeHelper.Dispose(ref _ui);

            if (!IsBooked) return;
            IsBooked = false;
            await Task.Run(() => KunosApiProvider.TryToUnbook(Ip, PortHttp));
            if (!_updating) {
                Update(UpdateMode.Lite, true, true).Forget();
            }
        }

        private void PrepareBookingUi() {
            if (_ui == null) {
                _ui = _factory.Create();
                _ui.Show(this);
            }
        }

        private void ProcessBookingResponse(BookingResult response) {
            if (_ui?.CancellationToken.IsCancellationRequested == true) {
                CancelBooking().Forget();
                return;
            }

            if (response == null) {
                BookingErrorMessage = "Cannot get any response";
                return;
            }

            if (response.IsSuccessful) {
                StartTime = DateTime.Now + response.Left;
                BookingErrorMessage = null;
                IsBooked = response.IsSuccessful;

                if (!_updating) {
                    Update(UpdateMode.Lite, true, true).Forget();
                }
            } else {
                BookingErrorMessage = response.ErrorMessage;
                IsBooked = false;
            }

            _ui?.OnUpdate(response);
        }

        public async Task<bool> RebookSkin() {
            if (!IsBooked || !BookingMode || BookingTimeLeft < TimeSpan.FromSeconds(2) || Cars == null) {
                return false;
            }

            var carEntry = SelectedCarEntry;
            if (carEntry == null) return false;

            var correctId = GetCorrectId(carEntry.Id);
            if (correctId == null) {
                Logging.Error("Can’t find correct ID");
                return false;
            }

            PrepareBookingUi();

            var result = await Task.Run(() => KunosApiProvider.TryToBook(Ip, PortHttp, Password, correctId, carEntry.AvailableSkin?.Id,
                    DriverName.GetOnline(), ""));
            if (result?.IsSuccessful != true) return false;

            ProcessBookingResponse(result);
            return true;
        }

        private CommandBase _joinCommand;

        public ICommand JoinCommand => _joinCommand ?? (_joinCommand = new AsyncCommand<object>(Join,
                o => ReferenceEquals(o, ForceJoin) || IsAvailable));

        private CommandBase _cancelBookingCommand;

        public ICommand CancelBookingCommand => _cancelBookingCommand ?? (_cancelBookingCommand = new AsyncCommand(CancelBooking, () => IsBooked));

        [CanBeNull]
        private IBookingUi _ui;

        public static readonly object ActualJoin = new object();
        public static readonly object ForceJoin = new object();

        [CanBeNull]
        public CarSkinObject GetSelectedCarSkin() {
            return SelectedCarEntry?.AvailableSkin;
        }

        /// <summary>
        /// Theoretically, local car ID might have a different case comparing to remote car ID, and server can’t
        /// process this case properly. So, to avoid problems, before contacting server we need to know what ID
        /// server uses.
        /// </summary>
        /// <param name="carId">Local car ID.</param>
        /// <returns>Remote car ID.</returns>
        [CanBeNull]
        private string GetCorrectId(string carId) {
            return Cars?.FirstOrDefault(x => string.Equals(x.Id, carId, StringComparison.OrdinalIgnoreCase))?.Id;
        }

        private async Task Join(object o) {
            var carEntry = SelectedCarEntry;
            if (carEntry == null || Cars == null) return;

            var correctId = GetCorrectId(carEntry.Id);
            if (correctId == null) {
                Logging.Error("Can’t find correct ID");
                return;
            }

            if (!IsBookedForPlayer && BookingMode && !ReferenceEquals(o, ActualJoin) && !ReferenceEquals(o, ForceJoin)) {
                if (_factory == null) {
                    Logging.Error("Booking: UI factory is missing");
                    return;
                }

                PrepareBookingUi();
                ProcessBookingResponse(await Task.Run(() => KunosApiProvider.TryToBook(Ip, PortHttp, Password, correctId, carEntry.AvailableSkin?.Id,
                        DriverName.GetOnline(), "")));
                return;
            }

            DisposeHelper.Dispose(ref _ui);
            IsBooked = false;
            BookingErrorMessage = null;

            var properties = new Game.StartProperties(new Game.BasicProperties {
                CarId = carEntry.Id,
                CarSkinId = carEntry.AvailableSkin?.Id,
                TrackId = Track?.Id,
                TrackConfigurationId = Track?.LayoutId
            }, null, null, null, new Game.OnlineProperties {
                RequestedCar = correctId,
                ServerIp = Ip,
                ServerName = DisplayName,
                ServerPort = PortRace,
                ServerHttpPort = PortHttp,
                Guid = SteamIdHelper.Instance.Value,
                Password = Password
            });

            await GameWrapper.StartAsync(properties);
            var whatsGoingOn = properties.GetAdditional<AcLogHelper.WhatsGoingOn>();
            WrongPassword = whatsGoingOn?.Type == AcLogHelper.WhatsGoingOnType.OnlineWrongPassword;
            // if (whatsGoingOn == null) RecentManagerOld.Instance.AddRecentServer(OriginalInformation);
        }

        private static IAnyFactory<IBookingUi> _factory;

        public static void RegisterFactory(IAnyFactory<IBookingUi> factory) {
            _factory = factory;
        }
    }
}