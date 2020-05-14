using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace MTGProxyBuilder.Main.Classes
{
	public class APIInterface
	{
		private const string DefaultUrl = "https://api.scryfall.com";

		private static HttpClient Client = new HttpClient();

		public static async Task<HttpResponseMessage> Get(string apiUrl, params string[] parameters) =>
				await Client.GetAsync(CreateUri(apiUrl, DefaultUrl, parameters));
		
		public static async Task<HttpResponseMessage> Post(string apiUrl, HttpContent body = null, params string[] parameters) =>
				await Client.PostAsync(CreateUri(apiUrl, DefaultUrl, parameters), body);

		private static Uri CreateUri(string apiUrl, string url = DefaultUrl, params string[] par)
		{
			string full = url + apiUrl + (par.Length != 0 ? "?" : "");
			string req = string.Join("&", par);
			return new Uri(full + req);
		}
	}
}