namespace SamLib.Util
{
    using System.Diagnostics;

    static class Util
    {
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
    }
}
