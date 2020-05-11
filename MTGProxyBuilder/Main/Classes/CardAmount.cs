using System.Collections.Generic;

namespace MTGProxyBuilder.Main.Classes
{
	public class CardAmount
	{
		public string CardName { get; set; }

		public string DisplayName { get; set; }
		
		public int Amount { get; set; }

		public List<string> EditionNames { get; set; }
		public List<string> ArtworkURLs;
		
		public string BackFaceURL;
		
		public string SelectedEdition { get; set; }
	}
}
