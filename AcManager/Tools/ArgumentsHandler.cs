﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using AcManager.Controls;
using AcManager.Controls.CustomShowroom;
using AcManager.Controls.ViewModels;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Drive;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Helpers.Api.Kunos;
using AcManager.Tools.Helpers.Loaders;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Online;
using AcManager.Tools.Managers.Presets;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.DataFile;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;
using SharpCompress.Archives.Zip;
using SharpCompress.Readers;
using WaitingDialog = FirstFloor.ModernUI.Dialogs.WaitingDialog;

namespace AcManager.Tools {
    internal static class NameValueCollectionExtension {
        public static bool GetFlag(this NameValueCollection collection, [Localizable(false)] string key) {
            return collection.Keys.OfType<string>().Contains(key) &&
                    Regex.IsMatch(collection.Get(key) ?? @"on", @"^(1|yes|ok|on|true)$", RegexOptions.IgnoreCase);
        }
    }

    public class ArgumentsHandler {
        public async Task<ArgumentHandleResult> ProcessArgument(string argument) {
            if (string.IsNullOrWhiteSpace(argument)) return ArgumentHandleResult.FailedShow;

            if (argument.StartsWith(CustomUriSchemeHelper.UriScheme, StringComparison.InvariantCultureIgnoreCase)) {
                return await ProcessUriRequest(argument);
            }

            if (argument.StartsWith(@"http:", StringComparison.OrdinalIgnoreCase) || argument.StartsWith(@"https:", StringComparison.OrdinalIgnoreCase) ||
                    argument.StartsWith(@"ftp:", StringComparison.OrdinalIgnoreCase)) {
                argument = await LoadRemoveFile(argument);
                if (string.IsNullOrWhiteSpace(argument)) return ArgumentHandleResult.FailedShow;
            }

            try {
                if (!FileUtils.Exists(argument)) return ArgumentHandleResult.FailedShow;
            } catch (Exception) {
                return ArgumentHandleResult.FailedShow;
            }

            return await ProcessInputFile(argument);
        }

        private async Task<string> LoadRemoveFile(string argument, string name = null, string extension = null) {
            using (var waiting = new WaitingDialog(ControlsStrings.Common_Loading)) {
                return await FlexibleLoader.LoadAsync(argument, name, extension, waiting, waiting.CancellationToken);
            }
        }

        private async Task<string> LoadRemoveFileTo(string argument, string destination) {
            using (var waiting = new WaitingDialog(ControlsStrings.Common_Loading)) {
                return await FlexibleLoader.LoadAsyncTo(argument, destination, waiting, waiting.CancellationToken);
            }
        }

        private async Task<ArgumentHandleResult> ProcessInputFile(string filename) {
            var isDirectory = FileUtils.IsDirectory(filename);
            if (!isDirectory && filename.EndsWith(@".acreplay", StringComparison.OrdinalIgnoreCase) ||
                    Path.GetDirectoryName(filename)?.Equals(FileUtils.GetReplaysDirectory(), StringComparison.OrdinalIgnoreCase) == true) {
                await GameWrapper.StartReplayAsync(new Game.StartProperties(new Game.ReplayProperties {
                    Filename = filename
                }));
                return ArgumentHandleResult.Successful;
            }

            if (!isDirectory && filename.EndsWith(@".kn5", StringComparison.OrdinalIgnoreCase)) {
                await CustomShowroomWrapper.StartAsync(filename);
                return ArgumentHandleResult.Successful;
            }

            try {
                new InstallAdditionalContentDialog(filename).ShowDialog();
            } catch (Exception e) {
                NonfatalError.Notify(AppStrings.Arguments_CannotInstallAdditionalContent, e);
                return ArgumentHandleResult.Failed;
            }

            return ArgumentHandleResult.Successful;
        }

        [Obsolete]
        private async Task<ArgumentHandleResult> ProcessUriRequestObsolete(string request) {
            string key, param;
            NameValueCollection query;

            {
                var splitted = request.Split(new[] { '/' }, 2);
                if (splitted.Length != 2) return ArgumentHandleResult.FailedShow;

                key = splitted[0];
                param = splitted[1];

                var index = param.IndexOf('?');
                if (index != -1) {
                    query = HttpUtility.ParseQueryString(param.SubstringExt(index + 1));
                    param = param.Substring(0, index);
                } else {
                    query = null;
                }
            }

            switch (key) {
                case "quickdrive":
                    var preset = Convert.FromBase64String(param).ToUtf8String();
                    if (!QuickDrive.RunSerializedPreset(preset)) {
                        NonfatalError.Notify(AppStrings.Common_CannotStartRace, AppStrings.Arguments_CannotStartRace_Commentary);
                        return ArgumentHandleResult.Failed;
                    }
                    break;

                case "race":
                    var raceIni = Convert.FromBase64String(param).ToUtf8String();
                    await GameWrapper.StartAsync(new Game.StartProperties {
                        PreparedConfig = IniFile.Parse(raceIni)
                    });
                    break;

                case "open":
                case "install":
                    var address = Convert.FromBase64String(param).ToUtf8String();
                    var path = await LoadRemoveFile(address, query?.Get(@"name"));
                    if (string.IsNullOrWhiteSpace(path)) return ArgumentHandleResult.FailedShow;

                    try {
                        if (!FileUtils.Exists(path)) return ArgumentHandleResult.FailedShow;
                    } catch (Exception) {
                        return ArgumentHandleResult.FailedShow;
                    }

                    return await ProcessInputFile(path);
            }

            return ArgumentHandleResult.Successful;
        }

        private async Task<ArgumentHandleResult> ProcessUriRequest(string uri) {
            if (!uri.StartsWith(CustomUriSchemeHelper.UriScheme, StringComparison.OrdinalIgnoreCase)) return ArgumentHandleResult.FailedShow;

            var request = uri.SubstringExt(CustomUriSchemeHelper.UriScheme.Length);
            Logging.Debug("URI Request: " + request);

            if (!request.StartsWith(@"//", StringComparison.Ordinal)) {
#pragma warning disable 612
                return await ProcessUriRequestObsolete(request);
#pragma warning restore 612
            }

            CustomUriRequest custom;
            try {
                custom = CustomUriRequest.Parse(uri);
            } catch (Exception) {
                NonfatalError.Notify(AppStrings.Arguments_CannotParseRequest, AppStrings.Main_CannotProcessArgument_Commentary);
                return ArgumentHandleResult.Failed;
            }

            try {
                switch (custom.Path.ToLowerInvariant()) {
                    case "setsteamid":
                        return ArgumentHandleResult.Ignore; // TODO?

                    case "race/online":
                        return await ProgressRaceOnline(custom.Params);

                    case "race/online/join":
                        return await ProgressRaceOnlineJoin(custom.Params);

                    case "loadgooglespreadsheetslocale":
                        return await ProcessGoogleSpreadsheetsLocale(custom.Params.Get(@"id"), custom.Params.Get(@"locale"), custom.Params.GetFlag(@"around"));

                    case "replay":
                        return await ProcessReplay(custom.Params.Get(@"url"), custom.Params.Get(@"uncompressed") == null);

                    case "rsr":
                        return await ProcessRsrEvent(custom.Params.Get(@"id"));

                    case "rsr/setup":
                        return await ProcessRsrSetup(custom.Params.Get(@"id"));

                    case "shared":
                        return await ProcessShared(custom.Params.Get(@"id"));

                    default:
                        NonfatalError.Notify(string.Format(AppStrings.Main_NotSupportedRequest, custom.Path), AppStrings.Main_CannotProcessArgument_Commentary);
                        return ArgumentHandleResult.Failed;
                }
            } catch (Exception e) {
                NonfatalError.Notify(AppStrings.Arguments_CannotProcessRequest, AppStrings.Arguments_CannotProcessRequest_Commentary, e);
                return ArgumentHandleResult.Failed;
            }
        }

        public async Task<ArgumentHandleResult> ProgressRaceOnline(NameValueCollection p) {
            /* required arguments */
            var ip = p.Get(@"ip");
            var port = FlexibleParser.TryParseInt(p.Get(@"port"));
            var httpPort = FlexibleParser.TryParseInt(p.Get(@"httpPort"));
            var carId = p.Get(@"car");

            /* optional arguments */
            var allowWithoutSteamId = p.GetFlag("allowWithoutSteamId");
            var carSkinId = p.Get(@"skin");
            var trackId = p.Get(@"track");
            var name = p.Get(@"name");
            var nationality = p.Get(@"nationality");
            var password = p.Get(@"plainPassword");
            var encryptedPassword = p.Get(@"password");

            if (string.IsNullOrWhiteSpace(ip)) {
                throw new InformativeException("IP is missing");
            }

            if (!port.HasValue) {
                throw new InformativeException("Port is missing or is in invalid format");
            }

            if (!httpPort.HasValue) {
                throw new InformativeException("HTTP port is missing or is in invalid format");
            }

            if (string.IsNullOrWhiteSpace(password) && !string.IsNullOrWhiteSpace(encryptedPassword)) {
                password = OnlineServer.DecryptSharedPassword(ip, httpPort.Value, encryptedPassword);
            }

            if (string.IsNullOrWhiteSpace(carId)) {
                throw new InformativeException("Car ID is missing");
            }

            var car = CarsManager.Instance.GetById(carId);
            if (car == null) {
                throw new InformativeException("Car is missing");
            }

            if (!string.IsNullOrWhiteSpace(carSkinId) && car.GetSkinById(carSkinId) == null) {
                throw new InformativeException("Car skin is missing");
            }

            var track = string.IsNullOrWhiteSpace(trackId) ? null : TracksManager.Instance.GetLayoutByKunosId(trackId);
            if (!string.IsNullOrWhiteSpace(trackId) && track == null) {
                throw new InformativeException("Track is missing");
            }


            if (!SteamIdHelper.Instance.IsReady && !allowWithoutSteamId) {
                throw new InformativeException(ToolsStrings.Common_SteamIdIsMissing);
            }

            await GameWrapper.StartAsync(new Game.StartProperties {
                BasicProperties = new Game.BasicProperties {
                    CarId = carId,
                    TrackId = track?.MainTrackObject.Id ?? @"imola",
                    TrackConfigurationId = track?.LayoutId,
                    CarSkinId = carSkinId,
                    DriverName = name,
                    DriverNationality = nationality
                },
                ModeProperties = new Game.OnlineProperties {
                    Guid = SteamIdHelper.Instance.Value,
                    ServerIp = ip,
                    ServerPort = port.Value,
                    ServerHttpPort = httpPort.Value,
                    Password = password,
                    RequestedCar = carId
                }
            });

            return ArgumentHandleResult.Successful;
        }

        private class FakeSource : IOnlineListSource {
            private ServerInformation _information;
            private readonly string _id;

            public FakeSource(string ip, int httpPort) {
                _information = new ServerInformation { Ip = ip, PortHttp = httpPort };
                _id = _information.Id;
            }

            public string Id => _id;

            public string DisplayName => "Temporary Source";

            public event EventHandler Obsolete {
                add { }
                remove { }
            }
            
            public Task<bool> LoadAsync(ListAddCallback<ServerInformation> callback, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
                // This source will load provided server, but only once — call .ReloadAsync() and server will be nicely removed.
                callback(new[] { _information }.NonNull());
                _information = null;
                return Task.FromResult(true);
            }
        }

        public async Task<ArgumentHandleResult> ProgressRaceOnlineJoin(NameValueCollection p) {
            /* required arguments */
            var ip = p.Get(@"ip");
            var httpPort = FlexibleParser.TryParseInt(p.Get(@"httpPort"));
            var password = p.Get(@"plainPassword");
            var encryptedPassword = p.Get(@"password");

            if (string.IsNullOrWhiteSpace(ip)) {
                throw new InformativeException("IP is missing");
            }

            if (!httpPort.HasValue) {
                throw new InformativeException("HTTP port is missing or is in invalid format");
            }

            if (string.IsNullOrWhiteSpace(password) && !string.IsNullOrWhiteSpace(encryptedPassword)) {
                password = OnlineServer.DecryptSharedPassword(ip, httpPort.Value, encryptedPassword);
            }

            var list = OnlineManager.Instance.List;
            var source = new FakeSource(ip, httpPort.Value);
            var wrapper = new OnlineSourceWrapper(list, source);

            ServerEntry server;

            using (var waiting = new WaitingDialog()) {
                waiting.Report("Loading information…");
                
                await wrapper.EnsureLoadedAsync();
                server = list.GetByIdOrDefault(source.Id);
                if (server == null) {
                    throw new Exception(@"Unexpected");
                }
            }

            if (password != null) {
                server.Password = password;
            }

            var content = new OnlineServer(server) {
                Margin = new Thickness(0, 0, 0, -51),
                ToolBar = { FitWidth = true },

                // Values taken from ModernDialog.xaml
                // TODO: Extract them to some style?
                Title = { FontSize = 24, FontWeight = FontWeights.Light, Margin = new Thickness(6, 0, 0, 8) }
            };

            content.Title.SetValue(TextOptions.TextFormattingModeProperty, TextFormattingMode.Ideal);

            var dlg = new ModernDialog {
                ShowTitle = false,
                Content = content,
                MinHeight = 400,
                MinWidth = 450,
                MaxHeight = 99999,
                MaxWidth = 700,
                Padding = new Thickness(0),
                ButtonsMargin = new Thickness(8),
                SizeToContent = SizeToContent.Manual,
                ResizeMode = ResizeMode.CanResizeWithGrip,
                LocationAndSizeKey = @".OnlineServerDialog"
            };

            dlg.SetBinding(Window.TitleProperty, new Binding {
                Path = new PropertyPath(nameof(server.DisplayName)),
                Source = server
            });

            await dlg.ShowDialogAsync();
            await wrapper.ReloadAsync(true);
            return ArgumentHandleResult.Successful;
        }

        public async Task<ArgumentHandleResult> ProcessGoogleSpreadsheetsLocale(string id, [CanBeNull] string locale, bool around) {
            if (string.IsNullOrWhiteSpace(id)) {
                throw new InformativeException("ID is missing");
            }

            var url = around ? $@"http://acstuff.ru/u/around?id={id}" : $@"https://docs.google.com/spreadsheets/d/{id}/export?format=xlsx&authuser=0";
            var path = await LoadRemoveFileTo(url, LocaleHelper.GetGoogleSheetsFilename());
            if (string.IsNullOrWhiteSpace(path)) {
                throw new InformativeException("Can’t load file");
            }

            SettingsHolder.Locale.LoadUnpacked = true;
            if (locale != null) {
                SettingsHolder.Locale.LocaleName = locale;
            }

            if (ModernDialog.ShowMessage("Custom locales updated. Would you like to restart app now?", "Locales Updated", MessageBoxButton.YesNo) ==
                    MessageBoxResult.Yes) {
                WindowsHelper.RestartCurrentApplication();
            }

            return ArgumentHandleResult.Successful;
        }

        public async Task<ArgumentHandleResult> ProcessReplay(string url, bool compressed) {
            var path = await LoadRemoveFile(url, extension: compressed ? @".zip" : @".acreplay");
            if (string.IsNullOrWhiteSpace(path)) return ArgumentHandleResult.FailedShow;

            try {
                if (!FileUtils.Exists(path)) return ArgumentHandleResult.FailedShow;
            } catch (Exception) {
                return ArgumentHandleResult.FailedShow;
            }

            try {
                if (!compressed) {
                    return await ProcessInputFile(path);
                }

                var filename = FileUtils.GetTempFileName(Path.GetTempPath(), @".acreplay");

                /*using (var archive = ZipFile.OpenRead(path)) {
                    foreach (var entry in archive.Entries.Where(x => !string.Equals(x.Name, "ReadMe.txt", StringComparison.OrdinalIgnoreCase))) {
                        await Task.Run(() => {
                            entry.ExtractToFile(filename);
                        });
                        break;
                    }
                }*/

                var archive = ZipArchive.Open(path);
                var acreplay = archive.Entries.FirstOrDefault(
                        x => x.IsDirectory == false && !string.Equals(x.Key, @"ReadMe.txt", StringComparison.OrdinalIgnoreCase));
                if (acreplay == null) {
                    return ArgumentHandleResult.FailedShow;
                }

                using (var stream = acreplay.OpenEntryStream())
                using (var output = new FileStream(filename, FileMode.CreateNew)) {
                    await stream.CopyToAsync(output);
                }

                try {
                    return await ProcessInputFile(filename);
                } finally {
                    try {
                        File.Delete(filename);
                    } catch (Exception) {
                        // ignored
                    }
                }
            } finally {
                try {
                    File.Delete(path);
                } catch (Exception) {
                    // ignored
                }
            }
        }

        public async Task<ArgumentHandleResult> ProcessRsrEvent(string id) {
            Logging.Write("RSR Event: " + id);
            return await Rsr.RunAsync(id) ? ArgumentHandleResult.SuccessfulShow : ArgumentHandleResult.Failed;
        }

        public async Task<ArgumentHandleResult> ProcessRsrSetup(string id) {
            string data, header;
            using (var client = new WebClient {
                Headers = {
                    [HttpRequestHeader.UserAgent] = CmApiProvider.UserAgent
                }
            }) {
                data = await client.DownloadStringTaskAsync($"http://www.radiators-champ.com/RSRLiveTiming/ajax.php?action=download_setup&id={id}");
                header = client.ResponseHeaders[@"Content-Disposition"]?.Split(new[] { @"filename=" }, StringSplitOptions.None).ElementAtOrDefault(1)?.Trim();
            }

            if (data == null || header == null) {
                throw new InformativeException(AppStrings.Arguments_CannotInstallCarSetup, AppStrings.Arguments_CannotInstallSetup_Commentary);
            }

            var match = Regex.Match(header, @"^([^_]+_.+)_\d+_\d+_\d+_(.+)\.ini$");
            if (!match.Success) {
                throw new InformativeException(AppStrings.Arguments_CannotInstallCarSetup, AppStrings.Arguments_CannotInstallSetup_CommentaryFormat);
            }

            var ids = match.Groups[1].Value;
            var author = match.Groups[2].Value;

            CarObject car = null;
            TrackObjectBase track = null;

            var splitted = ids.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 1; i < splitted.Length - 1 && (car == null || track == null); i++) {
                var candidateCarId = splitted.Skip(i).JoinToString('_');
                var candidateTrackId = splitted.Take(i).JoinToString('_');
                car = CarsManager.Instance.GetById(candidateCarId);
                track = TracksManager.Instance.GetById(candidateTrackId);
            }

            if (car == null || track == null) {
                throw new InformativeException(AppStrings.Arguments_CannotInstallCarSetup, AppStrings.Arguments_CannotInstallSetup_CommentaryFormat);
            }

            var result = ShowDialog(new SharedEntry {
                Author = author,
                Data = new byte[0],
                EntryType = SharedEntryType.CarSetup,
                Id = header,
                Target = car.DisplayName
            }, applyable: false, additionalButton: AppStrings.Arguments_SaveAsGeneric);

            switch (result) {
                case Choise.Save:
                case Choise.Extra:
                    var filename = FileUtils.EnsureUnique(Path.Combine(FileUtils.GetCarSetupsDirectory(car.Id),
                            result == Choise.Save ? track.Id : CarSetupObject.GenericDirectory, header));
                    Directory.CreateDirectory(Path.GetDirectoryName(filename) ?? "");
                    File.WriteAllText(filename, data);
                    return ArgumentHandleResult.SuccessfulShow;
                default:
                    return ArgumentHandleResult.Failed;
            }
        }

        private enum Choise {
            ApplyAndSave,
            Apply,
            Save,
            Extra,
            Cancel
        }

        /// <summary>
        /// Shows dialog with all information about shared entry and offers a choise to user what to do with it.
        /// </summary>
        /// <param name="shared">Shared entry.</param>
        /// <param name="additionalButton">Label of additional button.</param>
        /// <param name="saveable">Can be saved.</param>
        /// <param name="applyable">Can be applied.</param>
        /// <param name="appliableWithoutSaving">Can be applied without saving.</param>
        /// <returns>User choise.</returns>
        private Choise ShowDialog(SharedEntry shared, string additionalButton = null, bool saveable = true, bool applyable = true,
                bool appliableWithoutSaving = true) {
            var description = string.Format(AppStrings.Arguments_SharedMessage, shared.Name ?? AppStrings.Arguments_SharedMessage_EmptyValue,
                    shared.EntryType == SharedEntryType.Weather
                            ? AppStrings.Arguments_SharedMessage_Id : AppStrings.Arguments_SharedMessage_For,
                    shared.Target ?? AppStrings.Arguments_SharedMessage_EmptyValue,
                    shared.Author ?? AppStrings.Arguments_SharedMessage_EmptyValue);

            var dlg = new ModernDialog {
                Title = shared.EntryType.GetDescription().ToTitle(),
                Content = new ScrollViewer {
                    Content = new BbCodeBlock {
                        BbCode = description + '\n' + '\n' + (
                                saveable ? AppStrings.Arguments_Shared_ShouldApplyOrSave : AppStrings.Arguments_Shared_ShouldApply),
                        Margin = new Thickness(0, 0, 0, 8)
                    },
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
                },
                MinHeight = 0,
                MinWidth = 0,
                MaxHeight = 480,
                MaxWidth = 640
            };

            dlg.Buttons = new[] {
                applyable && saveable ? dlg.CreateCloseDialogButton(
                        appliableWithoutSaving ? AppStrings.Arguments_Shared_ApplyAndSave : AppStrings.Arguments_Shared_SaveAndApply,
                        true, false, MessageBoxResult.Yes) : null,
                appliableWithoutSaving && applyable
                        ? dlg.CreateCloseDialogButton(saveable ? AppStrings.Arguments_Shared_ApplyOnly : AppStrings.Arguments_Shared_Apply,
                                true, false, MessageBoxResult.OK) : null,
                saveable ? dlg.CreateCloseDialogButton(
                        applyable && appliableWithoutSaving ? AppStrings.Arguments_Shared_SaveOnly : AppStrings.Toolbar_Save,
                        true, false, MessageBoxResult.No) : null,
                additionalButton == null ? null : dlg.CreateCloseDialogButton(additionalButton, true, false, MessageBoxResult.None),
                dlg.CancelButton
            }.NonNull();
            dlg.ShowDialog();

            switch (dlg.MessageBoxResult) {
                case MessageBoxResult.None:
                    return Choise.Extra;
                case MessageBoxResult.OK:
                    return Choise.Apply;
                case MessageBoxResult.Cancel:
                    return Choise.Cancel;
                case MessageBoxResult.Yes:
                    return Choise.ApplyAndSave;
                case MessageBoxResult.No:
                    return Choise.Save;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private async Task<ArgumentHandleResult> ProcessShared(string id) {
            SharedEntry shared;

            using (var waiting = new WaitingDialog()) {
                waiting.Report(ControlsStrings.Common_Loading);
                shared = await SharingHelper.GetSharedAsync(id, waiting.CancellationToken);
            }

            var data = shared?.Data;
            if (data == null) return ArgumentHandleResult.Failed;

            switch (shared.EntryType) {
                case SharedEntryType.Weather: {
                    var result = ShowDialog(shared, applyable: false);
                    switch (result) {
                        case Choise.Save:
                            var directory = FileUtils.EnsureUnique(Path.Combine(
                                    WeatherManager.Instance.Directories.EnabledDirectory, shared.GetFileName()));
                            Directory.CreateDirectory(directory);

                            var written = 0;
                            using (var stream = new MemoryStream(data)) {
                                var reader = ReaderFactory.Open(stream);

                                try {
                                    while (reader.MoveToNextEntry()) {
                                        if (!reader.Entry.IsDirectory) {
                                            reader.WriteEntryToDirectory(directory, new ExtractionOptions {
                                                ExtractFullPath = true,
                                                Overwrite = true
                                            });
                                            written++;
                                        }
                                    }
                                } catch (EndOfStreamException) {
                                    if (written < 2) {
                                        throw;
                                    }
                                }
                            }

                            return ArgumentHandleResult.SuccessfulShow;
                        default:
                            return ArgumentHandleResult.Failed;
                    }
                }

                case SharedEntryType.PpFilter: {
                    var result = ShowDialog(shared, appliableWithoutSaving: false);
                    switch (result) {
                        case Choise.Save:
                        case Choise.ApplyAndSave:
                            var filename = FileUtils.EnsureUnique(Path.Combine(
                                    PpFiltersManager.Instance.Directories.EnabledDirectory, shared.GetFileName()));
                            Directory.CreateDirectory(Path.GetDirectoryName(filename) ?? "");
                            File.WriteAllBytes(filename, data);
                            if (result == Choise.ApplyAndSave) {
                                AcSettingsHolder.Video.PostProcessingFilter = Path.GetFileNameWithoutExtension(filename);
                            }
                            return ArgumentHandleResult.SuccessfulShow;
                        default:
                            return ArgumentHandleResult.Failed;
                    }
                }

                case SharedEntryType.CarSetup: {
                    var content = data.ToUtf8String();
                    var metadata = SharingHelper.GetMetadata(SharedEntryType.CarSetup, content, out content);

                    var carId = metadata.GetValueOrDefault("car");
                    var trackId = metadata.GetValueOrDefault("track") ?? CarSetupObject.GenericDirectory;
                    if (carId == null) {
                        throw new InformativeException(AppStrings.Arguments_CannotInstallCarSetup, AppStrings.Arguments_MetadataIsMissing);
                    }

                    var result = ShowDialog(shared, applyable: false,
                            additionalButton: trackId == CarSetupObject.GenericDirectory ? null : AppStrings.Arguments_SaveAsGeneric);
                    switch (result) {
                        case Choise.Save:
                        case Choise.Extra:
                            var filename = FileUtils.EnsureUnique(Path.Combine(FileUtils.GetCarSetupsDirectory(carId),
                                    result == Choise.Save ? trackId : CarSetupObject.GenericDirectory, shared.GetFileName()));
                            Directory.CreateDirectory(Path.GetDirectoryName(filename) ?? "");
                            File.WriteAllText(filename, content);
                            return ArgumentHandleResult.SuccessfulShow;
                        default:
                            return ArgumentHandleResult.Failed;
                    }
                }

                case SharedEntryType.ControlsPreset: {
                    var result = ShowDialog(shared, AppStrings.Arguments_Shared_ApplyFfbOnly);
                    switch (result) {
                        case Choise.Save:
                        case Choise.ApplyAndSave:
                            var filename = FileUtils.EnsureUnique(Path.Combine(
                                    AcSettingsHolder.Controls.UserPresetsDirectory, @"Loaded", shared.GetFileName()));
                            Directory.CreateDirectory(Path.GetDirectoryName(filename) ?? "");
                            File.WriteAllBytes(filename, data);
                            if (result == Choise.ApplyAndSave) {
                                AcSettingsHolder.Controls.LoadPreset(filename, true);
                            }
                            return ArgumentHandleResult.SuccessfulShow;
                        case Choise.Apply:
                            if (File.Exists(AcSettingsHolder.Controls.Filename)) {
                                FileUtils.Recycle(AcSettingsHolder.Controls.Filename);
                            }
                            File.WriteAllBytes(AcSettingsHolder.Controls.Filename, data);
                            return ArgumentHandleResult.SuccessfulShow;
                        case Choise.Extra: // ffb only
                            var ini = IniFile.Parse(data.ToUtf8String());
                            AcSettingsHolder.Controls.LoadFfbFromIni(ini);
                            return ArgumentHandleResult.SuccessfulShow;
                        default:
                            return ArgumentHandleResult.Failed;
                    }
                }

                case SharedEntryType.ForceFeedbackPreset: {
                    var result = ShowDialog(shared, saveable: false);
                    switch (result) {
                        case Choise.Apply:
                            var ini = IniFile.Parse(data.ToUtf8String());
                            AcSettingsHolder.Controls.LoadFfbFromIni(ini);
                            AcSettingsHolder.System.LoadFfbFromIni(ini);
                            return ArgumentHandleResult.SuccessfulShow;
                        default:
                            return ArgumentHandleResult.Failed;
                    }
                }

                case SharedEntryType.VideoSettingsPreset: {
                    var result = ShowDialog(shared);
                    switch (result) {
                        case Choise.Save:
                        case Choise.ApplyAndSave:
                            var filename = FileUtils.EnsureUnique(Path.Combine(
                                    PresetsManager.Instance.GetDirectory(AcSettingsHolder.VideoPresets.PresetableKey), @"Loaded", shared.GetFileName()));
                            Directory.CreateDirectory(Path.GetDirectoryName(filename) ?? "");
                            File.WriteAllBytes(filename, data);
                            if (result == Choise.ApplyAndSave) {
                                UserPresetsControl.LoadPreset(AcSettingsHolder.VideoPresets.PresetableKey, filename);
                            }
                            return ArgumentHandleResult.SuccessfulShow;
                        case Choise.Apply:
                            AcSettingsHolder.VideoPresets.ImportFromPresetData(data.ToUtf8String());
                            return ArgumentHandleResult.SuccessfulShow;
                        default:
                            return ArgumentHandleResult.Failed;
                    }
                }

                case SharedEntryType.AssistsSetupPreset: {
                    var result = ShowDialog(shared);
                    switch (result) {
                        case Choise.Save:
                        case Choise.ApplyAndSave:
                            var filename = FileUtils.EnsureUnique(Path.Combine(
                                    PresetsManager.Instance.GetDirectory(AssistsViewModel.Instance.PresetableKey), @"Loaded", shared.GetFileName()));
                            Directory.CreateDirectory(Path.GetDirectoryName(filename) ?? "");
                            File.WriteAllBytes(filename, data);
                            if (result == Choise.ApplyAndSave) {
                                UserPresetsControl.LoadPreset(AssistsViewModel.Instance.PresetableKey, filename);
                            }
                            return ArgumentHandleResult.SuccessfulShow;
                        case Choise.Apply:
                            AssistsViewModel.Instance.ImportFromPresetData(data.ToUtf8String());
                            return ArgumentHandleResult.SuccessfulShow;
                        default:
                            return ArgumentHandleResult.Failed;
                    }
                }

                case SharedEntryType.QuickDrivePreset: {
                    var result = ShowDialog(shared, AppStrings.Arguments_Shared_JustGo);
                    switch (result) {
                        case Choise.Save:
                        case Choise.ApplyAndSave:
                            var filename = FileUtils.EnsureUnique(Path.Combine(
                                    PresetsManager.Instance.GetDirectory(QuickDrive.PresetableKeyValue), @"Loaded", shared.GetFileName()));
                            Directory.CreateDirectory(Path.GetDirectoryName(filename) ?? "");
                            File.WriteAllBytes(filename, data);
                            if (result == Choise.ApplyAndSave) {
                                QuickDrive.LoadPreset(filename);
                            }
                            return ArgumentHandleResult.SuccessfulShow;
                        case Choise.Apply:
                            QuickDrive.LoadSerializedPreset(data.ToUtf8String());
                            return ArgumentHandleResult.SuccessfulShow;
                        case Choise.Extra: // just go
                            if (!QuickDrive.RunSerializedPreset(data.ToUtf8String())) {
                                throw new InformativeException(AppStrings.Common_CannotStartRace, AppStrings.Arguments_CannotStartRace_Commentary);
                            }

                            return ArgumentHandleResult.SuccessfulShow;
                        default:
                            return ArgumentHandleResult.Failed;
                    }
                }

                case SharedEntryType.RaceGridPreset: {
                    var result = ShowDialog(shared);
                    switch (result) {
                        case Choise.Save:
                        case Choise.ApplyAndSave:
                            var filename = FileUtils.EnsureUnique(Path.Combine(
                                    PresetsManager.Instance.GetDirectory(RaceGridViewModel.PresetableKeyValue), @"Loaded", shared.GetFileName()));
                            Directory.CreateDirectory(Path.GetDirectoryName(filename) ?? "");
                            File.WriteAllBytes(filename, data);
                            if (result == Choise.ApplyAndSave) {
                                RaceGridViewModel.LoadPreset(filename);
                                QuickDrive.NavigateToPage();
                            }
                            return ArgumentHandleResult.SuccessfulShow;
                        case Choise.Apply:
                            RaceGridViewModel.LoadSerializedPreset(data.ToUtf8String());
                            QuickDrive.NavigateToPage();
                            return ArgumentHandleResult.SuccessfulShow;
                        default:
                            return ArgumentHandleResult.Failed;
                    }
                }

                default:
                    throw new Exception(string.Format(AppStrings.Arguments_SharedUnsupported, shared.EntryType));
            }
        }
    }
}