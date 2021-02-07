using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace MTGProxyBuilder.Main.Windows
{
	public partial class Changelog : Window
	{
		private bool LoadingFailed = false;

		public Changelog()
		{
			InitializeComponent();
			Scroll.ScrollToBottom();
			if (ChangePanel.Children.Count == 0 || LoadingFailed)
			{
				ChangePanel.Children.Clear();
				try
				{
					List<JToken> commits = Task.Run(LoadCommits).Result.ToList();
					commits.Reverse();
					foreach (JToken jt in commits)
					{
						TextBlock tb = new TextBlock() { TextWrapping = TextWrapping.Wrap };
						tb.Inlines.Add(new Run(jt["commit"]["author"]["date"].ToString() + Environment.NewLine) { FontWeight = FontWeights.Bold });
						tb.Inlines.Add(new Run(jt["commit"]["message"].ToString() + Environment.NewLine));
						ChangePanel.Children.Add(tb);
					}
				}
				catch (Exception)
				{
					TextBlock err = new TextBlock
					{
						Text = "Changelog could not be loaded.",
						HorizontalAlignment = HorizontalAlignment.Center,
						VerticalAlignment = VerticalAlignment.Center
					};
					ChangePanel.Children.Add(err);
					LoadingFailed = true;
				}
			}
		}

		private async Task<JToken> LoadCommits()
		{			
			using (HttpClient client = new HttpClient())
			{
				client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(new ProductHeaderValue("MTGProxyBuilder")));
				HttpResponseMessage resp = await client.GetAsync("https://api.github.com/repos/EpicZocker/MTGProxyBuilder/commits");
				return JToken.Parse(resp.Content.ReadAsStringAsync().Result);
			}
		}
	}
}
