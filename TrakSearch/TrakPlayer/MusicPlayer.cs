﻿using System;
using System.ComponentModel;
using CSCore;
using CSCore.Codecs;
using CSCore.CoreAudioAPI;
using CSCore.SoundOut;


namespace Shravan.DJ.TrakPlayer
{
	public class MusicPlayer : IDisposable
	{
		private ISoundOut _soundOut;
		private IWaveSource _waveSource;

		public event EventHandler<PlaybackStoppedEventArgs> PlaybackStopped;

		public static MMDevice GetDefaultRenderDevice()
		{
			using (var enumerator = new MMDeviceEnumerator())
			{
				return enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
			}
		}

		public static MMDevice GetPlaybackDevice()
		{
			using (var enumerator = new MMDeviceEnumerator())
			{
				var data = enumerator.EnumAudioEndpoints(DataFlow.Render, DeviceState.Active);
				return data[5];
			}
		}

		public PlaybackState PlaybackState
		{
			get
			{
				if (_soundOut != null)
					return _soundOut.PlaybackState;
				return PlaybackState.Stopped;
			}
		}

		public TimeSpan Position
		{
			get
			{
				if (_waveSource != null)
					return _waveSource.GetPosition();
				return TimeSpan.Zero;
			}
			set
			{
				if (_waveSource != null)
				{
					if (value > TimeSpan.Zero)
					{

						_waveSource.SetPosition(value);
					}
					else
					{
						_waveSource.SetPosition(TimeSpan.Zero);
					}
				}

			}
		}

		public TimeSpan Length
		{
			get
			{
				if (_waveSource != null)
					return _waveSource.GetLength();
				return TimeSpan.Zero;
			}
		}

		public int Volume
		{
			get
			{
				if (_soundOut != null)
					return Math.Min(90, Math.Max((int)(_soundOut.Volume * 100), 0));
				return 90;
			}
			set
			{
				if (_soundOut != null)
				{
					_soundOut.Volume = Math.Min(0.9f, Math.Max(value / 100f, 0f));
				}
			}
		}

		public void Open(string filename, MMDevice device)
		{
			CleanupPlayback();

			_waveSource =
				 CodecFactory.Instance.GetCodec(filename)
					  .ToSampleSource()
					  .ToStereo()
					  .ToWaveSource();
			_soundOut = new WasapiOut() { Latency = 100, Device = device };
			_soundOut.Initialize(_waveSource);
			if (PlaybackStopped != null)
				_soundOut.Stopped += PlaybackStopped;
		}

		public void Play()
		{
			if (_soundOut != null)
				_soundOut.Play();
		}

		public void Pause()
		{
			if (_soundOut != null)
				_soundOut.Pause();
		}

		public void Stop()
		{
			if (_soundOut != null)
				_soundOut.Stop();
		}

		private void CleanupPlayback()
		{
			if (_soundOut != null)
			{
				_soundOut.Dispose();
				_soundOut = null;
			}
			if (_waveSource != null)
			{
				_waveSource.Dispose();
				_waveSource = null;
			}
		}


		public void Dispose()
		{
			CleanupPlayback();
		}
	}
}
