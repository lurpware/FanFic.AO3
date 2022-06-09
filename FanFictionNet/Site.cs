using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FanFictionScraper.FanFictionNet
{
	public class Site : SiteCommon
	{
		public List<string> Subscriptions;
		public List<string> Favorites;
		public List<string> Calibre;
		/// <summary>
		/// Adds the given story Url's to the favorite list
		/// </summary>
		/// <param name="links"></param>
		public void AddToFavorites(List<string> links)
		{
			Driver.Navigate().GoToUrl("https://www.fanfiction.net/favorites/story.php");
			links = links.Select(s => s.Replace("https://www.fanfiction.net/s/", "")).ToList();
			links = links.Select(s => s.Substring(0, s.IndexOf("/"))).ToList();
			foreach (var link in links)
			{
				var input = Driver.FindElementByName("storyid");
				input.SendKeys(link);
				var button = Driver.FindElementByXPath("//button[text()='Add']");
				button.Click();
				Wait(500);
			}

			Driver.Navigate().GoToUrl("https://www.fanfiction.net/alert/story.php");
			foreach (var link in links)
			{
				var input = Driver.FindElementByName("storyid");
				input.SendKeys(link);
				var button = Driver.FindElementByXPath("//button[text()='Add']");
				button.Click();
				Wait(500);
			}
			//var line = Console.ReadLine();
		}

		/// <summary>
		/// Gets the current user's subscriptions (alerts)
		/// </summary>
		/// <returns></returns>
		public List<string> GetSubscriptions()
		{
			Driver.Navigate().GoToUrl("https://www.fanfiction.net/alert/story.php");
			Subscriptions = new List<string>();
			for (int i = 1; i < 70; i++)
			{
				Driver.Navigate().GoToUrl("https://www.fanfiction.net/alert/story.php?sort=&categoryid=0&userid=0&p=" + i);
				Wait(5000);
				var list = Driver.FindElements(By.XPath("//a[contains(@href,'/s/')]"));
				Subscriptions.AddRange(list.Select(l => l.GetAttribute("href")));
				if (list.Count == 0)
					break;
				Console.WriteLine("https://www.fanfiction.net/alert/story.php?sort=&categoryid=0&userid=0&p=" + i);

			}

			return Subscriptions;
		}

		/// <summary>
		/// Gets the current user's subscriptions (alerts)
		/// </summary>
		/// <returns></returns>
		public List<string> GetFavorites()
		{
			Driver.Navigate().GoToUrl("https://www.fanfiction.net/alert/story.php");
			Favorites = new List<string>();
			for (int i = 1; i < 70; i++)
			{
				Driver.Navigate().GoToUrl("https://www.fanfiction.net/favorites/story.php?sort=&categoryid=0&userid=0&p=" + i);
				Wait(5000);
				var list = Driver.FindElements(By.XPath("//a[contains(@href,'/s/')]"));
				Favorites.AddRange(list.Select(l => l.GetAttribute("href")));
				if (list.Count == 0)
					break;
				Console.WriteLine("https://www.fanfiction.net/favorites/story.php?sort=&categoryid=0&userid=0&p=" + i);

			}

			return Favorites;
		}

		public List<string> UrlsFromCalibre()
		{
			Calibre = new List<string>();
			using (var db = CalibreDatabase)
			{
				using (var command = db.CreateCommand())
				{
					command.CommandText = "SELECT * FROM identifiers WHERE val LIKE '%fanfiction.net%'";
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
			var favorites = GetFavorites();
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
			Console.WriteLine();
			Console.WriteLine();
			foreach (var item in favorites.Except(subscriptions))
			{
				Console.WriteLine(item);
			}

			Console.WriteLine();
			Console.WriteLine();
			Console.WriteLine();
			Console.WriteLine("Need to add a favorite: ");
			foreach (var item in subscriptions.Except(favorites))
			{
				Console.WriteLine(item);
			}

			AddToFavorites(missingSubscriptions);
			AddToFavorites(favorites.Except(subscriptions).ToList());
			AddToFavorites(subscriptions.Except(favorites).ToList());
		}
	}
}
