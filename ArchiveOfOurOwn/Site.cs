using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FanFictionScraper.ArchiveOfOurOwn
{
	public class Site : SiteCommon
	{
		public List<string> Subscriptions;
		public List<string> Favorites = new List<string>();
		public List<string> Calibre;

		/// <summary>
		/// Gets the current user's Bookmarks
		/// </summary>
		/// <returns></returns>
		public List<string> GetBookmarks()
		{
			Driver.Navigate().GoToUrl("https://archiveofourown.org/users/callmebob/bookmarks");
			var links = new List<string>();
			for (int i = 1; i < 70; i++)
			{
				Driver.Navigate().GoToUrl("https://archiveofourown.org/users/callmebob/bookmarks?page=" + i);
				Wait();
				var list = Driver.FindElements(By.XPath("//li/div/h4/a"));
				links.AddRange(list.Select(l => l.GetAttribute("href")).Where(l => l.Contains("/works/")));
			}
			links = links.Distinct().ToList();
			Console.Clear();
			foreach (var item in links)
			{
				Console.WriteLine(item);
			}

			return links;
		}

		/// <summary>
		/// Gets the current user's Subscriptions
		/// </summary>
		/// <returns></returns>
		public List<string> GetSubscriptions()
		{
			Driver.Navigate().GoToUrl("https://archiveofourown.org/users/callmebob/subscriptions");
			Subscriptions = new List<string>();
			var linkCount = 0;
			for (int i = 1; i < 200; i++)
			{
				Driver.Navigate().GoToUrl("https://archiveofourown.org/users/callmebob/subscriptions?page=" + i);
				Wait();
				var list = Driver.FindElements(By.XPath("//dl/dt/a"));
				Subscriptions.AddRange(list.Select(l => l.GetAttribute("href")).Where(l => l.Contains("/works/")));
				if (linkCount == Subscriptions.Count)
					break;
				linkCount = Subscriptions.Count;
				Console.WriteLine("https://archiveofourown.org/users/callmebob/subscriptions?page=" + i);
			}
			Subscriptions = Subscriptions.Distinct().ToList();

			return Subscriptions;
		}

		public List<string> UrlsFromCalibre()
		{
			Calibre = new List<string>();
			using (var db = CalibreDatabase)
			{
				using (var command = db.CreateCommand())
				{
					command.CommandText = "SELECT * FROM identifiers WHERE val LIKE '%archiveofourown.org%'";
					var reader = command.ExecuteReader();
					while (reader.Read())
						Calibre.Add((string)reader["val"]);

					return Calibre;
				}
			}
		}

		public void FixSubscriptions()
		{
			var subscriptions = GetSubscriptions();
			var calibreUrls = UrlsFromCalibre();

			var missingInCalibre = subscriptions.Except(calibreUrls).ToList();
			var missingSubscriptions = calibreUrls.Except(subscriptions).ToList();

			Console.Clear();
			Console.WriteLine("Need to add to Calibre: ");
			foreach (var item in missingInCalibre)
			{
				Console.WriteLine(item);
			}

			Console.WriteLine();
			Console.WriteLine();
			Console.WriteLine();
			Console.WriteLine("Need to add a subscription: ");
			foreach (var item in missingSubscriptions)
			{
				Console.WriteLine(item);
			}
		}
	}
}
