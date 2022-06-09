using Microsoft.Data.Sqlite;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Remote;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text;

namespace FanFictionScraper
{
	public abstract class SiteCommon : IDisposable
	{
		private RemoteWebDriver driver;
		private static readonly object syncLock = new object();
		private bool internalDriver = false;

		public SiteCommon()
		{
		}

		public SiteCommon(RemoteWebDriver driver)
		{
			this.driver = driver;
		}

		public RemoteWebDriver Driver
		{
			get
			{
				lock (syncLock)
				{
					if (driver != null)
						return driver;

					driver = CreateDriver();
					internalDriver = true;
					return driver;
				}
			}
		}

		/// <summary>
		/// Creates an instance of the web browser driver
		/// </summary>
		/// <returns></returns>
		public static RemoteWebDriver CreateDriver()
		{
			ChromeOptions options = new ChromeOptions();
			options.AddArguments("--disable-notifications", "--disable-blink-features=AutomationControlled");
			options.AddAdditionalCapability("useAutomationExtension", false);
			//Now initialize chrome driver with chrome options which will switch off this browser notification on the chrome browser
			return new ChromeDriver(options);
		}

		/// <summary>
		/// Closed and Disposes of the driver
		/// </summary>
		public void ResetDriver()
		{
			lock (syncLock)
			{
				driver?.Dispose();
				driver = null;
			}
		}

		protected void Wait(int minWaitInMs = 500, int maxWaitInSec = 30)
		{
			System.Threading.Thread.Sleep(minWaitInMs);
			IWait<IWebDriver> wait = new WebDriverWait(Driver, TimeSpan.FromSeconds(maxWaitInSec));
			try
			{
				wait.Until(driver1 => ((IJavaScriptExecutor)Driver).ExecuteScript("return document.readyState").Equals("complete"));
			}
			catch (Exception ex)
			{ }
		}

		public void Dispose()
		{
			if (internalDriver)
				lock (syncLock)
				{
					driver?.Dispose();
					driver = null;
				}
		}
		public static DbConnection CalibreDatabase
		{
			get
			{
				var connectionStringBuilder = new SqliteConnectionStringBuilder();
				connectionStringBuilder.DataSource = "\\\\lurp-server\\Shares\\Dockers\\Calibre-FanFicFare\\FanFicFareLibrary\\metadata.db";
				var rtn = new SqliteConnection(connectionStringBuilder.ConnectionString);
				rtn.Open();
				return rtn;
			}
		}
	}
}
