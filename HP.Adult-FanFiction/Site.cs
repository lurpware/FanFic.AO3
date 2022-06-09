using FanFictionScraper.FicWad;
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

namespace FanFictionScraper.HP.Adult_FanFiction
{
	public class Site: SiteCommon
	{
		private SqliteConnection dbConn;
		private List<KeyValuePair<int, string>> characterDictionary = new List<KeyValuePair<int, string>>();
		private List<KeyValuePair<int, string>> genereDictionary = new List<KeyValuePair<int, string>>();
		private List<KeyValuePair<int, string>> warningDictionary = new List<KeyValuePair<int, string>>();
		private bool running;
		private List<Meta> stories;
		private bool doneScraping = true;
		public void SiteToDatabase()
		{
			running = true;
			doneScraping = false;
			stories = new List<Meta>();

			Driver.Navigate().GoToUrl($"http://hp.adult-fanfiction.org/index.php");
			System.Threading.Thread.Sleep(10000);
			var worker = new System.Threading.Thread(new System.Threading.ThreadStart(ProcessStories));
			worker.Start();

			for (int i = 1; i <= 1063; i++)
			{
				Console.WriteLine("Begin Processing Page: " + i);
				Driver.Navigate().GoToUrl($"http://hp.adult-fanfiction.org/index.php?page={i}");
				Wait(1000);

				HtmlDocument doc = new HtmlDocument();
				doc.LoadHtml(Driver.PageSource);
				stories.AddRange(ParsePage(doc));

				//stories.AddRange(ParsePage());
			}

			doneScraping = true;

			while (running)
			{
				System.Threading.Thread.Sleep(100);
				continue;
			}
		}

		private IEnumerable<Meta> ParsePage(HtmlDocument doc)
		{
			var items = doc.DocumentNode.SelectNodes("//ul/table[@width]");
			var stories = new List<Meta>();
			//Parallel.ForEach(items, item => { stories.Add(ParseStory(item)); });
			foreach (var item in items)
			{
				var story = ParseStory(item);
				stories.Add(story);
			}
			return stories;
		}

		private Meta ParseStory(HtmlNode item)
		{
			var rtn = new Meta();
			var metaRegexString = "(?<Title>.*) -:- By : (?<Author>.*) -:- Published : (?<Published>.*)";
			var metaRegex = new Regex(metaRegexString);
			var text = item.SelectNodes("tbody/tr/td")[0].InnerText;
			var metaMatches = metaRegex.Match(text);

			rtn.Url = item.SelectSingleNode("tbody/tr/td/b/a").GetAttributeValue("href", "");
			try
			{
				var title = metaMatches.Groups["Title"];
				rtn.Title = title.Value.Replace("&amp;", "&");
			}
			catch (Exception ex)
			{
				rtn.Title = "Unknown";
			}

			try
			{
				var author = metaMatches.Groups["Author"];
				rtn.Author = author.Value.Replace("&amp;", "&");
			}
			catch (Exception ex)
			{
				rtn.Author = "Unknown";
			}

			try
			{
				var published = metaMatches.Groups["Published"];
				rtn.Published = DateTime.Parse(published.Value);
			}
			catch (Exception ex)
			{
				rtn.Published = new DateTime();
			}

			// Second Row
			metaRegexString = "Updated : (?<Updated>.*) -:- Rated : (?<Rated>.*) -:- Chapters : (?<Chapters>.*) -:- Reviews : (?<Reviews>.*) -:- Dragon prints : (?<Points>.*)Located : .*&gt; (?<Located>.*)";
			metaRegex = new Regex(metaRegexString);
			text = item.SelectNodes("tbody/tr/td")[1].InnerText.Replace("  "," ");
			metaMatches = metaRegex.Match(text);

			try
			{
				var updated = metaMatches.Groups["Updated"];
				rtn.Updated = DateTime.Parse(updated.Value);
			}
			catch (Exception ex)
			{
				rtn.Updated = rtn.Published;
			}

			try
			{
				var chapters = metaMatches.Groups["Chapters"];
				rtn.Chapters = int.Parse(chapters.Value);
			}
			catch (Exception ex)
			{
				rtn.Chapters = 0;
			}

			try
			{
				var rated = metaMatches.Groups["Rated"];
				rtn.Rated = rated.Value;
			}
			catch (Exception ex)
			{
				rtn.Rated = "Unknown";
			}

			try
			{
				var rating = metaMatches.Groups["Points"];
				rtn.Rating = int.Parse(rating.Value);
			}
			catch (Exception ex)
			{
				rtn.Rating = 0;
			}

			try
			{
				var warnings = metaMatches.Groups["Located"];
				rtn.Warnings = warnings.Value.Trim().Replace("&gt;", ">").Replace("&amp;", "&").Split(">").Select(s => s.Trim()).ToList();
			}
			catch (Exception ex)
			{
				rtn.Warnings = new List<string>();
			}

			// Third Row
			rtn.Summary = item.SelectNodes("tbody/tr/td")[2].InnerText.Trim().Replace("&amp;", "&").Replace("�","'");

			//Forth row
			var meta = item.SelectNodes("tbody/tr/td")[3].InnerText;
			meta = meta.Replace("Content Tags : ", "").Trim();
			meta = meta.Replace("~", ",").Trim();

			if (meta.Contains(','))
				rtn.Genres = meta.Split(",", StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
			else
				rtn.Genres = meta.Split(" ", StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();

			rtn.Completed = rtn.Genres.Any(w => w.ToUpper().Contains("COMPLETE"));

			Console.WriteLine("Scraped story: " + rtn.Title);

			//AddStory(rtn);
			return rtn;
		}
		private void ProcessStories()
		{
			Console.WriteLine("Setting up database");
			var databaseFile = new FileInfo("AdultFanFiction.db");
			//if (databaseFile.Exists)
			//	databaseFile.Delete();
			var connectionStringBuilder = new SqliteConnectionStringBuilder();
			connectionStringBuilder.DataSource = databaseFile.FullName;
			var fileDb = new SqliteConnection(connectionStringBuilder.ConnectionString);


			connectionStringBuilder.DataSource = ":memory:";
			using (dbConn = new SqliteConnection(connectionStringBuilder.ConnectionString))
			{
				dbConn.Open();
				if (databaseFile.Exists)
				{
					fileDb.Open();
					fileDb.BackupDatabase(dbConn);
					fileDb.Close();
				}
				else
				{
					CreateTables();
				}
				Console.WriteLine("Ready to add Stories to database...");

				while (!doneScraping || stories.Any())
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

					if (new Random().Next() % 10 == 0)
					{
						fileDb.Open();
						dbConn.BackupDatabase(fileDb);
						fileDb.Close();
					}
				}


				dbConn.Close();
				running = false;
			}
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

				cmd.CommandText = $"SELECT id FROM Stories WHERE Url=@url";
				var storyId = cmd.ExecuteScalar();
				if (storyId != null)
				{
					cmd.CommandText = $"SELECT id FROM Stories WHERE Url=@url AND Updated=@updated";
					var updatedStoryId = cmd.ExecuteScalar();
					if (updatedStoryId != null)
						return;

					var idParam = cmd.Parameters.AddWithValue("@storyId", storyId);
					cmd.CommandText = $"DELETE FROM StoryCharacters WHERE StoryId=@storyId";
					cmd.ExecuteNonQuery();
					cmd.CommandText = $"DELETE FROM StoryGenres WHERE StoryId=@storyId";
					cmd.ExecuteNonQuery();
					cmd.CommandText = $"DELETE FROM StoryWarnings WHERE StoryId=@storyId";
					cmd.ExecuteNonQuery();
					cmd.CommandText = $"DELETE FROM Stories WHERE id=@storyId";
					cmd.ExecuteNonQuery();
					cmd.Parameters.Remove(idParam);
				}

				cmd.CommandText = $"INSERT INTO Stories (Title,Author,Published,Rating,Summary,Url,Words,Chapters,Updated,Completed,Rated) VALUES (@title,@author,@published,@rating,@summary,@url,@words,@chapters,@updated,@completed,@rated)";
				cmd.ExecuteNonQuery();

				cmd.CommandText = $"SELECT id FROM Stories WHERE Url=@url";
				storyId = cmd.ExecuteScalar();
				cmd.Parameters.AddWithValue("@storyId", storyId);

				foreach (var item in story.Characters)
				{
					if (string.IsNullOrWhiteSpace(item))
						continue;
					cmd.CommandText = $"INSERT INTO StoryCharacters (StoryId, CharacterId) VALUES (@storyId, {characterDictionary.First(k => k.Value == item).Key})";
					cmd.ExecuteNonQuery();
				}
				foreach (var item in story.Genres)
				{
					if (string.IsNullOrWhiteSpace(item))
						continue;
					cmd.CommandText = $"INSERT INTO StoryGenres (StoryId, GenreId) VALUES (@storyId, {genereDictionary.First(k => k.Value == item).Key})";
					cmd.ExecuteNonQuery();
				}
				foreach (var item in story.Warnings)
				{
					if (string.IsNullOrWhiteSpace(item))
						continue;
					cmd.CommandText = $"INSERT INTO StoryWarnings (StoryId, WarningId) VALUES (@storyId, {warningDictionary.First(k => k.Value == item).Key})";
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

	}
}
