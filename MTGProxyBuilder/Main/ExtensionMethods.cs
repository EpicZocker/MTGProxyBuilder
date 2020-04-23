using System.Windows.Controls;
using System.Windows.Documents;

namespace MTGProxyBuilder
{
	public static class ExtensionMethods
    {
	    public static void SetText(this RichTextBox richTextBox, string text)
	    {
		    richTextBox.Document.Blocks.Clear();
		    richTextBox.Document.Blocks.Add(new Paragraph(new Run(text)));
	    }

	    public static string GetText(this RichTextBox richTextBox)
	    {
		    return new TextRange(richTextBox.Document.ContentStart, richTextBox.Document.ContentEnd).Text;
	    }
    }
}
