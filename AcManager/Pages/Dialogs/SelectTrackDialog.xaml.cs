﻿using System;
using System.ComponentModel;
using System.Windows;
using AcManager.Controls.Helpers;
using AcManager.Pages.Miscellaneous;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Converters;
using FirstFloor.ModernUI.Windows.Navigation;
using JetBrains.Annotations;

namespace AcManager.Pages.Dialogs {
    public partial class SelectTrackDialog {

        private static WeakReference<SelectTrackDialog> _instance;

        public static SelectTrackDialog Instance {
            get {
                if (_instance == null) {
                    return null;
                }

                SelectTrackDialog result;
                return _instance.TryGetTarget(out result) ? result : null;
            }
        }

        public SelectTrackDialog(TrackObjectBase selectedTrackConfiguration) {
            _instance = new WeakReference<SelectTrackDialog>(this);

            DataContext = new ViewModel(selectedTrackConfiguration);
            InitializeComponent();
            
            Model.PropertyChanged += Model_PropertyChanged;
            BackgroundImage0.Source = UriToCachedImageConverter.Convert(Model.CurrentPreviewImage);

            Buttons = new[] { OkButton, CancelButton };
        }

        public static TrackObjectBase Show(TrackObjectBase track) {
            var dialog = new SelectTrackDialog(track);
            dialog.ShowDialog();
            return !dialog.IsResultOk || dialog.Model.SelectedTrackConfiguration == null ? track : dialog.Model.SelectedTrackConfiguration;
        }

        private int _state;

        private void Model_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName != nameof(Model.CurrentPreviewImage)) return;
            (_state == 0 ? BackgroundImage1 : BackgroundImage0).Source = UriToCachedImageConverter.Convert(Model.CurrentPreviewImage);
            VisualStateManager.GoToElementState(BackgroundImage1, @"State" + _state, true);
            _state = 1 - _state;
        }

        private ISelectedItemPage<AcObjectNew> _list;
        private IChoosingItemControl<AcObjectNew> _choosing;

        private void Tabs_OnNavigated(object sender, NavigationEventArgs e) {
            if (_list != null) {
                _list.PropertyChanged -= List_PropertyChanged;
            }

            if (_choosing != null) {
                _choosing.ItemChosen -= Choosing_ItemChosen;
            }

            var content = ((ModernTab)sender).Frame.Content;
            _list = content as ISelectedItemPage<AcObjectNew>;
            _choosing = content as IChoosingItemControl<AcObjectNew>;

            if (_list != null) {
                _list.SelectedItem = Model.SelectedTrackConfiguration?.MainTrackObject;
                _list.PropertyChanged += List_PropertyChanged;
            }

            if (_choosing != null) {
                _choosing.ItemChosen += Choosing_ItemChosen;
            }
        }

        private void Choosing_ItemChosen(object sender, ItemChosenEventArgs<AcObjectNew> e) {
            var c = e.ChosenItem as TrackObjectBase;
            if (c != null) {
                Model.SelectedTrackConfiguration = c.MainTrackObject.SelectedLayout;
                CloseWithResult(MessageBoxResult.OK);
            }
        }

        private void List_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(_list.SelectedItem)) {
                Model.SelectedTrackConfiguration = (_list.SelectedItem as TrackObject)?.SelectedLayout ?? _list.SelectedItem as TrackObjectBase;
            }
        }

        public ViewModel Model => (ViewModel)DataContext;

        public class ViewModel : NotifyPropertyChanged {
            private readonly DelayedPropertyWrapper<TrackObjectBase> _selectedTrackConfiguration;

            [CanBeNull]
            public TrackObjectBase SelectedTrackConfiguration {
                get { return _selectedTrackConfiguration.Value; }
                set {
                    if (value != null) {
                        _selectedTrackConfiguration.Value = value;
                    }
                }
            }

            public TrackObject SelectedTrack => SelectedTrackConfiguration?.MainTrackObject;

            private string _currentPreviewImage;

            public string CurrentPreviewImage {
                get { return _currentPreviewImage; }
                set {
                    if (Equals(value, _currentPreviewImage)) return;
                    _currentPreviewImage = value;
                    OnPropertyChanged();
                }
            }

            public ViewModel(TrackObjectBase selectedTrackConfiguration) {
                _selectedTrackConfiguration = new DelayedPropertyWrapper<TrackObjectBase>(v => {
                    if (v == null) return;

                    v.MainTrackObject.SelectedLayout = v;
                    CurrentPreviewImage = v.PreviewImage;

                    OnPropertyChanged(nameof(SelectedTrackConfiguration));
                    OnPropertyChanged(nameof(SelectedTrack));
                });

                SelectedTrackConfiguration = selectedTrackConfiguration;
            }
        }
    }
}
