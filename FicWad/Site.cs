using HtmlAgilityPack;
using Microsoft.Data.Sqlite;
using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FanFictionScraper.FicWad
{
	public class Site : SiteCommon
	{
		private SqliteConnection dbConn;
		private List<KeyValuePair<int, string>> characterDictionary = new List<KeyValuePair<int, string>>();
		private List<KeyValuePair<int, string>> genereDictionary = new List<KeyValuePair<int, string>>();
		private List<KeyValuePair<int, string>> warningDictionary = new List<KeyValuePair<int, string>>();
		private bool running;
		private List<Meta> stories;

		public void SiteToDatabase()
		{
			running = true;
			stories = new List<Meta>();

			Driver.Navigate().GoToUrl($"https://ficwad.com/category/19/");

			var worker = new System.Threading.Thread(new System.Threading.ThreadStart(ProcessStories));
			worker.Start();

			for (int i = 1; i <= 185; i++)
			{
				Console.WriteLine("Begin Processing Page: " + i);
				Driver.Navigate().GoToUrl($"https://ficwad.com/category/19/{i}");
				Wait(1000);

				HtmlDocument doc = new HtmlDocument();
				doc.LoadHtml(Driver.PageSource);
				stories.AddRange(ParsePage(doc));

				//stories.AddRange(ParsePage());
			}

			running = false;

		}

		private void ProcessStories()
		{
			Console.WriteLine("Setting up database");
			var databaseFile = new FileInfo("ficwad.db");
			if (databaseFile.Exists)
				databaseFile.Delete();
			var connectionStringBuilder = new SqliteConnectionStringBuilder();


			connectionStringBuilder.DataSource = ":memory:";
			using (dbConn = new SqliteConnection(connectionStringBuilder.ConnectionString))
			{
				dbConn.Open();
				CreateTables();

				Console.WriteLine("Ready to add Stories to database...");

				while (running || stories.Any())
				{
					if (!stories.Any())
					{
						System.Threading.Thread.Sleep(100);
						continue;
					}

					var story = stories.First();
					Console.WriteLine($"(Backlog: {stories.Count}) Adding story to Database: {story.Title}");
					AddStory(story);
					stories.Remove(story);
				}

				connectionStringBuilder.DataSource = databaseFile.FullName;
				var fileDb = new SqliteConnection(connectionStringBuilder.ConnectionString);
				fileDb.Open();
				dbConn.BackupDatabase(fileDb);
				fileDb.Close();
				dbConn.Close();
			}
		}

		private void AddDependentValues(Meta story)
		{
			bool needToUpdate = false;
			using (var cmd = dbConn.CreateCommand())
			{
				foreach (var item in story.Characters)
				{
					if (string.IsNullOrWhiteSpace(item))
						continue;
					if (characterDictionary.Any(s => s.Value == item))
						continue;
					cmd.CommandText = "INSERT INTO Characters (Character) VALUES (@character)";
					cmd.Parameters.AddWithValue("@character", item.Trim());
					cmd.ExecuteNonQuery();
					cmd.Parameters.Clear();
					needToUpdate = true;
				}

				foreach (var item in story.Genres)
				{
					if (string.IsNullOrWhiteSpace(item))
						continue;
					if (genereDictionary.Any(s => s.Value == item))
						continue;
					cmd.CommandText = "INSERT INTO Genres (Genre) VALUES (@genre)";
					cmd.Parameters.AddWithValue("@genre", item.Trim());
					cmd.ExecuteNonQuery();
					cmd.Parameters.Clear();
					needToUpdate = true;
				}

				foreach (var item in story.Warnings)
				{
					if (string.IsNullOrWhiteSpace(item))
						continue;
					if (warningDictionary.Any(s => s.Value == item))
						continue;
					cmd.CommandText = "INSERT INTO Warnings (Warning) VALUES (@warning)";
					cmd.Parameters.AddWithValue("@warning", item.Trim());
					cmd.ExecuteNonQuery();
					cmd.Parameters.Clear();
					needToUpdate = true;
				}

				if (needToUpdate)
				{
					characterDictionary = SqlToKVP("SELECT id, Character FROM Characters");
					genereDictionary = SqlToKVP("SELECT id, Genre FROM Genres");
					warningDictionary = SqlToKVP("SELECT id, Warning FROM Warnings");
				}
			}
		}

		private void AddStory(Meta story)
		{
			AddDependentValues(story);
			using (var cmd = dbConn.CreateCommand())
			{
				cmd.CommandText = $"INSERT INTO Stories (Title,Author,Published,Rating,Summary,Url,Words,Chapters,Updated,Completed,Rated) VALUES (@title,@author,@published,@rating,@summary,@url,@words,@chapters,@updated,@completed,@rated)";
				cmd.Parameters.AddWithValue("@title", story.Title.Trim());
				cmd.Parameters.AddWithValue("@author", story.Author.Trim());
				cmd.Parameters.AddWithValue("@published", story.Published);
				cmd.Parameters.AddWithValue("@rating", story.Rating);
				cmd.Parameters.AddWithValue("@summary", story.Summary.Trim());
				cmd.Parameters.AddWithValue("@url", story.Url.Trim());
				cmd.Parameters.AddWithValue("@words", story.Words);
				cmd.Parameters.AddWithValue("@chapters", story.Chapters);
				cmd.Parameters.AddWithValue("@updated", story.Updated);
				cmd.Parameters.AddWithValue("@completed", story.Completed);
				cmd.Parameters.AddWithValue("@rated", story.Rated);
				cmd.ExecuteNonQuery();
				cmd.Parameters.Clear();
				cmd.CommandText = $"SELECT id FROM Stories WHERE Url='{story.Url}'";
				var storyId = cmd.ExecuteScalar();

				foreach (var item in story.Characters)
				{
					if (string.IsNullOrWhiteSpace(item))
						continue;
					cmd.CommandText = $"INSERT INTO StoryCharacters (StoryId, CharacterId) VALUES ({storyId}, {characterDictionary.First(k => k.Value == item).Key})";
					cmd.ExecuteNonQuery();
				}
				foreach (var item in story.Genres)
				{
					if (string.IsNullOrWhiteSpace(item))
						continue;
					cmd.CommandText = $"INSERT INTO StoryGenres (StoryId, GenreId) VALUES ({storyId}, {genereDictionary.First(k => k.Value == item).Key})";
					cmd.ExecuteNonQuery();
				}
				foreach (var item in story.Warnings)
				{
					if (string.IsNullOrWhiteSpace(item))
						continue;
					cmd.CommandText = $"INSERT INTO StoryWarnings (StoryId, WarningId) VALUES ({storyId}, {warningDictionary.First(k => k.Value == item).Key})";
					cmd.ExecuteNonQuery();
				}
			}
		}

		/// <summary>
		/// Column 0 as int, column 1 as string
		/// </summary>
		/// <param name="sql"></param>
		/// <returns></returns>
		private List<KeyValuePair<int, string>> SqlToKVP(string sql)
		{
			var rtn = new List<KeyValuePair<int, string>>();
			using (var dataSet = new DataSet())
			{
				using (var cmd = dbConn.CreateCommand())
				{
					cmd.CommandText = sql;
					var query = cmd.ExecuteReader();

					while (query.Read())
					{
						rtn.Add(new KeyValuePair<int, string>(query.GetInt32(0), query.GetString(1)));
					}
				}
			}

			return rtn;

		}

		private void CreateTables()
		{
			var sql = @"CREATE TABLE Stories( 
id INTEGER PRIMARY KEY AUTOINCREMENT,
Title TEXT,
Author TEXT,
Published TEXT,
Rating INTEGER,
Rated TEXT,
Summary TEXT,
Url TEXT,
Words INTEGER,
Chapters INTEGER,
Updated TEXT,
Completed INTEGER)";
			using (var cmd = dbConn.CreateCommand())
			{
				cmd.CommandText = sql;
				cmd.ExecuteNonQuery();
			}

			sql = @"CREATE TABLE Characters( 
id INTEGER PRIMARY KEY,
Character TEXT)";
			using (var cmd = dbConn.CreateCommand())
			{
				cmd.CommandText = sql;
				cmd.ExecuteNonQuery();
			}

			sql = @"CREATE TABLE Genres( 
id INTEGER PRIMARY KEY,
Genre TEXT)";
			using (var cmd = dbConn.CreateCommand())
			{
				cmd.CommandText = sql;
				cmd.ExecuteNonQuery();
			}

			sql = @"CREATE TABLE Warnings( 
id INTEGER PRIMARY KEY,
Warning TEXT)";
			using (var cmd = dbConn.CreateCommand())
			{
				cmd.CommandText = sql;
				cmd.ExecuteNonQuery();
			}

			sql = @"CREATE TABLE StoryCharacters( 
StoryId INTEGER,
CharacterId INTEGER)";
			using (var cmd = dbConn.CreateCommand())
			{
				cmd.CommandText = sql;
				cmd.ExecuteNonQuery();
			}

			sql = @"CREATE TABLE StoryGenres( 
StoryId INTEGER,
GenreId INTEGER)";
			using (var cmd = dbConn.CreateCommand())
			{
				cmd.CommandText = sql;
				cmd.ExecuteNonQuery();
			}

			sql = @"CREATE TABLE StoryWarnings( 
StoryId INTEGER,
WarningId INTEGER)";
			using (var cmd = dbConn.CreateCommand())
			{
				cmd.CommandText = sql;
				cmd.ExecuteNonQuery();
			}

		}

		private IEnumerable<Meta> ParsePage(HtmlDocument doc)
		{
			var items = doc.DocumentNode.SelectNodes("//ul[@class='storylist']/li");
			var stories = new List<Meta>();
			//Parallel.ForEach(items, item => { stories.Add(ParseStory(item)); });
			foreach (var item in items)
			{
				stories.Add(ParseStory(item));
			}
			return stories;
		}

		private Meta ParseStory(HtmlNode item)
		{
			var rtn = new Meta();
			var meta = item.SelectSingleNode("p[@class='meta']").InnerText.Replace("&nbsp;", " ");

			rtn.Title = item.SelectSingleNode("h4").InnerText;
			rtn.Author = item.SelectSingleNode("span[@class='author']/a").InnerText;
			try
			{
				rtn.Characters = item.SelectSingleNode("p/span[@class='story-characters']")?.InnerText.Replace("Characters:&nbsp;", "").Split(new char[] { ',' }).ToList();
				rtn.Characters = rtn.Characters?.Select(c => c.Replace("&nbsp;", " ").Trim()).ToList();
				if (rtn.Characters == null)
					rtn.Characters = new List<string>();
			}
			catch { rtn.Characters = new List<string>(); }
			rtn.Genres = Regex.Match(meta, ".*Genres: ([^\\s-]*)").Groups[1].Value.Split(new char[] { ',' }).ToList();
			rtn.Published = DateTime.Parse(item.SelectSingleNode("p/span[@data-ts]").GetAttributeValue("title", ""));
			rtn.Rating = int.Parse(item.SelectSingleNode("div/span[@class='score_number']").InnerText);
			rtn.Summary = item.SelectSingleNode("blockquote[@class='summary']").InnerText;
			rtn.Url = "https://ficwad.com" + item.SelectSingleNode("h4 /a").GetAttributeValue("href", "");
			rtn.Warnings = item.SelectNodes("p/span[@class='story-warnings']/a")?.Select(h => h.GetAttributeValue("title", "")).ToList() ?? new List<string>();
			rtn.Words = int.Parse(Regex.Match(meta, "(\\d*) words").Groups[1].Value);
			rtn.Rated = Regex.Match(meta, "Rating: ([\\w\\-]*)").Groups[1].Value.Trim();
			try
			{
				rtn.Chapters = int.Parse(Regex.Match(meta, ".*Chapters: (\\d*)").Groups[1].Value);
			}
			catch { rtn.Chapters = 1; }
			try
			{
				rtn.Updated = DateTime.Parse(item.SelectNodes("p/span[@data-ts]")[1].GetAttributeValue("title", ""));
			}
			catch { rtn.Updated = rtn.Published; }

			rtn.Completed = System.Text.RegularExpressions.Regex.Match(meta, "(words - Complete)").Captures.Count == 1;

			Console.WriteLine("Scraped story: " + rtn.Title);
			return rtn;
		}

		public List<string> UrlsFromCalibre()
		{
			var rtn = new List<string>();
			using (var db = CalibreDatabase)
			{
				var command = db.CreateCommand();
				command.CommandText = "SELECT * FROM identifiers WHERE val LIKE '%ficwad.com%'";
				var reader = command.ExecuteReader();
				while (reader.Read())
					rtn.Add((string)reader["val"]);

				return rtn;
			}
		}
	}
}
