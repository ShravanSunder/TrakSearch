﻿using Lucene.Net.Analysis.Standard;
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
	public class SearchEngineService : SearchEngineBase
	{

		public SearchEngineService()
		{

		}


		public static IEnumerable<Id3TagData> Search(string search)
		{
			if (string.IsNullOrEmpty(search))
				return new List<Id3TagData>();

			var bracketKeys = new List<string>() { "(", ")", "\\(\\)" };
			var logicalKeys = new List<string>() { "+", "-" };
			var restrictedKeys = new List<string>() { "*", "?", "~", "\"" };
			//var removeKeys = new List<string>() { "AND", "OR", "NOT" };
			restrictedKeys.AddRange(logicalKeys);
			restrictedKeys.AddRange(bracketKeys);
			restrictedKeys.AddRange(Id3TagData.Fields.Select(s => s + ":"));

			search = search.Length > 100 ? search.Substring(0, 99) : search;

			var specialTerms = FindSpecialTerms(search);
			var searchTerm = search;
			foreach (var s in specialTerms)
			{
				searchTerm = searchTerm.Replace(s.Trim(), "");
			}

			var query = new BooleanQuery();
			if (!string.IsNullOrEmpty(searchTerm.Trim()))
				CreateQueryWithWildCard(restrictedKeys, searchTerm, query);
			CreateKeyQueries(specialTerms, query);
			CreateBpmQueries(specialTerms, query);



			return SearchInternal(query);

		}

		private static void CreateQueryWithWildCard(List<string> restrictedKeys, string searchTerms, BooleanQuery query)
		{
			var terms = Regex.Matches(searchTerms, @"[\""].+?[\""]|[^ ]+")
									 .Cast<Match>()
									 .Select(m => m.Value)
									 .Where(x => !string.IsNullOrEmpty(x)).ToList();

			var wildTerm = string.Join(" ", terms
					.Select(s =>
					{
						if (restrictedKeys.Any(w => s.Contains(w)))
							return s;
						else
							return s + "*";
					}));

			query.Add(CreateQuery(wildTerm), Occur.MUST);

		}

		private static List<string> FindSpecialTerms(string search)
		{
			var specialTerms = new[] { "BPM:", "Key:" };
			var terms = new List<string>();

			foreach (var s in specialTerms)
			{

				var pattern = @"(" + s + @")[^\(\)\""]+?($|\s)";
				terms.AddRange(
					Regex.Matches(search, pattern)
						 .Cast<Match>()
						 .Select(m => m.Value)
						 .Where(x => !string.IsNullOrEmpty(x)).ToList());

				pattern = @"(" + s + @"\"").+?[""]";
				terms.AddRange(
					Regex.Matches(search, pattern)
						 .Cast<Match>()
						 .Select(m => m.Value)
						 .Where(x => !string.IsNullOrEmpty(x)).ToList());

				pattern = @"(" + s + @"\().+?[)]";
				terms.AddRange(
					Regex.Matches(search, @"(" + s + @"\().+?[)]")
						 .Cast<Match>()
						 .Select(m => m.Value)
						 .Where(x => !string.IsNullOrEmpty(x)).ToList());
			}

			return terms;
		}

		private static Occur GetBooleanOp(string str)
		{
			switch (str)
			{
				case "+":
					return Occur.MUST;
				case "-":
					return Occur.MUST_NOT;
				default:
					return Occur.SHOULD;
			}
		}



		private static IEnumerable<string> CreateKeyQueries(List<string> terms, BooleanQuery query)
		{
			var keyInputs = terms.Where(q => q.StartsWith("Key:")).ToList();
			var keyTerms = GetRelatedKeysTerms(keyInputs);
			var boolQuery = new BooleanQuery();

			foreach (var k in keyTerms)
			{
				boolQuery.Add(CreateQuery(k, "Key"), Occur.SHOULD);
			}

			if (boolQuery.Count() > 0)
				query.Add(boolQuery, Occur.MUST);

			return keyInputs;
		}

		private static IEnumerable<string> CreateBpmQueries(List<string> terms, BooleanQuery query)
		{
			var bpmInputs = terms
				.Where(q => q.StartsWith("BPM:"));

			var bpmTerms = bpmInputs
				.Select(s =>
				{
					var bpmString = s.Replace("BPM:", "");
					int bpm = 0;
					int.TryParse(bpmString, out bpm);
					return bpm;
				});



			foreach (var bpm in bpmTerms.Where(b => b > 0 && b < 200))
			{
				query.Add(CreateQueryInt("BPM", bpm, 0.10), Occur.MUST);
			}

			return bpmInputs;
		}

		public static List<string> GetRelatedKeysTerms(IEnumerable<string> keys)
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

			return result;
		}


		private static void AddToLuceneIndex(Id3TagData id3, IndexWriter writer)
		{
			// remove older index entry

			var searchQuery = new TermQuery(new Term("Index", id3.Index));
			writer.DeleteDocuments(searchQuery);

			// add new index entry
			var doc = new Document();

			doc.Add(new Field("Index", id3.Index, Field.Store.YES, Field.Index.NOT_ANALYZED, Field.TermVector.NO));
			doc.Add(new Field("FullPath", id3.FullPath, Field.Store.YES, Field.Index.NO, Field.TermVector.NO));
			doc.Add(new Field("DateModified", DateTools.DateToString(id3.DateModified, DateTools.Resolution.SECOND), Field.Store.YES, Field.Index.NO, Field.TermVector.NO));
			foreach (var kv in id3.Data)
			{
				if (kv.Key == "BPM")
				{
					int val = (int)kv.Value;
					doc.Add(new NumericField(kv.Key, Field.Store.YES, true).SetIntValue(val));
				}
				else if (kv.Key == "Commment")
				{
					string val = kv.Value as string;
					string[] comments = val.Split(new[] { '\\', '/' });

					doc.Add(new Field(kv.Key, val ?? "", Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.NO));
					doc.Add(new Field(kv.Key + "_Split", val ?? "", Field.Store.NO, Field.Index.ANALYZED, Field.TermVector.NO));

				}
				else
				{
					var val = kv.Value as string;
					doc.Add(new Field(kv.Key, val ?? "", Field.Store.YES, Field.Index.ANALYZED, Field.TermVector.NO));
				}
			}

			// add entry to index
			writer.AddDocument(doc);
		}




		private static NumericRangeQuery<int> CreateQueryInt(string searchField, int value, double range)
		{
			int? min = value - (int)Math.Ceiling(value * range);
			int? max = value + (int)Math.Ceiling(value * range);

			if (!string.IsNullOrEmpty(searchField))
			{
				var query = NumericRangeQuery.NewIntRange(searchField, min, max, true, true);

				return query;
			}

			return null;

		}


		private static Query CreateQuery(string searchQuery, string searchField = "")
		{
			// validation
			if (string.IsNullOrEmpty(searchQuery.Replace("*", "").Replace("?", "")))
				return null;


			var analyzer = new StandardAnalyzer(LUCENE_VER);

			// search by single field
			if (!string.IsNullOrEmpty(searchField))
			{
				var parser = new QueryParser(LUCENE_VER, searchField, analyzer);
				var query = ParseQuery(searchQuery, parser);

				return query;
			}
			else
			{
				// search by multiple fields (ordered by RELEVANCE)
				var parser = new MultiFieldQueryParser
					 (LUCENE_VER, Id3TagData.Fields.ToArray(), analyzer);
				parser.DefaultOperator = QueryParser.Operator.AND;
				var query = ParseQuery(searchQuery, parser);

				return query;
			}

		}


		private static IEnumerable<Id3TagData> SearchInternal(Query query)
		{
			var hits_limit = 500;
			var analyzer = new StandardAnalyzer(LUCENE_VER);


			using (var searcher = new IndexSearcher(_directory, true))
			{
				searcher.SetDefaultFieldSortScoring(true, true);
				var hits = searcher.Search(query, hits_limit).ScoreDocs;
				var results = MapLuceneToDataList(hits, searcher);
				analyzer.Close();
				searcher.Dispose();
				return results;
			}
		}
		#region LuceneMapping


		private static Id3TagData MapLuceneDocumentToData(Document doc, double? score = null)
		{
			var fullPath = doc.Get("FullPath");
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
			var top = hits.OrderByDescending(h => h.Score).Take(100);

			var find = top;

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
