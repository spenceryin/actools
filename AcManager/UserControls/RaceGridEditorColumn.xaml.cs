﻿using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using AcManager.Controls.UserControls;
using AcManager.Controls.ViewModels;
using AcManager.Pages.Miscellaneous;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;

namespace AcManager.UserControls {
    public sealed partial class RaceGridEditorColumn : INotifyPropertyChanged {
        public RaceGridEditorColumn() {
            InputBindings.AddRange(new[] {
                new InputBinding(new DelegateCommand(() => {
                    if (SelectCarPopup.IsOpen) {
                        var model = Model;
                        var selectCar = (SelectCarPopup.Content as DependencyObject)?.FindLogicalChild<SelectCar>();
                        if (model == null || selectCar == null) return;

                        foreach (var car in selectCar.GetSelectedCars().ToList()) {
                            var entry = model.FilteredView.OfType<RaceGridEntry>().LastOrDefault(x => x.Car == car);
                            if (entry != null) {
                                model.DeleteEntry(entry);
                            }
                        }
                    } else if (DetailsPopup.IsOpen) {
                        var dataGrid = (DetailsPopup.Content as FrameworkElement)?.FindVisualChild<DataGrid>();
                        if (dataGrid == null) return;

                        foreach (var entry in dataGrid.SelectedItems.OfType<RaceGridEntry>().ToList()) {
                            entry.DeleteCommand.Execute(null);
                        }
                    } else {
                        foreach (var entry in ListBox.SelectedItems.OfType<RaceGridEntry>().ToList()) {
                            entry.DeleteCommand.Execute(null);
                        }
                    }
                }), new KeyGesture(Key.Delete)),
                new InputBinding(new DelegateCommand(() => {
                    if (SelectCarPopup.IsOpen) {
                        AddOpponentCarCommand.Execute(null);
                    }
                }), new KeyGesture(Key.Enter)),
            });
            InitializeComponent();
            SelectCarPopup.CustomPopupPlacementCallback = CustomPopupPlacementCallback;
            DetailsPopup.CustomPopupPlacementCallback = CustomPopupPlacementCallback;
        }

        private const string KeySelectedCar = ".RaceGridEditor:SelectedCar";

        [CanBeNull]
        private static CarObject LoadSelected() {
            var saved = ValuesStorage.GetString(KeySelectedCar);
            return saved == null ? null : CarsManager.Instance.GetById(saved);
        }

        private static CustomPopupPlacement[] CustomPopupPlacementCallback(Size popupSize, Size targetSize, Point offset) {
            return new [] {
                new CustomPopupPlacement {
                    Point = new Point(targetSize.Width - 12, -targetSize.Height - 200),
                    PrimaryAxis = PopupPrimaryAxis.Vertical
                },
                new CustomPopupPlacement {
                    Point = new Point(-popupSize.Width, -targetSize.Height - 200),
                    PrimaryAxis = PopupPrimaryAxis.Vertical
                },
            };
        }

        [CanBeNull]
        public RaceGridViewModel Model => DataContext as RaceGridViewModel;

        private ICommand _cloneSelectedCommand;

        public ICommand CloneSelectedCommand => _cloneSelectedCommand ?? (_cloneSelectedCommand = new DelegateCommand(() => {
            var items = ListBox.SelectedItems.OfType<RaceGridEntry>().Select(x => x.Clone()).ToList();
            if (items.Count == 0 || Model == null) return;

            var destination = Model.NonfilteredList.IndexOf(ListBox.SelectedItems.OfType<RaceGridEntry>().Last()) + 1;
            foreach (var item in items) {
                Model.InsertEntry(destination++, item);
            }

            ListBox.SelectedItems.Clear();
            foreach (var item in items) {
                ListBox.SelectedItems.Add(item);
            }
        }));

        private ICommand _deleteSelectedCommand;

        public ICommand DeleteSelectedCommand => _deleteSelectedCommand ?? (_deleteSelectedCommand = new DelegateCommand(() => {
            var items = ListBox.SelectedItems.OfType<RaceGridEntry>().ToList();
            if (items.Count == 0 || Model == null) return;
            
            foreach (var item in items) {
                Model.DeleteEntry(item);
            }
        }));

        public ICommand SavePresetCommand => Model?.SavePresetCommand;

        private CommandBase _addOpponentCarCommand;

        public ICommand AddOpponentCarCommand => _addOpponentCarCommand ??
                (_addOpponentCarCommand = new DelegateCommand(AddSelected, () => SelectedCar != null));

        private void AddSelected() {
            var model = Model;
            var selectCar = (SelectCarPopup.Content as DependencyObject)?.FindLogicalChild<SelectCar>();
            if (model == null || selectCar == null) return;

            foreach (var car in selectCar.GetSelectedCars()) {
                model.AddEntry(car);
            }
        }

        private void SelectCar_OnItemChosen(object sender, ItemChosenEventArgs<CarObject> e) {
            Model?.AddEntry(e.ChosenItem);
        }

        private ICommand _closeAddingPopupCommand;

        public ICommand ClosePopupsCommand => _closeAddingPopupCommand ?? (_closeAddingPopupCommand = new DelegateCommand(() => {
            SelectCarPopup.IsOpen = false;
            DetailsPopup.IsOpen = false;
        }));

        private CarObject _selectedCar;

        public CarObject SelectedCar {
            get { return _selectedCar; }
            set {
                if (Equals(value, _selectedCar) || value == null) return;
                _selectedCar = value;
                OnPropertyChanged();
                ValuesStorage.Set(KeySelectedCar, value.Id);
                _addOpponentCarCommand?.RaiseCanExecuteChanged();
            }
        }

        [ValueConversion(typeof(bool), typeof(string))]
        private class InnerModeToLabelConverter : IValueConverter {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
                return value as bool? == true ? AppStrings.RaceGrid_Candidates : AppStrings.RaceGrid_Opponents;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
                throw new NotSupportedException();
            }
        }

        public static readonly IValueConverter ModeToLabelConverter = new InnerModeToLabelConverter();

        private void Item_OnPreviewDoubleClick(object sender, MouseButtonEventArgs e) {}

        private void ItemsControl_OnDrop(object sender, DragEventArgs e) {
            var raceGridEntry = e.Data.GetData(RaceGridEntry.DraggableFormat) as RaceGridEntry;
            var carObject = e.Data.GetData(CarObject.DraggableFormat) as CarObject;

            if (raceGridEntry == null && carObject == null || Model == null) {
                e.Effects = DragDropEffects.None;
                return;
            }

            var newIndex = ((ItemsControl)sender).GetMouseItemIndex();
            if (raceGridEntry != null) {
                Model.InsertEntry(newIndex, raceGridEntry);
            } else {
                Model.InsertEntry(newIndex, carObject);
            }

            e.Effects = DragDropEffects.Move;
        }

        private void SelectCarPopup_OnOpened(object sender, EventArgs e) {
            if (SelectedCar == null) {
                SelectedCar = LoadSelected() ?? Model?.PlayerCar ?? CarsManager.Instance.GetDefault();
            }
        }

        private void OpponentSkin_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            var entry = (sender as FrameworkElement)?.DataContext as RaceGridEntry;
            if (entry?.SpecialEntry != false) return;

            var dataGrid = (DetailsPopup.Content as FrameworkElement)?.FindVisualChild<DataGrid>();
            if (dataGrid != null) {
                dataGrid.SelectedItem = entry;
            }

            DetailsPopup.StaysOpen = true;

            var control = new CarBlock {
                Car = entry.Car,
                SelectedSkin = entry.CarSkin ?? entry.Car.SelectedSkin,
                SelectSkin = true,
                OpenShowroom = true
            };

            var dialog = new ModernDialog {
                Content = control,
                Width = 640,
                Height = 720,
                MaxWidth = 640,
                MaxHeight = 720,
                SizeToContent = SizeToContent.Manual,
                Title = entry.Car.DisplayName
            };

            dialog.Buttons = new[] { dialog.OkButton, dialog.CancelButton };
            dialog.ShowDialog();

            if (dialog.IsResultOk) {
                entry.CarSkin = control.SelectedSkin;
            }

            DetailsPopup.StaysOpen = false;
        }

        private void OpponentSkinCell_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (e.ClickCount == 2) {
                OpponentSkin_OnMouseLeftButtonDown(sender, e);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        private void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
