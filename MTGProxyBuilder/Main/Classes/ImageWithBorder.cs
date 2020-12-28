namespace MTGProxyBuilder.Main.Classes
{
    public class ImageWithBorder
    {
        public byte[] Image;
        public string BorderColor;

        public ImageWithBorder(byte[] img, string border)
        {
            Image = img;
            BorderColor = border;
        }
    }
}
