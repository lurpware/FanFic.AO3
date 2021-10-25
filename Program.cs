using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AO3Subscriptions
{
	class Program
	{
		static void Main(string[] args)
		{
			ChromeOptions options = new ChromeOptions();
			options.AddArguments("--disable-notifications");
			//Now initialize chrome driver with chrome options which will switch off this browser notification on the chrome browser
			using (var driver = new ChromeDriver(options))
			{
				driver.Navigate().GoToUrl("https://archiveofourown.org/users/callmebob/subscriptions");
				var links = new List<string>();
				for (int i = 1; i < 70; i++)
				{
					driver.Navigate().GoToUrl("https://archiveofourown.org/users/callmebob/bookmarks?page=" + i);
					Wait(driver);
					var list = driver.FindElementsByXPath("//dl/dt/a");
					 list = driver.FindElementsByXPath("//li/div/h4/a");
					links.AddRange(list.Select(l => l.GetAttribute("href")).Where(l=>l.Contains("/works/")));
				}
				foreach (var item in links)
				{
					Console.WriteLine(item);
				}
			}
		}

		protected static void Wait(ChromeDriver driver, int minWaitInMs = 500, int maxWaitInSec = 30)
		{
			System.Threading.Thread.Sleep(minWaitInMs);
			IWait<IWebDriver> wait = new WebDriverWait(driver, TimeSpan.FromSeconds(maxWaitInSec));
			try
			{
				wait.Until(driver1 => ((IJavaScriptExecutor)driver).ExecuteScript("return document.readyState").Equals("complete"));
			}
			catch (Exception ex)
			{ }
		}
	}
}
