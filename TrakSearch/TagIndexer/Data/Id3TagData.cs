using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TagLib;
using TagLib.Id3v2;

namespace Shravan.DJ.TagIndexer.Data
{
	public class Id3TagData //: TagLib.Id3v2.Tag
	{
		/// <summary>
		/// ExpandoObject
		/// </summary>
		protected dynamic _innerData;

		public string Title { get; private set; }
		public string Artist { get; private set; }
		public string Key { get; private set; }
		public string Energy { get; private set; }
		public string BPM { get; private set; }
		public string Comment { get; private set; }
		public string Album { get; private set; }
		public string FullPath { get; private set; }

		[System.ComponentModel.Bindable(false)]
		[System.ComponentModel.Browsable(false)]
		public dynamic Data { get { return _innerData; } set { value = _innerData; } }

		[System.ComponentModel.Bindable(false)]
		[System.ComponentModel.Browsable(false)]
		public string Index { get; private set; }

		public static List<string> Fields { get; } = new List<string>()
		{
			"Title",
			"Artist",
			"Comment",
			"BPM",
			"Key",
			"Energy",
			"Album"
		};




		public Id3TagData(string fullPath, TagLib.Id3v2.Tag metaData)
		{
			FullPath = fullPath;

			PopulateFields(metaData, null);
		}


		public Id3TagData(string fullPath, IDictionary<string, object> dic)
		{
			FullPath = fullPath;
			dynamic data = dic;
			PopulateFields(null, dic);

			_innerData = data;
		}

		private void PopulateFields(TagLib.Id3v2.Tag metaData, dynamic data)
		{

			try
			{
				if (metaData != null)
				{
					data = new ExpandoObject();
					data.Title = metaData.Title;
					data.Album = metaData.Album;
					data.Energy = metaData.Album?.Contains("starzz") ?? false ? metaData.Album : "";
					data.BPM = metaData.BeatsPerMinute;
					data.Comment = metaData.Comment;
					//data.Pictures = metaData.Pictures;
					data.Artist = metaData.Performers.FirstOrDefault();
					data.Key = metaData.FirstOrDefault(f => f.FrameId == ByteVector.FromString("TKEY", StringType.UTF8)).ToString();
				}


				Title = data.Title;
				Album = data.Album;
				Energy = data.Energy;
				BPM = data.BPM.ToString();
				Comment = data.Comment;
				//data.Pictures = metaData.Pictures;
				Artist = data.Artist;
				Key = data.Key;

				_innerData = (IDictionary<string, object>)data;
			}
			catch
			{
				_innerData = (IDictionary<string, object>)new ExpandoObject();
			}
		}
	}
}
