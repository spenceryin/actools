using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using AcManager.Controls.Helpers;
using AcManager.Tools.GameProperties;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using Hardcodet.Wpf.TaskbarNotification;
using Application = System.Windows.Application;

namespace AcManager {
    public partial class AppHibernator {
        private TaskbarIcon _icon;

        private void AddTrayIconWpf() {
            Application.Current.Dispatcher.Invoke(() => {
                var rhm = new MenuItem { Header = "RHM Settings", Command = RhmService.Instance.ShowSettingsCommand };
                rhm.SetBinding(UIElement.VisibilityProperty, new Binding {
                    Source = RhmService.Instance,
                    Path = new PropertyPath(nameof(RhmService.Active))
                });

                var restore = new MenuItem { Header = UiStrings.Restore };
                var close = new MenuItem { Header = UiStrings.Close };

                restore.Click += RestoreMenuItem_Click;
                close.Click += CloseMenuItem_Click;

                _icon = new TaskbarIcon {
                    Icon = AppIconService.GetTrayIcon(),
                    ToolTipText = AppStrings.Hibernate_TrayText,
                    ContextMenu = new ContextMenu {
                        Items = {
                            rhm,
                            restore,
                            new Separator(),
                            close
                        }
                    },
                    DoubleClickCommand = new DelegateCommand(WakeUp)
                };

            });
        }

        private void RemoveTrayIconWpf() {
            DisposeHelper.Dispose(ref _icon);
        }
    }
}