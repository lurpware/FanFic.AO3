using System;
using System.Collections.Generic;
using System.Text;

namespace FanFictionScraper.FicWad
{
public	class Meta
	{
		public string Title { get; set; }
		public string Author { get; set; }
		public string Summary { get; set; }
		public int Rating { get; set; }
		public string Rated { get; set; }
		public int Chapters { get; set; }
		public DateTime Published { get; set; }
		public DateTime Updated { get; set; }
		public int Words { get; set; }
		public List<string> Genres { get; set; } = new List<string>();
		public List<string> Characters { get; set; } = new List<string>();
		public List<string> Warnings { get; set; } = new List<string>();
		public string Url { get; set; }
		public bool Completed { get; set; }
	}
}
