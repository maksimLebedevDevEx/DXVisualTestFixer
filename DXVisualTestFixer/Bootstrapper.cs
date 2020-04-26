﻿using System.Windows;
using DevExpress.Utils.CommonDialogs;
using DevExpress.Xpf.Dialogs;
using DevExpress.Xpf.Docking;
using DXVisualTestFixer.Common;
using DXVisualTestFixer.Core;
using DXVisualTestFixer.Core.Configuration;
using DXVisualTestFixer.Minio;
using DXVisualTestFixer.Git;
using DXVisualTestFixer.PrismCommon;
using DXVisualTestFixer.Services;
using DXVisualTestFixer.UI.Common;
using DXVisualTestFixer.UI.PrismCommon;
using DXVisualTestFixer.UI.Services;
using DXVisualTestFixer.UI.ViewModels;
using DXVisualTestFixer.UI.Views;
using Microsoft.Practices.Unity;
using Prism.Regions;
using Prism.Unity;

namespace DXVisualTestFixer {
	public class Bootstrapper : UnityBootstrapper {
		protected override DependencyObject CreateShell() => new Shell();
		protected override void InitializeShell() {
			Container.Resolve<IAppearanceService>()?.SetTheme( /*Config.ThemeName*/ "Office2019Colorful", "DarkLilac");
			Application.Current.MainWindow.Show();
		}

		protected override void ConfigureContainer() {
			base.ConfigureContainer();
			RegisterTypeIfMissing(typeof(INotificationService), typeof(NotificationService), true);
			RegisterTypeIfMissing(typeof(IConfigSerializer), typeof(ConfigSerializer), true);
			RegisterTypeIfMissing(typeof(ILoadingProgressController), typeof(LoadingProgressController), true);
			RegisterTypeIfMissing(typeof(ILoggingService), typeof(LoggingService), true);
			RegisterTypeIfMissing(typeof(ITestsService), typeof(TestsService), true);
			RegisterTypeIfMissing(typeof(IGitWorker), typeof(GitWorker), true);
			RegisterTypeIfMissing(typeof(IMinioWorker), typeof(MinioWorker), true);
			RegisterTypeIfMissing(typeof(IVersionService), typeof(VersionService), true);
			RegisterTypeIfMissing(typeof(ISettingsViewModel), typeof(SettingsViewModel), false);
			RegisterTypeIfMissing(typeof(IFolderBrowserDialog), typeof(DXFolderBrowserDialog), false);
			RegisterTypeIfMissing(typeof(IAppearanceService), typeof(AppearanceService), true);
			RegisterTypeIfMissing(typeof(IUpdateService), typeof(SquirrelUpdateService), true);
			RegisterTypeIfMissing(typeof(IDXNotification), typeof(DXNotification), false);
			RegisterTypeIfMissing(typeof(IDXConfirmation), typeof(DXConfirmation), false);
			RegisterTypeIfMissing(typeof(IActiveService), typeof(IsActiveService), true);
		}

		protected override RegionAdapterMappings ConfigureRegionAdapterMappings() {
			var mappings = base.ConfigureRegionAdapterMappings();
			mappings.RegisterMapping(typeof(LayoutPanel), Container.Resolve<LayoutPanelRegionAdapter>());
			return mappings;
		}

		protected override void ConfigureViewModelLocator() {
			base.ConfigureViewModelLocator();
			Container.Resolve<IRegionManager>().RegisterViewWithRegion(Regions.Main, typeof(MainView));
		}
	}
}