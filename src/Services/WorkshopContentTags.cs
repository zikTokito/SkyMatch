using System;
using System.Collections.Generic;

namespace SkyMatch
{
    /// <summary>
    /// Steam content tags that are not blocked by scenic priority. Metadata
    /// tags such as resolution/type/age are deliberately excluded.
    /// </summary>
    public static class WorkshopContentTags
    {
        public static readonly string[] Allowed =
        {
            "Abstract",
            "Animal",
            "Fantasy",
            "Landscape",
            "Nature",
            "Relaxing",
            "Retro",
            "Cyberpunk",
            "Pixel art"
        };

        public static readonly string[] Blocked =
        {
            "Game", "Anime", "Cartoon", "Girls", "Guys", "Medieval",
            "Memes", "MMD", "Music", "Sci-Fi", "Sports", "Technology",
            "Television", "Vehicle", "3D", "Unspecified"
        };

        public static readonly string[] All = BuildAll();

        private static string[] BuildAll()
        {
            List<string> result = new List<string>(Allowed);
            result.AddRange(Blocked);
            return result.ToArray();
        }

        public static int Count
        {
            get { return Allowed.Length; }
        }

        public static int AllCount
        {
            get { return All.Length; }
        }
    }
}
