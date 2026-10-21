using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace SkyMatch
{
    public class VisualFeatures
    {
        public double Brightness { get; set; }
        public double Saturation { get; set; }
        public double Warmth { get; set; }
        public double Coolness { get; set; }
        public double Purple { get; set; }
        public double Green { get; set; }
        public double Contrast { get; set; }
        public double DarkRatio { get; set; }
        public double BrightRatio { get; set; }
        public double GrayRatio { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool HasImage { get; set; }
    }

    public class WallpaperClassification
    {
        public Dictionary<WeatherKind, double> Scores { get; set; }
        public Dictionary<WeatherKind, List<string>> KeywordHits { get; set; }
        public WeatherKind Primary { get; set; }
        public double Confidence { get; set; }
        public string Reason { get; set; }
        public Dictionary<TimeSlot, double> TimeScores { get; set; }
        public TimeSlot TimePrimary { get; set; }
        public double TimeConfidence { get; set; }
        public string TimeReason { get; set; }
        public bool IsSuitable { get; set; }
        public string ExcludeReason { get; set; }
        public bool CharacterFocus { get; set; }
        public bool HasPeople { get; set; }
        public bool PeopleScanned { get; set; }
        public VisualFeatures Features { get; set; }
    }

    public static class WallpaperClassifier
    {
        private static readonly Dictionary<WeatherKind, string[]> Keywords =
            new Dictionary<WeatherKind, string[]>
            {
                {
                    WeatherKind.ClearDay, new[]
                    {
                        "晴", "晴朗", "阳光", "日光", "日出", "日落", "黄昏", "夕阳", "晨曦",
                        "白天", "蓝天", "sunny", "sunshine", "sunset", "sunrise", "dawn", "dusk",
                        "golden hour", "clear sky", "blue sky", "daylight", "sea", "ocean", "beach"
                    }
                },
                {
                    WeatherKind.ClearNight, new[]
                    {
                        "夜", "夜晚", "夜空", "星夜", "月", "月亮", "月色", "星", "星空", "星河",
                        "霓虹", "灯", "灯火", "夜色", "night", "midnight", "moon", "starry",
                        "stars", "neon", "after dark", "city lights", "nocturne"
                    }
                },
                {
                    WeatherKind.Cloudy, new[]
                    {
                        "云朵", "云层", "多云", "浮云", "白云", "云海", "天空", "cloud", "clouds",
                        "partly cloudy", "skyscape", "sky"
                    }
                },
                {
                    WeatherKind.Overcast, new[]
                    {
                        "阴", "阴天", "灰暗", "灰调", "沉闷", "overcast", "grey", "gray",
                        "gloomy", "moody", "desaturated"
                    }
                },
                {
                    WeatherKind.Rain, new[]
                    {
                        "雨", "下雨", "雨滴", "雨天", "暴雨", "小雨", "中雨", "大雨", "阵雨",
                        "雨夜", "潮湿", "rain", "rainy", "rainfall", "drizzle", "wet", "umbrella",
                        "monsoon", "raining"
                    }
                },
                {
                    WeatherKind.Storm, new[]
                    {
                        "雷", "雷电", "雷雨", "雷暴", "闪电", "风暴", "暴风雨", "骤雨",
                        "thunder", "lightning", "storm", "tempest", "thunderstorm", "dramatic sky"
                    }
                },
                {
                    WeatherKind.Snow, new[]
                    {
                        "雪", "下雪", "雪花", "雪景", "雪山", "积雪", "寒冬", "冰雪", "霜",
                        "snow", "snowy", "winter", "frost", "ice", "blizzard", "glacier", "glacial"
                    }
                },
                {
                    WeatherKind.Fog, new[]
                    {
                        "雾", "雾气", "大雾", "雾天", "薄雾", "烟雾", "朦胧", "迷蒙", "雾霭",
                        "fog", "foggy", "mist", "misty", "haze", "hazy", "smoke", "vapor"
                    }
                }
            };

        private static readonly Dictionary<TimeSlot, string[]> TimeKeywords =
            new Dictionary<TimeSlot, string[]>
            {
                {
                    TimeSlot.Dawn, new[]
                    {
                        "清晨", "晨曦", "朝霞", "黎明", "破晓", "日出", "晨光", "早晨",
                        "dawn", "sunrise", "daybreak", "morning", "first light"
                    }
                },
                {
                    TimeSlot.Day, new[]
                    {
                        "白天", "日间", "正午", "午后", "daylight", "daytime", "noon",
                        "afternoon", "sunny day", "bright day"
                    }
                },
                {
                    TimeSlot.Dusk, new[]
                    {
                        "黄昏", "夕阳", "日落", "晚霞", "暮色", "落日", "傍晚", "余晖",
                        "sunset", "dusk", "twilight", "evening", "golden hour"
                    }
                },
                {
                    TimeSlot.Night, new[]
                    {
                        "夜", "夜晚", "夜空", "星夜", "月", "月亮", "月色", "星", "星空",
                        "星河", "霓虹", "灯火", "night", "midnight", "moon", "starry",
                        "stars", "neon", "after dark", "city lights", "nocturne"
                    }
                }
            };

        // Franchise/game names often contain weather or time words ("Twilight Creed",
        // "Rain World", "Sunset Overdrive"). They are removed before keyword matching so
        // the visual classifier is not pulled toward a misleading category.
        private static readonly string[] NoisePhrases =
        {
            "assassin's creed", "assassins creed", "刺客信条", "奥德赛", "odyssey",
            "暮光信条", "twilight creed", "creed", "cyberpunk 2077", "cyberpunk",
            "赛博朋克", "the witcher", "巫师", "elden ring", "艾尔登法环",
            "black myth", "黑神话", "genshin impact", "原神", "honkai", "崩坏",
            "star rail", "星穹铁道", "arknights", "明日方舟", "league of legends",
            "英雄联盟", "valorant", "无畏契约", "overwatch", "守望先锋",
            "wuthering waves", "鸣潮", "zenless zone zero", "绝区零",
            "blue archive", "蔚蓝档案", "girls' frontline", "少女前线",
            "azur lane", "碧蓝航线", "dota", "apex legends", "apex", "pubg",
            "绝地求生", "grand theft auto", "gta", "red dead", "荒野大镖客",
            "the last of us", "最后生还者", "god of war", "战神", "final fantasy",
            "最终幻想", "resident evil", "生化危机", "silent hill", "寂静岭",
            "death stranding", "死亡搁浅", "ghost of tsushima", "对马岛",
            "sekiro", "只狼", "dark souls", "黑暗之魂", "minecraft", "我的世界",
            "fortnite", "堡垒之夜", "call of duty", "使命召唤", "battlefield",
            "战地", "diablo", "暗黑破坏神", "world of warcraft", "魔兽世界",
            "starcraft", "星际争霸", "hearthstone", "炉石传说", "honor of kings",
            "王者荣耀", "naraka", "永劫无间", "justice online", "逆水寒",
            "jianwang", "剑网", "moonlight blade", "天涯明月刀", "gujian", "古剑",
            "xianjian", "仙剑", "vocaloid", "初音", "hatsune miku", "洛天依",
            "touhou", "东方project", "sunset overdrive", "rain world", "snowrunner",
            "frostpunk", "stormworks", "cloudpunk", "night in the woods"
        };

        private static readonly string[] HardRejectTerms =
        {            "头像", "avatar", "profile picture", "profile pic", "profile icon",
            "headshot", "bust", "half-body", "half body", "半身", "人物头像",
            "角色头像", "portrait", "手机壁纸", "phone wallpaper", "mobile wallpaper",
            "竖屏", "vertical wallpaper", "1:1", "square", "方形", "logo",
            "纯文字", "text only", "text-only", "quote", "引言", "文字壁纸",
            "test wallpaper", "测试壁纸", "demo", "模板", "template", "未完成",
            "wip", "collection", "合集", "pack", "整合包", "compilation",
            "request", "求图", "commission", "定制壁纸", "tutorial", "教程"
        };

        // These are not hard exclusions: character-led wallpapers are still valid
        // wallpapers, but they should not outrank scenery when everything else is equal.
        private static readonly string[] CharacterTerms =
        {
            "anime", "girl", "girls", "waifu", "character", "fanart", "cosplay",
            "人物", "角色", "少女", "美少女", "立绘", "同人", "女仆", "巫女",
            "偶像", "vtuber", "hololive", "nikke", "blue archive", "genshin",
            "原神", "崩坏", "明日方舟", "fate", "初音", "洛天依", "东方",
            "frieren", "芙莉莲", "艾希", "icey", "hazel", "medusa",
            "odyssey", "creed", "assassin", "twilight creed", "奥德赛", "暮光信条",
            "people", "portrait", "idol", "maid", "cosplay", "vtuber", "waifu",
            "visual novel", "galgame", "eroge", "character art", "character design",
            "cartoon", "furry", "sports"
        };

        // Common character / franchise names that are often not tagged as anime.
        // Kept as full names where possible to avoid matching scenery words.
        private static readonly string[] CharacterNameTerms =
        {
            "祀满潜", "终末地", "王从天降", "奶龙的救赎", "奶龙", "雏田", "星塔旅人",
            "心月狐", "爱弥斯", "绯雪", "清宵", "小钇", "乡间奶蛙", "芙露德莉斯", "蕾米埃尔",
            "苏雨果", "老八", "若不是你突然闯进我生活", "Shrek", "Lucía", "Lucia",
            "苏莓果", "顾冷冷",
            "GTA", "Valorant", "Loud valorant", "FURRY", "Barrie", "脓疱",
            "魂类画师", "子非鱼", "림버스", "Limbus", "Acheron", "HSR",
            "林初雪", "云羽", "蓝皮书", "Shadow Fiend", "Nevermore", "杨幂", "小丑",
            "直到大地变成一颗脓疱", "你可以永远看着它", "Faust", "KGF1",
            "桐条美鹤", "结城理", "Dohrn", "DeepSeek", "NIKKE", "KDA", "K/DA",
            "FIRESTARTER", "Leon", "Resident Evil", "Life Is Strange", "Aporia",
            "Chrome Heart", "Malfoid", "Earthbound", "Hollow Knight", "Celeste",
            "Hades", "Ori and the Blind Forest", "2B", "NieR", "Nier",
            "Punishing Gray Raven", "Punishing", "Arknights", "Azur Lane",
            "Girls Frontline", "Honkai", "Star Rail", "Zenless Zone Zero", "Wuthering Waves",
            "鸣潮", "绝区零", "星穹铁道", "碧蓝档案", "胜利女神", "妮姬", "英雄联盟",
            "无畏契约", "守望先锋", "艾尔登法环", "黑神话", "赛博朋克", "最终幻想",
            "蒂法", "爱丽丝", "尤菲", "克劳德", "萨菲罗斯", "初音未来", "镜音铃",
            "巡音流歌", "MEIKO", "KAITO", "GUMI", "洛天依", "言和", "乐正绫",
            "神里绫华", "胡桃", "甘雨", "雷电将军", "八重神子", "纳西妲", "芙宁娜",
            "刻晴", "莫娜", "可莉", "温迪", "钟离", "达达利亚", "宵宫", "珊瑚宫心海",
            "妮露", "迪希雅", "赛诺", "提纳里", "艾尔海森", "流浪者", "珐露珊",
            "莱依拉", "米卡", "卡维", "绮良良", "林尼", "琳妮特", "菲米尼",
            "那维莱特", "莱欧斯利", "夏洛蒂", "娜维娅", "夏沃蕾", "千织", "阿蕾奇诺",
            "克洛琳德", "希格雯", "艾梅莉埃", "玛拉妮", "基尼奇", "希诺宁", "恰斯卡",
            "玛薇卡", "茜特菈莉", "蓝砚", "梦见月瑞希", "瓦蕾莎", "伊安珊", "爱可菲",
            "丝柯克", "塔利雅", "伊涅芙", "菲林斯", "奈芙尔", "杜林", "雅珂达",
            "三月七", "丹恒", "姬子", "瓦尔特", "布洛妮娅", "希儿", "克拉拉", "史瓦罗",
            "杰帕德", "佩拉", "桑博", "娜塔莎", "虎克", "卢卡", "希露瓦", "卡芙卡",
            "银狼", "罗刹", "景元", "停云", "素裳", "白露", "彦卿", "符玄", "镜流",
            "托帕", "桂乃芬", "藿藿", "寒鸦", "雪衣", "阮·梅", "真理医生", "花火",
            "黑天鹅", "黄泉", "砂金", "知更鸟", "波提欧", "流萤", "翡翠", "云璃",
            "椒丘", "飞霄", "灵砂", "貊泽", "乱破", "星期日", "忘归人", "大黑塔",
            "阿格莱雅", "缇宝", "万敌", "遐蝶", "那刻夏", "风堇", "赛飞儿", "白厄",
            "海瑟音", "刻律德菈", "长夜月", "昔涟", "白子", "星野", "日奈", "阿露",
            "优香", "诺亚", "爱丽丝", "桃井", "柚子", "泉奈", "若藻", "阿罗娜",
            "普拉娜", "圣娅", "未花", "小春", "花子", "日富美", "小玉", "真纪",
            "千寻", "真白", "美咲", "日和", "纱织", "敦子", "圣园未花",
            "拉毗", "阿妮斯", "尼恩", "红莲", "长发公主", "白雪公主", "桃乐丝",
            "爱丽丝", "尤妮", "毒蛇", "豺狼", "饼干", "罗珊娜", "布兰",
            "诺亚尔", "玛律恰那", "皇冠", "那由他", "灰姑娘", "格拉维", "拉普拉斯",
            "德雷克", "麦斯威尔", "艾玛", "银华", "贝斯蒂", "普琳", "蒂亚", "娜嘉",
            "特罗妮", "鲁德米拉", "米兰达", "波莉", "迪塞尔", "索达", "尤利娅",
            "贝伊", "诺伊斯", "露菲", "克拉乌", "尤尔夏", "舒格", "艾克希亚",
            "星街彗星", "樱巫女", "白上吹雪", "凑阿库娅", "大空昴", "戌神沁音",
            "猫又小粥", "兔田佩克拉", "宝钟玛琳", "白银诺艾尔", "不知火芙蕾雅",
            "常暗永远", "天音彼方", "角卷绵芽", "星野露娜", "尾丸波尔卡",
            "夜乃空", "周防帕特拉", "噶呜·古拉", "华生·艾米莉亚", "一伊那尔栖",
            "森美声", "小鸟游琪亚拉", "哈珂斯·贝尔斯", "伊莉莎白·露丝",
            "可波·卡娜埃露", "塞莱斯·法乌娜", "吉吉·穆林", "莉泽·赫露艾斯塔",
            "安洁·卡特莉娜", "月之美兔", "静凛", "樋口枫", "本间向日葵", "笹木咲",
            "椎名唯华", "星川莎拉", "町田千麻", "森中花咲", "阿狸", "拉克丝",
            "金克丝", "卡特琳娜", "阿卡丽", "卡莎", "萨勒芬妮", "伊泽瑞尔", "亚索",
            "永恩", "瑟提", "厄斐琉斯", "格温", "辛德拉", "婕拉", "莫甘娜", "凯尔",
            "蕾欧娜", "戴安娜", "佐伊", "妮蔻", "莉莉娅", "瑟庄妮", "艾瑞莉娅",
            "锐雯", "菲奥娜", "卡蜜尔", "杰斯", "维克托", "梅尔", "安蓓萨",
            "捷风", "贤者", "雷兹", "蝰蛇", "霓虹", "蕾娜", "斯凯", "奇乐",
            "黑梦", "铁臂", "幽影", "不死鸟", "猎枭", "尚勃勒", "盖可", "维斯",
            "钛狐", "幻棱", "猎空", "天使", "D.Va", "黑百合", "艾什", "雾子",
            "朱诺", "伊拉锐", "生命之梭", "索杰恩", "查莉娅", "布丽吉塔",
            "法老之鹰", "黑影", "秩序之光", "莫伊拉", "Saber", "阿尔托莉雅",
            "远坂凛", "间桐樱", "伊莉雅", "玛修", "贞德", "尼禄", "斯卡哈",
            "玉藻前", "吉尔伽美什", "卫宫", "士郎", "2B", "尼尔",
            "蕾姆", "拉姆", "艾米莉亚", "亚丝娜", "桐人", "诗乃", "三笠",
            "艾伦", "利威尔", "阿尔敏", "萨莎", "韩吉", "祢豆子", "炭治郎",
            "善逸", "伊之助", "富冈义勇", "蝴蝶忍", "玛奇玛", "电次", "帕瓦",
            "早川秋", "约尔", "阿尼亚", "尤里", "碧蓝航线", "少女前线",
            "战双帕弥什", "蔚蓝档案", "葬送的芙莉莲", "辉夜", "四宫辉夜",
            "白银御行", "藤原千花", "石上优", "伊井野弥子", "雪之下雪乃",
            "比企谷八幡", "由比滨结衣", "一色彩羽", "加藤惠", "泽村英梨梨",
            "霞之丘诗羽", "和泉纱雾", "和泉正宗", "高坂桐乃", "黑猫",
            "新垣绫濑", "五更琉璃", "时崎狂三", "夜刀神十香", "鸢一折纸",
            "五河琴里", "四糸乃", "诱宵美九", "崇宫澪", "本条二亚", "星野爱",
            "露比", "阿库亚", "有马加奈", "黑川赤音", "MEM啾", "壹护",
            "朽木露琪亚", "井上织姬", "石田雨龙", "茶渡泰虎", "浦原喜助",
            "四枫院夜一", "蓝染惣右介", "市丸银", "东仙要", "更木剑八",
            "草鹿八千流", "斑目一角", "绫濑川弓亲", "桧佐木修兵", "日番谷冬狮郎",
            "松本乱菊", "雏森桃", "蓝染", "剑八"
        };

        public static void ClassifyAll(IList<WallpaperItem> wallpapers, AppSettings settings)
        {
            foreach (WallpaperItem item in wallpapers)
            {
                WallpaperClassification classification = ClassifyOne(item, settings);
                SetAutoResult(item, classification);
                ApplyManualOverride(item, settings);
            }
        }

        public static WallpaperClassification ClassifyOne(WallpaperItem item)
        {
            return ClassifyOne(item, null);
        }

        public static WallpaperClassification ClassifyOne(WallpaperItem item, AppSettings settings)
        {
            VisualFeatures features = AnalyzePreview(item.PreviewPath);
            WallpaperClassification heuristic = ClassifyCore(item, features);
            bool blankPreview = item.PreviewQualityChecked
                ? item.BlankPreview : PreviewQuality.IsSolidColor(item.PreviewPath);
            item.BlankPreview = blankPreview;
            item.PreviewQualityChecked = true;
            if (blankPreview && item.IsRemote)
            {
                heuristic.IsSuitable = false;
                heuristic.ExcludeReason = "预览为单色块，已过滤";
                heuristic.PeopleScanned = true;
                return heuristic;
            }
            if (blankPreview)
            {
                // A local wallpaper the user already downloaded must remain
                // visible in the library even if its preview is black.
                heuristic.IsSuitable = true;
                heuristic.ExcludeReason = "";
                heuristic.PeopleScanned = true;
                return heuristic;
            }
            bool yoloPeople = PersonDetector.HasPerson(item.PreviewPath);
            heuristic.HasPeople = heuristic.HasPeople || yoloPeople;
            heuristic.PeopleScanned = true;
            if (!ClipVisionClassifier.IsAvailableFor("clip_large")) return heuristic;

            ClipVisionResult clip = ClipVisionClassifier.Classify(item.PreviewPath, "clip_large");
            if (clip == null || !clip.Available) return heuristic;

            WallpaperClassification result = new WallpaperClassification();
            result.Features = heuristic.Features;
            result.IsSuitable = heuristic.IsSuitable;
            result.ExcludeReason = heuristic.ExcludeReason;
            result.KeywordHits = new Dictionary<WeatherKind, List<string>>();
            result.CharacterFocus = clip.CharacterFocus || HasCharacterNameInTitle(item.Title);
            result.HasPeople = yoloPeople || result.CharacterFocus;
            result.PeopleScanned = true;

            bool useHeuristicWeather = false;
            result.Primary = useHeuristicWeather ? heuristic.Primary : clip.Weather;
            result.Confidence = useHeuristicWeather ? heuristic.Confidence : clip.WeatherConfidence;
            result.Scores = new Dictionary<WeatherKind, double>();
            foreach (WeatherKind kind in WeatherKindInfo.All)
            {
                double value = 0;
                if (!useHeuristicWeather && clip.WeatherScores.TryGetValue(kind, out value)) { }
                else if (useHeuristicWeather && heuristic.Scores.TryGetValue(kind, out value)) { }
                result.Scores[kind] = value;
            }

            result.TimePrimary = clip.Time;
            result.TimeConfidence = clip.TimeConfidence;
            result.TimeScores = new Dictionary<TimeSlot, double>();
            foreach (TimeSlot slot in TimeSlotInfo.All)
            {
                double value = 0;
                clip.TimeScores.TryGetValue(slot, out value);
                result.TimeScores[slot] = value;
            }
            result.Reason = "CLIP 识图 · " + (result.CharacterFocus ? "主人物" : "主风景")
                + " · 天气「" + WeatherKindInfo.Name(clip.Weather) + "」"
                + (useHeuristicWeather ? "（文字修正为「" + WeatherKindInfo.Name(heuristic.Primary) + "」）" : "")
                + " · 时段「" + TimeSlotInfo.Name(clip.Time) + "」"
                + " · 天气 " + Math.Round(clip.WeatherConfidence) + "%"
                + " · 时段 " + Math.Round(clip.TimeConfidence) + "%";
            result.TimeReason = result.Reason;
            return result;
        }

        public static WallpaperClassification ClassifyQuick(WallpaperItem item)
        {
            VisualFeatures features = new VisualFeatures();
            features.HasImage = false;
            return ClassifyCore(item, features);
        }

        private static WallpaperClassification ClassifyCore(WallpaperItem item, VisualFeatures features)
        {
            item.Brightness = features.Brightness;
            item.Saturation = features.Saturation;
            item.Warmth = features.Warmth;
            item.Coolness = features.Coolness;
            item.Contrast = features.Contrast;

            string rawText = BuildRawText(item);
            // Chinese weather words in a wallpaper title are too noisy (for
            // example "雨" in a character name). Keep English title words and
            // use tags/description as textual hints; visual features carry
            // most of the weight now.
            string keywordText = SanitizeNoise(BuildKeywordText(item));
            string suitability = CheckSuitability(rawText, features);
            Dictionary<WeatherKind, List<string>> hits = FindKeywordHits(keywordText);
            Dictionary<WeatherKind, double> scores = new Dictionary<WeatherKind, double>();
            foreach (WeatherKind kind in WeatherKindInfo.All)
            {
                double keywordScore = KeywordScore(kind, keywordText);
                double visualScore = VisualScore(kind, features);
                scores[kind] = Math.Max(-2.0, keywordScore * 0.72 + visualScore * 1.85);
            }

            WeatherKind top = WeatherKind.ClearDay;
            double topScore = Double.MinValue;
            double secondScore = Double.MinValue;
            foreach (WeatherKind kind in WeatherKindInfo.All)
            {
                double score = scores[kind];
                if (score > topScore)
                {
                    secondScore = topScore;
                    topScore = score;
                    top = kind;
                }
                else if (score > secondScore)
                {
                    secondScore = score;
                }
            }
            if (secondScore == Double.MinValue) secondScore = 0;

            double confidence = 58.0 + 38.0 * ((topScore - secondScore) / (Math.Abs(secondScore) + 2.5));
            confidence = Math.Max(50.0, Math.Min(96.0, confidence));
            string reason = BuildReason(top, hits, features, !features.HasImage);

            Dictionary<TimeSlot, List<string>> timeHits = FindTimeHits(keywordText);
            Dictionary<TimeSlot, double> timeScores = new Dictionary<TimeSlot, double>();
            foreach (TimeSlot slot in TimeSlotInfo.All)
            {
                double keywordScore = TimeKeywordScore(slot, keywordText);
                double visualScore = VisualTimeScore(slot, features);
                timeScores[slot] = Math.Max(-2.0, keywordScore * 0.78 + visualScore * 1.65);
            }
            TimeSlot timeTop = TimeSlot.Day;
            double timeTopScore = Double.MinValue;
            double timeSecondScore = Double.MinValue;
            foreach (TimeSlot slot in TimeSlotInfo.All)
            {
                double score = timeScores[slot];
                if (score > timeTopScore)
                {
                    timeSecondScore = timeTopScore;
                    timeTopScore = score;
                    timeTop = slot;
                }
                else if (score > timeSecondScore)
                {
                    timeSecondScore = score;
                }
            }
            if (timeSecondScore == Double.MinValue) timeSecondScore = 0;
            double timeConfidence = 58.0 + 38.0 * ((timeTopScore - timeSecondScore)
                / (Math.Abs(timeSecondScore) + 2.5));
            timeConfidence = Math.Max(50.0, Math.Min(96.0, timeConfidence));
            string timeReason = BuildTimeReason(timeTop, timeHits, features, !features.HasImage);

            WallpaperClassification result = new WallpaperClassification();
            result.Scores = scores;
            result.KeywordHits = hits;
            result.Primary = top;
            result.Confidence = confidence;
            result.Reason = reason;
            result.TimeScores = timeScores;
            result.TimePrimary = timeTop;
            result.TimeConfidence = timeConfidence;
            result.TimeReason = timeReason;
            result.IsSuitable = String.IsNullOrEmpty(suitability);
            result.ExcludeReason = suitability;
            result.CharacterFocus = HasCharacterFocus(rawText);
            result.HasPeople = result.CharacterFocus;
            result.Features = features;
            return result;
        }

        public static void ApplyManualOverride(WallpaperItem item, AppSettings settings)
        {
            if (item == null) return;
            int manualValue = 0;
            bool manual = settings != null && settings.ManualKinds != null
                && settings.ManualKinds.TryGetValue(item.Id, out manualValue);
            if (manual)
            {
                item.ManualKind = manualValue;
                item.PrimaryWeather = (WeatherKind)manualValue;
                item.Confidence = 100;
                string autoName = WeatherKindInfo.Name(item.AutoPrimaryWeather);
                item.Reason = "已手动锁定为「" + WeatherKindInfo.Name(item.PrimaryWeather)
                    + "」" + (item.AutoPrimaryWeather == item.PrimaryWeather ? "" : "，自动识别原为「" + autoName + "」");
            }
            else
            {
                item.ManualKind = null;
                item.PrimaryWeather = item.AutoPrimaryWeather;
                item.Confidence = item.AutoConfidence;
                item.Reason = item.AutoReason;
            }
            item.Hidden = settings != null && settings.HiddenIds != null && settings.HiddenIds.Contains(item.Id);
        }

        public static void SetAutoResult(WallpaperItem item, WallpaperClassification c)
        {
            item.AutoScores = c.Scores;
            item.Scores = new Dictionary<WeatherKind, double>(c.Scores);
            item.AutoPrimaryWeather = c.Primary;
            item.AutoConfidence = c.Confidence;
            item.AutoReason = c.Reason;
            item.IsSuitable = (item.IsRemote && item.BlankPreview) ? false : c.IsSuitable;
            item.ExcludeReason = (item.IsRemote && item.BlankPreview)
                ? "预览为单色块，已过滤" : c.ExcludeReason;
            item.CharacterFocus = c.CharacterFocus;
            item.HasPeople = c.HasPeople;
            item.PeopleScanned = c.PeopleScanned;
            item.PrimaryWeather = c.Primary;
            item.Confidence = c.Confidence;
            item.Reason = c.Reason;
            item.ManualKind = null;

            item.AutoTimeScores = c.TimeScores;
            item.TimeScores = new Dictionary<TimeSlot, double>(c.TimeScores);
            item.AutoPrimaryTime = c.TimePrimary;
            item.AutoTimeConfidence = c.TimeConfidence;
            item.AutoTimeReason = c.TimeReason;
            item.PrimaryTime = c.TimePrimary;
            item.TimeConfidence = c.TimeConfidence;
            item.TimeReason = c.TimeReason;
        }

        public static string ConfidenceLabel(double confidence)
        {
            if (confidence >= 82) return "高";
            if (confidence >= 65) return "中";
            return "低";
        }

        private static string BuildKeywordText(WallpaperItem item)
        {
            StringBuilder text = new StringBuilder();
            if (!String.IsNullOrEmpty(item.Title))
            {
                foreach (char c in item.Title)
                {
                    if (c < 128) text.Append(c);
                    else text.Append(' ');
                }
                text.Append(' ');
            }
            if (item.Tags != null)
            {
                foreach (string tag in item.Tags) text.Append(tag).Append(' ');
            }
            if (!String.IsNullOrEmpty(item.Description)) text.Append(item.Description).Append(' ');
            return text.ToString();
        }

        private static string BuildRawText(WallpaperItem item)
        {
            StringBuilder text = new StringBuilder();
            text.Append(item.Title).Append(" ");
            if (item.Tags != null)
            {
                foreach (string tag in item.Tags) text.Append(tag).Append(" ");
            }
            if (!String.IsNullOrEmpty(item.Description)) text.Append(item.Description).Append(" ");
            if (!String.IsNullOrEmpty(item.FilePath))
            {
                try { text.Append(Path.GetFileNameWithoutExtension(item.FilePath)).Append(" "); }
                catch { }
            }
            return text.ToString().ToLowerInvariant();
        }

        private static string SanitizeNoise(string text)
        {
            string lowered = text;
            foreach (string phrase in NoisePhrases)
                lowered = lowered.Replace(phrase, " ");
            return lowered;
        }

        private static string CheckSuitability(string text, VisualFeatures features)
        {
            foreach (string term in HardRejectTerms)
            {
                if (ContainsKeyword(text, term))
                    return "不适合作为桌面壁纸：包含「" + term + "」";
            }
            return "";
        }

        public static bool LooksLikeCharacter(WallpaperItem item)
        {
            if (item == null) return false;
            return HasCharacterFocus(BuildRawText(item));
        }

        private static bool HasCharacterNameInTitle(string title)
        {
            if (String.IsNullOrEmpty(title)) return false;
            string text = title.ToLowerInvariant();
            foreach (string term in CharacterNameTerms)
            {
                if (!String.IsNullOrEmpty(term) && text.Contains(term.ToLowerInvariant())) return true;
            }
            return false;
        }

        private static bool HasCharacterFocus(string text)
        {
            foreach (string term in CharacterTerms)
                if (text.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            foreach (string term in CharacterNameTerms)
                if (text.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private static Dictionary<WeatherKind, List<string>> FindKeywordHits(string text)
        {
            Dictionary<WeatherKind, List<string>> result = new Dictionary<WeatherKind, List<string>>();
            foreach (WeatherKind kind in WeatherKindInfo.All)
            {
                List<string> found = new List<string>();
                foreach (string keyword in Keywords[kind])
                {
                    if (ContainsKeyword(text, keyword))
                    {
                        found.Add(keyword);
                        if (found.Count >= 4) break;
                    }
                }
                result[kind] = found;
            }
            return result;
        }

        private static double KeywordScore(WeatherKind kind, string text)
        {
            double score = 0;
            int count = 0;
            foreach (string keyword in Keywords[kind])
            {
                if (ContainsKeyword(text, keyword))
                {
                    score += keyword.Length >= 4 ? 2.2 : 2.8;
                    count++;
                    if (count >= 3) break;
                }
            }
            return Math.Min(score, 7.5);
        }

        private static bool ContainsKeyword(string text, string keyword)
        {
            if (String.IsNullOrEmpty(keyword)) return false;
            bool asciiWord = true;
            foreach (char c in keyword)
            {
                if (c > 127) { asciiWord = false; break; }
            }
            if (asciiWord)
            {
                string pattern = "(?<![a-z0-9])" + Regex.Escape(keyword) + "(?![a-z0-9])";
                return Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase);
            }
            return text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Dictionary<TimeSlot, List<string>> FindTimeHits(string text)
        {
            Dictionary<TimeSlot, List<string>> result = new Dictionary<TimeSlot, List<string>>();
            foreach (TimeSlot slot in TimeSlotInfo.All)
            {
                List<string> found = new List<string>();
                foreach (string keyword in TimeKeywords[slot])
                {
                    if (ContainsKeyword(text, keyword))
                    {
                        found.Add(keyword);
                        if (found.Count >= 3) break;
                    }
                }
                result[slot] = found;
            }
            return result;
        }

        private static double TimeKeywordScore(TimeSlot slot, string text)
        {
            double score = 0;
            int count = 0;
            foreach (string keyword in TimeKeywords[slot])
            {
                if (ContainsKeyword(text, keyword))
                {
                    score += keyword.Length >= 4 ? 2.2 : 2.8;
                    count++;
                    if (count >= 3) break;
                }
            }
            return Math.Min(score, 7.5);
        }

        private static double VisualTimeScore(TimeSlot slot, VisualFeatures f)
        {
            double lowContrast = 1.0 - Math.Min(1.0, f.Contrast / 0.27);
            double mid = 1.0 - Math.Min(1.0, Math.Abs(f.Brightness - 0.48) / 0.42);
            double warm = f.Warmth;
            double cool = f.Coolness;
            switch (slot)
            {
                case TimeSlot.Dawn:
                    return 1.2 + 2.3 * warm + 1.0 * mid + 0.9 * lowContrast + 0.6 * f.Brightness
                        + 0.3 * f.BrightRatio - 0.8 * f.DarkRatio - 0.6 * cool;
                case TimeSlot.Day:
                    return 1.0 + 2.4 * f.Brightness + 1.2 * f.Saturation + 0.7 * f.BrightRatio
                        + 0.3 * f.Contrast - 1.2 * f.DarkRatio - 0.2 * warm;
                case TimeSlot.Dusk:
                    return 0.9 + 2.5 * warm + 1.2 * f.Purple + 0.9 * f.Contrast + 0.6 * mid
                        - 0.4 * f.Brightness - 0.3 * f.DarkRatio;
                default:
                    return 1.0 + 3.0 * f.DarkRatio + 1.4 * cool + 1.1 * f.BrightRatio
                        + 1.3 * f.Purple + 0.4 * f.Saturation - 1.0 * f.Brightness;
            }
        }

        private static string BuildTimeReason(TimeSlot slot, Dictionary<TimeSlot, List<string>> hits,
            VisualFeatures f, bool noImage)
        {
            string visual;
            switch (slot)
            {
                case TimeSlot.Dawn: visual = "暖色柔光与较低对比的清晨感"; break;
                case TimeSlot.Day: visual = "明亮通透的日间光线"; break;
                case TimeSlot.Dusk: visual = "暖橙或紫色、较低角度的黄昏光"; break;
                default: visual = "深色夜景、月光或霓虹高光"; break;
            }
            List<string> found = hits.ContainsKey(slot) ? hits[slot] : new List<string>();
            if (found.Count > 0)
            {
                if (noImage) return "文字线索「" + found[0] + "」";
                return "文字线索「" + found[0] + "」·" + visual;
            }
            return noImage ? "按标题与标签推断" : visual;
        }

        private static double VisualScore(WeatherKind kind, VisualFeatures f)
        {
            double lowSat = 1.0 - Math.Min(1.0, f.Saturation / 0.34);
            double lowContrast = 1.0 - Math.Min(1.0, f.Contrast / 0.27);
            double mid = 1.0 - Math.Min(1.0, Math.Abs(f.Brightness - 0.48) / 0.42);
            double warm = f.Warmth;
            double cool = f.Coolness;
            double gray = f.GrayRatio;
            double dark = f.DarkRatio;
            double bright = f.Brightness;
            double highlight = f.BrightRatio;

            switch (kind)
            {
                case WeatherKind.ClearDay:
                    return 1.3 + 2.0 * bright + 2.6 * warm + 1.3 * f.Saturation
                        + 0.8 * highlight + 0.2 * f.Contrast - 1.8 * dark - 0.5 * cool;
                case WeatherKind.ClearNight:
                    return 0.8 + 3.4 * dark + 1.5 * cool + 0.7 * highlight
                        + 1.0 * f.Purple + 0.3 * f.Saturation - 1.1 * Math.Max(0, bright - 0.55);
                case WeatherKind.Cloudy:
                    return 1.0 + 1.2 * mid + 1.5 * lowSat + 0.4 * cool + 0.5 * lowContrast
                        - 1.2 * warm - 0.9 * dark;
                case WeatherKind.Overcast:
                    return 0.4 + 1.8 * lowSat + 1.0 * gray + 0.9 * cool + 0.5 * mid
                        + 0.3 * lowContrast - 0.9 * warm - 1.0 * dark;
                case WeatherKind.Rain:
                    return 0.1 + 1.8 * cool + 1.6 * lowSat + 1.0 * f.Contrast + 0.5 * mid
                        + 0.3 * gray - 1.0 * warm - 1.2 * f.Saturation - 0.6 * bright;
                case WeatherKind.Storm:
                    return 0.0 + 2.0 * dark + 2.0 * f.Contrast + 1.0 * cool + 0.8 * f.Purple
                        + 0.4 * f.Saturation - 1.3 * bright;
                case WeatherKind.Snow:
                    return 0.2 + 3.0 * bright + 2.2 * lowSat + 1.9 * cool + 0.8 * highlight
                        - 1.2 * warm - 1.8 * dark - 1.7 * f.Saturation;
                case WeatherKind.Fog:
                    return 0.0 + 1.5 * lowContrast + 1.2 * gray + 0.8 * lowSat + 0.3 * mid
                        - 0.6 * f.Contrast - 0.8 * dark;
                default:
                    return 0;
            }
        }

        private static string BuildReason(WeatherKind top, Dictionary<WeatherKind, List<string>> hits,
            VisualFeatures f, bool noImage)
        {
            string visual;
            switch (top)
            {
                case WeatherKind.ClearDay: visual = "画面明亮、带暖色光"; break;
                case WeatherKind.ClearNight: visual = "暗色画面与冷色高光"; break;
                case WeatherKind.Cloudy: visual = "柔和低饱和的云层色调"; break;
                case WeatherKind.Overcast: visual = "灰蓝、低饱和的暗调"; break;
                case WeatherKind.Rain: visual = "冷蓝灰影调"; break;
                case WeatherKind.Storm: visual = "高对比暗调与强烈色光"; break;
                case WeatherKind.Snow: visual = "高亮、冷白的画面"; break;
                case WeatherKind.Fog: visual = "低对比、朦胧的灰调"; break;
                default: visual = "画面色调"; break;
            }

            List<string> found = hits.ContainsKey(top) ? hits[top] : new List<string>();
            if (found.Count > 0)
            {
                string word = found[0];
                if (noImage) return "文字线索「" + word + "」";
                return "文字线索「" + word + "」·" + visual;
            }
            return noImage ? "按标题与标签推断" : visual;
        }

        private static readonly Dictionary<string, VisualFeatures> PreviewFeatureCache =
            new Dictionary<string, VisualFeatures>(StringComparer.OrdinalIgnoreCase);
        private static readonly object PreviewFeatureLock = new object();

        public static VisualFeatures AnalyzePreview(string path)
        {
            if (String.IsNullOrEmpty(path) || !File.Exists(path))
            {
                VisualFeatures empty = new VisualFeatures();
                empty.HasImage = false;
                return empty;
            }
            string key = path;
            try
            {
                FileInfo info = new FileInfo(path);
                key = path + "|" + info.Length + "|" + info.LastWriteTimeUtc.Ticks;
            }
            catch
            {
            }
            lock (PreviewFeatureLock)
            {
                VisualFeatures cached;
                if (PreviewFeatureCache.TryGetValue(key, out cached)) return cached;
            }
            VisualFeatures result = AnalyzePreviewCore(path);
            lock (PreviewFeatureLock)
            {
                PreviewFeatureCache[key] = result;
            }
            return result;
        }

        private static VisualFeatures AnalyzePreviewCore(string path)
        {
            VisualFeatures f = new VisualFeatures();
            f.HasImage = false;
            if (String.IsNullOrEmpty(path) || !File.Exists(path)) return f;
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (Image source = Image.FromStream(stream, true, false))
                {
                    f.Width = source.Width;
                    f.Height = source.Height;
                    const int size = 72;
                    using (Bitmap bitmap = new Bitmap(size, size, PixelFormat.Format24bppRgb))
                    {
                        using (Graphics g = Graphics.FromImage(bitmap))
                        {
                            g.Clear(Color.FromArgb(32, 36, 44));
                            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            double scale = Math.Max((double)size / source.Width, (double)size / source.Height);
                            int w = (int)Math.Ceiling(source.Width * scale);
                            int h = (int)Math.Ceiling(source.Height * scale);
                            g.DrawImage(source, (size - w) / 2, (size - h) / 2, w, h);
                        }

                        double totalLum = 0;
                        double totalSat = 0;
                        double[] lumValues = new double[size * size];
                        int index = 0;
                        int dark = 0;
                        int bright = 0;
                        int gray = 0;
                        double warm = 0;
                        double cool = 0;
                        double purple = 0;
                        double green = 0;
                        for (int y = 0; y < size; y++)
                        {
                            for (int x = 0; x < size; x++)
                            {
                                Color c = bitmap.GetPixel(x, y);
                                double lum = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
                                int max = Math.Max(c.R, Math.Max(c.G, c.B));
                                int min = Math.Min(c.R, Math.Min(c.G, c.B));
                                double sat = max == 0 ? 0 : (double)(max - min) / max;
                                totalLum += lum;
                                totalSat += sat;
                                lumValues[index++] = lum;
                                if (lum < 0.22) dark++;
                                if (lum > 0.78) bright++;
                                if (sat < 0.13) gray++;
                                if (sat > 0.12)
                                {
                                    double hue = c.GetHue();
                                    if (hue >= 15 && hue < 70) warm += sat * (0.35 + lum);
                                    else if (hue >= 170 && hue < 260) cool += sat * (0.35 + lum);
                                    else if (hue >= 260 && hue < 330) purple += sat * (0.35 + lum);
                                    else if (hue >= 70 && hue < 170) green += sat * (0.35 + lum);
                                }
                            }
                        }
                        int count = size * size;
                        f.Brightness = totalLum / count;
                        f.Saturation = totalSat / count;
                        f.DarkRatio = (double)dark / count;
                        f.BrightRatio = (double)bright / count;
                        f.GrayRatio = (double)gray / count;
                        f.Warmth = Math.Min(1.0, warm / count * 1.8);
                        f.Coolness = Math.Min(1.0, cool / count * 1.8);
                        f.Purple = Math.Min(1.0, purple / count * 2.2);
                        f.Green = Math.Min(1.0, green / count * 1.8);
                        double mean = f.Brightness;
                        double variance = 0;
                        foreach (double lum in lumValues)
                        {
                            double diff = lum - mean;
                            variance += diff * diff;
                        }
                        f.Contrast = Math.Min(1.0, Math.Sqrt(variance / count) * 3.0);
                        f.HasImage = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn("分析壁纸预览失败：" + path + " | " + ex.Message);
            }
            return f;
        }
    }
}
