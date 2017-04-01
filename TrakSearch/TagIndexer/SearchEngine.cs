using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Shravan.DJ.TagIndexer.Data;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text.RegularExpressions;
using Version = Lucene.Net.Util.Version;

namespace Shravan.DJ.TagIndexer
{
	public class SearchEngine : LuceneInterface
	{

		public SearchEngine()
		{

		}

		
		public static IEnumerable<Id3TagData> Search(string input, string fieldName = "")
		{
			if (string.IsNullOrEmpty(input))
				return new List<Id3TagData>();


			var quotes = Regex.Matches(input, @"[\""].+?[\""]|[^ ]+")
					 .Cast<Match>()
					 .Select(m => m.Value)
					 .Where(x => !string.IsNullOrEmpty(x)).ToList();


			var keyInputs = quotes.Where(q => q.StartsWith("Key:")).ToList();


			foreach (var key in keyInputs)
				quotes.Remove(key);

			input = string.Join(" ", quotes) + GetRelatedKeys(keyInputs);



			var bpmInputs = quotes.Where(q => q.StartsWith("BPM:"))
				.Select(s =>
				{
					var bpmString = s.Replace("BPM:", "");
					int bpm = 0;
					int.TryParse(bpmString, out bpm);
					return bpm;
				});

			List<Id3TagData> bpmResult = new List<Id3TagData>();


			foreach (var bpm in bpmInputs.Where(b => b > 0 && b < 200))
			{
				bpmResult.AddRange(SearchIntInternal("BPM", bpm, 0.10));
			}


			var result = SearchInternal(input, fieldName);

			return result.Concat(bpmResult);

		}

		public static IEnumerable<Id3TagData> SearchDefault(string input, string fieldName = "")
		{
			return string.IsNullOrEmpty(input) ? new List<Id3TagData>() : SearchInternal(input, fieldName);
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
				if (kv.Key == "BPM")
				{
					int val = (int)kv.Value;
					doc.Add(new NumericField(kv.Key, Field.Store.YES, true).SetIntValue(val));
				}
				else
				{
					var val = kv.Value as string;
					doc.Add(new Field(kv.Key, val ?? "", Field.Store.YES, Field.Index.ANALYZED));
				}
			}

			// add entry to index
			writer.AddDocument(doc);
		}




		private static IEnumerable<Id3TagData> SearchIntInternal(string searchField, int value, double range)
		{
			int? min = value - (int)Math.Ceiling(value * range);
			int? max = value + (int)Math.Ceiling(value * range);

			// set up lucene searcher
			using (var searcher = new IndexSearcher(_directory, false))
			{
				searcher.SetDefaultFieldSortScoring(true, true);
				var hits_limit = 500;
				var analyzer = new StandardAnalyzer(LUCENE_VER);

				// search by single field
				if (!string.IsNullOrEmpty(searchField))
				{
					var parser = new QueryParser(LUCENE_VER, searchField, analyzer);
					var query = NumericRangeQuery.NewIntRange(searchField, min, max, true, true);
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
				var analyzer = new StandardAnalyzer(LUCENE_VER);

				// search by single field
				if (!string.IsNullOrEmpty(searchField))
				{
					var parser = new QueryParser(LUCENE_VER, searchField, analyzer);
					var query = ParseQuery(searchQuery, parser);
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
						 (LUCENE_VER, Id3TagData.Relevance.ToArray(), analyzer);
					parser.DefaultOperator = QueryParser.Operator.AND;
					var query = ParseQuery(searchQuery, parser);
					var hits = searcher.Search
						(query, null, hits_limit, Sort.RELEVANCE).ScoreDocs;
					var results = MapLuceneToDataList(hits, searcher);
					analyzer.Close();
					searcher.Dispose();
					return results;
				}
			}
		}


		#region LuceneMapping


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

		public static void AddUpdateLuceneIndex(IEnumerable<Id3TagData> tagList)
		{
			// init lucene
			var analyzer = new StandardAnalyzer(LUCENE_VER);
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

		#endregion


	}
}
