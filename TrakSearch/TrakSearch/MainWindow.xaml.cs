using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Shravan.DJ.TagIndexer;
using Shravan.DJ.TagIndexer.Data;

namespace DJ.TrakSearch
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		TagParser AllTagData = new TagParser();
		SearchEngineService Search = new SearchEngineService();

		public MainWindow()
		{
			try
			{
				InitializeComponent();

				this.MusicData.ItemsSource = new List<Id3TagData>();
				this.MusicData.Items.Refresh();
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.ToString());
			}

		}

		private void SearchBox_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter)
			{
				SearchMusic(SearchBox.Text);
			}
			else if (e.Key == Key.Escape)
			{
				SearchBox.Clear();
				UpdateItemSource();
			}
		}

		private void SearchMusic(string text)
		{
			var result = new List<Id3TagData>().AsEnumerable();

			try
			{
				result = SearchEngineService.Search(text);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.ToString());
			}

			if (!AllTagData.tagList.IsEmpty)
			{
				UpdateItemSource(result);
			}

		}

		private void UpdateItemSource(IEnumerable<Id3TagData> data = null)
		{
			data = data ?? AllTagData.tagList;

			MusicData.ItemsSource = data;
			MusicData.Items.Refresh();
		}

		private void SearchButton_Click(object sender, RoutedEventArgs e)
		{
			SearchMusic(SearchBox.Text);
		}

		private void CancelButton_Click(object sender, RoutedEventArgs e)
		{
			SearchBox.Clear();
			UpdateItemSource();
		}

		private void MusicData_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			var data = (Id3TagData)MusicData.SelectedItem;
			Clipboard.SetData(DataFormats.Text, data.Title ?? "" + "  " + data.Artist ?? "");
		}

		private void FolderButton_Click(object sender, RoutedEventArgs e)
		{
			FolderButton.IsEnabled = false;
			Folder2Button.IsEnabled = false;
			FolderButton.Content = "Loading...";
			Folder2Button.Content = "Loading...";


			var dialog = new WPFFolderBrowser.WPFFolderBrowserDialog("Select Music Folder");
			var done = dialog.ShowDialog(this);
			//AllTagData.IndexDirectory(@"H:\Zouk");

			if (done == true)
			{
				var folder = dialog.FileName;

				AllTagData.IndexDirectory(folder);

				SearchEngineService.AddUpdateLuceneIndex(AllTagData.tagList.AsEnumerable());

				Folder2Button.Visibility = Visibility.Hidden;
				this.MusicData.ItemsSource = AllTagData.tagList.Cast<Id3TagDataBase>();
				this.MusicData.Items.Refresh();
			}

			FolderButton.Content = "Folder";
			FolderButton.IsEnabled = true;

		}
	}
}
