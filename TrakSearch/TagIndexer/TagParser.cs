using System;
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

			const int BATCH_SIZE = 10;
			int batchCount = 0;
			while (batchCount < files.Count())
			{
				var start = batchCount;
				var t = new Task(() => StartTask(start, files, BATCH_SIZE));
				tasks.Add(t);
				t.Start();
				batchCount += BATCH_SIZE;
			}

			Task.WaitAll(tasks.ToArray());
		}

		private void StartTask(int start, List<dynamic> files, int BATCH_SIZE)
		{
			var batch = files.Skip(start).Take(BATCH_SIZE);

			foreach (var file in batch)
			{
				IndexFiles(file);
			}
		}

		public void IndexFiles(FileInfo fileInfo)
		{
			{
				try
				{
					var file = new TagLib.Mpeg.File(fileInfo.FullName, ReadStyle.None);

					var tagDataFromIndex = SearchEngineService.SearchWithIndex(fileInfo.FullName);

					if (tagDataFromIndex == null || string.IsNullOrEmpty(tagDataFromIndex.Index) || tagDataFromIndex.DateModified < fileInfo.LastAccessTimeUtc)
					{
						var tagDataFromFile = new Id3TagData(fileInfo, new Tag(file, 0)); ;

						if (!string.IsNullOrEmpty(tagDataFromFile.Title) && !string.IsNullOrEmpty(tagDataFromFile.Artist))
							tagList.Add(tagDataFromFile);
					}
					else
					{
					}
				}
				catch (Exception e)
				{
					Console.WriteLine(e);
				}
			}
		}
	}
}
