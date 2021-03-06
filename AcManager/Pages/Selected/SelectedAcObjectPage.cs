﻿using System.Windows.Controls;
using System.Windows.Input;
using JetBrains.Annotations;
using AcManager.Tools.AcObjectsNew;

namespace AcManager.Pages.Selected {
    public class SelectedAcObjectPage : UserControl {
        public AcCommonObject SelectedAcObject { get; private set; }

        [UsedImplicitly]
        public SelectedAcObjectPage() { }

        protected void InitializeAcObjectPage([NotNull] ISelectedAcObjectViewModel model) {
            SelectedAcObject = model.SelectedAcObject;
            InputBindings.Clear();
            InputBindings.AddRange(new[] {
                new InputBinding(SelectedAcObject.CopyIdCommand, new KeyGesture(Key.C, ModifierKeys.Control)),
                new InputBinding(SelectedAcObject.CopyIdCommand, new KeyGesture(Key.C, ModifierKeys.Control | ModifierKeys.Shift)) { CommandParameter = @"name" },
                new InputBinding(SelectedAcObject.CopyIdCommand, new KeyGesture(Key.C, ModifierKeys.Control | ModifierKeys.Alt)) { CommandParameter = @"path" },
                new InputBinding(SelectedAcObject.ViewInExplorerCommand, new KeyGesture(Key.F, ModifierKeys.Control)),
                new InputBinding(SelectedAcObject.ReloadCommand, new KeyGesture(Key.R, ModifierKeys.Control)),
                new InputBinding(SelectedAcObject.ReloadCommand, new KeyGesture(Key.R, ModifierKeys.Control | ModifierKeys.Shift)) { CommandParameter = @"full" },
                new InputBinding(SelectedAcObject.ToggleCommand, new KeyGesture(Key.D, ModifierKeys.Control)),
                new InputBinding(SelectedAcObject.SaveCommand, new KeyGesture(Key.S, ModifierKeys.Control)),
                new InputBinding(model.ChangeIdCommand, new KeyGesture(Key.F2, ModifierKeys.Control | ModifierKeys.Shift)),
                new InputBinding(model.CloneCommand, new KeyGesture(Key.D, ModifierKeys.Control | ModifierKeys.Shift)),
                new InputBinding(model.FindInformationCommand, new KeyGesture(Key.I, ModifierKeys.Control)),
                new InputBinding(SelectedAcObject.DeleteCommand, new KeyGesture(Key.Delete, ModifierKeys.Control))
            });
            DataContext = model;

            if (!_set) {
                _set = true;
                Loaded += OnLoaded;
                Unloaded += OnUnloaded;
            }
        }

        private bool _set, _loaded;

        private void OnLoaded(object sender, System.Windows.RoutedEventArgs e) {
            if (_loaded) return;
            _loaded = true;

            ((ISelectedAcObjectViewModel)DataContext).Load();
        }

        private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e) {
            if (!_loaded) return;
            _loaded = false;

            ((ISelectedAcObjectViewModel)DataContext).Unload();
        }
    }
}
