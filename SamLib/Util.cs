namespace SamLib.Util
{
    using System.Diagnostics;
    using System.Text;

    static class Util
    {

        #region General
        public static void Log(string text)
        {
            Console.WriteLine(text);
        }

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
        #endregion

        #region Conversions
        public enum CharacterSet
        {
            UTF8,
            ASCII,
            UNICODE
        }

        public static string ToString(this byte[] data, CharacterSet cset = CharacterSet.UTF8)
        {
            switch(cset){
                case CharacterSet.UTF8:
                    return  Encoding.UTF8.GetString(data);
                case CharacterSet.ASCII:
                    return Encoding.ASCII.GetString(data);
                case CharacterSet.UNICODE:
                    return Encoding.Unicode.GetString(data);
                default:
                    return null;
        }
        #endregion
    }
}
