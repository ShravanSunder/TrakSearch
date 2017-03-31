using System;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shravan.DJ.TagIndexer;

namespace UnitTestProject1
{
	[TestClass]
	public class IndexerTest
	{
		[TestMethod]
		public void ReadTags()
		{
			var time = new Stopwatch();
			time.Start();

			var parser = new TagParser();

			parser.IndexDirectory(@"H:\Zouk");
		
			//parser.IndexDirectory(@"F:\Shravan's Documents\Dropbox\!Backup\MkLinks\Mp3\Dance\Zouk");

			//var mp3 = new TagParser(@"C:\Users\shravan\Dropbox\!Backup\MkLinks\Mp3\Dance\");
			// var mp3 = new Mp3(@"C:\Users\shravan\Dropbox\!Backup\MkLinks\Mp3\Dance\Traktor-New Songs\");

			//mp3.TryIndex();

			Console.WriteLine(parser.tagList.Count);

			Console.WriteLine(time.ElapsedMilliseconds);
		}
	}
}
