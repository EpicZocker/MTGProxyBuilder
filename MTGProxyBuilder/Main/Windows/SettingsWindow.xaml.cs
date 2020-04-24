using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Windows;
using Microsoft.WindowsAPICodePack.Dialogs;
using MTGProxyBuilder.Main.Classes;
using static System.Environment;

namespace MTGProxyBuilder.Main
{
	public partial class SettingsWindow : Window
	{
		public MainWindow HostWindow;
		public SettingsFile File;

		private string DefaultOutputDirectory;

		public SettingsWindow()
		{
			InitializeComponent();
			string[] settingsFile = System.IO.File.ReadAllLines(CurrentDirectory + "\\Settings.txt");
		}

		private void SettingsWindowClosing(object sender, CancelEventArgs e)
		{
			HostWindow.IsEnabled = true;
			//Save Settings
		}

		private void SelectButtonClicked(object sender, RoutedEventArgs e)
		{
			CommonOpenFileDialog dialog = new CommonOpenFileDialog
			{
				InitialDirectory = GetFolderPath(SpecialFolder.Desktop),
				IsFolderPicker = true
			};

			if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
			{
				DefaultOutputDirectory = dialog.FileName;
				DefaultOutputDirectoryBox.Text = DefaultOutputDirectory;
			}
		}

		private void SaveSettings()
		{

		}
	}
}
