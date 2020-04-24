﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MTGProxyBuilder.Main
{
	/// <summary>
	/// Interaction logic for CustomizeCardsWindow.xaml
	/// </summary>
	public partial class CustomizeCardsWindow : Window
	{
		public MainWindow HostWindow;

		public CustomizeCardsWindow()
		{
			InitializeComponent();
		}

		private void CustomizeCardsWindowClosing(object sender, CancelEventArgs e)
		{
			HostWindow.IsEnabled = true;
		}
	}
}
