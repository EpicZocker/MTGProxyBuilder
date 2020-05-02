﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
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
		private string PDFOutputPath;

		public MainWindow()
		{
			InitializeComponent();
		}

		public List<CardAmount> InterpretCardlist()
		{
			string[] cards = null;
			Dispatcher.Invoke(() => { cards = Decklist.Text.Split(new[] { NewLine }, StringSplitOptions.RemoveEmptyEntries); });
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

		private async void CreatePDFButtonClicked(object sender, RoutedEventArgs eventArgs)
		{
			string fileName = UserSettings.DefaultFilename.EndsWith(".pdf") ? UserSettings.DefaultFilename : UserSettings.DefaultFilename + ".pdf";
			CommonOpenFileDialog dialog = null;
			if (string.IsNullOrEmpty(UserSettings.DefaultOutputDirectory))
			{
				dialog = new CommonOpenFileDialog
				{
					InitialDirectory = GetFolderPath(SpecialFolder.Desktop),
					IsFolderPicker = true
				};
				if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
				{
					PDFOutputPath = dialog.FileName + Path.DirectorySeparatorChar + fileName;
					await Task.Run(() => PrintPDF());
				}
				else Focus();
			}
			else
			{
				PDFOutputPath = UserSettings.DefaultOutputDirectory + Path.DirectorySeparatorChar + fileName;
				await Task.Run(() => PrintPDF());
			}
		}

		private void PrintPDF()
		{
			List<byte[]> images = new List<byte[]>();
			WebClient webClient = new WebClient();

			Dispatcher.Invoke(() => 
			{
				Info.Show();
				Info.Owner = this;
				IsEnabled = false;
			});

			foreach (CardAmount card in CardsWithAmounts)
			{
				Dispatcher.Invoke(() => { Info.TextBlock.Text = "Fetching Images, Progress: " + images.Count + "/" + CardsWithAmounts.Count; });

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

			Dispatcher.Invoke(() => { Info.TextBlock.Text = "Building PDF..."; });

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

			pdf.Save(PDFOutputPath);
			pdf.Close();

			Dispatcher.Invoke(() =>
			{
				Info.Hide();
				IsEnabled = true;
			});
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

		private async void CustomizeCardsClicked(object sender, RoutedEventArgs eventArgs)
		{
			if (CardGrid.Items.Count != 0)
			{
				if (MessageBox.Show("There seems to be data in the grid. Do you want to override it?",
				                    "Override data?", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
					await Task.Run(() => ParseDecklist());
			}
			else await Task.Run(() => ParseDecklist());
		}

		private async void ParseDecklist()
		{
			Dispatcher.Invoke(() => 
			{ 
				Info.Owner = this;
				Info.TextBlock.Text = "Parsing decklist...";
				IsEnabled = false;
				Info.Show();
			});

			HttpResponseMessage resp = await PullAllDecklistData();
			JObject jo = JObject.Parse(resp.Content.ReadAsStringAsync().Result);
			JToken data = jo["data"];
			List<CardAmount> allCards = InterpretCardlist();

			//["promo_types"] prerelease promostamped showcase nyxtouched judgegift ["full_art"] ["textless"]
			//https://scryfall.com/docs/api/layouts - Frame Effects
			//https://scryfall.com/docs/api/cards
			foreach (CardAmount currentCard in allCards)
			{
				foreach (JToken jt in data.Children())
				{
					List<string> cardNames = jt["name"].Value<string>().Split(new[] { " // " }, StringSplitOptions.RemoveEmptyEntries).ToList();
					if (cardNames.Contains(currentCard.CardName, StringComparer.OrdinalIgnoreCase))
					{
						HttpResponseMessage allPrintsResp = await APIInterface.Get("/cards/search",
							jt["prints_search_uri"].Value<string>().Split('?')[1]);
						JToken allPrints = JToken.Parse(allPrintsResp.Content.ReadAsStringAsync().Result);
						List<KeyValuePair<string, string>> editionNames = new List<KeyValuePair<string, string>>();
						foreach (JToken singlePrint in allPrints["data"])
						{
							string image_uri = singlePrint["image_uris"] != null ? singlePrint["image_uris"]["png"].Value<string>() :
													   singlePrint["card_faces"][0]["image_uris"]["png"].Value<string>();
							editionNames.Add(new KeyValuePair<string, string>(singlePrint["set_name"].Value<string>() +
																			  " (" + singlePrint["set"].Value<string>().ToUpper() + ")", image_uri));
							if (singlePrint["layout"].Value<string>() == "transform") //impl meld layout
								currentCard.BackFaceURL = singlePrint["card_faces"][1]["image_uris"]["png"].Value<string>();
						}
						currentCard.SelectedEdition = editionNames[0].Key;
						currentCard.EditionNamesArtworkURLs = editionNames;
						currentCard.DisplayName = cardNames.Count > 1 ? cardNames[0] + " // " + cardNames[1] : cardNames[0];
						break;
					}
				}
			}

			allCards.RemoveAll(e => e.EditionNamesArtworkURLs == null);
			CardsWithAmounts = allCards;

			Dispatcher.Invoke(() =>
			{
				CardGrid.ItemsSource = CardsWithAmounts;
				CardGrid.Items.Refresh();
				CreatePDFButton.IsEnabled = CardGrid.Items.Count != 0;
				Info.Hide();
				IsEnabled = true;
			});

			string notFoundList = jo["not_found"].Children().Aggregate("", (s, token) => s + "\"" + token["name"] + "\", ").TrimEnd(',', ' ');
			if (!string.IsNullOrEmpty(notFoundList))
				MessageBox.Show("Cards not found: " + notFoundList, "Cards not found");
		}

		private async Task<HttpResponseMessage> PullAllDecklistData()
		{
			List<CardAmount> cards = InterpretCardlist().GroupBy(e => e.CardName).Select(e => e.First()).ToList();
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
