using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace MTGProxyBuilder.Main.Classes
{
	public class APIInterface
	{
		private const string Url = "https://api.scryfall.com";

		private static HttpClient Client = new HttpClient();
		
		public static async Task<HttpResponseMessage> Get(string apiUrl, params string[] parameters) =>
				await Client.GetAsync(CreateUri(apiUrl, parameters));
		
		public static async Task<HttpResponseMessage> Post(string apiUrl, params string[] parameters) =>
				await Client.PostAsync(CreateUri(apiUrl, parameters), null);

		public static async Task<HttpResponseMessage> PostWithBody(string apiUrl, string body, params string[] parameters) =>
				await Client.PostAsync(CreateUri(apiUrl, parameters), new StringContent(body, Encoding.UTF8, "application/json"));

		private static Uri CreateUri(string apiUrl, params string[] par)
		{
			string full = Url + apiUrl + (par.Length != 0 ? "?" : "");
			string req = string.Join("&", par);
			return new Uri(full + req);
		}
	}
}