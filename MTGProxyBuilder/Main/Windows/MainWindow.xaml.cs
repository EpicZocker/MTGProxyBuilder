using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
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
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;

namespace MTGProxyBuilder.Main.Windows
{
	public partial class MainWindow : Window
	{
		public List<CustomCard> CustomCards;

		private List<Card> Cards;
		private Settings UserSettings = Settings.Default;
		private InfoWindow Info = new InfoWindow();
		private string PDFOutputPath;

		private readonly string DecklistSavePath = AppDomain.CurrentDomain.BaseDirectory + Path.DirectorySeparatorChar + "MTGProxyBuilder-DecklistSave.txt";
		private string DecklistFileContent;

		public MainWindow()
		{
			InitializeComponent();
			DeleteOldFile();
			ImportSavedDecklist();
			AppDomain.CurrentDomain.UnhandledException += LogAllExceptions;
		}

		private List<Card> InterpretCardlist()
		{
			string[] cards = null;
			Dispatcher.Invoke(() => { cards = Decklist.Text.Split(NewLine); });
			cards.ToList().ForEach(e => e.Trim());

			List<Card> cardAmounts = new List<Card>();
			foreach (string s in cards)
			{
				string strAmount = s.Split(' ')[0];
				bool num = int.TryParse(strAmount, out int amount);
				strAmount = num ? strAmount : "";
				cardAmounts.Add(new Card(){ Amount = amount == 0 ? 1 : amount, CardName = s.TrimStart(strAmount.ToCharArray()).Trim() });
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
			List<ImageWithBorder> imagesWithBorder = new List<ImageWithBorder>();
			WebClient webClient = new WebClient();

			Dispatcher.Invoke(() =>
			{
				Info.TextBlock.Text = "Fetching images...";
				Info.Show();
				Info.Owner = this;
				IsEnabled = false;
			});

			if (Cards != null)
			{
				foreach (Card card in Cards)
				{
					Dispatcher.Invoke(() => Info.TextBlock.Text = "Fetching images, Progress: " + imagesWithBorder.Count + "/" + Cards.Count);

					Edition selectedEdition = card.Editions[card.SelectedEditionIndex];
					byte[] img = webClient.DownloadData(selectedEdition.ArtworkURL);
					byte[] backFace = !string.IsNullOrEmpty(selectedEdition.BackFaceURL) ?
							           webClient.DownloadData(selectedEdition.BackFaceURL) : null;

					for (int j = 0; j < card.Amount; j++)
					{
						imagesWithBorder.Add(new ImageWithBorder(img, selectedEdition.BorderColor));
						if (backFace != null)
							imagesWithBorder.Add(new ImageWithBorder(backFace, selectedEdition.BorderColor));
					}
				}
			}

			if (CustomCards != null)
				foreach (CustomCard cca in CustomCards)
					for(int i = 0; i < cca.Amount; i++)
						imagesWithBorder.Add(new ImageWithBorder(cca.CardImage, "Black"));

			Dispatcher.Invoke(() => Info.TextBlock.Text = "Building PDF..." );

			PdfDocument pdf = new PdfDocument();
			PdfPage page = new PdfPage(pdf);
			XGraphics draw = XGraphics.FromPdfPage(page);

			float cardWidth = 178 * (UserSettings.ProxySizePercentage / 100f);
			float cardHeight = 249 * (UserSettings.ProxySizePercentage / 100f);
			int widthGap = UserSettings.GapX;
			int heightGap = UserSettings.GapY;

			for (int i = 0; i < Math.Ceiling(imagesWithBorder.Count / 9f); i++)
			{
				float x = UserSettings.OffsetLeft;
				float y = UserSettings.OffsetTop;

				int remainingImages = imagesWithBorder.Count - i * 9;
				if (remainingImages > 9)
					remainingImages = 9;

				for (int j = 1; j < remainingImages + 1; j++)
				{
					Dispatcher.Invoke(() => Info.TextBlock.Text = "Drawing images, Progress: " + (j + i * 9) + "/" + imagesWithBorder.Count);

					MemoryStream mem = new MemoryStream(imagesWithBorder.Skip(i * 9).ToList()[j - 1].Image);
					if (UserSettings.FillCorners)
					{
						XBrush cornerBrush = null;
						string currentColor = imagesWithBorder.Skip(i * 9).ToList()[j - 1].BorderColor;
						switch (currentColor.ToLower())
						{
							case "black": cornerBrush = new XSolidBrush(XColor.FromArgb(23, 20, 15));
								break;
							case "gold": cornerBrush = new XSolidBrush(XColor.FromArgb(166, 135, 76));
								break;
							case "silver": cornerBrush = new XSolidBrush(XColor.FromArgb(162, 173, 182)); //127,127,127
								break;
						}
						if(cornerBrush != null)
							draw.DrawRectangle(cornerBrush, x, y, cardWidth, cardHeight);
					}

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
				Focus();
			});
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
				Info.Show();
				IsEnabled = false;
			});
			
			JToken pulledCards = await PullAllDecklistData();
			List<Card> allCards = InterpretCardlist();
			int progress = 0;

			foreach (Card currentCard in allCards)
			{
				Dispatcher.Invoke(() => Info.TextBlock.Text = "Parsing decklist, Progress: " + ++progress + "/" + allCards.Count);
				foreach (JToken jt in pulledCards["data"].Children())
				{
					List<string> cardNames = jt["name"].Value<string>().Split(" // ").ToList();
					string displayName = cardNames.Count > 1 ? cardNames[0] + " // " + cardNames[1] : cardNames[0];
					string regexPattern = @"[^a-zA-Z0-9]";
					for (int i = 0; i < cardNames.Count; i++)
						cardNames[i] = Regex.Replace(cardNames[i], regexPattern, "");
					string cardname = Regex.Replace(currentCard.CardName, regexPattern, "");
					if (cardNames.Contains(cardname, StringComparer.OrdinalIgnoreCase) ||
					    jt["name"].Value<string>().Equals(currentCard.CardName, StringComparison.OrdinalIgnoreCase))
					{
						HttpResponseMessage allPrintsResp = await APIInterface.Get("/cards/search",
							jt["prints_search_uri"].Value<string>().Split('?')[1]);
						JToken allPrints = JToken.Parse(allPrintsResp.Content.ReadAsStringAsync().Result);
						List<Edition> editions = new List<Edition>();
						foreach (JToken singlePrint in allPrints["data"])
						{
							List<string> specialEffects = new List<string>();
							Edition ed = new Edition();
							string specialEffectsStr = "";

							if (singlePrint["full_art"].Value<bool>())
								specialEffects.Add("Fullart");

							if (singlePrint["textless"].Value<bool>())
								specialEffects.Add("Textless");

							if (singlePrint["frame"].Value<string>() == "future")
								specialEffects.Add("Future Sight frame");
										
							if (singlePrint["border_color"].Value<string>() == "borderless")
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
							
							ed.Name = setName;
							ed.SetCode = setCode;
							ed.SpecialEffects = specialEffectsStr;
							ed.ArtworkURL = imageUri;
							ed.CardNumber = singlePrint["collector_number"].Value<string>();
							ed.BorderColor = singlePrint["border_color"].Value<string>();

							if (singlePrint["card_faces"] != null)
								if(singlePrint["card_faces"].Children().Count() > 0)
									if(singlePrint["card_faces"][1]["image_uris"] != null)
										ed.BackFaceURL = singlePrint["card_faces"][1]["image_uris"]["png"].Value<string>();

							editions.Add(ed);
						}
						editions.Reverse();
						currentCard.SelectedEditionIndex = 0;
						currentCard.Editions = editions;
						currentCard.DisplayName = displayName;
						break; //stops the loop when the first matching card is found
					}
				}
			}

			allCards.RemoveAll(e => e.Editions == null || e.Editions.Count == 0);
			Cards = allCards;

			Dispatcher.Invoke(() =>
			{
				CardGrid.ItemsSource = Cards;
				CardGrid.Items.Refresh();
				Info.Hide();
				IsEnabled = true;
			});

			string notFoundList = pulledCards["not_found"].Children().Aggregate("", (s, token) => s + "\"" + token["name"] + "\", ").TrimEnd(',', ' ');
			if (!string.IsNullOrEmpty(notFoundList))
				MessageBox.Show("Cards not found: " + notFoundList, "Cards not found");
		}

		private async Task<JToken> PullAllDecklistData()
		{
			List<Card> cards = InterpretCardlist().GroupBy(e => e.CardName).Select(e => e.First()).ToList();
			JArray collection = new JArray();
			if (cards.Count > 75)
			{
				JToken fullData = null;
				List<HttpResponseMessage> responses = new List<HttpResponseMessage>();
				int loopAmount = (int) Math.Ceiling(cards.Count / 75f);
				for (int i = 0; i < loopAmount; i++)
				{
					int remainingAmount = cards.Count - (i * 75) > 75 ? 75 : cards.Count - (i * 75);
					for (int j = 0; j < remainingAmount; j++)
						collection.Add(JToken.Parse("{\"name\":\"" + cards[j + i * 75].CardName + "\"}"));
					string bigBody = "{\"identifiers\":" + collection.ToString() + "}";
					HttpResponseMessage resp = await APIInterface.Post("/cards/collection",
						new StringContent(bigBody, Encoding.UTF8, "application/json"));
					if (fullData == null)
						fullData = JToken.Parse(resp.Content.ReadAsStringAsync().Result);
					else
						responses.Add(resp);
					collection.Clear();
					Thread.Sleep(100);
				}

				foreach (HttpResponseMessage resp in responses)
				{
					JToken jt = JToken.Parse(resp.Content.ReadAsStringAsync().Result);
					foreach(JToken data in jt["data"])
						fullData["data"].Value<JArray>().Add(data);
				}

				return fullData;
			}
			else
			{
				foreach (Card ca in cards)
					collection.Add(JToken.Parse("{\"name\":\"" + ca.CardName + "\"}"));
				string body = "{\"identifiers\":" + collection.ToString() + "}";
				HttpResponseMessage singleResp = await APIInterface.Post("/cards/collection",
					new StringContent(body, Encoding.UTF8, "application/json")); //, "include_multilingual=true"
				return JToken.Parse(singleResp.Content.ReadAsStringAsync().Result);
			}
		}

		private async void VersionCheck(object sender, EventArgs e)
		{
			string currentVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
			Title = $"MTGProxyBuilder (v{currentVersion.TrimEnd('0', '.')})";
			JToken jt;

			using (HttpClient client = new HttpClient())
			{
				client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(new ProductHeaderValue("MTGProxyBuilder")));
				HttpResponseMessage resp = await client.GetAsync("https://api.github.com/repos/EpicZocker/MTGProxyBuilder/releases/latest");
				jt = JToken.Parse(resp.Content.ReadAsStringAsync().Result);
			}

			string tag = jt["tag_name"].Value<string>();
			if (currentVersion != tag.Insert(tag.Length, ".0").TrimStart('v', '.'))
			{
				if (MessageBox.Show($"New version available ({tag}). Do you want to download it and replace the current version?",
				                    "New version", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
				{
					JToken assets = jt["assets"][0];
					string currentDir = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar;
					string downloadedFilePath = currentDir + assets["name"].Value<string>();
					File.Move(Assembly.GetExecutingAssembly().Location, currentDir + "Old" + Properties.Resources.DefaultFileName);
					using (WebClient webClient = new WebClient())
						webClient.DownloadFile(assets["browser_download_url"].Value<string>(), downloadedFilePath);
					Process.Start(downloadedFilePath);
					Exit(0);
				}
			}
		}

		private void OpenSettingsWindow(object sender, RoutedEventArgs e)
		{
			SettingsWindow sw = new SettingsWindow();
			sw.Owner = this;
			sw.ShowDialog();
		}

		private void AddCustomImagesButtonClicked(object sender, RoutedEventArgs e)
		{
			CustomCardsWindow ccw = new CustomCardsWindow();
			ccw.Owner = this;
			if(!string.IsNullOrEmpty(DecklistFileContent) && DecklistFileContent?.Split(NewLine + NewLine).Length > 1)
				ccw.LoadImages(DecklistFileContent.Split(NewLine + NewLine)[1].Split(NewLine));
			ccw.ShowDialog();
		}

		private void DeleteOldFile()
		{
			string oldPath = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "OldMTGProxyBuilder.exe";
			if (File.Exists(oldPath))
			{
				File.Delete(oldPath);
				MessageBox.Show("Updates: \nadded global exception logging\nfixed backface bug with adventure cards\n" +
					"decklist is now saved on close/crash\nupdated settings\nnew features after update\nadded clear all button in custom cards menu",
					"New features", MessageBoxButton.OK);
			}
		}

		private void DisableTab(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Tab)
				e.Handled = true;
		}

		private void ApplicationClosing(object sender, CancelEventArgs e)
		{
			if (UserSettings.DecklistSaveOnClose)
				ExportDecklist();
			Exit(0);
		}

		private void CardGridContextMenuOpenInScryfallClicked(object sender, RoutedEventArgs e)
		{
			int i = CardGrid.SelectedIndex;
			Edition selectedEdition = Cards[i].Editions[Cards[i].SelectedEditionIndex];
			Process.Start("https://www.scryfall.com/card/" + $"{selectedEdition.SetCode.ToLower()}/" +
			             $"{selectedEdition.CardNumber}/{Cards[i].CardName}");
		}

		private void CardGridContextMenuEditAmountClicked(object sender, RoutedEventArgs e)
		{
			EditAmountWindow eaw = new EditAmountWindow();
			eaw.ShowDialog();
			Cards[CardGrid.SelectedIndex].Amount = eaw.NewAmount;
			CardGrid.Items.Refresh();
		}

		private void CardGridContextMenuDeleteClicked(object sender, RoutedEventArgs e)
		{
			Cards.RemoveAt(CardGrid.SelectedIndex);
			CardGrid.Items.Refresh();
		}
		
		private void ExportDecklist()
		{
			if (!string.IsNullOrEmpty(Decklist.Text) || (CustomCards != null && CustomCards?.Count != 0))
			{
				using (StreamWriter sw = new StreamWriter(DecklistSavePath))
				{
					sw.WriteLine(Decklist.Text + NewLine);
					if (UserSettings.DecklistSaveCustomCards)
						if (CustomCards != null && CustomCards?.Count != 0)
							sw.Write(CustomCards.Aggregate("", (x, y) => x + y.Directory + Path.DirectorySeparatorChar + y.CardName + NewLine));
					sw.Flush();
				}
			}
		}

		private void LogAllExceptions(object sender, UnhandledExceptionEventArgs e)
		{
			if (UserSettings.ExceptionLogging)
			{
				using (StreamWriter sw = new StreamWriter("MTGProxyBuilder.log"))
				{
					Exception ex = e.ExceptionObject as Exception;
					sw.WriteLine(ex.Message);
					sw.WriteLine(ex.StackTrace);
					sw.Flush();
				}
			}

			if (UserSettings.DecklistSaveOnCrash)
				ExportDecklist();
		}

		private void ImportSavedDecklist()
		{
			if (File.Exists(DecklistSavePath))
			{
				using (StreamReader sr = new StreamReader(DecklistSavePath))
				{
					DecklistFileContent = sr.ReadToEnd();
					Decklist.Text = DecklistFileContent.Split(NewLine + NewLine)[0];
				}
			}
		}
	}
}
