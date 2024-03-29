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
using System.Text;
using System.Text.RegularExpressions;
using Shravan.DJ.TrakPlayer;
using System.Windows.Data;
using System.Dynamic;
using System.Windows.Media.Imaging;
using DJ.Utilities;
using System.Collections.ObjectModel;

namespace Shravan.DJ.TrakSearch
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IDisposable
    {
        private readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger(); // creates a logger using the class name

        TagParser AllTagData = new TagParser();
        SearchEngineService SearchEngine = new SearchEngineService();
        System.Windows.Threading.DispatcherTimer KeyTimer;
        Stopwatch WindowTimer = new Stopwatch();
        bool Searching = false;
        ObservableCollection<Id3TagData> Playlist = new ObservableCollection<Id3TagData>();


        public static RoutedCommand SearchHotkey = new RoutedCommand();
        public static RoutedCommand PlayerPlayHotkey = new RoutedCommand();
        public static RoutedCommand PlayerRewindHotkey = new RoutedCommand();
        public static RoutedCommand PlayerForwardHotkey = new RoutedCommand();
        public static RoutedCommand PlayerStopHotkey = new RoutedCommand();
        public static RoutedCommand PlaylistAddHotkey = new RoutedCommand();
        public static RoutedCommand PlaylistToggleHotkey = new RoutedCommand();

        public static RoutedCommand AdvancedHarmonicsHotkey = new RoutedCommand();

        Mutex SearchingMutex = new Mutex();
        DataGridColumn _MusicDataSortColumn;

        MusicPlayer _Player;

        public bool AutoSearchTrigger { get; private set; }
        public int SearchKeyCounterForDebounce { get; private set; }

        public MainWindow()
        {
            try
            {
                var proc = System.Diagnostics.Process.GetCurrentProcess();
                proc.PriorityClass = System.Diagnostics.ProcessPriorityClass.BelowNormal;
                proc.ProcessorAffinity = (IntPtr)0x0003;

                InitializeComponent();

                this.MusicDataGrid.ItemsSource = new List<Id3TagData>();
                this.MusicDataGrid.Items.Refresh();

                InitKeyBindings();

                _Player = new MusicPlayer();

                SetWindowPosition();
                StyleDataGrid();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error initalizing window");
            }

            try
            {
                this.PlaylistData.ItemsSource = Playlist;
            }
            catch(Exception ex)
            {
                logger.Error(ex, "Error initializing playlist");
            }

        }

        private void InitKeyBindings()
        {
            KeyTimer = new System.Windows.Threading.DispatcherTimer();
            KeyTimer.Interval = new TimeSpan(0, 0, 0, 0, 200);
            KeyTimer.Tick += new EventHandler(SearchForMusicEvent_KeyTimerTick);
            KeyTimer.Start();

            WindowTimer.Start();

            SearchHotkey.InputGestures.Add(new KeyGesture(Key.F, ModifierKeys.Control));
            SearchHotkey.InputGestures.Add(new KeyGesture(Key.F1, ModifierKeys.None));

            AdvancedHarmonicsHotkey.InputGestures.Add(new KeyGesture(Key.F3, ModifierKeys.None));
            //HotKeyCommands.InputGestures.Add(new KeyGesture(Key.D1, ModifierKeys.Alt));
            //HotKeyCommands.InputGestures.Add(new KeyGesture(Key.D2, ModifierKeys.Alt));
            //HotKeyCommands.InputGestures.Add(new KeyGesture(Key.D3, ModifierKeys.Alt));
            //HotKeyCommands.InputGestures.Add(new KeyGesture(Key.D4, ModifierKeys.Alt));

            PlayerPlayHotkey.InputGestures.Add(new KeyGesture(Key.Up, ModifierKeys.Control));
            PlayerRewindHotkey.InputGestures.Add(new KeyGesture(Key.Left, ModifierKeys.Control));
            PlayerForwardHotkey.InputGestures.Add(new KeyGesture(Key.Right, ModifierKeys.Control));
            PlayerStopHotkey.InputGestures.Add(new KeyGesture(Key.Down, ModifierKeys.Control));

            PlaylistToggleHotkey.InputGestures.Add(new KeyGesture(Key.F2, ModifierKeys.None));
        }

        private void SetWindowPosition()
        {
            var height = SystemParameters.WorkArea.Height / 2;

            this.Top = SystemParameters.WorkArea.Height / 2;
            this.Left = SystemParameters.WorkArea.Left;
            this.Height = SystemParameters.WorkArea.Height / 2;
            this.Width = SystemParameters.WorkArea.Width;
        }

        private void SearchForMusicEvent_KeyTimerTick(object sender, EventArgs e)
        {
            if (!AutoSearchTrigger)
                return;

            if (Searching)
                return;

            if (SearchKeyCounterForDebounce > 0)
            {
                SearchKeyCounterForDebounce = 0;
                return;
            }

            if (!string.IsNullOrEmpty(SearchBox.Text.Trim())
                    || !string.IsNullOrEmpty(BpmSearchBox.Text.Trim())
                    || !string.IsNullOrEmpty(KeySearchBox.Text.Trim())
                    || !string.IsNullOrEmpty(EnergySearchBox.Text.Trim())
                    || !string.IsNullOrEmpty(YearSearchBox.Text.Trim())
                    || !string.IsNullOrEmpty(NotSearchBox.Text.Trim()))
            {
                AutoSearchTrigger = false;
                SearchMusic(SearchBox.Text, BpmSearchBox.Text, KeySearchBox.Text, EnergySearchBox.Text, YearSearchBox.Text, NotSearchBox.Text);
            }
            else
            {
                AutoSearchTrigger = false;
                ClearSearch();
            }
        }

        private void KeyBindingEvent_KeyUp(object sender, KeyEventArgs e)
        {
            var isSenderDataGrid = sender is DataGrid;
            var isSenderCheckbox = sender is CheckBox;
            if ((e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                || e.Key == Key.F1)
            {
                if (SearchBox.Focus())
                {
                    SearchBox.SelectAll();
                }
            }
            else if (e.Key == Key.Down || e.Key == Key.Up)
            {

                if (MusicDataGrid.SelectedIndex == 0 && e.Key == Key.Up)
                {
                    SearchBox.Focus();
                    return;
                }
                else if (MusicDataGrid.SelectedIndex < 0)
                {
                    MusicDataGrid.SelectedIndex = 0;
                }

                DataGridRow row = (DataGridRow)MusicDataGrid.ItemContainerGenerator.ContainerFromIndex(MusicDataGrid.SelectedIndex);
                row?.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            }

            if (isSenderDataGrid)
            {
                if (e.Key == Key.Z)
                {
                    var data = (Id3TagData)MusicDataGrid.SelectedItem;
                    AddToPlaylist(data);
                }
            }
            else if (isSenderCheckbox)
            {
                SearchMusic(SearchBox.Text, BpmSearchBox.Text, KeySearchBox.Text, EnergySearchBox.Text, YearSearchBox.Text, NotSearchBox.Text);
            }
            else
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
                        SearchMusic(SearchBox.Text, BpmSearchBox.Text, KeySearchBox.Text, EnergySearchBox.Text, YearSearchBox.Text, NotSearchBox.Text);
                    }
                }
                else if ((((e.Key >= Key.A && e.Key <= Key.Z) || (e.Key >= Key.D0 && e.Key <= Key.D9) || (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
                        || (e.Key == Key.Back || e.Key == Key.Delete)) && Keyboard.Modifiers == ModifierKeys.None)
                    || ((e.Key == Key.V || e.Key == Key.X) && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control))
                {
                    AutoSearchTrigger = true;
                    SearchKeyCounterForDebounce++;
                }
                else if (e.Key == Key.Escape)
                {
                    AutoSearchTrigger = false;
                    ClearSearch();
                }
            }
        }

        private void AddToPlaylist(Id3TagData data)
        {
            if (data != null && !string.IsNullOrEmpty(data.Artist) && !string.IsNullOrEmpty(data.Title)
                                    && !Playlist.Any(p => p.Index == data.Index))
            {
                this.Dispatcher.Invoke(() =>
                {
                    Playlist.Add(data);
                });
            }
        }

        private void ClearSearch()
        {
            SearchBox.Clear();
            EnergySearchBox.Clear();
            KeySearchBox.Clear();
            BpmSearchBox.Clear();

            UpdateMusicDataGrid();
        }

        private void SearchMusic(string searchText, string bpmText, string keyText, string energyText, string yearText, string notText)
        {
            var harmonicAdvanced = false;
            if (HarmonicAdvancedCheckBox.IsChecked == true)
                harmonicAdvanced = true;

            var search = CreateSearchText(searchText, bpmText, keyText, energyText, yearText, notText);

            var task = new Task(() =>
            {
                SearchingMutex.WaitOne();
                Searching = true;

                var result = new List<Id3TagData>().AsEnumerable();

                try
                {
                    result = SearchEngineService.Search(search, harmonicAdvanced);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }

                if (!AllTagData.TagList.IsEmpty)
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        UpdateMusicDataGrid(result);
                    });
                }


                Searching = false;
                SearchingMutex.ReleaseMutex();
            });

            task.Start();

        }

        private string CreateSearchText(string searchText, string bpmText, string keyText, string energyText, string yearText, string notText, bool relatedEnergy = false)
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
            if (!string.IsNullOrEmpty(notText))
            {
                search.Append(" -Artist:" + notText.Trim() + "*");
                search.Append(" -Title:" + notText.Trim() + "*");
                search.Append(" -Comment:" + notText.Trim() + "*");
            }
            if (!string.IsNullOrEmpty(energyText))
            {
                if (relatedEnergy)
                {
                    //int energy = 0;
                    //int.TryParse(energyText, out energy);

                    //search.Append(" (Energy:" + (energy - 1).ToString() + "starzz*" + " OR " + " Energy:" + (energy + 1).ToString() + "starzz*)");
                }
                else
                {
                    search.Append(" Energy:" + energyText + "starzz*");
                }
            }
            if (!string.IsNullOrEmpty(yearText))
            {
                search.Append(" Year:" + yearText);
            }
            return search.ToString();
        }

        private void UpdateMusicDataGrid(IEnumerable<Id3TagData> data = null)
        {
            data = data ?? AllTagData.TagList;

            this.Dispatcher.Invoke(() =>
            {
                MusicDataGrid.ItemsSource = data;
                ResultCountLabel.Content = data.Count();
                if (_MusicDataSortColumn != null)
                {
                    MusicDataGrid.Items.SortDescriptions.Add(
                        new System.ComponentModel.SortDescription(_MusicDataSortColumn.SortMemberPath, _MusicDataSortColumn.SortDirection ?? System.ComponentModel.ListSortDirection.Ascending));

                }
                MusicDataGrid.Items.Refresh();
            });
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            SearchMusic(SearchBox.Text, BpmSearchBox.Text, KeySearchBox.Text, EnergySearchBox.Text, YearSearchBox.Text, NotSearchBox.Text);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            ClearSearch();
        }

        private void TrakData_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var data = (Id3TagData)((DataGrid)sender).SelectedItem;
                var text = data.Title ?? "";
                text += "  " + data.Artist ?? "";
                text = GetAlphaNumericOnly(text);

                Clipboard.SetData(DataFormats.Text, text);
                AddToPlaylist(data);
            }
            catch
            {
                // do nothing
            }
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

                try
                {

                    AllTagData.TagList = new System.Collections.Concurrent.ConcurrentBag<Id3TagData>();
                    var folder = dialog.FileName;
                    AllTagData.IndexDirectory(folder);

                    Folder2Button.Visibility = Visibility.Hidden;
                    this.MusicDataGrid.ItemsSource = AllTagData.TagList.Cast<Id3TagDataBase>();
                    this.MusicDataGrid.Items.Refresh();
                    this.ResultCountLabel.Content = AllTagData.TagList.Count();

                    var bpmSort = MusicDataGrid.Columns.FirstOrDefault(w => w.Header.ToString() == "BPM");
                    if (bpmSort != null)
                    {
                        _MusicDataSortColumn = bpmSort;
                    }

                    UpdateMusicDataGrid();

                    StyleDataGrid();
                    timer.Stop();
                    var time = timer.ElapsedMilliseconds;
                }
                catch
                {
                    //something went wrong
                }
            }

            FolderButton.IsEnabled = true;
        }

        public void StyleDataGrid()
        {
            var dataGrid = MusicDataGrid;

            var largeColumn = new List<string> { "Comment" };
            var smallColumn = new List<string> { "BPM", "Key", "Energy", "Year", "Track" };
            var normalColumn = new List<string> { "Title", "Artist", "Album", "Publisher", "Remixer", "Genre" };

            var windowSize = this.RenderSize.Width > 600 ? this.RenderSize.Width : 600;

            foreach (DataGridColumn c in dataGrid.Columns)
            {
                var header = c.Header;
                if (largeColumn.Any(w => c.Header.ToString() == w))
                {
                    c.MaxWidth = windowSize * 0.35;
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


                if (c.Header.ToString() == "Comment")
                {
                    c.MinWidth = windowSize * 0.35;
                    c.Width = windowSize * 0.35;
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

        private void AdvancedHarmonics_HotKeyCommand(object sender, ExecutedRoutedEventArgs e)
        {
            if (HarmonicAdvancedCheckBox.IsChecked == false)
                HarmonicAdvancedCheckBox.IsChecked = true;
            else
                HarmonicAdvancedCheckBox.IsChecked = false;
        }


        private void PlayMusic()
        {
            try
            {
                var data = (Id3TagData)MusicDataGrid.SelectedItem;
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


                //this.Dispatcher.Invoke(() =>
                //{
                //    Waveform.Source = null;

                //    Waveform.Source = waveRenderer.DrawNormalizedAudio(data.FullPath, Convert.ToInt32(Waveform.Height), Convert.ToInt32(Waveform.Width));
                //});

            }
            catch (Exception ex)
            {
                logger.Error(ex, "Player Error");
            }
        }

        private void StopMusic()
        {
            try
            {
                _Player.Stop();
                PlayIndicator.IsChecked = false;

                //this.Dispatcher.Invoke(() =>
                //{
                //    Waveform.Source = null;
                //});
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

        private static string GetAlphaNumericOnly(string text)
        {
            //alpha numeric with space
            Regex regex = new Regex(@"[^A-Za-z0-9- ']+"); //regex that matches disallowed text
            return regex.Replace(text, " ");
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

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        public void Dispose()
        {
            //waveRenderer.Dispose();
        }

        private void PlaylistToggle_Click(object sender, RoutedEventArgs e)
        {
            if (PlaylistData.Visibility == Visibility.Collapsed)
                PlaylistData.Visibility = Visibility.Visible;
            else
                PlaylistData.Visibility = Visibility.Collapsed;
        }

        private void Playlist_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {
            var allow = new List<string> { "Title", "Artist", "Energy", "Key", "BPM", "Comment"};

            if (!allow.Contains(e.Column.Header as string))
            {
                e.Cancel = true;
            }
            else
            {
                if (e.Column.Header.ToString() == "Comment")
                {
                    e.Column.MaxWidth = 400;
                    e.Column.Width = 400;
                }
                else if (e.Column.Header.ToString() == "Artist" || e.Column.Header.ToString() == "Title")
                {
                    e.Column.Width = 100;
                    e.Column.MaxWidth = 100;
                }
                else
                {
                    e.Column.MaxWidth = 100;
                }

            }
        }
    }
}