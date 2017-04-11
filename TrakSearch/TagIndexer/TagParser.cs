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
		public ConcurrentBag<Id3TagData> TagList;
		protected ConcurrentBag<Id3TagData> LuceneUpdates;


		public TagParser()
		{
			TagList = new ConcurrentBag<Id3TagData>();
		}

		public void IndexDirectory(string path)
		{
			IndexDirectory(new DirectoryInfo(path));

			SearchEngineService.CurrentPartition = Id3TagData.CreateIndex(path);
		}

		public void IndexDirectory(DirectoryInfo directory)
		{
			try
			{
				ClearCurrentData();

				var files = new List<dynamic>();

				foreach (var file in directory.GetFiles("*.mp3", SearchOption.AllDirectories))
					files.Add(file);

				foreach (var file in directory.GetFiles("*.mp4", SearchOption.AllDirectories))
					files.Add(file);

				SearchEngineService.InitDirectoryifRequried();


				var tasks = new List<Task>();

				const int BATCH_SIZE = 50;
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

				SearchEngineService.AddOrUpdateLuceneIndex(LuceneUpdates);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex);
			}
		}

		private void ClearCurrentData()
		{
			LuceneUpdates = new ConcurrentBag<Id3TagData>();
			TagList = new ConcurrentBag<Id3TagData>();
		}

		private void StartTask(int start, List<dynamic> files, int BATCH_SIZE)
		{
			var batch = files.Skip(start).Take(BATCH_SIZE);
			var updateIndex = new List<Id3TagData>();

			foreach (var file in batch)
			{
				IndexFiles(file, updateIndex);
			}

			foreach (var t in updateIndex)
				LuceneUpdates.Add(t);
		}

		public void IndexFiles(FileInfo fileInfo, List<Id3TagData> updateIndex)
		{
			{
				try
				{
					var file = new TagLib.Mpeg.File(fileInfo.FullName, ReadStyle.None);

					var tagDataFromIndex = SearchEngineService.SearchWithIndex(Id3TagData.CreateIndex(fileInfo.FullName)).ToList();

					if (tagDataFromIndex == null || !tagDataFromIndex.Any() || tagDataFromIndex.Count() > 1)
					{
						if (tagDataFromIndex != null && tagDataFromIndex.Count() > 1)
						{
							foreach (var tag in tagDataFromIndex)
							{
								SearchEngineBase.ClearLuceneIndexRecord(tag);
							}
						}

						var tagDataFromFile = new Id3TagData(fileInfo, new Tag(file, 0)); ;

						if (!string.IsNullOrEmpty(tagDataFromFile.Title) && !string.IsNullOrEmpty(tagDataFromFile.Artist))
						{
							TagList.Add(tagDataFromFile);
							updateIndex.Add(tagDataFromFile);
						}



					}
					else if (tagDataFromIndex.Count() == 1)
					{
						if (RoundUp(tagDataFromIndex.First().DateModified) < RoundUp(fileInfo.LastAccessTimeUtc))
						{
							var tagDataFromFile = new Id3TagData(fileInfo, new Tag(file, 0)); ;

							if (!string.IsNullOrEmpty(tagDataFromFile.Title) && !string.IsNullOrEmpty(tagDataFromFile.Artist))
							{
								TagList.Add(tagDataFromFile);
								updateIndex.Add(tagDataFromFile);
							}
						}
						else
						{
							var tag = tagDataFromIndex.First();
							TagList.Add(tag);
						}
					}
					else
					{
						//something went wrong?
					}
				}
				catch (Exception e)
				{
					Console.WriteLine(e);
				}
			}
		}

		public DateTime RoundUp(DateTime dt, TimeSpan? span = null)
		{
			if (!span.HasValue)
			{
				span = TimeSpan.FromMilliseconds(1000);
			}

			long ticks = dt.Ticks / span.Value.Ticks;
			return new DateTime(ticks);
		}
	}
}
