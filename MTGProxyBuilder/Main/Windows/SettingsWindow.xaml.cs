using System;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.WindowsAPICodePack.Dialogs;
using MTGProxyBuilder.Properties;
using static System.Environment;

namespace MTGProxyBuilder.Main
{
	public partial class SettingsWindow : Window
	{
		public MainWindow HostWindow;

		private string ProxySizePercentage { get; set; }

		public SettingsWindow()
		{
			InitializeComponent();
		}

		private void SettingsWindowClosing(object sender, CancelEventArgs e)
		{
			HostWindow.IsEnabled = true;
			Settings.Default.Save();
		}

		private void SelectButtonClicked(object sender, RoutedEventArgs e)
		{
			CommonOpenFileDialog dialog = new CommonOpenFileDialog
			{
				InitialDirectory = GetFolderPath(SpecialFolder.Desktop),
				IsFolderPicker = true
			};

			if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
				DefaultOutputDirectoryBox.Text = dialog.FileName;
		}

		private void DeleteButtonClicked(object sender, RoutedEventArgs e)
		{
			DefaultOutputDirectoryBox.Text = "";
		}

		private void InputCheck(object sender, TextCompositionEventArgs e)
		{
			Regex regex = new Regex("[^0-9]+");
			e.Handled = regex.IsMatch(e.Text);
		}
	}
}
