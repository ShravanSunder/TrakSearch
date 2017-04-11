using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using TagLib;
using TagLib.Id3v2;

namespace Shravan.DJ.TagIndexer.Data
{
	public class Id3TagData : Id3TagDataBase
	{
		/// <summary>
		/// ExpandoObject
		/// </summary>
		protected dynamic _innerData;


		internal dynamic Data { get { return _innerData; } set { value = _innerData; } }

		internal string Index { get; set; }
		internal DateTime DateModified { get; set; }
		internal bool MarkPurgeRecord { get; set; }



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
		

		public Id3TagData(System.IO.FileInfo file, TagLib.Id3v2.Tag metaData)
		{
			FullPath = file.FullName;
			DateModified = file.LastAccessTimeUtc;
			Index = CreateIndex(file.FullName);

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
				if (data != null)
				{

					if (((IDictionary<string, object>)data).Keys.Contains("Index"))
					{
						Index = CreateIndex(data.Index);
					}
					else
					{
						Index = CreateIndex(FullPath);
					}

					if (((IDictionary<string, object>)data).Keys.Contains("DateModified"))
					{
						DateModified = Lucene.Net.Documents.DateTools.StringToDate(data.DateModified);
					}
				}
				if (metaData != null)
				{
					data = new ExpandoObject();
					data.Title = metaData.Title;
					data.Album = metaData.Album;
					data.Energy = metaData.Album?.Contains("starzz") ?? false ? metaData.Album : "";
					data.BPM = metaData.BeatsPerMinute;
					data.Comment = metaData.Comment;
					data.Artist = metaData.Performers.FirstOrDefault();
					data.Key = metaData.FirstOrDefault(f => f.FrameId == ByteVector.FromString("TKEY", StringType.UTF8)).ToString();
					data.Publisher = metaData.FirstOrDefault(f => f.FrameId == ByteVector.FromString("TPUB", StringType.UTF8)).ToString();
				}


				Title = data.Title;
				Album = data.Album;
				Energy = data.Energy;
				BPM = data.BPM.ToString();
				Comment = data.Comment;
				Publisher = data.Publisher;
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


		public bool IndexEqualsPath(string fullPath)
		{
			var input = fullPath;

			return input.Equals(Index);
		}

		public static string CreateIndex(string fullPath)
		{
			return System.Text.RegularExpressions.Regex.Replace(fullPath, "[^0-9a-zA-Z]+", "_");
		}

	}
}
