namespace SamLib.Util
{
    using System.Diagnostics;
    using System.Globalization;

    static class Util
    {
        /// <summary>
        /// Log a message with the time and date
        /// </summary>
        /// <param name="text"></param>
        public static void Log(string text)
        {
            string time = DateTime.Now.ToString("hh:mm:ss");
            string date = DateTime.Now.ToString("dd/MM/yyyy");
            Console.WriteLine($"[{date}][{time}]: {text}");
        }

        /// <summary>
        /// Execute a cmd command
        /// </summary>
        /// <param name="command"></param>
        /// <param name="remain_open"></param>
        /// <param name="show_terminal"></param>
        /// <returns></returns>
        public static string RunCommand(string command, bool remain_open = false, bool show_terminal = false)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/{(remain_open ? "k" : "c")} {command}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = show_terminal
            };

            using (Process process = Process.Start(startInfo))
            {
                using (StreamReader reader = process.StandardOutput)
                {
                    string result = reader.ReadToEnd();
                    process.WaitForExit();
                    return result;
                }
            }
        }
    }

    public static class Extensions
    {
        #region Conversions
        private static readonly NumberStyles Style = NumberStyles.Any;
        private static readonly IFormatProvider Culture = CultureInfo.InvariantCulture;

        #region Integer
        /// <summary> Throws exception if parsing fails. </summary>
        public static int ToInt(this string value) => int.Parse(value, Style, Culture);

        /// <summary> Returns defaultValue if parsing fails. </summary>
        public static int ToIntOrDefault(this string value, int defaultValue = 0) =>
            int.TryParse(value, Style, Culture, out var result) ? result : defaultValue;
        #endregion

        #region Float
        /// <summary> Throws exception if parsing fails. </summary>
        public static float ToFloat(this string value) => float.Parse(value, Style, Culture);

        /// <summary> Returns defaultValue if parsing fails. </summary>
        public static float ToFloatOrDefault(this string value, float defaultValue = 0f) =>
            float.TryParse(value, Style, Culture, out var result) ? result : defaultValue;
        #endregion

        #region Double
        /// <summary> Throws exception if parsing fails. </summary>
        public static double ToDouble(this string value) => double.Parse(value, Style, Culture);

        /// <summary> Returns defaultValue if parsing fails. </summary>
        public static double ToDoubleOrDefault(this string value, double defaultValue = 0.0) =>
            double.TryParse(value, Style, Culture, out var result) ? result : defaultValue;
        #endregion

        #region Decimal
        /// <summary> Throws exception if parsing fails. </summary>
        public static decimal ToDecimal(this string value) => decimal.Parse(value, Style, Culture);

        /// <summary> Returns defaultValue if parsing fails. </summary>
        public static decimal ToDecimalOrDefault(this string value, decimal defaultValue = 0m) =>
            decimal.TryParse(value, Style, Culture, out var result) ? result : defaultValue;
        #endregion

        #region Boolean
        /// <summary> Throws exception if parsing fails. </summary>
        public static bool ToBool(this string value) => bool.Parse(value);

        /// <summary> Returns defaultValue if parsing fails. </summary>
        public static bool ToBoolOrDefault(this string value, bool defaultValue = false) =>
            bool.TryParse(value, out var result) ? result : defaultValue;
        #endregion
        #endregion

        #region Other utilities

        /// <summary>
        /// Is not null, empty, or whitespace
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool HasValue(this string value)
        {
            return !string.IsNullOrWhiteSpace(value);
        }

        /// <summary>
        /// Get random element from a list
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <returns></returns>
        public static T GetRandom<T>(this IList<T> list)
        {
            if (list.Count == 0) return default;
            return list[new Random().Next(list.Count)];
        }

        /// <summary>
        /// Is a collection null or empty
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="collection"></param>
        /// <returns></returns>
        public static bool IsNullOrEmpty<T>(this IEnumerable<T> collection)
        {
            return collection == null || !collection.Any();
        }

        /// <summary>
        /// Quickly wrap a text in quotations
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string InQuotes(this string value) => $"\"{value}\"";

        /// <summary>
        /// Is value even
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool IsEven(this int value) => value % 2 == 0;
        #endregion
    }
}
