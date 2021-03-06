﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Presentation;

namespace FirstFloor.ModernUI.Windows.Controls {
    /// <summary>
    /// Represents a Modern UI styled dialog window.
    /// </summary>
    public class ModernDialog : DpiAwareWindow {
        /// <summary>
        /// Identifies the BackgroundContent dependency property.
        /// </summary>
        public static readonly DependencyProperty BackgroundContentProperty = DependencyProperty.Register("BackgroundContent", typeof(object),
                typeof(ModernDialog));

        /// <summary>
        /// Identifies the Buttons dependency property.
        /// </summary>
        public static readonly DependencyProperty ButtonsProperty = DependencyProperty.Register("Buttons", typeof(IEnumerable<Button>), typeof(ModernDialog));

        private Button _okButton;
        private Button _goButton;
        private Button _cancelButton;
        private Button _yesButton;
        private Button _noButton;
        private Button _closeButton;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModernDialog"/> class.
        /// </summary>
        public ModernDialog() {
            DefaultStyleKey = typeof(ModernDialog);
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            CloseCommand = new DelegateCommand<MessageBoxResult?>(CloseWithResult);
            Buttons = new[] { CloseButton };
        }

        protected void CloseWithResult(MessageBoxResult? result) {
            if (result.HasValue) {
                MessageBoxResult = result.Value;

                try {
                    // sets the Window.DialogResult as well
                    // ReSharper disable once SwitchStatementMissingSomeCases
                    switch (result.Value) {
                        case MessageBoxResult.OK:
                        case MessageBoxResult.Yes:
                            DialogResult = true;
                            break;
                        case MessageBoxResult.Cancel:
                        case MessageBoxResult.No:
                            DialogResult = false;
                            break;
                        default:
                            DialogResult = null;
                            break;
                    }
                } catch (InvalidOperationException) {
                    // TODO: Maybe there is a better way?
                }
            }

            Close();
        }

        public static Button CreateExtraDialogButton(string content, ICommand command, bool isDefault = false) {
            return new Button {
                Content = content /*.ToLower()*/,
                IsDefault = isDefault,
                MinHeight = 21,
                MinWidth = 65,
                Margin = new Thickness(4, 0, 0, 0),
                Command = command
            };
        }

        public static Button CreateExtraDialogButton(string content, Action action, Func<bool> canExecute) {
            return CreateExtraDialogButton(content, new DelegateCommand(action, canExecute));
        }

        public static Button CreateExtraDialogButton(string content, Action action) {
            return CreateExtraDialogButton(content, new DelegateCommand(action));
        }

        public Button CreateExtraStyledDialogButton(string styleKey, string content, ICommand command) {
            return new Button {
                Content = content /*.ToLower()*/,
                MinHeight = 21,
                MinWidth = 65,
                Margin = new Thickness(4, 0, 0, 0),
                Style = FindResource(styleKey) as Style,
                Command = command
            };
        }

        public Button CreateExtraStyledDialogButton([Localizable(false)] string styleKey, string content, Action action, Func<bool> canExecute = null) {
            return CreateExtraStyledDialogButton(styleKey, content, new DelegateCommand(action, canExecute));
        }

        public Button CreateCloseDialogButton(string content, bool isDefault, bool isCancel, MessageBoxResult result, ICommand command = null) {
            return new Button {
                Content = content,
                Command = command == null ? CloseCommand : new CombinedCommand(CloseCommand, command),
                CommandParameter = result,
                IsDefault = isDefault,
                IsCancel = isCancel,
                MinHeight = 21,
                MinWidth = 65,
                Margin = new Thickness(4, 0, 0, 0)
            };
        }

        public Button CreateStyledCloseDialogButton(string styleKey, string content, bool isDefault, bool isCancel, MessageBoxResult result,
                Action action = null) {
            return new Button {
                Content = content,
                Command = action == null ? CloseCommand : new CombinedCommand(CloseCommand, new DelegateCommand(action)),
                CommandParameter = result,
                IsDefault = isDefault,
                IsCancel = isCancel,
                MinHeight = 21,
                MinWidth = 65,
                Margin = new Thickness(4, 0, 0, 0),
                Style = FindResource(styleKey) as Style
            };
        }

        /// <summary>
        /// Gets the close window command.
        /// </summary>
        public ICommand CloseCommand { get; }

        /// <summary>
        /// Gets the Ok button.
        /// </summary>
        public Button OkButton => _okButton ??
                (_okButton = CreateCloseDialogButton(UiStrings.Ok, true, false, MessageBoxResult.OK));

        /// <summary>
        /// Gets the Go button (result is MessageBoxResult.OK).
        /// </summary>
        public Button GoButton => _goButton ??
                (_goButton =
                        CreateStyledCloseDialogButton(@"Go.Button", UiStrings.Go, true, false, MessageBoxResult.OK));

        /// <summary>
        /// Gets the Cancel button.
        /// </summary>
        public Button CancelButton => _cancelButton ??
                (_cancelButton = CreateCloseDialogButton(UiStrings.Cancel, false, true, MessageBoxResult.Cancel));

        /// <summary>
        /// Gets the Yes button.
        /// </summary>
        public Button YesButton => _yesButton ??
                (_yesButton = CreateCloseDialogButton(UiStrings.Yes, true, false, MessageBoxResult.Yes));

        /// <summary>
        /// Gets the No button.
        /// </summary>
        public Button NoButton => _noButton ??
                (_noButton = CreateCloseDialogButton(UiStrings.No, false, true, MessageBoxResult.No));

        /// <summary>
        /// Gets the Close button.
        /// </summary>
        public Button CloseButton => _closeButton ??
                (_closeButton =
                        CreateCloseDialogButton(UiStrings.Close, true, false, MessageBoxResult.None));

        /// <summary>
        /// Gets or sets the background content of this window instance.
        /// </summary>
        public object BackgroundContent {
            get { return GetValue(BackgroundContentProperty); }
            set { SetValue(BackgroundContentProperty, value); }
        }

        /// <summary>
        /// Gets or sets the dialog buttons.
        /// </summary>
        public IEnumerable<Button> Buttons {
            get { return (IEnumerable<Button>)GetValue(ButtonsProperty); }
            set { SetValue(ButtonsProperty, value); }
        }

        /// <summary>
        /// Gets the message box result.
        /// </summary>
        /// <value>
        /// The message box result.
        /// </value>
        public MessageBoxResult MessageBoxResult { get; private set; } = MessageBoxResult.None;

        public bool IsResultCancel => MessageBoxResult == MessageBoxResult.Cancel;

        public bool IsResultOk => MessageBoxResult == MessageBoxResult.OK;

        public bool IsResultYes => MessageBoxResult == MessageBoxResult.Yes;

        /// <summary>
        /// Displays a messagebox.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="title">The title.</param>
        /// <param name="button">The button.</param>
        /// <param name="owner">The window owning the messagebox. The messagebox will be located at the center of the owner.</param>
        /// <returns></returns>
        public static MessageBoxResult ShowMessage(string text, string title, MessageBoxButton button, Window owner = null) {
            var dlg = new ModernDialog {
                Title = title,
                Content = new ScrollViewer {
                    Content = new SelectableBbCodeBlock { BbCode = text, Margin = new Thickness(0, 0, 0, 8) },
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
                },
                MinHeight = 0,
                MinWidth = 0,
                MaxHeight = 480,
                MaxWidth = 640
            };

            if (owner != null) {
                dlg.Owner = owner;
            }

            dlg.Buttons = GetButtons(dlg, button);
            dlg.ShowDialog();
            return dlg.MessageBoxResult;
        }

        public static MessageBoxResult ShowMessage(string text) {
            return ShowMessage(text, "", MessageBoxButton.OK);
        }

        private static IEnumerable<Button> GetButtons(ModernDialog owner, MessageBoxButton button) {
            // ReSharper disable once SwitchStatementMissingSomeCases
            switch (button) {
                case MessageBoxButton.OK:
                    yield return owner.OkButton;
                    break;
                case MessageBoxButton.OKCancel:
                    yield return owner.OkButton;
                    yield return owner.CancelButton;
                    break;
                case MessageBoxButton.YesNo:
                    yield return owner.YesButton;
                    yield return owner.NoButton;
                    break;
                case MessageBoxButton.YesNoCancel:
                    yield return owner.YesButton;
                    yield return owner.NoButton;
                    yield return owner.CancelButton;
                    break;
            }
        }

        public static readonly DependencyProperty IconSourceProperty = DependencyProperty.Register(nameof(IconSource), typeof(string),
                typeof(ModernDialog));

        public string IconSource {
            get { return (string)GetValue(IconSourceProperty); }
            set { SetValue(IconSourceProperty, value); }
        }

        public static readonly DependencyProperty ButtonsRowContentProperty = DependencyProperty.Register(nameof(ButtonsRowContent), typeof(object),
                typeof(ModernDialog));

        public object ButtonsRowContent {
            get { return GetValue(ButtonsRowContentProperty); }
            set { SetValue(ButtonsRowContentProperty, value); }
        }

        public static readonly DependencyProperty ButtonsMarginProperty = DependencyProperty.Register(nameof(ButtonsMargin), typeof(Thickness),
                typeof(ModernDialog));

        public Thickness ButtonsMargin {
            get { return (Thickness)GetValue(ButtonsMarginProperty); }
            set { SetValue(ButtonsMarginProperty, value); }
        }

        public static readonly DependencyProperty ShowTitleProperty = DependencyProperty.Register(nameof(ShowTitle), typeof(bool),
                typeof(ModernDialog), new PropertyMetadata(true));

        public bool ShowTitle {
            get { return (bool)GetValue(ShowTitleProperty); }
            set { SetValue(ShowTitleProperty, value); }
        }
    }

    public class FatalErrorMessage : ModernDialog {
        internal FatalErrorMessage() {
            DefaultStyleKey = typeof(FatalErrorMessage);
        }

        public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(nameof(Message), typeof(string),
                typeof(FatalErrorMessage));

        public string Message {
            get { return (string)GetValue(MessageProperty); }
            set { SetValue(MessageProperty, value); }
        }

        public static readonly DependencyProperty StackTraceProperty = DependencyProperty.Register(nameof(StackTrace), typeof(string),
                typeof(FatalErrorMessage));

        public string StackTrace {
            get { return (string)GetValue(StackTraceProperty); }
            set { SetValue(StackTraceProperty, value); }
        }

        private ICommand _copyCommand;

        public ICommand CopyCommand => _copyCommand ?? (_copyCommand = new DelegateCommand(() => {
            Clipboard.SetText(StackTrace);
        }));

        private ICommand _restartCommand;

        public ICommand RestartCommand => _restartCommand ?? (_restartCommand = new DelegateCommand(() => {
            _restartHelper?.Restart();
        }));

        private ICommand _exitCommand;

        public ICommand ExitCommand => _exitCommand ?? (_exitCommand = new DelegateCommand(() => {
            if (Application.Current != null) {
                Application.Current.Shutdown();
            } else {
                Environment.Exit(0);
            }
        }));

        private static IAppRestartHelper _restartHelper;

        public static void Register(IAppRestartHelper helper) {
            _restartHelper = helper;
        }

        public interface IAppRestartHelper {
            void Restart();
        }
    }
}
