using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.WindowsAPICodePack.Dialogs;
using MTGProxyBuilder.Main.Classes;
using Image = System.Drawing.Image;
using System.Diagnostics;

namespace MTGProxyBuilder.Main.Windows
{
	public partial class CustomCardsWindow : Window
	{
		private List<CustomCard> CustomCards = new List<CustomCard>();
		private HoverImage HoverImg = new HoverImage();
		
		public CustomCardsWindow()
		{
			InitializeComponent();
		}

		private void CustomizeCardsClosing(object sender, CancelEventArgs e)
		{
			MainWindow mw = Owner as MainWindow;
			if (CustomCards != null)
			{
				mw.CustomCards = CustomCards;
				mw.CreatePDFButton.IsEnabled = CustomCards.Count != 0;
			}
			mw.Focus();
		}

		private void SelectFileClicked(object sender, RoutedEventArgs e)
		{
			HoverImg.Owner = this;
			CommonOpenFileDialog dialog = new CommonOpenFileDialog
			{
				DefaultDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
				EnsureFileExists = true,
				Multiselect = true,
			};
			dialog.Filters.Add(new CommonFileDialogFilter("Images", "png, jpg, jpeg"));
			if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
				LoadImages(dialog.FileNames.ToArray());
		}
		
		public void LoadImages(string[] paths)
		{
			foreach (string s in paths)
				LoadImageFromPath(s);
			if (CustomCards != null)
			{
				CardGrid.ItemsSource = CustomCards;
				CardGrid.Items.Refresh();
			}
		}

		private void LoadImageFromPath(string path)
		{
			Image img = Image.FromFile(path);
			using (MemoryStream ms = new MemoryStream())
			{
				img.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
				ms.Position = 0;

				BitmapImage bi = new BitmapImage();
				bi.BeginInit();
				bi.CacheOption = BitmapCacheOption.OnLoad;
				bi.StreamSource = ms;
				bi.EndInit();
				if (CustomCards.Find(e => e.CardName == Path.GetFileName(path)) != null)
					CustomCards.Find(e => e.CardName == Path.GetFileName(path)).Amount++;
				else
					CustomCards.Add(new CustomCard() { Amount = 1, CardName = Path.GetFileName(path),
						CardImage = ms.ToArray(), Directory = Path.GetDirectoryName(path) });
				img.Dispose();
			}
		}

		private void NumberInputCheck(object sender, TextCompositionEventArgs e)
		{
			Regex regex = new Regex("[^0-9]+");
			e.Handled = regex.IsMatch(e.Text);
		}

		private void PreventPaste(object sender, ExecutedRoutedEventArgs e)
		{
			if (e.Command == ApplicationCommands.Copy || e.Command == ApplicationCommands.Cut || e.Command == ApplicationCommands.Paste)
				e.Handled = true;
		}

		private void CardGridLeftClick(object sender, MouseEventArgs e)
		{
			if (Scale.Value == 0)
				return;
			HitTestResult hitTestResult = VisualTreeHelper.HitTest(CardGrid, e.GetPosition(CardGrid));
			DataGridRow dataGridRow = hitTestResult.VisualHit.GetParentOfType<DataGridRow>();
			if(dataGridRow != null)
			{
				int index = dataGridRow.GetIndex();
				HoverImg.CardImage.Source = LoadImage(CustomCards[index].CardImage);
				HoverImg.Left = System.Windows.Forms.Cursor.Position.X;
				HoverImg.Top = System.Windows.Forms.Cursor.Position.Y;
				HoverImg.Visibility = Visibility.Visible;
			}
			else HoverImg.Visibility = Visibility.Hidden;
		}

		private BitmapImage LoadImage(byte[] imageData)
		{
			if (imageData == null || imageData.Length == 0)
				return null;
			BitmapImage image = new BitmapImage();
			using (MemoryStream mem = new MemoryStream(imageData))
			{
				mem.Position = 0;
				image.BeginInit();
				image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
				image.CacheOption = BitmapCacheOption.OnLoad;
				image.UriSource = null;
				image.StreamSource = mem;
				image.EndInit();
			}
			image.Freeze();
			return image;
		}

		private void ScaleChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
		{
			HoverImg.Width = 149 * e.NewValue;
			HoverImg.Height = 208 * e.NewValue;
		}

		private void OpenInExplorerClicked(object sender, RoutedEventArgs e)
		{
			Process.Start(CustomCards[CardGrid.SelectedIndex].Directory);
		}

		private void EditAmountClicked(object sender, RoutedEventArgs e)
		{
			EditAmountWindow eaw = new EditAmountWindow();
			eaw.ShowDialog();
			CustomCards[CardGrid.SelectedIndex].Amount = eaw.NewAmount;
			CardGrid.Items.Refresh();
		}

		private void DeleteCardClicked(object sender, RoutedEventArgs e)
		{
			CustomCards.RemoveAt(CardGrid.SelectedIndex);
			CardGrid.Items.Refresh();
		}

		private void ClearAllClicked(object sender, RoutedEventArgs e)
		{
			if (MessageBox.Show("Clear all custom cards?", "Clear all", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
			{
				CustomCards.Clear();
				CardGrid.Items.Refresh();
			}
		}
	}
}
