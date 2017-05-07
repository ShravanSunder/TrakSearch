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
		public string Publisher { get; protected set; }
		public string Remixer { get; protected set; }

		public string FullPath { get; protected set; }

	}
}
