using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.WindowsAPICodePack.Dialogs;
using MTGProxyBuilder.Main.Classes;
using MTGProxyBuilder.Properties;
using Newtonsoft.Json.Linq;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using static System.Environment;

namespace MTGProxyBuilder.Main.Windows
{
	public partial class MainWindow : Window
	{
		public List<CustomCardAmount> CustomCards;

		private List<CardAmount> CardsWithAmounts;
		private Settings UserSettings = Properties.Settings.Default;
		private InfoWindow Info = new InfoWindow();
		private string PDFOutputPath;

		public MainWindow()
		{
			InitializeComponent();
		}

		private List<CardAmount> InterpretCardlist()
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
				cardAmounts.Add(new CardAmount(){ Amount = amount, CardName = s.TrimStart(strAmount.ToCharArray()).Trim() });
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
					IsFolderPicker = true,
					EnsureFileExists = true
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
				Info.TextBlock.Text = "Fetching images...";
				Info.Show();
				Info.Owner = this;
				IsEnabled = false;
			});

			if (CardsWithAmounts != null)
			{
				foreach (CardAmount card in CardsWithAmounts)
				{
					Dispatcher.Invoke(() => Info.TextBlock.Text = "Fetching images, Progress: " + images.Count + "/" + CardsWithAmounts.Count);
					
					byte[] img = webClient.DownloadData(card.ArtworkURLs[card.EditionNames.IndexOf(card.SelectedEdition)]);
					byte[] backFace = !string.IsNullOrEmpty(card.BackFaceURL) ? webClient.DownloadData(card.BackFaceURL) : null;

					for (int j = 0; j < card.Amount; j++)
					{
						images.Add(img);
						if (backFace != null)
							images.Add(backFace);
					}
				}
			}

			if (CustomCards != null)
				foreach (CustomCardAmount cca in CustomCards)
					for(int i = 0; i < cca.Amount; i++)
						images.Add(cca.CardImage);

			Dispatcher.Invoke(() => Info.TextBlock.Text = "Building PDF..." );

			PdfDocument pdf = new PdfDocument();
			PdfPage page = new PdfPage(pdf);
			XGraphics draw = XGraphics.FromPdfPage(page);

			float cardWidth = 178 * (UserSettings.ProxySizePercentage / 100f);
			float cardHeight = 249 * (UserSettings.ProxySizePercentage / 100f);
			int widthGap = UserSettings.GapX;
			int heightGap = UserSettings.GapY;

			for (int i = 0; i < Math.Ceiling(images.Count / 9f); i++)
			{
				float x = UserSettings.OffsetLeft;
				float y = UserSettings.OffsetTop;

				int remainingImages = images.Count - i * 9;
				if (remainingImages > 9)
					remainingImages = 9;

				for (int j = 1; j < remainingImages + 1; j++)
				{
					Dispatcher.Invoke(() => Info.TextBlock.Text = "Drawing images, Progress: " + (j + i * 9) + "/" + images.Count);

					MemoryStream mem = new MemoryStream(images.Skip(i * 9).ToList()[j - 1]);
					draw.DrawImage(XImage.FromStream(mem), x, y, cardWidth, cardHeight);
					draw.Save();
					if (j % 3 != 0)
						x += cardWidth + widthGap;
					else
					{
						x = UserSettings.OffsetLeft;
						y += cardHeight + heightGap;
					}
				}

				x = UserSettings.OffsetLeft;
				y = UserSettings.OffsetTop;

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
			CustomCardsWindow ccw = new CustomCardsWindow();
			ccw.Owner = this;
			ccw.Show();
			IsEnabled = false;
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
			JObject pulledCards = JObject.Parse(resp.Content.ReadAsStringAsync().Result);
			List<CardAmount> allCards = InterpretCardlist();
			int progress = 0;

			foreach (CardAmount currentCard in allCards)
			{
				progress++;
				Dispatcher.Invoke(() => Info.TextBlock.Text = "Parsing decklist, Progress: " + progress + "/" + allCards.Count);
				foreach (JToken jt in pulledCards["data"].Children())
				{
					List<string> cardNames = jt["name"].Value<string>().Split(new[] { " // " }, StringSplitOptions.RemoveEmptyEntries).ToList();
					if (cardNames.Contains(currentCard.CardName, StringComparer.OrdinalIgnoreCase))
					{
						HttpResponseMessage allPrintsResp = await APIInterface.Get("/cards/search",
							jt["prints_search_uri"].Value<string>().Split('?')[1]);
						JToken allPrints = JToken.Parse(allPrintsResp.Content.ReadAsStringAsync().Result);
						List<string> editionNames = new List<string>();
						List<string> artworkURLs = new List<string>();
						foreach (JToken singlePrint in allPrints["data"])
						{
							List<string> specialEffects = new List<string>();
							string specialEffectsStr = "";

							if (singlePrint["full_art"].Value<bool>())
								specialEffects.Add("Fullart");

							if (singlePrint["textless"].Value<bool>())
								specialEffects.Add("Textless");

							if (singlePrint["frame"].Value<string>() == "future")
								specialEffects.Add("Future Sight frame");
										
							if(singlePrint["border_color"].Value<string>() == "borderless")
								specialEffects.Add("Borderless");

							if (singlePrint["frame_effects"] != null)
							{
								Dictionary<string, string> frameEffects = new Dictionary<string, string>()
								{
									{"extendedart", "Extended art"},
									{"showcase", "Showcase"},
									{"inverted", "FNM promo"},
									{"colorshifted", "Colorshifted"}
								};
								List<string> containedFrameEffects = singlePrint["frame_effects"].Children().Values<string>().ToList();
								foreach (string s in containedFrameEffects)
									if(frameEffects.ContainsKey(s))
										specialEffects.Add(frameEffects[s]);
							}

							if (singlePrint["promo_types"] != null)
							{
								Dictionary<string, string> promoTypes = new Dictionary<string, string>()
								{
									{"prerelease", "Prerelease"},
									{"datestamped", "Datestamped"},
									{"promostamped", "Promostamped"},
									{"judgegift", "Judge promo"},
									{"buyabox", "Buy-a-Box promo"},
									{"gameday", "Gameday promo"}
								};
								List<string> containedPromoTypes = singlePrint["promo_types"].Children().Values<string>().ToList();
								foreach (string s in containedPromoTypes)
									if(promoTypes.ContainsKey(s))
										specialEffects.Add(promoTypes[s]);
							}
							
							bool differentName = false;
							if (singlePrint["printed_name"] != null)
								differentName = singlePrint["name"].Value<string>() != singlePrint["printed_name"].Value<string>() &&
								                singlePrint["lang"].Value<string>() == "en";
							string setName = singlePrint["set_name"].Value<string>();
							string setCode = differentName ? singlePrint["printed_name"].Value<string>() : singlePrint["set"].Value<string>().ToUpper();
							string imageUri = singlePrint["image_uris"] != null ? singlePrint["image_uris"]["png"].Value<string>() :
								singlePrint["card_faces"][0]["image_uris"]["png"].Value<string>();

							if (specialEffects.Count > 0 && !differentName)
								specialEffectsStr = specialEffects.Aggregate("", (s, listStr) => s + listStr + ", ")
								                                  .TrimEnd(',', ' ').Insert(0, " [") + "]";

							editionNames.Add(setName + " (" + setCode + ")" + specialEffectsStr);
							artworkURLs.Add(imageUri);

							if (singlePrint["layout"].Value<string>() == "transform")
								currentCard.BackFaceURL = singlePrint["card_faces"][1]["image_uris"]["png"].Value<string>();
						}
						editionNames.Reverse();
						artworkURLs.Reverse();
						currentCard.SelectedEdition = editionNames[0];
						currentCard.EditionNames = editionNames;
						currentCard.ArtworkURLs = artworkURLs;
						currentCard.DisplayName = cardNames.Count > 1 ? cardNames[0] + " // " + cardNames[1] : cardNames[0];
					}
				}
			}
			
			CardsWithAmounts = allCards;

			Dispatcher.Invoke(() =>
			{
				CardGrid.ItemsSource = CardsWithAmounts;
				CardGrid.Items.Refresh();
				CreatePDFButton.IsEnabled = CardGrid.Items.Count != 0;
				Info.Hide();
				IsEnabled = true;
			});
			string notFoundList = pulledCards["not_found"].Children().Aggregate("", (s, token) => s + "\"" + token["name"] + "\", ").TrimEnd(',', ' ');
			if (!string.IsNullOrEmpty(notFoundList))
				MessageBox.Show("Cards not found: " + notFoundList, "Cards not found");
		}

		private async Task<HttpResponseMessage> PullAllDecklistData()
		{
			List<CardAmount> cards = InterpretCardlist().GroupBy(e => e.CardName).Select(e => e.First()).ToList();
			if (cards.Count > 75)
			{
				//fixed crash when trying to parse more than 75 cards
			}
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
