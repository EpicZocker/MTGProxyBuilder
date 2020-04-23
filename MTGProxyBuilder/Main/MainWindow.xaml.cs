using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using BinanceTradeBot_Backend.Communication;
using Microsoft.WindowsAPICodePack.Dialogs;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using static System.Environment;

namespace MTGProxyBuilder
{
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
		}

		public List<KeyValuePair<int, string>> InterpretCardlist()
		{
			string[] cards = Decklist.Text.Split(new []{ NewLine }, StringSplitOptions.RemoveEmptyEntries);
			cards.ToList().ForEach(e => e.Trim());

			List<KeyValuePair<int, string>> cardAmounts = new List<KeyValuePair<int, string>>();
			foreach (string s in cards)
			{
				string strAmount = s.Split(' ')[0];
				int amount = 1;
				try
				{
					amount = int.Parse(strAmount);
				}
				catch (Exception)
				{
					strAmount = "";
				}
				cardAmounts.Add(new KeyValuePair<int, string>(amount, s.TrimStart(strAmount.ToCharArray())));
			}
			return cardAmounts;
		}

		private void CreatePDFButtonClicked(object sender, RoutedEventArgs e)
		{
			CommonOpenFileDialog dialog = new CommonOpenFileDialog
			{
				InitialDirectory = GetFolderPath(SpecialFolder.Desktop),
				IsFolderPicker = true
			};

			if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
			{
				ProgressWindow pw = new ProgressWindow();

				Task backgroundWorker = new Task(async () =>
				{
					List<KeyValuePair<int, string>> cardAmounts = InterpretCardlist();
					List<byte[]> images = new List<byte[]>();

					pw.ProgressBar.Maximum = cardAmounts.Count;

					Decklist.IsEnabled = false;
					CreatePDFButton.IsEnabled = false;

					for (int i = 0; i < cardAmounts.Count; i++)
					{
						pw.TextBlock.Text = "Fetching Images, Progress: " + images.Count + "/" + cardAmounts.Count;
						pw.ProgressBar.Value = images.Count;

						byte[] img = await GetImage(cardAmounts[i].Value);

						if (FlipCards.IsChecked == true)
						{
							byte[] backImg = await GetFlipImage(cardAmounts[i].Value);
							if (backImg != null)
								images.Add(backImg);
						}

						if (img == null)
						{
							Console.WriteLine(cardAmounts[i].Value + " is not a valid card");
							break;
						}
						for (int j = 0; j < cardAmounts[i].Key; j++)
							images.Add(img);
						Thread.Sleep(100);
					}

					PdfDocument pdf = new PdfDocument("Proxies.pdf");
					PdfPage page = new PdfPage(pdf);
					XGraphics draw = XGraphics.FromPdfPage(page);

					int cardWidth = 178;
					int cardHeight = 249;
					int widthGap = 1;
					int heightGap = 1;

					for (int i = 0; i < Math.Ceiling(images.Count / 9f); i++)
					{
						int x = 0;
						int y = 0;

						int remainingImages = images.Count - i * 9;
						if (remainingImages > 9)
							remainingImages = 9;

						for (int j = 1; j < remainingImages + 1; j++)
						{
							MemoryStream mem = new MemoryStream(images.Skip(i * 9).ToList()[j - 1]);
							draw.DrawImage(XImage.FromStream(mem), x, y, cardWidth, cardHeight);
							draw.Save();
							if (j % 3 != 0)
								x += cardWidth + widthGap;
							else
							{
								x = 0;
								y += cardHeight + heightGap;
							}
						}

						x = 0;
						y = 0;

						pdf.AddPage(page);
						page = new PdfPage(pdf);
						draw = XGraphics.FromPdfPage(page);
					}
					pdf.Save(dialog.FileName + "\\Proxies.pdf");
					pdf.Close();
					pw.Close();

					Decklist.IsEnabled = true;
					CreatePDFButton.IsEnabled = true;
				});

				pw.Show();
				backgroundWorker.RunSynchronously();
			}
		}

		private async Task<byte[]> GetImage(string cardname)
		{
			HttpResponseMessage resp = await APIInterface.Get("/cards/named",
				"exact=" + cardname, "format=image", "version=png");
			if(resp.IsSuccessStatusCode)
				return await resp.Content.ReadAsByteArrayAsync();
			return null;
		}

		private async Task<byte[]> GetFlipImage(string cardname)
		{
			if (FlipCards.IsChecked == true)
			{
				HttpResponseMessage flipResp = await APIInterface.Get("/cards/named",
					"exact=" + cardname, "format=image", "version=png", "face=back");
				if (flipResp.IsSuccessStatusCode)
					return await flipResp.Content.ReadAsByteArrayAsync();
			}
			return null;
		}
	}
}
