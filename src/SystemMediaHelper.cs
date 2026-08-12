using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Foundation;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace WidgUI
{
    public class MediaState
    {
        public bool HasSession { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public BitmapSource AlbumArt { get; set; }
        public bool IsPlaying { get; set; }
        public TimeSpan Position { get; set; }
        public TimeSpan Duration { get; set; }
        public bool CanPlayPause { get; set; }
        public bool CanSkipNext { get; set; }
        public bool CanSkipPrevious { get; set; }
        public bool CanSeek { get; set; }

        public static MediaState Empty()
        {
            return new MediaState
            {
                HasSession = false,
                Title = "Sin reproduccion",
                Artist = "Reproduce musica en Spotify, YouTube, etc.",
                IsPlaying = false,
                Position = TimeSpan.Zero,
                Duration = TimeSpan.Zero
            };
        }
    }

    public class SystemMediaHelper
    {
        public event Action<MediaState> StateChanged;

        private readonly Dispatcher _dispatcher;
        private GlobalSystemMediaTransportControlsSessionManager _manager;
        private GlobalSystemMediaTransportControlsSession _session;
        private DispatcherTimer _pollTimer;
        private int _thumbnailRequestId;
        private string _lastTitle;
        private string _lastArtist;
        private int _pollCount;
        private bool _propertiesPending;

        public SystemMediaHelper(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public void Initialize()
        {
            IAsyncOperation<GlobalSystemMediaTransportControlsSessionManager> operation =
                GlobalSystemMediaTransportControlsSessionManager.RequestAsync();

            operation.Completed = (asyncInfo, asyncStatus) =>
            {
                if (asyncStatus != AsyncStatus.Completed)
                {
                    PublishState(MediaState.Empty());
                    return;
                }

                try
                {
                    _manager = asyncInfo.GetResults();
                    _dispatcher.BeginInvoke(new Action(StartPolling));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("SystemMediaHelper init: " + ex.Message);
                    PublishState(MediaState.Empty());
                }
            };
        }

        private void StartPolling()
        {
            if (_pollTimer != null)
            {
                return;
            }

            _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
            _pollTimer.Tick += PollTimer_Tick;
            _pollTimer.Start();
            RefreshState(true);
        }

        public void Dispose()
        {
            if (_pollTimer != null)
            {
                _pollTimer.Stop();
                _pollTimer = null;
            }

            _manager = null;
            _session = null;
        }

        public void TogglePlayPause()
        {
            if (_session == null)
            {
                return;
            }

            try
            {
                _session.TryTogglePlayPauseAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("TogglePlayPause: " + ex.Message);
            }
        }

        public void SkipNext()
        {
            if (_session == null)
            {
                return;
            }

            try
            {
                _session.TrySkipNextAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("SkipNext: " + ex.Message);
            }
        }

        public void SkipPrevious()
        {
            if (_session == null)
            {
                return;
            }

            try
            {
                _session.TrySkipPreviousAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("SkipPrevious: " + ex.Message);
            }
        }

        public void SeekToPercent(double percent)
        {
            if (_session == null)
            {
                return;
            }

            try
            {
                GlobalSystemMediaTransportControlsSessionTimelineProperties timeline =
                    _session.GetTimelineProperties();

                if (timeline.EndTime.TotalMilliseconds <= 0)
                {
                    return;
                }

                double clamped = Math.Max(0, Math.Min(1, percent));
                long targetMs = (long)(timeline.EndTime.TotalMilliseconds * clamped);
                _session.TryChangePlaybackPositionAsync(targetMs);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("SeekToPercent: " + ex.Message);
            }
        }

        private void PollTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                RefreshState(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("PollTimer_Tick: " + ex.Message);
            }
        }

        private void RefreshState(bool forceProperties)
        {
            try
            {
                RefreshStateCore(forceProperties);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("RefreshState: " + ex.Message);
            }
        }

        private void RefreshStateCore(bool forceProperties)
        {
            if (_manager == null)
            {
                PublishState(MediaState.Empty());
                return;
            }

            _session = _manager.GetCurrentSession();
            if (_session == null)
            {
                _lastTitle = null;
                _lastArtist = null;
                PublishState(MediaState.Empty());
                return;
            }

            MediaState state = BuildTimelineState();
            GlobalSystemMediaTransportControlsSessionPlaybackInfo playbackInfo = _session.GetPlaybackInfo();

            if (playbackInfo != null)
            {
                state.IsPlaying = playbackInfo.PlaybackStatus ==
                    GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

                if (playbackInfo.Controls != null)
                {
                    state.CanPlayPause = playbackInfo.Controls.IsPlayPauseToggleEnabled;
                    state.CanSkipNext = playbackInfo.Controls.IsNextEnabled;
                    state.CanSkipPrevious = playbackInfo.Controls.IsPreviousEnabled;
                    state.CanSeek = playbackInfo.Controls.IsPlaybackPositionEnabled;
                }
            }

            state.Title = _lastTitle;
            state.Artist = _lastArtist;
            PublishState(state);

            _pollCount++;
            if (forceProperties || _lastTitle == null || _pollCount % 6 == 0)
            {
                RequestMediaProperties(forceProperties);
            }
        }

        private MediaState BuildTimelineState()
        {
            MediaState state = new MediaState { HasSession = true };

            try
            {
                GlobalSystemMediaTransportControlsSessionTimelineProperties timeline =
                    _session.GetTimelineProperties();

                state.Position = timeline.Position;
                state.Duration = timeline.EndTime;
            }
            catch
            {
                state.Position = TimeSpan.Zero;
                state.Duration = TimeSpan.Zero;
            }

            return state;
        }

        private void RequestMediaProperties(bool forceReload)
        {
            if (_session == null || _propertiesPending)
            {
                return;
            }

            _propertiesPending = true;
            int requestId = ++_thumbnailRequestId;

            IAsyncOperation<GlobalSystemMediaTransportControlsSessionMediaProperties> operation =
                _session.TryGetMediaPropertiesAsync();

            operation.Completed = (asyncInfo, asyncStatus) =>
            {
                _propertiesPending = false;

                if (asyncStatus != AsyncStatus.Completed || requestId != _thumbnailRequestId)
                {
                    return;
                }

                try
                {
                    GlobalSystemMediaTransportControlsSessionMediaProperties properties =
                        asyncInfo.GetResults();

                    if (properties == null)
                    {
                        return;
                    }

                    string title = string.IsNullOrWhiteSpace(properties.Title) ? "Sin titulo" : properties.Title;
                    string artist = string.IsNullOrWhiteSpace(properties.Artist)
                        ? (string.IsNullOrWhiteSpace(properties.AlbumArtist) ? "Artista desconocido" : properties.AlbumArtist)
                        : properties.Artist;

                    bool metadataChanged = forceReload || title != _lastTitle || artist != _lastArtist;
                    _lastTitle = title;
                    _lastArtist = artist;

                    MediaState state = BuildTimelineState();
                    state.Title = title;
                    state.Artist = artist;

                    GlobalSystemMediaTransportControlsSessionPlaybackInfo playbackInfo = _session.GetPlaybackInfo();
                    if (playbackInfo != null)
                    {
                        state.IsPlaying = playbackInfo.PlaybackStatus ==
                            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

                        if (playbackInfo.Controls != null)
                        {
                            state.CanPlayPause = playbackInfo.Controls.IsPlayPauseToggleEnabled;
                            state.CanSkipNext = playbackInfo.Controls.IsNextEnabled;
                            state.CanSkipPrevious = playbackInfo.Controls.IsPreviousEnabled;
                            state.CanSeek = playbackInfo.Controls.IsPlaybackPositionEnabled;
                        }
                    }

                    if (properties.Thumbnail == null || !metadataChanged)
                    {
                        PublishState(state);
                        return;
                    }

                    IAsyncOperation<IRandomAccessStreamWithContentType> thumbOperation =
                        properties.Thumbnail.OpenReadAsync();

                    thumbOperation.Completed = (thumbInfo, thumbStatus) =>
                    {
                        if (thumbStatus != AsyncStatus.Completed || requestId != _thumbnailRequestId)
                        {
                            PublishState(state);
                            return;
                        }

                        try
                        {
                            byte[] imageBytes;
                            using (Stream stream = thumbInfo.GetResults().AsStreamForRead())
                            {
                                imageBytes = ReadAllBytes(stream);
                            }

                            _dispatcher.BeginInvoke(new Action(() =>
                            {
                                if (requestId != _thumbnailRequestId)
                                {
                                    return;
                                }

                                try
                                {
                                    BitmapImage image = new BitmapImage();
                                    image.BeginInit();
                                    image.CacheOption = BitmapCacheOption.OnLoad;
                                    image.StreamSource = new MemoryStream(imageBytes);
                                    image.EndInit();
                                    image.Freeze();
                                    state.AlbumArt = image;
                                }
                                catch
                                {
                                    state.AlbumArt = null;
                                }

                                PublishState(state);
                            }));
                        }
                        catch
                        {
                            state.AlbumArt = null;
                            PublishState(state);
                        }
                    };
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("RequestMediaProperties: " + ex.Message);
                }
            };
        }

        private void PublishState(MediaState state)
        {
            if (!_dispatcher.CheckAccess())
            {
                _dispatcher.BeginInvoke(new Action(() => PublishState(state)));
                return;
            }

            Action<MediaState> handler = StateChanged;
            if (handler != null)
            {
                handler(state);
            }
        }

        private static byte[] ReadAllBytes(Stream stream)
        {
            using (MemoryStream buffer = new MemoryStream())
            {
                stream.CopyTo(buffer);
                return buffer.ToArray();
            }
        }
    }
}
