﻿using System;
using System.Windows;
using AcManager.Controls.ViewModels;
using AcManager.Tools.Filters;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Converters;
using StringBasedFilter;

namespace AcManager.Pages.Lists {
    public partial class ReplaysListPage : IParametrizedUriContent {
        public void OnUri(Uri uri) {
            var filter = uri.GetQueryParam("Filter");
            DataContext = new ReplaysListPageViewModel(string.IsNullOrEmpty(filter) ? null : Filter.Create(ReplayObjectTester.Instance, filter));
            InitializeComponent();
        }

        private void ReplaysListPage_OnLoaded(object sender, RoutedEventArgs e) {
            ((ReplaysListPageViewModel)DataContext).Load();
        }

        private void ReplaysListPage_OnUnloaded(object sender, RoutedEventArgs e) {
            ((ReplaysListPageViewModel)DataContext).Unload();
        }

        private class ReplaysListPageViewModel : AcListPageViewModel<ReplayObject> {
            public ReplaysListPageViewModel(IFilter<ReplayObject> listFilter)
                    : base(ReplaysManager.Instance, listFilter) {}

            protected override string GetStatus() => PluralizingConverter.PluralizeExt(MainList.Count, AppStrings.List_Replays);
        }
    }
}
