using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Shravan.DJ.TagIndexer.Data;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text.RegularExpressions;
using static Lucene.Net.Search.BooleanClause;
using System.IO;
//using Version = Lucene.Net.Util.Version;
using NLog;

namespace Shravan.DJ.TagIndexer
{
	public class SearchEngineService
	{

		private readonly static Logger logger = LogManager.GetCurrentClassLogger(); // creates a logger using the class name


		public static List<string> LuceneSpecialCharacters = new List<string>()
            { "*", "?", "~", "\"", "&&", "||", "!", "^", "!", ":", "{", "}", "!" };

        private static IndexSearcher _IndexSearcher = null;
		protected static IndexSearcher Searcher
		{
			get
			{
				if (_IndexSearcher == null)
				{
					_IndexSearcher = new IndexSearcher(DirectoryReader.Open(_directory));
				}

				return _IndexSearcher;
			}
		}

		public static string CurrentPartition { get; internal set; }

		protected static Lucene.Net.Util.LuceneVersion LUCENE_VER = Lucene.Net.Util.LuceneVersion.LUCENE_48;
		protected static string _luceneDir = System.IO.Path.GetTempPath() + @"\TrakSearch.Lucene.v2." + Environment.UserName.Replace(" ",".");
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


		public static void ClearLuceneIndex(Id3TagData tag)
		{
			ClearLuceneIndex(tag.Index);
		}

		public static void ClearLuceneIndex(string Index)
		{
			// init lucene
			var analyzer = new StandardAnalyzer(LUCENE_VER);
			using (var writer = new IndexWriter(_directory, new IndexWriterConfig(LUCENE_VER, analyzer)))
			{
				// remove older index entry
				var searchQuery = new TermQuery(new Term("Index", Index));
				writer.DeleteDocuments(searchQuery);

				// close handles
				//analyzer.Close();
				writer.Dispose();
			}
		}

		public static bool ClearLuceneIndex()
		{
			try
			{
				var analyzer = new StandardAnalyzer(LUCENE_VER);
				using (var writer = new IndexWriter(_directory, new IndexWriterConfig(LUCENE_VER, analyzer)))
				{
					// remove older index entries
					writer.DeleteAll();

					// close handles
					//analyzer.Close();
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

		static SearchEngineService()
		{

		}

		public static IEnumerable<Id3TagData> Search(string search, bool harmonicAdvanced = false)
        {
            if (string.IsNullOrEmpty(search))
                return new List<Id3TagData>();

            var bracketKeys = new List<string>() { "(", ")", "\\(\\)" };
            var logicalKeys = new List<string>() { "+", "-" };

            //var removeKeys = new List<string>() { "AND", "OR", "NOT" };
            LuceneSpecialCharacters.AddRange(logicalKeys);
            LuceneSpecialCharacters.AddRange(bracketKeys);
            LuceneSpecialCharacters.AddRange(Id3TagData.Fields.Select(s => s + ":"));

            search = search.Length > 100 ? search.Substring(0, 99) : search;

            var specialTerms = FindSpecialTerms(search);
            var searchTerm = search;
            foreach (var s in specialTerms)
            {
                searchTerm = searchTerm.Replace(s.Trim(), "");
            }

            var query = new BooleanQuery();
            if (!string.IsNullOrEmpty(CurrentPartition.Trim()))
            {
                query.Add(new WildcardQuery(new Term("Index", CurrentPartition + "*")), Occur.MUST);
            }

            if (!string.IsNullOrEmpty(searchTerm.Trim()))
            {
                CreateQueryWithWildCard(LuceneSpecialCharacters, searchTerm, query);
            }

            CreateKeyQuery(specialTerms, query, harmonicAdvanced);
            CreateBpmQuery(specialTerms, query);
            CreateYearQuery(specialTerms, query);

            return SearchInternal(query);
        }

        private static void CreateYearQuery(List<string> specialTerms, BooleanQuery query)
        {
            foreach (var s in specialTerms.Where(w => w.StartsWith("Year:")))
            {
                int year = 0;
                int.TryParse(s.Replace("Year:", ""), out year);
                query.Add(CreateQueryInt("Year", year), Occur.MUST);
            }
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
			var specialTerms = new[] { "BPM:", "Key:", "Year:" };
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

		private static IEnumerable<string> CreateKeyQuery(List<string> terms, BooleanQuery query, bool harmonicAdvanced = false)
		{
			var keyInputs = terms.Where(q => q.StartsWith("Key:")).ToList();
			var keyTerms = KeyTermHelper(keyInputs, harmonicAdvanced);
			var boolQuery = new BooleanQuery();

			foreach (var k in keyTerms)
			{
				boolQuery.Add(CreateQuery(k, "Key"), Occur.SHOULD);
			}

			if (boolQuery.Count() > 0)
				query.Add(boolQuery, Occur.MUST);

			return keyInputs;
		}

		private static IEnumerable<string> CreateBpmQuery(List<string> terms, BooleanQuery query)
		{
			var bpmInputs = terms
				.Where(q => q.StartsWith("BPM:"));

			var bpmTerms = bpmInputs
				.Select(s =>
				{
					var bpmString = s.Replace("BPM:", "").Trim();
					int bpm = 0;
					int.TryParse(bpmString, out bpm);
					return bpm;
				});

			foreach (var bpm in bpmTerms.Where(b => b > 0 && b < 220))
			{
				query.Add(CreateQueryInt("BPM", bpm, 0.09), Occur.MUST);
			}

			return bpmInputs;
		}

		public static List<string> KeyTermHelper(IEnumerable<string> keys, bool harmonicAdvanced = false)
		{
			var result = new List<string>();
			foreach (var key in keys)
			{
				try
				{

					var numStr = Regex.Replace(key, "[^0-9]", "");
					var num = int.Parse(numStr);
					var letter = Regex.Replace(key, "[^dDmM]", "");

				    if (num >= 1 && num <= 12)
					{

                        if (harmonicAdvanced)
                        {
                            //result.Add(key.Replace("Key:", ""));
                            //harmonic energy 1 semitone
                            result.Add(FormatKeyNum(num + 7) + letter);
                            //harmonic energy 2 semitone
                            result.Add(FormatKeyNum(num + 2) + letter);
                            //diagonal harmonic
                            result.Add(FormatKeyNum(num + 1) + (letter == "m" ? "d" : "m"));
                        }
                        else
                        {
                            result.Add(key.Replace("Key:", ""));
							result.Add(FormatKeyNum(num+1) + letter);
							result.Add(FormatKeyNum(num-1) + letter);
                            result.Add(num.ToString() + (letter == "m" ? "d" : "m"));
                        }
					}
				}
				catch
				{ }
			}

			return result;
		}

        public static string FormatKeyNum (int keyNum)
        {
            var result = 0;
            if (keyNum == 12)
                result = 12;
            else if (keyNum == 0)
                result = 12;
            else
                result = keyNum % 12;

            return result.ToString();
        }


		public static void InitDirectoryifRequried ()
		{
			try
			{
				SearchEngineService.GetIndexCount();
			}
			catch
			{
				SearchEngineService.InitDirectory();
			}
		}

		public static void InitDirectory()
		{
			// init lucene
			var analyzer = new StandardAnalyzer(LUCENE_VER, Lucene.Net.Analysis.Util.CharArraySet.EMPTY_SET);
			using (var writer = new IndexWriter(_directory, new IndexWriterConfig(LUCENE_VER, analyzer)))
			{
				// close handles
				//analyzer.Close();
				writer.Dispose();
				_IndexSearcher?.IndexReader.Dispose();
				_IndexSearcher = null;
			}
		}


		public static void AddIndex(IEnumerable<Id3TagData> tagList)
		{
			// init lucene
			var analyzer = new StandardAnalyzer(LUCENE_VER, Lucene.Net.Analysis.Util.CharArraySet.EMPTY_SET);
			using (var writer = new IndexWriter(_directory, new IndexWriterConfig(LUCENE_VER, analyzer)))
			{
				List<Document> docs = new List<Document>();
				// add data to lucene search index (replaces older entry if any)
				foreach (var tag in tagList)
				{
					var doc = new Document();
					NewIndex(tag, doc);
					docs.Add(doc);
				}

				writer.AddDocuments(docs);

				// close handles
				//analyzer.Close();
				writer.Dispose();
				_IndexSearcher?.IndexReader.Dispose();
				_IndexSearcher = null;
			}
		}

		private static void DeleteIndex(Id3TagData id3, IndexWriter writer)
		{
			// remove older index entry

			var searchQuery = new TermQuery(new Term("Index", id3.Index));
			writer.DeleteDocuments(searchQuery);

		}

		private static void AddOrUpdateIndex(Id3TagData id3, IndexWriter writer)
		{
			// remove older index entry

			var searchQuery = new TermQuery(new Term("Index", id3.Index));
			writer.DeleteDocuments(searchQuery);

			// add new index entry
			var doc = new Document();
			NewIndex(id3, doc);

			// add entry to index
			writer.AddDocument(doc);
		}

		private static void NewIndex(Id3TagData id3, Document doc)
		{
			try
			{

				doc.Add(new StringField("Index", id3.Index, Field.Store.YES));
				doc.Add(new StringField("FullPath", id3.FullPath, Field.Store.YES));
				doc.Add(new StringField("DateModified", DateTools.DateToString(id3.DateModified, DateTools.Resolution.SECOND), Field.Store.YES));

                NewIndexProperty(doc, "BPM", id3.BPM);
                NewIndexProperty(doc, "Comment", id3.Comment);
                NewIndexProperty(doc, "Artist", id3.Artist);
                NewIndexProperty(doc, "Title", id3.Title);
                NewIndexProperty(doc, "Publisher", id3.Publisher);
                NewIndexProperty(doc, "Year", id3.Year);
                NewIndexProperty(doc, "Key", id3.Key);
                NewIndexProperty(doc, "Genre", id3.Genre);
                NewIndexProperty(doc, "Energy", id3.Energy);
                NewIndexProperty(doc, "Album", id3.Album);
                NewIndexProperty(doc, "Track", id3.Track);
                NewIndexProperty(doc, "Remixer", id3.Remixer);

            }
			catch(Exception ex)
			{
				logger.Error(ex, "Failed to add to Lucene Index");
			}
		}

        private static void NewIndexProperty(Document doc, string key, object value)
        {
            if (key == "BPM")
            {
                var val = Convert.ToInt32(value);
                doc.Add(new Int32Field(key, val, Field.Store.YES));
            }
            else if (key == "Commment")
            {
                string val = value as string;
                string[] comments = val.Split(new[] { '\\', '/' });

                doc.Add(new TextField(key, val ?? "", Field.Store.YES));
                doc.Add(new TextField(key + "_Split", val ?? "", Field.Store.NO));
            }
            else if (value is uint || value is int)
            {
                var val = Convert.ToInt32(value);
                doc.Add(new Int32Field(key, val, Field.Store.YES));
            }
            else
            {
                var val = value as string;
                doc.Add(new TextField(key, val ?? "", Field.Store.YES));
            }
        }

        public static void AddOrUpdateIndex(IEnumerable<Id3TagData> tagList)
		{
			// init lucene
			var analyzer = new StandardAnalyzer(LUCENE_VER, Lucene.Net.Analysis.Util.CharArraySet.EMPTY_SET);
			using (var writer = new IndexWriter(_directory, new IndexWriterConfig(LUCENE_VER, analyzer)))
			{
				// add data to lucene search index (replaces older entry if any)
				foreach (var tag in tagList)
				{
					AddOrUpdateIndex(tag, writer);
				}

				// close handles
				//analyzer.Close();
				writer.Dispose();
				_IndexSearcher?.IndexReader.Dispose();
				_IndexSearcher = null;
			}
		}

		private static NumericRangeQuery<int> CreateQueryInt(string searchField, int value, double range = 0)
		{
			int? min = value - (int)Math.Ceiling(value * range);
			int? max = value + (int)Math.Ceiling(value * range);

			if (!string.IsNullOrEmpty(searchField))
			{
				var query = NumericRangeQuery.NewInt32Range(searchField, min, max, true, true);

				return query;
			}

			return null;

		}

		private static Query CreateQuery(string searchQuery, string searchField = "")
		{
			// validation
			if (string.IsNullOrEmpty(searchQuery.Replace("*", "").Replace("?", "")))
				return null;


			var analyzer = new StandardAnalyzer(LUCENE_VER, Lucene.Net.Analysis.Util.CharArraySet.EMPTY_SET);

			// search by single field
			if (!string.IsNullOrEmpty(searchField))
			{

				var query = new TermQuery(new Term(searchField, searchQuery));
				return query;
			}
			else
			{
				// search by multiple fields (ordered by RELEVANCE)
				var parser = new MultiFieldQueryParser
					 (LUCENE_VER, Id3TagData.Fields.ToArray(), analyzer);
				parser.DefaultOperator = QueryParser.AND_OPERATOR;
				var query = ParseQuery(searchQuery, parser);

				return query;
			}

		}

		public static IEnumerable<Id3TagData> SearchWithIndex(string Index)
		{
			var analyzer = new StandardAnalyzer(LUCENE_VER, Lucene.Net.Analysis.Util.CharArraySet.EMPTY_SET);

			var query = new TermQuery(new Term("Index", Index));

			var hits = Searcher.Search(query, 10).ScoreDocs;
			var results = MapLuceneToDataList(hits, Searcher);

			return results;

		}

		private static IEnumerable<Id3TagData> SearchInternal(Query query)
		{
			var hits_limit = 500;
			var analyzer = new StandardAnalyzer(LUCENE_VER, Lucene.Net.Analysis.Util.CharArraySet.EMPTY_SET);


			//searcher.SetDefaultFieldSortScoring(true, true);
			var hits = Searcher.Search(query, hits_limit).ScoreDocs;
			var results = MapLuceneToDataList(hits, Searcher);
			//analyzer.Close();
			//searcher.Dispose();
			return results;

		}


		#region LuceneMapping


		private static Id3TagData MapLuceneDocumentToData(Document doc, double? score = null)
		{
			var fullPath = doc.Get("FullPath");
			IDictionary<string, object> dic = new ExpandoObject();
			foreach (var field in doc)
			{
				dic.Add(field.Name, field.GetStringValue());
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
			var top = hits.OrderByDescending(h => h.Score).Take(500);

			var find = top;

			var result = find.Select(hit => MapLuceneDocumentToData(searcher.Doc(hit.Doc), hit.Score)).ToList();
			return result;
		}

   		internal static int GetIndexCount()
		{
			var reader = DirectoryReader.Open(_directory);
			return reader.MaxDoc;
		}

		internal static IEnumerable<Id3TagData> GetAllIndexRecords()
		{
			throw new NotImplementedException();
			//var reader = DirectoryReader.Open(_directory);
			//reader.MaxDoc;

			//var searcher = new IndexSearcher(reader);
			//MultiFields.GetFields(reader);

			//var docs = new List<Document>();
			//var term = reader

			//while (term.Next()) docs.Add(searcher.Doc(term.Doc));
			////reader.Dispose();
			////searcher.Dispose();
			//return MapLuceneToDataList(docs);
		}


		#endregion


	}
}
