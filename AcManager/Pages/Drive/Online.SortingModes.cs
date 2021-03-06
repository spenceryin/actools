﻿using System;
using System.Collections;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Online;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Pages.Drive {
    public partial class Online {
        public abstract class ServerEntrySorter : NotifyPropertyChanged, IComparer {
            int IComparer.Compare(object x, object y) {
                var xs = x as ServerEntry;
                var ys = y as ServerEntry;
                if (xs == null) return ys == null ? 0 : 1;
                if (ys == null) return -1;

                return Compare(xs, ys);
            }

            public abstract int Compare(ServerEntry x, ServerEntry y);

            public abstract bool IsAffectedBy(string propertyName);
        }

        public class SortingName : ServerEntrySorter {
            public override int Compare(ServerEntry x, ServerEntry y) {
                return string.Compare(x.DisplayName, y.DisplayName, StringComparison.CurrentCultureIgnoreCase);
            }

            public override bool IsAffectedBy(string propertyName) {
                return propertyName == nameof(ServerEntry.DisplayName);
            }
        }

        public class SortingFavourites : ServerEntrySorter {
            public override int Compare(ServerEntry x, ServerEntry y) {
                if (x.IsFavourited ^ y.IsFavourited) return x.IsFavourited ? -1 : 1;
                return string.Compare(x.DisplayName, y.DisplayName, StringComparison.CurrentCultureIgnoreCase);
            }

            public override bool IsAffectedBy(string propertyName) {
                return propertyName == nameof(ServerEntry.DisplayName) || propertyName == nameof(ServerEntry.IsFavourited);
            }
        }

        private class SortingDriversCount : ServerEntrySorter {
            public override int Compare(ServerEntry x, ServerEntry y) {
                var dif = -x.CurrentDriversCount.CompareTo(y.CurrentDriversCount);
                return dif == 0 ? string.Compare(x.DisplayName, y.DisplayName, StringComparison.Ordinal) : dif;
            }

            public override bool IsAffectedBy(string propertyName) {
                return propertyName == nameof(ServerEntry.CurrentDriversCount);
            }
        }

        private class SortingConnectedDriversCount : ServerEntrySorter {
            public override int Compare(ServerEntry x, ServerEntry y) {
                var dif = -x.ConnectedDrivers.CompareTo(y.ConnectedDrivers);
                return dif == 0 ? string.Compare(x.DisplayName, y.DisplayName, StringComparison.Ordinal) : dif;
            }

            public override bool IsAffectedBy(string propertyName) {
                return propertyName == nameof(ServerEntry.ConnectedDrivers);
            }
        }

        private class SortingCapacityCount : ServerEntrySorter {
            public override int Compare(ServerEntry x, ServerEntry y) {
                var dif = -x.Capacity.CompareTo(y.Capacity);
                return dif == 0 ? string.Compare(x.DisplayName, y.DisplayName, StringComparison.Ordinal) : dif;
            }

            public override bool IsAffectedBy(string propertyName) {
                return propertyName == nameof(ServerEntry.Capacity);
            }
        }

        private class SortingCarsNumberCount : ServerEntrySorter {
            public override int Compare(ServerEntry x, ServerEntry y) {
                var dif = -(x.Cars?.Count ?? 0).CompareTo(y.Cars?.Count ?? 0);
                return dif == 0 ? string.Compare(x.DisplayName, y.DisplayName, StringComparison.Ordinal) : dif;
            }

            public override bool IsAffectedBy(string propertyName) {
                return propertyName == nameof(ServerEntry.Cars);
            }
        }

        private class SortingPing : ServerEntrySorter {
            public override int Compare(ServerEntry x, ServerEntry y) {
                const long maxPing = 999999;
                var dif = (x.Ping ?? maxPing).CompareTo(y.Ping ?? maxPing);
                return dif == 0 ? string.Compare(x.DisplayName, y.DisplayName, StringComparison.Ordinal) : dif;
            }

            public override bool IsAffectedBy(string propertyName) {
                return propertyName == nameof(ServerEntry.Ping);
            }
        }

        [CanBeNull]
        public static ServerEntrySorter GetSorter(string modeKey) {
            switch (modeKey) {
                case "favourites":
                    return new SortingFavourites();
                case "drivers":
                    return new SortingDriversCount();
                case "connected":
                    return new SortingConnectedDriversCount();
                case "capacity":
                    return new SortingCapacityCount();
                case "cars":
                    return new SortingCarsNumberCount();
                case "ping":
                    return new SortingPing();
                case "name":
                    return new SortingName();
                default:
                    return null;
            }
        }

        private static SettingEntry[] DefaultSortingModes { get; } = {
            new SettingEntry("name", AppStrings.Online_Sorting_Name),
            new SettingEntry("favourites", "Favourites"),
            new SettingEntry("drivers", AppStrings.Online_Sorting_Drivers),
            new SettingEntry("connected", "Connected Drivers"),
            new SettingEntry("capacity", AppStrings.Online_Sorting_Capacity),
            new SettingEntry("cars", AppStrings.Online_Sorting_CarsNumber),
            new SettingEntry("ping", AppStrings.Online_Sorting_Ping)
        };
    }
}
