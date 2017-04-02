﻿using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Shravan.DJ.TagIndexer.Data;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;

namespace Shravan.DJ.TagIndexer
{
	public abstract class SearchEngineBase
	{
		public SearchEngineBase()
		{

		}


		protected static Lucene.Net.Util.Version LUCENE_VER = Lucene.Net.Util.Version.LUCENE_30;

		protected static string _luceneDir = System.IO.Path.GetTempPath() + @"\Lucene\";
		protected static FSDirectory _directoryTemp;
		protected static FSDirectory _directory
		{
			get
			{
				if (_directoryTemp == null) _directoryTemp = FSDirectory.Open(new DirectoryInfo(_luceneDir));
				if (IndexWriter.IsLocked(_directoryTemp)) IndexWriter.Unlock(_directoryTemp);
				var lockFilePath = Path.Combine(_luceneDir, "write.lock");
				if (File.Exists(lockFilePath)) File.Delete(lockFilePath);
				return _directoryTemp;
			}
		}


		public static void ClearLuceneIndexRecord(Id3TagData tag)
		{
			ClearLuceneIndexRecord(tag.Index);
		}

		public static void ClearLuceneIndexRecord(string Index)
		{
			// init lucene
			var analyzer = new StandardAnalyzer(LUCENE_VER);
			using (var writer = new IndexWriter(_directory, analyzer, IndexWriter.MaxFieldLength.UNLIMITED))
			{
				// remove older index entry
				var searchQuery = new TermQuery(new Term("Index", Index));
				writer.DeleteDocuments(searchQuery);

				// close handles
				analyzer.Close();
				writer.Dispose();
			}
		}

		public static bool ClearLuceneIndex()
		{
			try
			{
				var analyzer = new StandardAnalyzer(LUCENE_VER);
				using (var writer = new IndexWriter(_directory, analyzer, true, IndexWriter.MaxFieldLength.UNLIMITED))
				{
					// remove older index entries
					writer.DeleteAll();

					// close handles
					analyzer.Close();
					writer.Dispose();
				}
			}
			catch (System.Exception)
			{
				return false;
			}
			return true;
		}


		protected static Query ParseQuery(string searchQuery, QueryParser parser)
		{
			Query query;
			try
			{
				query = parser.Parse(searchQuery.Trim());

			}
			catch (ParseException)
			{
				try
				{
					query = parser.Parse(QueryParser.Escape(searchQuery.Trim()));
				}
				catch
				{
					query = new BooleanQuery();
				}
			}
			return query;
		}

	}



}