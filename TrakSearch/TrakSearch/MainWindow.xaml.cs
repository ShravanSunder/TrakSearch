﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Shravan.DJ.TagIndexer;
using Shravan.DJ.TagIndexer.Data;
using System.Windows.Controls;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;

namespace DJ.TrakSearch
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		TagParser AllTagData = new TagParser();
		SearchEngineService Search = new SearchEngineService();
		Stopwatch KeyTimer = new Stopwatch();
		Stopwatch WindowTimer = new Stopwatch();
		bool Searching = false;

		Mutex SearchingMutex = new Mutex();

		public MainWindow()
		{
			try
			{
				System.Diagnostics.Process.GetCurrentProcess().PriorityClass = System.Diagnostics.ProcessPriorityClass.BelowNormal;


				InitializeComponent();

				this.MusicData.ItemsSource = new List<Id3TagData>();
				this.MusicData.Items.Refresh();

				KeyTimer.Start();
				WindowTimer.Start();

				StyleDataGrid();
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.ToString());
			}

		}

		private void SearchBox_KeyUp(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter)
			{
				SearchMusic(SearchBox.Text);
				KeyTimer.Restart();
			}
			else if (e.Key == Key.Escape)
			{
				SearchBox.Clear();
				UpdateItemSource();
			}
			else if (!(e.Key < Key.A) || (e.Key > Key.Z)
				|| ((e.Key == Key.V || e.Key == Key.X) && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
				|| (e.Key == Key.Back || e.Key == Key.Delete)
				)
			{
				var s = SearchBox.Text;

				if (string.IsNullOrEmpty(s))
				{
					SearchBox.Clear();
					UpdateItemSource();
				}
				else if (KeyTimer.ElapsedMilliseconds > 250 && Searching == false)
				{
					KeyTimer.Restart();
					SearchMusic(s);
				}
			}
		}

		private void SearchMusic(string text)
		{
			var task = new Task(() =>
			{
				SearchingMutex.WaitOne();
				Searching = true;

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
					this.Dispatcher.Invoke(() =>
					{
						UpdateItemSource(result);
					});
				}


				Searching = false;
				SearchingMutex.ReleaseMutex();
			});

			task.Start();

		}

		private void UpdateItemSource(IEnumerable<Id3TagData> data = null)
		{
			data = data ?? AllTagData.tagList;

			this.Dispatcher.Invoke(() =>
			{
				MusicData.ItemsSource = data;
				MusicData.Items.Refresh();
			});
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
				AllTagData.tagList = new System.Collections.Concurrent.ConcurrentBag<Id3TagData>();
				this.MusicData.ItemsSource = new List<Id3TagData>();
				this.MusicData.Items.Refresh();

				var folder = dialog.FileName;

				SearchEngineService.ClearLuceneIndex();

				AllTagData.IndexDirectory(folder);

				SearchEngineService.AddUpdateLuceneIndex(AllTagData.tagList.AsEnumerable());

				Folder2Button.Visibility = Visibility.Hidden;
				this.MusicData.ItemsSource = AllTagData.tagList.Cast<Id3TagDataBase>();
				this.MusicData.Items.Refresh();
			}

			FolderButton.Content = "Folder";
			FolderButton.IsEnabled = true;

		}

		public void StyleDataGrid()
		{
			var dataGrid = MusicData;

			var largeColumn = new List<string> { "Comment", "FullPath" };
			var smallColumn = new List<string> { "BPM", "Key", "Energy", };
			var normalColumn = new List<string> { "Title", "Artist", "Album" };

			var windowSize = this.RenderSize.Width > 600 ? this.RenderSize.Width : 600;

			foreach (DataGridColumn c in dataGrid.Columns)
			{
				var header = c.Header;
				if (largeColumn.Any(w => c.Header.ToString() == w))
				{
					c.MaxWidth = windowSize * 0.5;
					if (c.Width.Value > c.MaxWidth)
						c.Width = new DataGridLength(c.MaxWidth, DataGridLengthUnitType.Star, c.Width.DesiredValue, c.MaxWidth);

				}
				else if (normalColumn.Any(w => c.Header.ToString() == w))
				{
					c.MaxWidth = windowSize * 0.15;

					if (c.Width.DisplayValue > c.MaxWidth)
					{
						c.Width = new DataGridLength(c.MaxWidth, DataGridLengthUnitType.Star, c.Width.DesiredValue, c.MaxWidth);

					}
				}
				else if (smallColumn.Any(w => c.Header.ToString() == w))
				{
					c.MaxWidth = windowSize * 0.05;

					if (c.Width.Value > c.MaxWidth)
						c.Width = new DataGridLength(c.MaxWidth, DataGridLengthUnitType.Star, c.Width.DesiredValue, c.MaxWidth);
				}
			}
		}

		private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			if (WindowTimer.ElapsedMilliseconds > 250)
			{
				this.Dispatcher.Invoke(() =>
				{
					StyleDataGrid();
					WindowTimer.Restart();
				});
			}
		}

	}
}