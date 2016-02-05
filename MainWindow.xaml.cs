using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using NAudio.Wave;
using Service;
using Timer = System.Timers.Timer;
using Newtonsoft.Json;
using Objects;

namespace StreamingClient
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private readonly Timer _t = new Timer();
        private float _volume = .5f;
        private BufferedWaveProvider _bufferedWaveProvider;
        private volatile bool _fullyDownloaded;
        private volatile StreamingPlaybackState _playbackState;
        private PlayBy _playByCurrent = PlayBy.Local;
        private Stream _s;
        private DateTime _started;
        private readonly Timer _timer1;
        private VolumeWaveProvider16 _volumeProvider;
        private IWavePlayer _waveOut;
        private HttpWebRequest _webRequest;
        private ServiceRunner _sr;
        private bool _isConnectedToServer = false;
        private string _currentDirectory = "";
        private bool _areDetailsSet = false;
        private Playlist _currentPlaylist;
        private List<Playlist> _playlists;
        private WebClient c;
        private bool _playlistRetrieved;

        public MainWindow()
        {
            GetAdminRights();
            _timer1 = new Timer();
            InitializeComponent();
            Closed += OnClosed;
            _timer1.Interval = 250;
            _timer1.Elapsed += timer1_Tick;
            rdoWeb.IsChecked = true;
            LoadSettings();
            c = new WebClient();
        }

        private void GetAdminRights()
        {
            if (!IsRunAsAdministrator())
            {
                var processInfo = new ProcessStartInfo(Assembly.GetExecutingAssembly().CodeBase)
                {
                    UseShellExecute = true,
                    Verb = "runas"
                };

                try
                {
                    Process.Start(processInfo);
                }
                catch (Exception)
                {
                    // The user did not allow the application to run as administrator
                    MessageBox.Show("Sorry, this application must be run as Administrator.");
                }

                // Shut down the current process
                Application.Current.Shutdown();
            }
        }

        private bool IsRunAsAdministrator()
        {
            var wi = WindowsIdentity.GetCurrent();
            var wp = new WindowsPrincipal(wi);

            return wp.IsInRole(WindowsBuiltInRole.Administrator);
        }


        private void LoadSettings()
        {
            txtIPAddress.Text = Properties.Settings.Default.ServerAddress;
            txtPort.Text = Properties.Settings.Default.ServerPort.ToString();
        }

        private bool IsBufferNearlyFull => _bufferedWaveProvider != null &&
                                           _bufferedWaveProvider.BufferLength - _bufferedWaveProvider.BufferedBytes
                                           < _bufferedWaveProvider.WaveFormat.AverageBytesPerSecond / 4;

        private void OnClosed(object sender, EventArgs eventArgs)
        {
            StopPlayback();
            if (_sr != null)
                _sr.StopService();
        }

        private void btnStartServer_Click(object sender, RoutedEventArgs e)
        {
            ThreadPool.QueueUserWorkItem(async (x) =>
            {
                Dispatcher.Invoke(() => btnStartServer.IsEnabled = false);
                Dispatcher.Invoke(() => btnStopServer.IsEnabled = true);
                _sr = new ServiceRunner();
                Dispatcher.Invoke(() => txtServerStatus.Text = "Finding and configuring your router...");
                await _sr.DiscoverDevices();
                Dispatcher.Invoke(() => txtServerStatus.Text = "Getting your external IP Address...");
                await _sr.GetExternalIP();
                Dispatcher.Invoke(() => txtServerStatus.Text = "Making sure port forwarding is enabled...");
                await _sr.CheckIfPortForwardingExists();
                Dispatcher.Invoke(() => "Requesting firewall exception...");
                await _sr.RequestFirewallRule();
                Dispatcher.Invoke(() => txtServerStatus.Text = "Starting Web Host...");
                string IP = _sr.StartHost();
                _isConnectedToServer = true;
                Dispatcher.Invoke(() => txtIP.Text = "IP Address is: " + IP);
                var address = IP.Split(':');
                Properties.Settings.Default.ServerAddress = address[0];
                Properties.Settings.Default.ServerPort = int.Parse(address[1]);
                Properties.Settings.Default.Save();
                _started = DateTime.Now;
                _t.Interval = 1000;
                _t.Elapsed += Elapsed_Trigger;
                _t.Start();
            });
        }


        private void Elapsed_Trigger(object o, ElapsedEventArgs args)
        {
            var s = args.SignalTime - _started;
            var x = s.ToString(@"hh\:mm\:ss");
            Dispatcher.Invoke(() => { txtServerStatus.Text = $"Server is running and has been for {x}"; });
        }

        private void btnStopServer_Click(object sender, RoutedEventArgs e)
        {
            _t.Stop();
            _sr.StopService();
            txtServerStatus.Text = "";
            btnStartServer.IsEnabled = true;
            btnStopServer.IsEnabled = false;
        }

        private void Mp3Streamer_Closing(object sender, CancelEventArgs e)
        {
            _sr?.StopService();
        }

        private void timer1_Tick(object sender, ElapsedEventArgs e)
        {
            if (_playbackState == StreamingPlaybackState.Stopped) return;

            if (_waveOut == null && _bufferedWaveProvider != null)
            {
                Debug.WriteLine("Creating WaveOut Device");
                _waveOut = CreateWaveOut();
                _waveOut.PlaybackStopped += OnPlaybackStopped;
                _volumeProvider = new VolumeWaveProvider16(_bufferedWaveProvider) { Volume = _volume };
                _waveOut.Init(_volumeProvider);
                Dispatcher.Invoke(
                    (Action)
                        (() => progressBarBuffer.Maximum = (int)_bufferedWaveProvider.BufferDuration.TotalMilliseconds));
            }
            else if (_bufferedWaveProvider != null)
            {
                var bufferedSeconds = _bufferedWaveProvider.BufferedDuration.TotalSeconds;
                ShowBufferState(bufferedSeconds);
                // make it stutter less if we buffer up a decent amount before playing
                if (bufferedSeconds < 0.5 && _playbackState == StreamingPlaybackState.Playing && !_fullyDownloaded)
                {
                    Pause();
                }
                else if (bufferedSeconds > 4 && _playbackState == StreamingPlaybackState.Buffering)
                {
                    Play();
                }
                else if (_fullyDownloaded && bufferedSeconds == 0)
                {
                    Debug.WriteLine("Reached end of stream");
                    StopPlayback();
                }
            }
        }

        private void btnPlay_Click(object sender, RoutedEventArgs e)
        {
            switch (_playbackState)
            {
                case StreamingPlaybackState.Stopped:
                    _playbackState = StreamingPlaybackState.Buffering;
                    _bufferedWaveProvider = null;
                    string url = rdoLocal.IsChecked.Value
                        ? txtNowPlaying.Text
                        : GetBaseAddress() + $"GetFile/{txtNowPlaying.Text}".Replace("\\", "|");

                    GetDetails();

                    ThreadPool.QueueUserWorkItem(StreamMp3, url);
                    _timer1.Enabled = true;
                    break;
                case StreamingPlaybackState.Paused:
                    _playbackState = StreamingPlaybackState.Buffering;
                    break;
            }
        }


        private async void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            //if (!TestConnection())
           // {
                // give user an error. 
            //    return;
           // }
            List<string> entries = await GetDirectoryList();
            FillListBox(entries);
            _areDetailsSet = false;
            //_playlists = GetAllPlaylists();
        }

        private async Task<List<Playlist>> GetAllPlaylists()
        {
            string url = GetBaseAddress() + "GetPlaylists";
            if (TestConnection())
                return await MakeJSSONCall<List<Playlist>>(url);
            
            return new List<Playlist>();
        }

        private bool TestConnection()
        {
            IPAddress add;
            if (!IPAddress.TryParse(txtIPAddress.Text, out add))
            {
                return false;
            }
            int port;
            if (!int.TryParse(txtPort.Text, out port))
            {
                return false;
            }

            if (Ping($"http://{txtIPAddress.Text}:{txtPort.Text}/AudioStream/GetBaseDirectoryList"))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private bool Ping(string url)
        {
            try
            {
                WebClient c = new WebClient();
                c.DownloadString(url);

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Timeout = 10000;
                request.AllowAutoRedirect = false; // find out if this site is up and don't follow a redirector
                request.Method = "GET";

                using (var response = request.GetResponse())
                {
                    return true;
                }
            }
            catch (Exception)
            {
                FillListBox(new List<string> { "Server is not connected!" });
                return false;
            }
        }

        private void FillListBox(List<string> entries)
        {
            if (entries != null && entries.Any())
            {
                lbExplorer.Visibility = Visibility.Visible;
                lbExplorer.Items.Clear();
                foreach (var entry in entries)
                {
                    lbExplorer.Items.Add(entry);
                }
            }
        }

        private async Task<T> MakeJSSONCall<T>(string callURL)
        {
            Uri u = new Uri(callURL);
            var e = await c.DownloadStringTaskAsync(u);
            

            var ret = JsonConvert.DeserializeObject<T>(e);

            return ret;
        }

        private async Task<List<string>> GetDirectoryList()
        {
            return
                await MakeJSSONCall<List<string>>(GetBaseAddress() + "GetBaseDirectoryList");
        }

        private string GetBaseAddress()
        {
            return $"http://{txtIPAddress.Text}:{txtPort.Text}/AudioStream/";
        }

        private async Task<List<string>> GetDirectoryList(string path)
        {
            return
                await MakeJSSONCall<List<string>>(GetBaseAddress() + $"GetDirectoryList/{path}");
        }

        private void btnPause_Click(object sender, RoutedEventArgs e)
        {
            if (_playbackState != StreamingPlaybackState.Playing && _playbackState != StreamingPlaybackState.Buffering)
                return;

            _waveOut.Pause();
            Debug.WriteLine("User requested Pause, waveOut.PlaybackState={0}", _waveOut.PlaybackState);
            _playbackState = StreamingPlaybackState.Paused;
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            StopPlayback();
        }

        private void GetDetails()
        {
            if (_areDetailsSet) return;

            GetSongDetails();

            GetArtwork();

            _areDetailsSet = true;
        }

        private void GetSongDetails()
        {
            string detailsURL = GetBaseAddress() + $"Metadata/{txtNowPlaying.Text}".Replace("\\", "|");

            
            string URI = detailsURL;
            var e = c.DownloadString(URI);

            var list = JsonConvert.DeserializeObject<List<KeyValuePair<string, string>>>(e);
            Dictionary<string, string> details = new Dictionary<string, string>();

            foreach (var keyValuePair in list)
            {
                details.Add(keyValuePair.Key, keyValuePair.Value);
            }


            if (details != null)
            {
                StringBuilder sb = new StringBuilder();
                foreach (var detail in details)
                {
                    sb.Append(detail.Key + " : " + detail.Value);
                    sb.Append(Environment.NewLine);
                }
                lblDetails.Content = sb.ToString();
            }
        }

        public void GetArtwork()
        {
            string artUrl = GetBaseAddress() + $"GetAlbumArtwork/{txtNowPlaying.Text}".Replace("\\", "|");
            var artwork = GetArtwork(artUrl);
            if (artwork != null)
            {
                var imageSource = new BitmapImage();
                imageSource.BeginInit();
                imageSource.StreamSource = artwork;
                imageSource.EndInit();

                imgAlbum.Source = imageSource;
            }
        }

        private Stream GetArtwork(string songPath)
        {

            HttpWebResponse resp = null;
            var url = songPath;

            _webRequest = (HttpWebRequest)WebRequest.Create(url);

            try
            {
                resp = (HttpWebResponse)_webRequest.GetResponse();
            }
            catch (WebException e)
            {
                if (e.Status != WebExceptionStatus.RequestCanceled)
                {
                    Console.WriteLine(e.Message);
                }
                return null;
            }
            return resp.GetResponseStream();
        }

        #region NAudio stuff

        private void StreamMp3(object state)
        {

            HttpWebResponse resp = null;
            _fullyDownloaded = false;
            var url = (string)state;
            if (_playByCurrent == PlayBy.Web)
            {
                _webRequest = (HttpWebRequest)WebRequest.Create(url);

                try
                {
                    resp = (HttpWebResponse)_webRequest.GetResponse();
                }
                catch (WebException e)
                {
                    if (e.Status != WebExceptionStatus.RequestCanceled)
                    {
                        Console.WriteLine(e.Message);
                    }
                    return;
                }
            }

            var buffer = new byte[16384 * 4]; // needs to be big enough to hold a decompressed frame

            IMp3FrameDecompressor decompressor = null;
            try
            {
                using (var responseStream = resp.GetResponseStream())
                //using (_s)
                {
                    var readFullyStream = new ReadFullyStream(responseStream);
                    do
                    {
                        if (IsBufferNearlyFull)
                        {
                            Debug.WriteLine("Buffer getting full, taking a break");
                            Thread.Sleep(500);
                        }
                        else
                        {
                            Mp3Frame frame;
                            try
                            {
                                frame = Mp3Frame.LoadFromStream(readFullyStream);
                            }
                            catch (EndOfStreamException)
                            {
                                _fullyDownloaded = true;
                                // reached the end of the MP3 file / stream
                                break;
                            }
                            catch (Exception e)
                            {
                                // probably we have aborted download from the GUI thread
                                Console.WriteLine(e);
                                break;
                            }
                            if (decompressor == null)
                            {
                                // don't think these details matter too much - just help ACM select the right codec
                                // however, the buffered provider doesn't know what sample rate it is working at
                                // until we have a frame
                                decompressor = CreateFrameDecompressor(frame);
                                _bufferedWaveProvider = new BufferedWaveProvider(decompressor.OutputFormat)
                                {
                                    BufferDuration = TimeSpan.FromSeconds(20)
                                };
                                // allow us to get well ahead of ourselves
                                //this.bufferedWaveProvider.BufferedDuration = 250;
                            }
                            var decompressed = decompressor.DecompressFrame(frame, buffer, 0);
                            //Debug.WriteLine(String.Format("Decompressed a frame {0}", decompressed));
                            _bufferedWaveProvider.AddSamples(buffer, 0, decompressed);
                        }
                    } while (_playbackState != StreamingPlaybackState.Stopped);
                    Debug.WriteLine("Exiting");
                    // was doing this in a finally block, but for some reason
                    // we are hanging on response stream .Dispose so never get there
                    decompressor?.Dispose();
                }
            }
            finally
            {
                decompressor?.Dispose();
            }
        }

        private static IMp3FrameDecompressor CreateFrameDecompressor(Mp3Frame frame)
        {
            WaveFormat waveFormat = new Mp3WaveFormat(frame.SampleRate, frame.ChannelMode == ChannelMode.Mono ? 1 : 2,
                frame.FrameLength, frame.BitRate);
            return new AcmMp3FrameDecompressor(waveFormat);
        }

        private void StopPlayback()
        {
            if (_playbackState != StreamingPlaybackState.Stopped)
            {
                if (!_fullyDownloaded)
                {
                    if (_playByCurrent == PlayBy.Web)
                        _webRequest.Abort();
                    else
                        _s.Close();
                }

                _playbackState = StreamingPlaybackState.Stopped;
                if (_waveOut != null)
                {
                    _waveOut.Stop();
                    _waveOut.Dispose();
                    _waveOut = null;
                }
                _timer1.Enabled = false;
                // n.b. streaming thread may not yet have exited
                Thread.Sleep(500);
                ShowBufferState(0);
            }
        }

        private void ShowBufferState(double totalSeconds)
        {
            Dispatcher.Invoke(() =>
            {
                lblBuffered.Content = $"{totalSeconds:0.0}s";
                progressBarBuffer.Value = (int)(totalSeconds * 1000);
            })
                ;
        }

        private void ShowPlayTime(double seconds)
        {
            Dispatcher.Invoke(() =>
            {
                pBarTime.Value = (int)(seconds * 1000);
            })
               ;
        }

        private void Play()
        {
            _waveOut.Play();
            Debug.WriteLine("Started playing, waveOut.PlaybackState={0}", _waveOut.PlaybackState);
            _playbackState = StreamingPlaybackState.Playing;
        }

        private void Pause()
        {
            _playbackState = StreamingPlaybackState.Buffering;
            _waveOut.Pause();
            Debug.WriteLine("Paused to buffer, waveOut.PlaybackState={0}", _waveOut.PlaybackState);
        }

        private IWavePlayer CreateWaveOut()
        {
            return new WaveOut();
        }

        #endregion

        private void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            Debug.WriteLine("Playback Stopped");
            if (e.Exception != null)
            {
                MessageBox.Show($"Playback Error {e.Exception.Message}");
            }
        }

        private void rdoPlaylist_Checked(object sender, RoutedEventArgs e)
        {
            _playByCurrent = PlayBy.Playlist;
            if (_isConnectedToServer)
            {
                _sr.StopService();
            }

            txtIPAddress.Visibility = Visibility.Hidden;
            txtPort.Visibility = Visibility.Hidden;
            lblIP.Visibility = Visibility.Hidden;
            lblIP_Copy.Visibility = Visibility.Hidden;
            btnConnect.Visibility = Visibility.Hidden;
            lbExplorer.Visibility = Visibility.Hidden;
        }

        private void rdoWeb_Checked(object sender, RoutedEventArgs e)
        {
            _playByCurrent = PlayBy.Web;
            if (!_isConnectedToServer)
            {
                btnConnect.Focus();
            }
            txtIPAddress.Visibility = Visibility.Visible;
            txtPort.Visibility = Visibility.Visible;
            lblIP.Visibility = Visibility.Visible;
            lblIP_Copy.Visibility = Visibility.Visible;
            btnConnect.Visibility = Visibility.Visible;
        }

        private void rdoLocal_Checked(object sender, RoutedEventArgs e)
        {
            _playByCurrent = PlayBy.Local;
            if (_isConnectedToServer)
            {
                _sr.StopService();
            }
            txtIPAddress.Visibility = Visibility.Hidden;
            txtPort.Visibility = Visibility.Hidden;
            lblIP.Visibility = Visibility.Hidden;
            lblIP_Copy.Visibility = Visibility.Hidden;
            btnConnect.Visibility = Visibility.Hidden;
            lbExplorer.Visibility = Visibility.Hidden;
        }

        private void sldVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            _volume = (float)e.NewValue;
            if (_volumeProvider != null)
                _volumeProvider.Volume = _volume;
        }

        private enum PlayBy
        {
            Web,
            Local,
            Playlist
        }

        private enum StreamingPlaybackState
        {
            Stopped,
            Playing,
            Buffering,
            Paused
        }

        private async void TabItem_GotFocus(object sender, RoutedEventArgs e)
        {
            if (_playlistRetrieved) return;
            _playlists = await GetAllPlaylists();

            var x = _playlists.Select(d => d.FriendlyName);
            
            lbPlaylists.ItemsSource = x;
            _playlistRetrieved = true;
        }


        private void txtIPAddress_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtIPAddress.Text != "IP")
            {
                Properties.Settings.Default.ServerAddress = txtIPAddress.Text;
                Properties.Settings.Default.Save();
            }
        }

        private void txtPort_TextChanged(object sender, TextChangedEventArgs e)
        {
            int portNumber;
            int.TryParse(txtPort.Text, out portNumber);
            if (portNumber != 0)
            {
                Properties.Settings.Default.ServerPort = portNumber;
                Properties.Settings.Default.Save();
            }
        }

        private async void lbExplorer_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                if (e.AddedItems[0].ToString().EndsWith("mp3"))
                {
                    txtNowPlaying.Text = Path.Combine(_currentDirectory, e.AddedItems[0].ToString());
                    ButtonAutomationPeer peer =
                        new ButtonAutomationPeer(btnPlay);
                    IInvokeProvider invokeProv =
                      peer.GetPattern(PatternInterface.Invoke)
                      as IInvokeProvider;
                    invokeProv.Invoke();
                    GetDetails();
                }
                else
                {
                    _currentDirectory = e.AddedItems[0].ToString();
                    _currentDirectory = _currentDirectory.Replace("\\", "|");
                    var entries = await GetDirectoryList(_currentDirectory);
                    _currentDirectory = _currentDirectory.Replace("|", "\\");
                    FillListBox(entries);
                }
            }
        }

        private void lbPlaylists_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _currentPlaylist = _playlists.First(x => x.FriendlyName == e.AddedItems[0].ToString());
        }

        private void lbPlaylists_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            //e.Handled = true;
            //var item = ItemsControl.ContainerFromElement(lbPlaylists, e.OriginalSource as DependencyObject) as ListBoxItem;
            //if (item != null)
            //{
            //    var p = item.Content;
            //    _currentPlaylist = _playlists.First(x => x.FriendlyName == p);
                
            //    //string URI = GetBaseAddress() + $"GetPlaylistData/{p}";
            //    string URI = GetBaseAddress() + $"GetPlaylists/{p}";
            //    var file = c.DownloadString(URI);
            //    _currentPlaylist.PlaylistData = new DataTable();
            //    _currentPlaylist.PlaylistData.TableName = p.ToString();
            //    JsonConvert.DeserializeObject<Playlist>(file);
                

            //   // File.WriteAll(@"C:\Temp\XMLTest.xml",file);

            //   // var d = new DataSet();
            //   // d.Tables.Add(_currentPlaylist.PlaylistData);
            //   //d.ReadXml(new MemoryStream(file));
            //   // dgvPlaylist.DataContext = _currentPlaylist.PlaylistData;

            //}
            
        }

        private void button_Click(object sender, RoutedEventArgs e)
        {
           e.Handled = true;
            var item = ItemsControl.ContainerFromElement(lbPlaylists, e.OriginalSource as DependencyObject) as ListBoxItem;
            if (item == null)
            {
               
                var p = _playlists.First().FriendlyName;
                _currentPlaylist = _playlists.First(x => x.FriendlyName == p);

                //string URI = GetBaseAddress() + $"GetPlaylistData/{p}";
                string URI = GetBaseAddress() + $"GetPlaylists/{p}";
                try
                {
                    var file = c.DownloadString(URI);
                    var list = JsonConvert.DeserializeObject<Playlist>(file);
                    _currentPlaylist = list;
                    dgvPlaylist.ItemsSource = list.Songs;
                    StyleGrid();
                }
                catch (Exception ex)
                {

                    Console.WriteLine(ex.Message);
                }
                
                


                // File.WriteAll(@"C:\Temp\XMLTest.xml",file);

                // var d = new DataSet();
                // d.Tables.Add(_currentPlaylist.PlaylistData);
                //d.ReadXml(new MemoryStream(file));
                // dgvPlaylist.DataContext = _currentPlaylist.PlaylistData;

            }
        }

        private void StyleGrid()
        {
            foreach (var dataGridColumn in dgvPlaylist.Columns)
            {
                var s = (string) dataGridColumn.Header;
                if (s == "StringLength" || s == "Path")
                {
                    dataGridColumn.Visibility = Visibility.Hidden;
                }
            }
        }

        private void rdoArtists_Checked(object sender, RoutedEventArgs e)
        {
            var sf = _currentPlaylist?.Songs.Select(s => s.Artist).Distinct();

            List<StringValue> strings = new List<StringValue>();

            foreach (var v in sf)
            {
                strings.Add(new StringValue(v));
            }
            dgvPlaylist.ItemsSource = strings;
            //dgvPlaylist.Items.Refresh();
            //dgvPlaylist.Columns.Add(new DataGridTextColumn {Header = "Artist"});
            //foreach (var w in sf)
            //{
            //    dgvPlaylist.Items.Add(w);
            //}
            //dgvPlaylist.ItemsSource = sf;
            //dgvPlaylist.Items.Refresh();
        }

        private void dgvPlaylist_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (rdoArtists.IsChecked.Value)
            {
                var w = _currentPlaylist.Songs.Where(s => s.Artist == ((StringValue)((DataGrid) sender).CurrentItem).stringValue);
                dgvPlaylist.ItemsSource = w;
                StyleGrid();
            }
        }
    }
}