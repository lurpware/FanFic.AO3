using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FanFictionScraper
{
	class Program
	{
		static void Main(string[] args)
		{
			var ao3 = new ArchiveOfOurOwn.Site();
			var ffn = new FanFictionNet.Site();
			var ficwad = new FicWad.Site();
			var hpaff = new HP.Adult_FanFiction.Site();

			hpaff.SiteToDatabase();

			ffn.FixSubscriptions();
			ao3.FixSubscriptions();

			var toAdd = ffn.Favorites.Except(ffn.Calibre).ToList();
			toAdd.AddRange(ffn.Subscriptions.Except(ffn.Calibre));
			toAdd.AddRange(ao3.Favorites.Except(ao3.Calibre));
			toAdd.AddRange(ao3.Subscriptions.Except(ao3.Calibre));
			toAdd = toAdd.Distinct().ToList();

			Console.Clear();
			Console.WriteLine("Need to add to Calibre:");
			foreach (var item in toAdd)
				Console.WriteLine(item);
		}










	}
}
