using System;
using System.Collections.Generic;

namespace Shelly_UI.Models;

public class CachedRssModel
{
    public List<RssModel> Rss { get; set; } = [];
    public DateTime? TimeCached { get; set; } 
}
