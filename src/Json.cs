using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Web.Script.Serialization;

namespace SkyMatch
{
    public static class Json
    {
        private static readonly JavaScriptSerializer Serializer = CreateSerializer();
        private static readonly object Sync = new object();

        private static JavaScriptSerializer CreateSerializer()
        {
            JavaScriptSerializer s = new JavaScriptSerializer();
            s.MaxJsonLength = Int32.MaxValue;
            s.RecursionLimit = 120;
            return s;
        }

        public static object Parse(string text)
        {
            if (String.IsNullOrEmpty(text)) return null;
            lock (Sync)
            {
                return Serializer.DeserializeObject(text);
            }
        }

        public static string Stringify(object value)
        {
            lock (Sync)
            {
                return Serializer.Serialize(value);
            }
        }

        public static Dictionary<string, object> Object(object value)
        {
            Dictionary<string, object> d = value as Dictionary<string, object>;
            if (d != null) return d;
            IDictionary<string, object> generic = value as IDictionary<string, object>;
            if (generic != null) return new Dictionary<string, object>(generic);
            return null;
        }

        public static Dictionary<string, object> GetObject(Dictionary<string, object> source, string key)
        {
            if (source == null) return null;
            object value;
            if (!source.TryGetValue(key, out value)) return null;
            return Object(value);
        }

        public static List<object> GetArray(Dictionary<string, object> source, string key)
        {
            if (source == null) return new List<object>();
            object value;
            if (!source.TryGetValue(key, out value)) return new List<object>();
            if (value is object[]) return new List<object>((object[])value);
            IList list = value as IList;
            if (list == null) return new List<object>();
            List<object> result = new List<object>(list.Count);
            foreach (object item in list) result.Add(item);
            return result;
        }

        public static string GetString(Dictionary<string, object> source, string key)
        {
            if (source == null) return "";
            object value;
            if (!source.TryGetValue(key, out value) || value == null) return "";
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        public static double GetDouble(Dictionary<string, object> source, string key, double fallback)
        {
            if (source == null) return fallback;
            object value;
            if (!source.TryGetValue(key, out value) || value == null) return fallback;
            double d;
            if (value is double) return (double)value;
            if (value is int) return (double)(int)value;
            if (value is long) return (double)(long)value;
            if (value is decimal) return (double)(decimal)value;
            if (Double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture),
                NumberStyles.Any, CultureInfo.InvariantCulture, out d)) return d;
            return fallback;
        }

        public static int GetInt(Dictionary<string, object> source, string key, int fallback)
        {
            return (int)Math.Round(GetDouble(source, key, fallback));
        }

        public static bool GetBool(Dictionary<string, object> source, string key, bool fallback)
        {
            if (source == null) return fallback;
            object value;
            if (!source.TryGetValue(key, out value) || value == null) return fallback;
            if (value is bool) return (bool)value;
            if (value is int) return (int)value != 0;
            bool b;
            if (Boolean.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out b)) return b;
            return fallback;
        }

        public static string GetNestedString(Dictionary<string, object> source, string key1, string key2)
        {
            return GetString(GetObject(source, key1), key2);
        }

        public static double GetNestedDouble(Dictionary<string, object> source, string key1, string key2, double fallback)
        {
            return GetDouble(GetObject(source, key1), key2, fallback);
        }

        public static List<Dictionary<string, object>> ObjectList(Dictionary<string, object> source, string key)
        {
            List<Dictionary<string, object>> result = new List<Dictionary<string, object>>();
            List<object> raw = GetArray(source, key);
            foreach (object item in raw)
            {
                Dictionary<string, object> d = Object(item);
                if (d != null) result.Add(d);
            }
            return result;
        }
    }

    public static class Log
    {
        private static readonly object Sync = new object();

        public static void Info(string message)
        {
            Write("INFO", message);
        }

        public static void Warn(string message)
        {
            Write("WARN", message);
        }

        public static void Error(string message, Exception ex)
        {
            string detail = ex == null ? "" : " | " + ex.GetType().Name + ": " + ex.Message
                + Environment.NewLine + ex.StackTrace;
            Write("ERROR", message + detail);
        }

        private static void Write(string level, string message)
        {
            try
            {
                lock (Sync)
                {
                    AppPaths.Ensure();
                    string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " [" + level + "] " + message
                        + Environment.NewLine;
                    File.AppendAllText(AppPaths.LogFile, line);
                }
            }
            catch
            {
            }
        }
    }
}
