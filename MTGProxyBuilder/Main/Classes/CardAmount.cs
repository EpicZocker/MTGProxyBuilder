using System.Collections.Generic;

namespace MTGProxyBuilder.Main.Classes
{
	public class CardAmount
	{
		public string CardName { get; set; }
		
		public int Amount { get; set; }

		public List<string> EditionNames { get; set; }
		public bool HasBackFace;
	}
}
