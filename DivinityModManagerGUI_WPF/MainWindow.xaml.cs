﻿using DivinityModManager.Models;
using DivinityModManager.Util;
using DivinityModManager.ViewModels;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Concurrency;
using DynamicData;
using DynamicData.Binding;
using System.Diagnostics;
using System.Globalization;
using AutoUpdaterDotNET;
using System.Windows.Threading;
using AdonisUI.Controls;
using Alphaleonis.Win32.Filesystem;
using DivinityModManager.WinForms;

namespace DivinityModManager.Views
{
#if debug
	public class MainWindowDebugData : MainWindowViewModel
	{
		public MainWindowDebugData () : base()
		{
			Random rnd = new Random();
			for(var i = 0; i < 60; i++)
			{
				var d = new DivinityModData()
				{
					Name = "Test" + i,
					Author = "LaughingLeader",
					Version = new DivinityModVersion(370871668),
					Description = "Test",
					UUID = DivinityModDataLoader.CreateHandle()
				};
				d.IsEditorMod = i%2 == 0;
				d.IsActive = rnd.Next(4) <= 2;
				this.AddMods(d);
			}

			for (var i = 0; i < 4; i++)
			{
				var p = new DivinityProfileData()
				{
					Name = "Profile" + i
				};
				p.ModOrder.AddRange(Mods.Select(m => m.UUID));
				Profiles.Add(p);
			}
			SelectedProfileIndex = 0;

			for (var i = 0; i < 4; i++)
			{
				var lo = new DivinityLoadOrder()
				{
					Name = "SavedOrder" + i
				};
				var orderList = this.mods.Items.Where(m => m.IsActive).Select(m => m.ToOrderEntry()).OrderBy(e => rnd.Next(10));
				lo.SetOrder(orderList);
				ModOrderList.Add(lo);
			}
			SelectedModOrderIndex = 2;
		}
	}
#endif

	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : AdonisWindow, IViewFor<MainWindowViewModel>
	{
		private static MainWindow self;
		public static MainWindow Self => self;

		private ConflictCheckerWindow conflictCheckerWindow;
		public ConflictCheckerWindow ConflictCheckerWindow => conflictCheckerWindow;

		private SettingsWindow settingsWindow;
		public SettingsWindow SettingsWindow => settingsWindow;

		private AboutWindow aboutWindow;
		public AboutWindow AboutWindow => aboutWindow;

		public MainWindowViewModel ViewModel { get; set; }
		object IViewFor.ViewModel { get; set; }

		//public WindowWrapper Wrapper { get; private set; }

		public MainWindow()
		{
			InitializeComponent();

			self = this;

			//Wrapper = new WindowWrapper(this);

			settingsWindow = new SettingsWindow();
			settingsWindow.OnWorkshopPathChanged += delegate
			{
				RxApp.MainThreadScheduler.Schedule(TimeSpan.FromMilliseconds(50), _ => ViewModel.LoadWorkshopMods());
			};
			settingsWindow.Closed += delegate
			{
				if(ViewModel?.Settings != null)
				{
					ViewModel.Settings.SettingsWindowIsOpen = false;
				}
			};
			settingsWindow.Hide();

			ViewModel = new MainWindowViewModel();

			if (File.Exists(Alphaleonis.Win32.Filesystem.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "debug")))
			{
				ViewModel.DebugMode = true;
				ViewModel.ToggleLogging(true);
				Trace.WriteLine("Enable logging due to the debug file next to the exe.");
			}

			this.TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.None;

			AlertBar.Show += AlertBar_Show;

			AutoUpdater.ApplicationExitEvent += AutoUpdater_ApplicationExitEvent;
			AutoUpdater.HttpUserAgent = "DivinityModManagerUser";

			var res = this.TryFindResource("ModUpdaterPanel");
			if(res != null && res is ModUpdatesLayout modUpdaterPanel)
			{
				Binding binding = new Binding("ModUpdatesViewData");
				binding.Source = ViewModel;
				modUpdaterPanel.SetBinding(ModUpdatesLayout.DataContextProperty, binding);
			}

			DataContext = ViewModel;

			this.WhenActivated(d =>
			{
				ViewModel.OnViewActivated(this);

				this.WhenAnyValue(x => x.ViewModel.Title).BindTo(this, view => view.Title);

				var canCheckForUpdates = this.WhenAnyValue(x => x.ViewModel.MainProgressIsActive, b => b == false);
				ViewModel.CheckForAppUpdatesCommand = ReactiveCommand.Create(() =>
				{
#if !DEBUG
				AutoUpdater.ReportErrors = true;
#endif
					AutoUpdater.Start(DivinityApp.URL_UPDATE);
					ViewModel.Settings.LastUpdateCheck = DateTimeOffset.Now.ToUnixTimeSeconds();
					ViewModel.SaveSettings();
#if !DEBUG
				Task.Delay(1000).ContinueWith(_ =>
				{
					AutoUpdater.ReportErrors = false;
				});
#endif
				}, canCheckForUpdates);

				var c = ReactiveCommand.Create(() =>
				{
					if (!SettingsWindow.IsVisible)
					{
						SettingsWindow.Init(this.ViewModel.Settings);
						SettingsWindow.Show();
						settingsWindow.Owner = this;
						ViewModel.Settings.SettingsWindowIsOpen = true;
					}
					else
					{
						SettingsWindow.Hide();
						ViewModel.Settings.SettingsWindowIsOpen = false;
					}
				});
				c.ThrownExceptions.Subscribe((ex) =>
				{
					Trace.WriteLine("Error opening settings window: " + ex.ToString());
				});
				ViewModel.OpenPreferencesCommand = c;

				ViewModel.OpenAboutWindowCommand = ReactiveCommand.Create(() =>
				{
					if (AboutWindow == null)
					{
						aboutWindow = new AboutWindow();
					}

					if (!AboutWindow.IsVisible)
					{
						AboutWindow.DataContext = ViewModel;
						AboutWindow.Show();
						AboutWindow.Owner = this;
					}
					else
					{
						AboutWindow.Hide();
					}
				});

				this.WhenAnyValue(x => x.ViewModel.MainProgressIsActive).Subscribe((b) =>
				{
					Trace.WriteLine($"Main Progress is active: {b}");
					if (b)
					{
						this.TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Normal;
					}
					else
					{
						this.TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.None;
					}
				});

				//this.OneWayBind(ViewModel, vm => vm.MainProgressValue, view => view.TaskbarItemInfo.ProgressValue).DisposeWith(ViewModel.Disposables);

				this.WhenAnyValue(x => x.ViewModel.MainProgressValue).BindTo(this, view => view.TaskbarItemInfo.ProgressValue);

				this.WhenAnyValue(x => x.ViewModel.AddOrderConfigCommand).BindTo(this, view => view.FileAddNewOrderMenuItem.Command);
				this.WhenAnyValue(x => x.ViewModel.SaveOrderCommand).BindTo(this, view => view.FileSaveOrderMenuItem.Command);
				this.WhenAnyValue(x => x.ViewModel.SaveOrderAsCommand).BindTo(this, view => view.FileSaveOrderAsMenuItem.Command);

				this.WhenAnyValue(x => x.ViewModel.ImportOrderFromSaveCommand).BindTo(this, view => view.FileImportOrderFromSave.Command);
				this.WhenAnyValue(x => x.ViewModel.ImportOrderFromSaveAsNewCommand).BindTo(this, view => view.FileImportOrderFromSaveAsNew.Command);
				this.WhenAnyValue(x => x.ViewModel.ImportOrderFromFileCommand).BindTo(this, view => view.FileImportOrderFromFile.Command);
				this.WhenAnyValue(x => x.ViewModel.ImportOrderZipFileCommand).BindTo(this, view => view.FileImportOrderZip.Command);
				this.FileImportOrderZip.Visibility = Visibility.Collapsed; // Disabled for now

				this.WhenAnyValue(x => x.ViewModel.ExportOrderCommand).BindTo(this, view => view.FileExportOrderToGameMenuItem.Command);
				this.WhenAnyValue(x => x.ViewModel.ExportLoadOrderAsArchiveCommand).BindTo(this, view => view.FileExportOrderToArchiveMenuItem.Command);
				this.WhenAnyValue(x => x.ViewModel.ExportLoadOrderAsArchiveToFileCommand).BindTo(this, view => view.FileExportOrderToArchiveAsMenuItem.Command);

				this.WhenAnyValue(x => x.ViewModel.RefreshCommand).BindTo(this, view => view.FileRefreshMenuItem.Command);

				this.WhenAnyValue(x => x.ViewModel.ToggleDisplayNameCommand).BindTo(this, view => view.EditToggleFileNameDisplayMenuItem.Command);

				this.WhenAnyValue(x => x.ViewModel.OpenPreferencesCommand).BindTo(this, view => view.SettingsPreferencesMenuItem.Command);
				this.WhenAnyValue(x => x.ViewModel.ToggleDarkModeCommand).BindTo(this, view => view.SettingsDarkModeMenuItem.Command);

				this.WhenAnyValue(x => x.ViewModel.ExtractSelectedModsCommands).BindTo(this, view => view.ToolsExtractSelectedModsMenuItem.Command);
				this.WhenAnyValue(x => x.ViewModel.RenameSaveCommand).BindTo(this, view => view.ToolsRenameSaveMenuItem.Command);

				this.WhenAnyValue(x => x.ViewModel.CheckForAppUpdatesCommand).BindTo(this, view => view.HelpCheckForUpdateMenuItem.Command);
				this.WhenAnyValue(x => x.ViewModel.OpenDonationPageCommand).BindTo(this, view => view.HelpDonationMenuItem.Command);
				this.WhenAnyValue(x => x.ViewModel.OpenRepoPageCommand).BindTo(this, view => view.HelpOpenRepoPageMenuItem.Command);
				this.WhenAnyValue(x => x.ViewModel.OpenAboutWindowCommand).BindTo(this, view => view.HelpOpenAboutWindowMenuItem.Command);

			});
		}

		private void AutoUpdater_ApplicationExitEvent()
		{
			ViewModel.Settings.LastUpdateCheck = DateTimeOffset.Now.ToUnixTimeSeconds();
			ViewModel.SaveSettings();
			App.Current.Shutdown();
		}

		private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
		{
			if (depObj != null)
			{
				for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
				{
					DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
					if (child != null && child is T)
					{
						yield return (T)child;
					}

					foreach (T childOfChild in FindVisualChildren<T>(child))
					{
						yield return childOfChild;
					}
				}
			}
		}

		private void AlertBar_Show(object sender, RoutedEventArgs e)
		{
			var spStandard = (StackPanel)AlertBar.FindName("spStandard");
			var spOutline = (StackPanel)AlertBar.FindName("spOutline");

			Grid grdParent;
			switch (AlertBar.Theme)
			{
				case DivinityModManager.Controls.AlertBar.ThemeType.Standard:
					grdParent = FindVisualChildren<Grid>(spStandard).FirstOrDefault();
					break;
				case DivinityModManager.Controls.AlertBar.ThemeType.Outline:
				default:
					grdParent = FindVisualChildren<Grid>(spOutline).FirstOrDefault();
					break;
			}

			TextBlock lblMessage = FindVisualChildren<TextBlock>(grdParent).FirstOrDefault();
			if(lblMessage != null)
			{
				Trace.WriteLine(lblMessage.Text);
			}
		}

		public void ToggleConflictChecker(bool openWindow)
		{
			if(openWindow)
			{
				if (ConflictCheckerWindow == null)
				{
					conflictCheckerWindow = new ConflictCheckerWindow();
				}
				
				if(!conflictCheckerWindow.IsVisible)
				{
					conflictCheckerWindow.Init(ViewModel);
					conflictCheckerWindow.Show();
				}
			}
			else
			{
				if (conflictCheckerWindow != null)
				{
					conflictCheckerWindow.Close();
					conflictCheckerWindow = null;
				}
			}
		}

		private void ExportTestButton_Click(object sender, RoutedEventArgs e)
		{
			//DivinityModDataLoader.ExportModOrder(Data.ActiveModOrder, Data.Mods);
		}

		private void ComboBox_KeyDown_LoseFocus(object sender, KeyEventArgs e)
		{
			if((e.Key == Key.Enter || e.Key == Key.Return))
			{
				UIElement elementWithFocus = Keyboard.FocusedElement as UIElement;
				elementWithFocus.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
				e.Handled = true;
			}
		}

		private void MenuItem_Click(object sender, RoutedEventArgs e)
		{

		}

		private void ProfileComboBox_OnUserClick(object sender, MouseButtonEventArgs e)
		{
			RxApp.MainThreadScheduler.Schedule(TimeSpan.FromMilliseconds(200), () =>
			{
				if (ViewModel.Settings != null && ViewModel.Settings.LastOrder != ViewModel.SelectedModOrder.Name)
				{
					ViewModel.Settings.LastOrder = ViewModel.SelectedModOrder.Name;
					ViewModel.SaveSettings();
				}
			});
		}

		private void OrdersComboBox_Loaded(object sender, RoutedEventArgs e)
		{
			if(sender is ComboBox ordersComboBox)
			{
				var tb = FindVisualChildren<TextBox>(ordersComboBox).FirstOrDefault();
				if(tb != null)
				{
					tb.ContextMenu = ordersComboBox.ContextMenu;
					tb.ContextMenu.DataContext = ViewModel;
				}
			}
		}
	}
}
