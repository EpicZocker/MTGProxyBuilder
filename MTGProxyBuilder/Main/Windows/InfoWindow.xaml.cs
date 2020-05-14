using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MTGProxyBuilder.Main.Windows
{
	public partial class InfoWindow : Window
	{
		public InfoWindow()
		{
			InitializeComponent();
		}

		//[DllImport("user32.dll")]
		//static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

		//[DllImport("user32.dll")]
		//static extern bool EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);

		//protected override void OnSourceInitialized(EventArgs e)
		//{
		//	base.OnSourceInitialized(e);
		//	IntPtr hwnd = new WindowInteropHelper(this).Handle;
		//	IntPtr hMenu = GetSystemMenu(hwnd, false);
		//	if (hMenu != IntPtr.Zero)
		//		EnableMenuItem(hMenu, 0xF060, 0x00000000 | 0x00000001);
		//}

		private void InfoWindowClosing(object sender, CancelEventArgs e)
		{
			(sender as Window).Hide();
			e.Cancel = true;
		}
	}
}
