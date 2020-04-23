﻿using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace BinanceTradeBot_Backend.Communication
{
	public class APIInterface
	{
		private const string Url = "https://api.scryfall.com";

		private static HttpClient Client = new HttpClient();
		
		public static async Task<HttpResponseMessage> Get(string apiUrl, params string[] parameters) =>
				await Client.GetAsync(CreateUri(apiUrl, parameters));
		
		public static async Task<HttpResponseMessage> Post(string apiUrl, params string[] parameters) =>
				await Client.PostAsync(CreateUri(apiUrl, parameters), null);

		private static Uri CreateUri(string apiUrl, params string[] par)
		{
			string full = Url + apiUrl + (par.Length != 0 ? "?" : "");
			string req = string.Join("&", par);
			return new Uri(full + req);
		}
	}
}