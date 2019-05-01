using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shravan.DJ.TagIndexer.Data
{
	public class Id3TagDataBase
	{
		public string Title { get; protected set; }
		public string Artist { get; protected set; }
		public string Key { get; protected set; }
		public string Energy { get; protected set; }
		public int BPM { get; protected set; }
		public string Comment { get; protected set; }
		public string Album { get; protected set; }
		public uint Year { get; protected set; }
		public uint Track { get; protected set; }
		/// <summary>
		/// Label id3
		/// </summary>
		public string Publisher { get; protected set; }
		/// <summary>
		/// MixArtist id3
		/// </summary>
		public string Remixer { get; protected set; }
		/// <summary>
		/// First Genre
		/// </summary>
		public string Genre  { get; protected set; }
		public string Composers { get; protected set; }

		public string FullPath { get; protected set; }
        public string Index { get; internal set; }

    }
}
