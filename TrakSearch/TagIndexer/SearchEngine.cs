using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Shravan.DJ.TagIndexer.Data;
using Directory = Lucene.Net.Store.Directory;
using Version = Lucene.Net.Util.Version;
using Lucene.Net.Analysis;
using System.Text.RegularExpressions;

namespace Shravan.DJ.TagIndexer
{
	public class SearchEngine
	{
		private static string _luceneDir = @"c:\temp\";
		private static FSDirectory _directoryTemp;
		private static FSDirectory _directory
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

		public SearchEngine()
		{

		}

		private static Id3TagData MapLuceneDocumentToData(Document doc, double? score = null)
		{
			var fullPath = doc.Get("Index");
			IDictionary<string, object> dic = new ExpandoObject();
			foreach (var field in doc.GetFields())
			{

				dic.Add(field.Name, field.StringValue);
			}

			return new Id3TagData(fullPath, dic);
		}

		private static IEnumerable<Id3TagData> MapLuceneToDataList(IEnumerable<Document> hits)
		{

			return hits.Select(h => MapLuceneDocumentToData(h)).ToList();
		}
		private static IEnumerable<Id3TagData> MapLuceneToDataList(IEnumerable<ScoreDoc> hits,
			 IndexSearcher searcher)
		{
			var filtered = hits.Where(h => h.Score > 0.33);
			var top = hits.OrderByDescending(h => h.Score).Where(h2 => h2.Score > 0.15).Take(1);

			var find = filtered.Any() ? filtered : top;

			var result = find.Select(hit => MapLuceneDocumentToData(searcher.Doc(hit.Doc), hit.Score)).ToList();

			return result;
		}

		private static Query parseQuery(string searchQuery, QueryParser parser)
		{
			Query query;
			try
			{
				query = parser.Parse(searchQuery.Trim());

			}
			catch (ParseException)
			{
				query = parser.Parse(QueryParser.Escape(searchQuery.Trim()));
			}
			return query;
		}

		private static void AddToLuceneIndex(Id3TagData id3, IndexWriter writer)
		{
			// remove older index entry

			var searchQuery = new TermQuery(new Term("Index", id3.FullPath));
			writer.DeleteDocuments(searchQuery);

			// add new index entry
			var doc = new Document();

			doc.Add(new Field("Index", id3.FullPath, Field.Store.YES, Field.Index.NO));
			foreach (var kv in id3.Data)
			{
				//if (kv.Key == "BPM")
				//{
				//	int val = (int)  kv.Value;
				//	doc.Add(new NumericField(kv.Key, val, Field.Store.YES, true));
				//}
				//else
				{
					var val = kv.Value as string;
					doc.Add(new Field(kv.Key, val ?? "", Field.Store.YES, Field.Index.ANALYZED));
				}
			}

			// add entry to index
			writer.AddDocument(doc);
		}

		private static IEnumerable<Id3TagData> SearchNumericInternal(string searchField, double value, double range)
		{

			// set up lucene searcher
			using (var searcher = new IndexSearcher(_directory, false))
			{
				searcher.SetDefaultFieldSortScoring(true, true);
				var hits_limit = 500;
				var analyzer = new StandardAnalyzer(Version.LUCENE_30);

				// search by single field
				if (!string.IsNullOrEmpty(searchField))
				{
					var parser = new QueryParser(Version.LUCENE_30, searchField, analyzer);
					var query = NumericRangeQuery.NewDoubleRange(searchField, value * range, value * (1 + range), true, true);
					var hits = searcher.Search(query, hits_limit).ScoreDocs;
					var results = MapLuceneToDataList(hits, searcher);
					analyzer.Close();
					searcher.Dispose();
					return results;
				}
			}

			return new List<Id3TagData>();
		}

		private static IEnumerable<Id3TagData> SearchInternal(string searchQuery, string searchField = "")
		{
			// validation
			if (string.IsNullOrEmpty(searchQuery.Replace("*", "").Replace("?", "")))
				return new List<Id3TagData>();

			// set up lucene searcher
			using (var searcher = new IndexSearcher(_directory, false))
			{
				searcher.SetDefaultFieldSortScoring(true, true);
				var hits_limit = 500;
				var analyzer = new StandardAnalyzer(Version.LUCENE_30);

				// search by single field
				if (!string.IsNullOrEmpty(searchField))
				{
					var parser = new QueryParser(Version.LUCENE_30, searchField, analyzer);
					var query = parseQuery(searchQuery, parser);
					var hits = searcher.Search(query, hits_limit).ScoreDocs;
					var results = MapLuceneToDataList(hits, searcher);
					analyzer.Close();
					searcher.Dispose();
					return results;
				}
				// search by multiple fields (ordered by RELEVANCE)
				else
				{
					var parser = new MultiFieldQueryParser
						 (Version.LUCENE_30, Id3TagData.Relevance.ToArray(), analyzer);
					parser.DefaultOperator = QueryParser.Operator.AND;
					var query = parseQuery(searchQuery, parser);
					var hits = searcher.Search
						(query, null, hits_limit, Sort.RELEVANCE).ScoreDocs;
					var results = MapLuceneToDataList(hits, searcher);
					analyzer.Close();
					searcher.Dispose();
					return results;
				}
			}
		}

		public static void AddUpdateLuceneIndex(IEnumerable<Id3TagData> tagList)
		{
			// init lucene
			var analyzer = new StandardAnalyzer(Version.LUCENE_30);
			using (var writer = new IndexWriter(_directory, analyzer, true, IndexWriter.MaxFieldLength.UNLIMITED))
			{
				// add data to lucene search index (replaces older entry if any)
				foreach (var tag in tagList)
				{
					AddToLuceneIndex(tag, writer);
				}

				// close handles
				analyzer.Close();
				writer.Dispose();
			}
		}

		public static void ClearLuceneIndexRecord(string fullPath)
		{
			// init lucene
			var analyzer = new StandardAnalyzer(Version.LUCENE_30);
			using (var writer = new IndexWriter(_directory, analyzer, IndexWriter.MaxFieldLength.UNLIMITED))
			{
				// remove older index entry
				var searchQuery = new TermQuery(new Term("Index", fullPath));
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
				var analyzer = new StandardAnalyzer(Version.LUCENE_30);
				using (var writer = new IndexWriter(_directory, analyzer, true, IndexWriter.MaxFieldLength.UNLIMITED))
				{
					// remove older index entries
					writer.DeleteAll();

					// close handles
					analyzer.Close();
					writer.Dispose();
				}
			}
			catch (Exception)
			{
				return false;
			}
			return true;
		}

		public static IEnumerable<Id3TagData> Search(string input, string fieldName = "")
		{
			if (string.IsNullOrEmpty(input))
				return new List<Id3TagData>();

			//var logical = input.Split(new string[] { "AND", "OR", "NOT" }, StringSplitOptions.RemoveEmptyEntries);

			var quotes = Regex.Matches(input, @"[\""].+?[\""]|[^ ]+")
					 .Cast<Match>()
					 .Select(m => m.Value)
					 .Where(x => !string.IsNullOrEmpty(x)).ToList();

			//var wild = quotes.Where(x => !string.IsNullOrEmpty(x)).Select(x => x.Trim() + "*");
			var keys = quotes.Where(q => q.StartsWith("Key:")).ToList();


			foreach (var key in keys)
				quotes.Remove(key);

			input = string.Join(" ", quotes) + GetRelatedKeys(keys);

			//var terms = input.Trim().Replace("-", " ").Split(' ')
			//	 .Where(x => !string.IsNullOrEmpty(x)).Select(x => x.Trim() + "*");
			// input = string.Join(" ", terms);


			//var bmp = quotes.Where(q => q.StartsWith("BPM:")).Select(s => s.Replace("BMP:", "").bpm.ToList();
			//var result = SearchNumericInternal(fieldName, bpm, 0.15);


			//result.AddRange (SearchInternal(input, fieldName));

			return (SearchInternal(input, fieldName));

		}

		public static IEnumerable<Id3TagData> SearchDefault(string input, string fieldName = "")
		{
			return string.IsNullOrEmpty(input) ? new List<Id3TagData>() : SearchInternal(input, fieldName);
		}

		internal static IEnumerable<Id3TagData> GetAllIndexRecords()
		{
			// validate search index
			if (!System.IO.Directory.EnumerateFiles(_luceneDir).Any())
				return new List<Id3TagData>();

			// set up lucene searcher
			var searcher = new IndexSearcher(_directory, false);
			var reader = IndexReader.Open(_directory, false);
			var docs = new List<Document>();
			var term = reader.TermDocs();
			while (term.Next()) docs.Add(searcher.Doc(term.Doc));
			reader.Dispose();
			searcher.Dispose();
			return MapLuceneToDataList(docs);
		}

		public static string GetRelatedKeys(IEnumerable<string> keys)
		{
			var result = new List<string>();
			foreach (var key in keys)
			{
				try
				{
					var num = int.Parse(key.Remove(key.Length - 1, 1).Replace("Key:", ""));
					var letter = key[key.Length - 1].ToString();

					if (num >= 1 && num <= 12 && (letter == "d" || letter == "m"))
					{

						result.Add(key.Replace("Key:", ""));
						result.Add((num + 1 == 13 ? 1 : num + 1).ToString() + letter);
						result.Add((num - 1 == 0 ? 12 : num - 1).ToString() + letter);
						result.Add(num.ToString() + (letter == "m" ? "d" : "m"));
					}
				}
				catch
				{ }
			}

			var finalString = "";

			if (result.Count > 0)
			{
				finalString = "(";
				finalString += string.Join(" OR ", result.Distinct().Select(s => "Key:" + s).ToArray());
				finalString += ")";
			}

			return finalString;
		}
	}
}
