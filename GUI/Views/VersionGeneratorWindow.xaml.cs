﻿using DivinityModManager.Models;

using ReactiveUI;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Concurrency;

namespace DivinityModManager.Views
{
	public class VersionGeneratorData : ReactiveObject
	{
		private long versionNumber;

		public long VersionNumber
		{
			get => versionNumber;
			set { this.RaiseAndSetIfChanged(ref versionNumber, value); }
		}

		private long major;

		public long Major
		{
			get => major;
			set { this.RaiseAndSetIfChanged(ref major, value); }
		}

		private long minor;

		public long Minor
		{
			get => minor;
			set { this.RaiseAndSetIfChanged(ref minor, value); }
		}

		private long revision;

		public long Revision
		{
			get => revision;
			set { this.RaiseAndSetIfChanged(ref revision, value); }
		}


		private long build;

		public long Build
		{
			get => build;
			set { this.RaiseAndSetIfChanged(ref build, value); }
		}

		public void UpdateVersionNumber()
		{

		}
	}

	/// <summary>
	/// Interaction logic for VersionGenerator.xaml
	/// </summary>
	public partial class VersionGeneratorWindow : HideWindowBase
	{
		public DivinityModVersion2 VersionData { get; set; } = new DivinityModVersion2(36028797018963968UL);

		public VersionGeneratorWindow()
		{
			InitializeComponent();

			DataContext = VersionData;
		}

		private Regex _numberOnlyRegex = new Regex("[^0-9]+");
		private void VersionNumberTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
		{
			e.Handled = _numberOnlyRegex.IsMatch(e.Text);
		}

		private void VersionNumberTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			
		}

		private void NumberSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
		{
			if(VersionNumberTextBox != null)
			{
				RxApp.MainThreadScheduler.Schedule(TimeSpan.FromMilliseconds(50), _ =>
				{
					VersionNumberTextBox.Text = VersionData.VersionInt.ToString();
				});
			}
		}

		private void CopyButton_Click(object sender, RoutedEventArgs e)
		{
			Clipboard.SetText(VersionData.VersionInt.ToString());
			AlertBar.SetSuccessAlert($"Copied {VersionData.VersionInt} to the clipboard.");
		}
		private void ResetButton_Click(object sender, RoutedEventArgs e)
		{
			VersionData.ParseInt(36028797018963968UL);
			VersionNumberTextBox.Text = "36028797018963968";
			AlertBar.SetWarningAlert($"Reset version number.");
		}

		private void NumberSlider_LostFocus(object sender, RoutedEventArgs e)
		{
			if (VersionNumberTextBox != null)
			{
				VersionNumberTextBox.Text = VersionData.VersionInt.ToString();
			}
		}

		private void VersionNumberTextBox_LostFocus(object sender, RoutedEventArgs e)
		{
			if (sender is TextBox tb)
			{
				if (UInt64.TryParse(tb.Text, out ulong version))
				{
					VersionData.ParseInt(version);
				}
				else
				{
					VersionData.ParseInt(36028797018963968UL);
					tb.Text = "36028797018963968";
				}
			}
		}
	}
}