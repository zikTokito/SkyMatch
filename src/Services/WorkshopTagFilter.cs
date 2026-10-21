using System;
using System.Collections.Generic;

namespace SkyMatch
{
    /// <summary>
    /// Workshop-only tag exclusions. Local downloaded wallpapers are never
    /// removed by this filter.
    /// </summary>
    public static class WorkshopTagFilter
    {
        private static readonly HashSet<string> Excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "卡通", "cartoon",
            "动漫", "anime",
            "游戏", "game", "games",
            "女性", "girls", "female",
            "男性", "guys", "male",
            "中世纪", "medieval",
            "网红事物", "memes", "meme",
            "mmd",
            "音乐", "music",
            "科幻", "sci-fi", "scifi", "science fiction",
            "运动", "sports", "sport",
            "科技", "technology", "tech",
            "电视节目", "television", "tv",
            "汽车", "vehicle", "vehicles", "car", "cars", "automobile",
            "3d",
            "未指定样式", "unspecified"
        };

        public static bool IsExcluded(IEnumerable<string> tags)
        {
            if (tags == null) return false;
            foreach (string tag in tags)
            {
                if (String.IsNullOrEmpty(tag)) continue;
                if (Excluded.Contains(tag.Trim())) return true;
            }
            return false;
        }
    }
}
