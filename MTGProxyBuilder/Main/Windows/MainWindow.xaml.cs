using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.WindowsAPICodePack.Dialogs;
using MTGProxyBuilder.Main;
using MTGProxyBuilder.Main.Classes;
using MTGProxyBuilder.Properties;
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
		private InfoWindow Info = new InfoWindow();

		public MainWindow()
		{
			InitializeComponent();
		}

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

		private void CreatePDFButtonClicked(object sender, RoutedEventArgs eventArgs)
		{
			IsEnabled = false;
			bool defaultDirectory = string.IsNullOrEmpty(UserSettings.DefaultOutputDirectory);
			string fileName = UserSettings.DefaultFilename.EndsWith(".pdf") ? UserSettings.DefaultFilename : UserSettings.DefaultFilename + ".pdf";
			CommonOpenFileDialog dialog = null;

			Task backgroundWorker = new Task(() =>
			{
				List<byte[]> images = new List<byte[]>();
				WebClient webClient = new WebClient();

				Info.Show();
				Info.Owner = this;
				IsEnabled = false;

				foreach (CardAmount card in CardsWithAmounts)
				{
					Info.TextBlock.Text = "Fetching Images, Progress: " + images.Count + "/" + CardsWithAmounts.Count;

					string selEdition = card.SelectedEdition.TrimStart('[').Split(',')[0];
					byte[] img = webClient.DownloadData(card.EditionNamesArtworkURLs.Find(e => e.Key == selEdition).Value);
					byte[] backFace = !string.IsNullOrEmpty(card.BackFaceURL) ? webClient.DownloadData(card.BackFaceURL) : null;

					for (int j = 0; j < card.Amount; j++)
					{
						images.Add(img);
						if (backFace != null)
							images.Add(backFace);
					}

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
				Info.Close();
				IsEnabled = true;
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
				else
					Focus();
			}
			else
				backgroundWorker.RunSynchronously();

			IsEnabled = true;
		}

		private void OpenSettingsWindow(object sender, RoutedEventArgs e)
		{
			SettingsWindow sw = new SettingsWindow();
			sw.Owner = this;
			sw.Show();
			IsEnabled = false;
		}

		private void AddCustomImagesButtonClicked(object sender, RoutedEventArgs e)
		{
			MessageBox.Show("This function is not implemented.", "Not implemented");
		}

		private void CustomizeCardsClicked(object sender, RoutedEventArgs eventArgs)
		{
			Task t = new Task(async () =>
			{
				Info.Show();
				Info.Owner = this;
				Info.TextBlock.Text = "Parsing decklist...";
				IsEnabled = false;

				HttpResponseMessage resp = await PullAllDecklistData();
				JObject jo = JObject.Parse(resp.Content.ReadAsStringAsync().Result);
				JToken data = jo["data"];
				List<CardAmount> allCards = InterpretCardlist();

				//foreach (CardAmount currentCard in allCards)
				//{

				//}

				foreach (JToken jt in data.Children()) //TODO Rewrite Loop
				{
					HttpResponseMessage prints_search_uri_resp = await APIInterface.Get("/cards/search",
						jt["prints_search_uri"].Value<string>().Split('?')[1]);
					JToken prints_search_uri = JToken.Parse(prints_search_uri_resp.Content.ReadAsStringAsync().Result);
					List<KeyValuePair<string, string>> editionNames = new List<KeyValuePair<string, string>>();
					List<string> cardNames = jt["name"].Value<string>().Split(new []{" // "}, StringSplitOptions.RemoveEmptyEntries).ToList();
					CardAmount currentCard = allCards.Find(card => cardNames.Contains(card.CardName, StringComparer.OrdinalIgnoreCase));
					foreach (JToken psu in prints_search_uri["data"])
					{
						string image_uri = psu["image_uris"] != null ? psu["image_uris"]["png"].Value<string>() :
								           psu["card_faces"][0]["image_uris"]["png"].Value<string>();
						editionNames.Add(new KeyValuePair<string, string>(psu["set_name"].Value<string>() + 
										" (" + psu["set"].Value<string>().ToUpper() + ")", image_uri));
						if (psu["card_faces"] != null && psu["layout"].Value<string>() == "transform")
							currentCard.BackFaceURL = psu["card_faces"][1]["image_uris"]["png"].Value<string>();
					}
					currentCard.SelectedEdition = editionNames[0].Key;
					currentCard.EditionNamesArtworkURLs = editionNames;
					currentCard.DisplayName = cardNames.Count > 1 ? cardNames[0] + " // " + cardNames[1] : cardNames[0];
				}
				allCards.RemoveAll(e => e.EditionNamesArtworkURLs == null);
				CardsWithAmounts = allCards;
				CardGrid.ItemsSource = CardsWithAmounts;
				CardGrid.Items.Refresh();
				CreatePDFButton.IsEnabled = CardGrid.Items.Count != 0;
				Info.Hide();
				IsEnabled = true;

				string notFoundList = jo["not_found"].Children().Aggregate("", (s, token) => s + "\"" + token["name"] + "\", ").TrimEnd(',', ' ');
				if (!string.IsNullOrEmpty(notFoundList))
					MessageBox.Show("Cards not found: " + notFoundList, "Cards not found");
			});

			if (CardGrid.Items.Count != 0)
			{
				if (MessageBox.Show("There seems to be data in the grid. Do you want to override it?",
				                    "Override data?", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
					t.RunSynchronously();
			}
			else
				t.RunSynchronously();
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

		private void DisableTab(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Tab)
				e.Handled = true;
		}

		private void ApplicationClosing(object sender, CancelEventArgs e)
		{
			Exit(0);
		}
	}
}
