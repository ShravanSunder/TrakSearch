using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using TagLib;
using TagLib.Id3v2;
using NLog;

namespace Shravan.DJ.TagIndexer.Data
{
	public class Id3TagData : Id3TagDataBase
	{
		private readonly Logger logger = LogManager.GetCurrentClassLogger(); // creates a logger using the class name

		/// <summary>
		/// ExpandoObject
		/// </summary>
		protected dynamic _innerData;
		private static List<string> _Fields = null;

		internal dynamic Data { get { return _innerData; } set { value = _innerData; } }

		internal string Index { get; set; }
		internal DateTime DateModified { get; set; }
		internal bool MarkPurgeRecord { get; set; }



		public static List<string> Fields
		{
			get
			{
				if (_Fields == null)
				{
					_Fields = typeof(Id3TagDataBase).GetProperties().Select(s => s.Name).ToList();
				}
				return _Fields;
			}
		}
		

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
					data.Artist = metaData.Performers?.FirstOrDefault() ?? "";
					data.Key = metaData.FirstOrDefault(f => f.FrameId == ByteVector.FromString("TKEY", StringType.UTF8))?.ToString();
					//Publisher / Label
					data.Publisher = metaData.FirstOrDefault(f => f.FrameId == ByteVector.FromString("TPUB", StringType.UTF8))?.ToString();
					data.Publisher = data.Publisher ?? metaData.FirstOrDefault(f => f.FrameId == ByteVector.FromString("TPE2", StringType.UTF8))?.ToString() ?? "";
					//MIXARTIST 
					data.Remixer = metaData.FirstOrDefault(f => f.FrameId == ByteVector.FromString("TPE4", StringType.UTF8))?.ToString() ?? "";

					data.Year = metaData.Year;
					data.Track = metaData.Track;
					data.Genre = metaData.FirstGenre;
					data.Composers = metaData.Composers.FirstOrDefault();
				}


				Title = data.Title;
				Album = data.Album;
				Energy = data.Energy;
				BPM = Convert.ToInt32(data.BPM);
				Comment = data.Comment;
				Publisher = data.Publisher;
				//data.Pictures = metaData.Pictures;
				Artist = data.Artist;
				Key = data.Key;
				Remixer = data.Remixer; //MIXARTIST 
				Track = data.Track;
				Year = data.Year;
				Genre = data.Genre;
				Composers = data.Composers;


				_innerData = (IDictionary<string, object>)data;
			}
			catch (Exception ex)
			{
				logger.Error(ex, "Error opening file" + metaData.ToString()); // which will log the stack trace.

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
