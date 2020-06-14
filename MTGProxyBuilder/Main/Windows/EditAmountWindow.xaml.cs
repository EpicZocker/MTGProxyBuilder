using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace MTGProxyBuilder.Main.Windows
{
    public partial class EditAmountWindow : Window
    {
	    public int NewAmount;

        public EditAmountWindow()
        {
            InitializeComponent();
        }

        private void NumberInputCheck(object sender, TextCompositionEventArgs e)
        {
	        Regex regex = new Regex("[^0-9]+");
	        e.Handled = regex.IsMatch(e.Text);
		}

        private void PreventPaste(object sender, ExecutedRoutedEventArgs e)
        {
	        if (e.Command == ApplicationCommands.Copy || e.Command == ApplicationCommands.Cut || e.Command == ApplicationCommands.Paste)
		        e.Handled = true;
        }

        private void ConfirmButtonClicked(object sender, RoutedEventArgs e)
        {
	        NewAmount = int.Parse(AmountText.Text);
	        Close();
        }

        private void CancelButtonClicked(object sender, RoutedEventArgs e)
        {
			Close();
        }
    }
}
