using System.Collections.Generic;

namespace MTGProxyBuilder.Main.Classes
{
	public class Card
	{
		public string CardName { get; set; }

		public string DisplayName { get; set; }
		
		public int Amount { get; set; }

		public List<Edition> Editions { get; set; }

		public int SelectedEditionIndex { get; set; }
	}
}
