using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.WindowsAPICodePack.Dialogs;
using MTGProxyBuilder.Main.Classes;
using Image = System.Drawing.Image;

namespace MTGProxyBuilder.Main.Windows
{
	public partial class CustomCardsWindow : Window
	{
		private List<CustomCardAmount> CustomCards;
		private byte[] CurrentImage;
		
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
				mw.CreatePDFButton.IsEnabled = true;
			}
			mw.IsEnabled = true;
	        mw.Focus();
        }

        private void SelectFileClicked(object sender, RoutedEventArgs e)
        {
	        CommonOpenFileDialog dialog = new CommonOpenFileDialog();
	        dialog.DefaultDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
	        dialog.EnsureFileExists = true;
	        dialog.Filters.Add(new CommonFileDialogFilter("Images", "png, jpg, jpeg, gif"));
	        if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
	        {
		        ImagePathBox.Text = dialog.FileName;
				LoadImageFromPath(dialog.FileName);
			}
        }

        private void AddCardToListButton(object sender, RoutedEventArgs e)
        {
	        if (CurrentImage != null)
	        {
		        if (CustomCards == null)
		        {
			        CustomCards = new List<CustomCardAmount>();
			        CardGrid.ItemsSource = CustomCards;
		        }

		        CustomCards.Add(new CustomCardAmount()
		        {
				    Amount = int.Parse(AmountText.Text),
				    CardName = CardnameBox.Text,
				    CardImage = CurrentImage
		        });

		        MessageBoxResult mb = MessageBox.Show("Card added!", "Card added");
		        IsEnabled = false;
		        if (mb == MessageBoxResult.OK)
		        {
			        IsEnabled = true;
			        AmountText.Text = "1";
			        CardnameBox.Text = "";
			        ImagePathBox.Text = "";
			        CardImage.Source = null;
			        CurrentImage = null;
		        }

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
		        CurrentImage = ms.ToArray();

				CardImage.Source = bi;
		        img.Dispose();
	        }
		}

        private void CheckFile(object sender, RoutedEventArgs e)
        {
	        TextBox tb = e.Source as TextBox;
	        if (!File.Exists(tb.Text))
	        {
		        tb.Text = "";
		        CardImage.Source = null;
		        CurrentImage = null;
	        }
	        else LoadImageFromPath(tb.Text);
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
	}
}
