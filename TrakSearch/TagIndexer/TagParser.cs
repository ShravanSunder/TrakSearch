﻿using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Shravan.DJ.TagIndexer.Data;
using TagLib;
using TagLib.Id3v2;
using TagLib.Mpeg;
using Tag = TagLib.Id3v2.Tag;
using System.Collections.Concurrent;

namespace Shravan.DJ.TagIndexer
{
	public class TagParser
	{
		public ConcurrentBag<Id3TagData> tagList;
		
		
		public TagParser()
		{	
			tagList = new ConcurrentBag<Id3TagData>();
		}
		
		public void IndexDirectory (string path)
		{
			IndexDirectory(new DirectoryInfo(path));
		}

		public void IndexDirectory(DirectoryInfo directory)
		{
			var files = new List<dynamic>();
						
			foreach (var file in directory.GetFiles("*.mp3", SearchOption.AllDirectories))
				files.Add(file);

			foreach (var file in directory.GetFiles("*.mp4", SearchOption.AllDirectories))
				files.Add(file);

			var tasks = new List<Task>();

			foreach (var file in files)
			{
				var t = new Task(() => IndexFiles(file));
				t.Start();
				tasks.Add(t);
			}

			Task.WaitAll(tasks.ToArray());
		}

		public void IndexFiles(FileInfo fileInfo)
		{
			//if (fileInfo.LastWriteTime > DateTime)
			{
				try
				{
					var file = new TagLib.Mpeg.File(fileInfo.FullName, ReadStyle.None);
					var data = new Id3TagData(fileInfo, new Tag(file, 0)); ;

					if (!string.IsNullOrEmpty(data.Title) && !string.IsNullOrEmpty(data.Artist))
						tagList.Add(data);
				}
				catch (Exception e)
				{
					Console.WriteLine(e);
				}
			}
		}
	}
}