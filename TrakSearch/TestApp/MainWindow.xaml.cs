using Shravan.DJ.TrakPlayer;
using System;
using System.Collections.Generic;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TestApp
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();

			MusicPlayer m = new MusicPlayer();

			var d = MusicPlayer.GetDefaultRenderDevice();
			m.Open(@"F:\Shravan's Documents\Dropbox\!Backup\MkLinks\Mp3\Dance\Salsa\Huey Dunbar\Huey Dunbar - IV (2010)\02 - Te Amare.mp3", d);
			m.Play();


			m.Position = m.Position.Add(new TimeSpan(0, 0, 20));


		}
	}
}
