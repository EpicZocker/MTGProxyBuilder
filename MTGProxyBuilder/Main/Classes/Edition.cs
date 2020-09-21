namespace MTGProxyBuilder.Main.Classes
{
	public class Edition
	{
		public string Name;
		public string SetCode;
		public string SpecialEffects;

		public string ArtworkURL;
		public string BackFaceURL;
		public string CardNumber;

		public string BorderColor;

		public string DisplayEdition { get { return Name + $" ({SetCode})" + SpecialEffects; } }
	}
}
