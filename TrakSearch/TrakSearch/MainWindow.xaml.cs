using System;
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
using System.Text;
using System.Text.RegularExpressions;
using Shravan.DJ.TrakPlayer;
using System.Windows.Data;
using System.Dynamic;

namespace Shravan.DJ.TrakSearch
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger(); // creates a logger using the class name

		TagParser AllTagData = new TagParser();
		SearchEngineService Search = new SearchEngineService();
		System.Windows.Threading.DispatcherTimer KeyTimer;
		Stopwatch WindowTimer = new Stopwatch();
		bool Searching = false;

		public static RoutedCommand SearchHotkey = new RoutedCommand();
		public static RoutedCommand PlayerPlayHotkey = new RoutedCommand();
		public static RoutedCommand PlayerRewindHotkey = new RoutedCommand();
		public static RoutedCommand PlayerForwardHotkey = new RoutedCommand();
		public static RoutedCommand PlayerStopHotkey = new RoutedCommand();

		Mutex SearchingMutex = new Mutex();
		DataGridColumn _MusicDataSortColumn;

		MusicPlayer _Player;
		

		public bool AutoSearchTrigger { get; private set; }

		public MainWindow()
		{
			try
			{
				System.Diagnostics.Process.GetCurrentProcess().PriorityClass = System.Diagnostics.ProcessPriorityClass.BelowNormal;


				InitializeComponent();

				this.MusicData.ItemsSource = new List<Id3TagData>();
				this.MusicData.Items.Refresh();

				KeyTimer = new System.Windows.Threading.DispatcherTimer();
				KeyTimer.Interval = new TimeSpan(0, 0, 0, 0, 333);
				KeyTimer.Tick += new EventHandler(Event_KeyTimerTick);
				KeyTimer.Start();
				
				WindowTimer.Start();


				SearchHotkey.InputGestures.Add(new KeyGesture(Key.F, ModifierKeys.Control));
				//HotKeyCommands.InputGestures.Add(new KeyGesture(Key.D1, ModifierKeys.Alt));
				//HotKeyCommands.InputGestures.Add(new KeyGesture(Key.D2, ModifierKeys.Alt));
				//HotKeyCommands.InputGestures.Add(new KeyGesture(Key.D3, ModifierKeys.Alt));
				//HotKeyCommands.InputGestures.Add(new KeyGesture(Key.D4, ModifierKeys.Alt));

				PlayerPlayHotkey.InputGestures.Add(new KeyGesture(Key.Up, ModifierKeys.Control));
				PlayerRewindHotkey.InputGestures.Add(new KeyGesture(Key.Left, ModifierKeys.Control));
				PlayerForwardHotkey.InputGestures.Add(new KeyGesture(Key.Right, ModifierKeys.Control));
				PlayerStopHotkey.InputGestures.Add(new KeyGesture(Key.Down, ModifierKeys.Control));

				StyleDataGrid();

				_Player = new MusicPlayer();


				SetWindowPosition();
				
				//if (Properties.Settings.Default.Height != 0 && Properties.Settings.Default.Width != 0)
				//{

				//	this.Top = Properties.Settings.Default.Top;
				//	this.Left = Properties.Settings.Default.Left;
				//	this.Height = Properties.Settings.Default.Height;
				//	this.Width = Properties.Settings.Default.Width;
				//}
				
			}
			catch (Exception ex)
			{
				logger.Error(ex, "Error initalizing window");
			}

		}

		private void SetWindowPosition()
		{

			var height = SystemParameters.WorkArea.Height / 2;

			this.Top = SystemParameters.WorkArea.Height / 2;
			this.Left = 0;
			this.Height = SystemParameters.WorkArea.Height / 2;
			this.Width = SystemParameters.WorkArea.Width;
			
		}

		private void Event_KeyTimerTick(object sender, EventArgs e)
		{
			if (!AutoSearchTrigger)
				return;

			if (Searching)
				return;


			if (! (string.IsNullOrEmpty(SearchBox.Text.Trim())
					&& string.IsNullOrEmpty(BpmSearchBox.Text.Trim())
					&& string.IsNullOrEmpty(KeySearchBox.Text.Trim())
					&& string.IsNullOrEmpty(EnergySearchBox.Text.Trim())) )
			{
				AutoSearchTrigger = false;
				SearchMusic(SearchBox.Text, BpmSearchBox.Text, KeySearchBox.Text, EnergySearchBox.Text);
			}
			else
			{
				AutoSearchTrigger = false;
				ClearSearch();
			}
		}

		private void SearchBox_KeyUp(object sender, KeyEventArgs e)
		{

			if (e.Key == Key.Enter)
			{
				AutoSearchTrigger = true;

				if (string.IsNullOrEmpty(SearchBox.Text.Trim())
					&& string.IsNullOrEmpty(BpmSearchBox.Text.Trim())
					&& string.IsNullOrEmpty(KeySearchBox.Text.Trim())
					&& string.IsNullOrEmpty(EnergySearchBox.Text.Trim()))
				{
					ClearSearch();
				}
				else
				{
					SearchMusic(SearchBox.Text, BpmSearchBox.Text, KeySearchBox.Text, EnergySearchBox.Text);
				}
			}
			else if ( (((e.Key >= Key.A && e.Key <= Key.Z) || (e.Key >= Key.D0 && e.Key <= Key.D9) || (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
					|| (e.Key == Key.Back || e.Key == Key.Delete)) && Keyboard.Modifiers == ModifierKeys.None )
				|| ((e.Key == Key.V || e.Key == Key.X) && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) )
			{
				AutoSearchTrigger = true;
			}
			else if (e.Key == Key.Escape)
			{
				AutoSearchTrigger = false;
				ClearSearch();
			}
			else if (e.Key == Key.Down || e.Key == Key.Up)
			{

				if (MusicData.SelectedIndex == 0 && e.Key == Key.Up)
				{
					SearchBox.Focus();
					return;
				}
				else if (MusicData.SelectedIndex < 0)
				{
					MusicData.SelectedIndex = 0;
				}
				
				DataGridRow row = (DataGridRow)MusicData.ItemContainerGenerator.ContainerFromIndex(MusicData.SelectedIndex);
				row?.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
				
			}
			else if ( (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
				|| e.Key == Key.F1 || e.Key == Key.F3)
			{
				if (SearchBox.Focus())
				{
					SearchBox.SelectAll();
				}
			}
		}

		private void ClearSearch()
		{
			SearchBox.Clear();
			EnergySearchBox.Clear();
			KeySearchBox.Clear();
			BpmSearchBox.Clear();

			UpdateItemSource();
		}

		private void SearchMusic(string searchText, string bpmText = null, string keyText = null, string energy = null)
		{
			var search = CreateSearchText(searchText, bpmText, keyText, energy);

			var task = new Task(() =>
			{
				SearchingMutex.WaitOne();
				Searching = true;

				var result = new List<Id3TagData>().AsEnumerable();

				try
				{
					result = SearchEngineService.Search(search);
				}
				catch (Exception ex)
				{
					Console.WriteLine(ex.ToString());
				}

				if (!AllTagData.TagList.IsEmpty)
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

		private string CreateSearchText(string searchText, string bpmText, string keyText, string energyText)
		{
			var search = new StringBuilder();
			search.Append(searchText + " ");

			if (!string.IsNullOrEmpty(bpmText))
			{
				search.Append(" BPM:" + bpmText);
			}
			if (!string.IsNullOrEmpty(keyText))
			{
				search.Append(" Key:" + keyText);
			}
			if (!string.IsNullOrEmpty(energyText))
			{
				search.Append(" Energy:" + energyText + "starzz*");
			}

			return search.ToString();
		}

		private void UpdateItemSource(IEnumerable<Id3TagData> data = null)
		{
			data = data ?? AllTagData.TagList;

			this.Dispatcher.Invoke(() =>
			{
				MusicData.ItemsSource = data;
				ResultCountLabel.Content = data.Count();
				if (_MusicDataSortColumn != null)
				{
					MusicData.Items.SortDescriptions.Add(
						new System.ComponentModel.SortDescription(_MusicDataSortColumn.SortMemberPath, _MusicDataSortColumn.SortDirection ?? System.ComponentModel.ListSortDirection.Ascending));

				}
				MusicData.Items.Refresh();
			});
		}

		private void SearchButton_Click(object sender, RoutedEventArgs e)
		{
			SearchMusic(SearchBox.Text);
		}

		private void CancelButton_Click(object sender, RoutedEventArgs e)
		{
			ClearSearch();
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
			Folder2Button.Content = "Loading...";

			var dialog = new WPFFolderBrowser.WPFFolderBrowserDialog("Select Music Folder");
			var done = dialog.ShowDialog(this);
			//AllTagData.IndexDirectory(@"H:\Zouk");

			if (done == true)
			{
				var timer = new Stopwatch();
				timer.Start();

				AllTagData.TagList = new System.Collections.Concurrent.ConcurrentBag<Id3TagData>();
				this.MusicData.ItemsSource = new List<Id3TagData>();
				this.MusicData.Items.Refresh();

				var folder = dialog.FileName;
				
				AllTagData.IndexDirectory(folder);

				
				Folder2Button.Visibility = Visibility.Hidden;

				this.MusicData.ItemsSource = AllTagData.TagList.Cast<Id3TagDataBase>();
				this.MusicData.Items.Refresh();
				this.ResultCountLabel.Content = AllTagData.TagList.Count();

				UpdateItemSource();

				timer.Stop();
				var time = timer.ElapsedMilliseconds;
			}

			FolderButton.IsEnabled = true;

		}

		public void StyleDataGrid()
		{
			var dataGrid = MusicData;

			var largeColumn = new List<string> { "Comment", "FullPath" };
			var smallColumn = new List<string> { "BPM", "Key", "Energy", };
			var normalColumn = new List<string> { "Title", "Artist", "Album", "Publisher", "Remixer" };

			var windowSize = this.RenderSize.Width > 600 ? this.RenderSize.Width : 600;

			foreach (DataGridColumn c in dataGrid.Columns)
			{
				var header = c.Header;
				if (largeColumn.Any(w => c.Header.ToString() == w))
				{
					c.MaxWidth = windowSize * 0.4;
					if (c.Width.Value > c.MaxWidth)
						c.Width = new DataGridLength(c.MaxWidth, DataGridLengthUnitType.Star, c.Width.DesiredValue, c.MaxWidth);

				}
				else if (normalColumn.Any(w => c.Header.ToString() == w))
				{
					c.MaxWidth = windowSize * 0.10;

					if (c.Width.DisplayValue > c.MaxWidth)
					{
						c.Width = new DataGridLength(c.MaxWidth, DataGridLengthUnitType.Star, c.Width.DesiredValue, c.MaxWidth);

					}
				}
				else if (smallColumn.Any(w => c.Header.ToString() == w))
				{
					c.MaxWidth = windowSize * 0.04;

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

		private void SearchBox_HotKeyCommand(object sender, ExecutedRoutedEventArgs e)
		{
			SearchBox.Focus();
		}

		private void PlayerPlay_HotKeyCommand(object sender, ExecutedRoutedEventArgs e)
		{
			PlayMusic();
		}

		private void PlayMusic()
		{
			try
			{
				var data = (Id3TagData)MusicData.SelectedItem;
				var device = MusicPlayer.GetDefaultRenderDevice();
				if (data != null)
				{
					_Player.Open(data.FullPath, device);
					_Player.Play();
					PlayIndicator.IsChecked = true;
				}
				else
				{
					PlayIndicator.IsChecked = false;
				}
			}
			catch(Exception ex)
			{
				logger.Error(ex, "Player Error");
			}
		}
		
		private void StopMusic ()
		{
			try
			{
				_Player.Stop();
				PlayIndicator.IsChecked = false;
			}
			catch (Exception ex)
			{
				logger.Error(ex, "Player Error");
			}
		}

		private void PlayerRewind_HotKeyCommand(object sender, ExecutedRoutedEventArgs e)
		{
			_Player.Position = _Player.Position.Subtract(new TimeSpan(0, 0, 20));
		}


		private void PlayerForward_HotKeyCommand(object sender, ExecutedRoutedEventArgs e)
		{
			_Player.Position = _Player.Position.Add(new TimeSpan(0, 0, 20));
		}

		private void PlayerStop_HotKeyCommand(object sender, ExecutedRoutedEventArgs e)
		{
			StopMusic();
		}

		private void NumberSearchBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
		{
			e.Handled = !NumbersOnly(e.Text);
		}

		private void KeySearchBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
		{
			e.Handled = !HarmonicKeyOnly(e.Text);
		}

		private static bool NumbersOnly(string text)
		{
			Regex regex = new Regex("[^0-9]+"); //regex that matches disallowed text
			return !regex.IsMatch(text);
		}

		private static bool HarmonicKeyOnly(string text)
		{
			Regex regex = new Regex("[^0-9md]+"); //regex that matches disallowed text
			return !regex.IsMatch(text);
		}


		private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			foreach (InputBinding inputBinding in this.InputBindings)
			{
				KeyGesture keyGesture = inputBinding.Gesture as KeyGesture;
				if (keyGesture != null && keyGesture.Key == e.Key && keyGesture.Modifiers == Keyboard.Modifiers)
				{
					if (inputBinding.Command != null)
					{
						inputBinding.Command.Execute(0);
						e.Handled = true;
					}
				}
			}

			foreach (CommandBinding cb in this.CommandBindings)
			{
				RoutedCommand command = cb.Command as RoutedCommand;
				if (command != null)
				{
					foreach (InputGesture inputGesture in command.InputGestures)
					{
						KeyGesture keyGesture = inputGesture as KeyGesture;
						if (keyGesture != null && keyGesture.Key == e.Key && keyGesture.Modifiers == Keyboard.Modifiers)
						{
							command.Execute(0, this);
							e.Handled = true;
						}
					}
				}
			}
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			_Player.Stop();
			_Player.Dispose();
			_Player = null;
			SaveWindowLocation();

		}

		private void SaveWindowLocation()
		{
			dynamic windowData = new ExpandoObject();
			if (WindowState != WindowState.Maximized)
			{
				//Properties.Settings.Default.Top = this.Top;
				//Properties.Settings.Default.Left = this.Left;
				//Properties.Settings.Default.Height = this.Height;
				//Properties.Settings.Default.Width = this.Width;
				//Properties.Settings.Default.Maximized = false;

				//Properties.Settings.Default.Save();
			}

			
			
		}

		private void PlayIndicator_Click(object sender, RoutedEventArgs e)
		{
			if (_Player.PlaybackState != CSCore.SoundOut.PlaybackState.Playing)
			{
				PlayMusic();
			}
			else
			{
				StopMusic();
			}

			PlayIndicator.UpdateLayout();
		}


		
		private void MusicData_Sorting(object sender, DataGridSortingEventArgs e)
		{
			_MusicDataSortColumn = e.Column;
			
		}


		
	}
}