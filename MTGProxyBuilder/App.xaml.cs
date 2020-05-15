using System.IO;
using System.Windows;

namespace MTGProxyBuilder
{
	public partial class App : Application
	{
		private void ApplicationStart(object sender, StartupEventArgs e)
		{
			string oldPath = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "OldMTGProxyBuilder.exe";
			if (File.Exists(oldPath))
				File.Delete(oldPath);
		}
	}
}
