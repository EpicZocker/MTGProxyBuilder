using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace MTGProxyBuilder.Main.Classes
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

		public static T GetParentOfType<T>(this DependencyObject element) where T : DependencyObject
		{
			Type type = typeof(T);
			if (element == null) 
				return null;
			DependencyObject parent = VisualTreeHelper.GetParent(element);
			if (parent == null && ((FrameworkElement)element).Parent is DependencyObject) 
				parent = ((FrameworkElement)element).Parent;
			if (parent == null) 
				return null;
			else if (parent.GetType() == type || parent.GetType().IsSubclassOf(type)) 
				return parent as T;
			return GetParentOfType<T>(parent);
		}
	}
}
