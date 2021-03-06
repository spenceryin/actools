﻿using System;
using System.Threading.Tasks;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Pages.Dialogs {
    public partial class CarSkinsDialog {
        private class ViewModel {
            [NotNull]
            public CarObject SelectedCar { get; }

            public Uri ListUri => UriExtension.Create("/Pages/Lists/CarSkinsListPage.xaml?CarId={0}", SelectedCar.Id);

            public ViewModel([NotNull] CarObject car) {
                SelectedCar = car;
            }
        }

        private ViewModel Model => (ViewModel) DataContext;

        public CarSkinsDialog([NotNull] CarObject car) {
            if (car == null) throw new ArgumentNullException(nameof(car));

            DataContext = new ViewModel(car);

            DefaultContentSource = Model.ListUri;
            MenuLinkGroups.Add(new LinkGroupFilterable {
                DisplayName = AppStrings.Main_Skins,
                Source = Model.ListUri
            });

            InitializeComponent();
        }

        private void OnInitialized(object sender, EventArgs e) {
            if (Model?.SelectedCar == null) return;
            Model.SelectedCar.AcObjectOutdated += SelectedCar_AcObjectOutdated;
        }

        private void OnClosed(object sender, EventArgs e) {
            if (Model?.SelectedCar == null) return;
            Model.SelectedCar.AcObjectOutdated -= SelectedCar_AcObjectOutdated;
        }

        private async void SelectedCar_AcObjectOutdated(object sender, EventArgs e) {
            Hide();

            await Task.Delay(10);
            Close();
        }
    }
}
