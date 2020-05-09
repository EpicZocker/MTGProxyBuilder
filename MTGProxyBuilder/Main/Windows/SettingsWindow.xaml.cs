using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.WindowsAPICodePack.Dialogs;
using MTGProxyBuilder.Properties;
using static System.Environment;

namespace MTGProxyBuilder.Main.Windows
{
	public partial class SettingsWindow : Window
	{
		private string ProxySizePercentage { get; set; }

		public SettingsWindow()
		{
			InitializeComponent();
		}

		private void SettingsWindowClosing(object sender, CancelEventArgs e)
		{
			Owner.IsEnabled = true;
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
			{
				DefaultOutputDirectoryBox.Text = dialog.FileName;
				Focus();
			}
		}

		private void DeleteButtonClicked(object sender, RoutedEventArgs e)
		{
			DefaultOutputDirectoryBox.Text = "";
		}

		private void NumberInputCheck(object sender, TextCompositionEventArgs e)
		{
			Regex regex = new Regex("[^0-9]+");
			e.Handled = regex.IsMatch(e.Text);
		}

		private void CheckDirectory(object sender, RoutedEventArgs e)
		{
			TextBox tb = e.Source as TextBox;
			if (!Directory.Exists(tb.Text))
				tb.Text = "";
		}

		private void InputCheckRange(object sender, RoutedEventArgs e)
		{
			TextBox tb = e.Source as TextBox;
			int num = int.Parse(tb.Text);
			if (num < PctSlider.Minimum)
				num = (int) PctSlider.Minimum;
			if (num > PctSlider.Maximum)
				num = (int) PctSlider.Maximum;
			tb.Text = num.ToString();
		}

		private void FilenameInputCheck(object sender, RoutedEventArgs e)
		{
			TextBox tb = e.Source as TextBox;
			if (string.IsNullOrEmpty(tb.Text))
				tb.Text = "Proxies.pdf";
		}

		private void PreventPaste(object sender, ExecutedRoutedEventArgs e)
		{
			if (e.Command == ApplicationCommands.Copy || e.Command == ApplicationCommands.Cut || e.Command == ApplicationCommands.Paste)
				e.Handled = true;
		}
	}
}
