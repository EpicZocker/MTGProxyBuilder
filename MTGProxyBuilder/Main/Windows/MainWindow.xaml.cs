using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.WindowsAPICodePack.Dialogs;
using MTGProxyBuilder.Main;
using MTGProxyBuilder.Main.Classes;
using MTGProxyBuilder.Properties;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using static System.Environment;

namespace MTGProxyBuilder
{
	public partial class MainWindow : Window
	{
		private List<CardAmount> CardsWithAmounts;
		private Settings UserSettings = Properties.Settings.Default;

		public MainWindow() => InitializeComponent();

		public List<CardAmount> InterpretCardlist()
		{
			string[] cards = Decklist.Text.Split(new []{ NewLine }, StringSplitOptions.RemoveEmptyEntries);
			cards.ToList().ForEach(e => e.Trim());

			List<CardAmount> cardAmounts = new List<CardAmount>();
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
				cardAmounts.Add(new CardAmount(){ Amount = amount, CardName = s.TrimStart(strAmount.ToCharArray()) });
			}
			return cardAmounts;
		}

		private void CreatePDFButtonClicked(object sender, RoutedEventArgs e)
		{
			IsEnabled = false;
			bool defaultDirectory = string.IsNullOrEmpty(UserSettings.DefaultOutputDirectory);
			string fileName = UserSettings.DefaultFilename.EndsWith(".pdf") ? UserSettings.DefaultFilename : UserSettings.DefaultFilename + ".pdf";
			CommonOpenFileDialog dialog = null;

			Task backgroundWorker = new Task(async () =>
			{
				List<CardAmount> cardAmounts = CardsWithAmounts ?? InterpretCardlist();
				List<byte[]> images = new List<byte[]>();

				if (cardAmounts.Count == 0)
					return;

				InfoWindow iw = new InfoWindow();
				iw.Show();
				iw.ProgressBar.Maximum = cardAmounts.Count;

				for (int i = 0; i < cardAmounts.Count; i++)
				{
					iw.TextBlock.Text = "Fetching Images, Progress: " + images.Count + "/" + cardAmounts.Count;
					iw.ProgressBar.Value = images.Count;

					byte[] img = await GetImage(cardAmounts[i].CardName);

					if (cardAmounts[i].HasBackFace)
					{
						byte[] backImg = await GetFlipImage(cardAmounts[i].CardName);
						if (backImg != null)
							images.Add(backImg);
					}

					if (img == null)
					{
						Console.WriteLine(cardAmounts[i].CardName + " is not a valid card");
						break;
					}

					for (int j = 0; j < cardAmounts[i].Amount; j++)
						images.Add(img);

					Thread.Sleep(100);
				}

				PdfDocument pdf = new PdfDocument();
				PdfPage page = new PdfPage(pdf);
				XGraphics draw = XGraphics.FromPdfPage(page);

				float cardWidth = 178 * (UserSettings.ProxySizePercentage / 100f);
				float cardHeight = 249 * (UserSettings.ProxySizePercentage / 100f);
				int widthGap = UserSettings.GapX;
				int heightGap = UserSettings.GapY;

				for (int i = 0; i < Math.Ceiling(images.Count / 9f); i++)
				{
					float x = 0;
					float y = 0;

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

				if(defaultDirectory)
					pdf.Save(dialog.FileName + Path.DirectorySeparatorChar + fileName);
				else
					pdf.Save(UserSettings.DefaultOutputDirectory + Path.DirectorySeparatorChar + fileName);
				
				pdf.Close();
				iw.Close();
			});

			if (defaultDirectory)
			{
				dialog = new CommonOpenFileDialog
				{
					InitialDirectory = GetFolderPath(SpecialFolder.Desktop),
					IsFolderPicker = true
				};
				if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
					backgroundWorker.RunSynchronously();
			}
			else
				backgroundWorker.RunSynchronously();

			IsEnabled = true;
		}

		private void AddCustomImagesButtonClicked(object sender, RoutedEventArgs e)
		{

		}

		private void ParseDecklistClicked(object sender, RoutedEventArgs e)
		{
			new Task(async () =>
			{
				HttpResponseMessage resp = await PullAllDecklistData();
				JObject jo = JObject.Parse(resp.Content.ReadAsStringAsync().Result);
				JToken not_found = jo["not_found"];
				JToken data = jo["data"];

				string notFoundList = "Cards not found: ";
				foreach (JToken jt in not_found.Children())
					notFoundList += jt["name"] + ",";
				notFoundList.TrimEnd(',');

				List<CardAmount> allCards = InterpretCardlist();
				foreach (JToken jt in data.Children())
				{
					HttpResponseMessage prints_search_uri_resp = await APIInterface.Get("/cards/search",
						jt["prints_search_uri"].Value<string>().Split('?')[1]);
					JToken prints_search_uri = JToken.Parse(prints_search_uri_resp.Content.ReadAsStringAsync().Result);
					List<string> editionNames = new List<string>();
					List<string> cardNames = jt["name"].Value<string>().Split(new []{" // "}, StringSplitOptions.RemoveEmptyEntries).ToList();
					foreach (JToken psu in prints_search_uri["data"])
					{
						editionNames.Add(psu["set_name"].Value<string>() + " (" + psu["set"].Value<string>().ToUpper() + ")");
						if (cardNames.Count > 1)
							allCards.Find(card => cardNames.Contains(card.CardName)).HasBackFace = true;
					}
					allCards.Find(card => cardNames.Contains(card.CardName)).EditionNames = editionNames;
				}
				CardsWithAmounts = allCards;
				CardGrid.ItemsSource = CardsWithAmounts;
			}).RunSynchronously();
		}

		private async Task<HttpResponseMessage> PullAllDecklistData()
		{
			List<CardAmount> cards = InterpretCardlist();
			JArray collection = new JArray();
			foreach (CardAmount ca in cards)
				collection.Add(JToken.Parse("{\"name\":\"" + ca.CardName + "\"}"));
			string body = "{\"identifiers\":" + collection.ToString() + "}";
			return await APIInterface.PostWithBody("/cards/collection", body);
		}

		private void OpenSettingsWindow(object sender, RoutedEventArgs e)
		{
			SettingsWindow sw = new SettingsWindow();
			sw.Owner = this;
			sw.Show();
			IsEnabled = false;
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
			if (FlipCards == true)
			{
				HttpResponseMessage flipResp = await APIInterface.Get("/cards/named",
					"exact=" + cardname, "format=image", "version=png", "face=back");
				if (flipResp.IsSuccessStatusCode)
					return await flipResp.Content.ReadAsByteArrayAsync();
			}
			return null;
		}

		private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Tab)
				e.Handled = true;
		}
	}
}
