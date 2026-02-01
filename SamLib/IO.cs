using System.ComponentModel.Design;

namespace SamLib.IO
{
    // Static IO functions
    public static class IO
    {
        public static readonly List<char> punctuation = new List<char> { '?', '!', '.', ',', ':', ';'};

        /// <summary>
        /// Animated type-writer display
        /// </summary>
        /// <param name="message">text to display</param>
        /// <param name="delay">delay in ms between character outputs</param>
        /// <param name="newLine">new line after output</param>
        /// <param name="punctuationMultiplier">multiplier for punctuation delay, (delay * puncMutliplier)</param>
        public static void Out(string message, int delay = 90, bool newLine = true, float punctuationMultiplier = 1.4f)
        {
            if (delay <= 0) { Console.Write(message); }
            else
            {
                foreach (char c in message)
                {
                    Console.Write(c);

                    if (punctuation.Contains(c)) { Thread.Sleep((int)(delay * punctuationMultiplier)); }
                    else { Thread.Sleep(delay); }
                }
            }
            if (newLine) Console.WriteLine();
        }

        /// <summary>
        /// Display a list of selectable options and return the selected string
        /// </summary>
        /// <param name="prompt">message prompt</param>
        /// <param name="options">options array</param>
        /// <param name="delay">animated display</param>
        /// <param name="selectedIdentifier">selected option identifier</param>
        /// <returns>Selected string</returns>
        public static string GetOption(string prompt, string[] options, int delay, string selectedIdentifier = ">")
        {
            int index = 0;
            bool selected = false;
            int cursorY = Console.CursorTop;
            Console.CursorVisible = false;

            Console.SetCursorPosition(0, cursorY);
            if (delay <= 0) { Console.Write($"{prompt}\n"); } else { Out(prompt, delay); }

            for (int i = 0; i < options.Length; i++)
            {
                if (delay <= 0)
                {
                    if (i == index)
                    {
                        Console.Write($"{selectedIdentifier} {options[i]}\n", delay);
                    }
                    else
                    {
                        Console.Write($"{string.Concat(Enumerable.Repeat(" ", selectedIdentifier.Length))} {options[i]}", delay);
                    }
                }
                else
                {
                    if (i == index)
                    {
                        Out($"{selectedIdentifier} {options[i]}\n", delay);
                    }
                    else
                    {
                        Out($"{string.Concat(Enumerable.Repeat(" ", selectedIdentifier.Length))} {options[i]}", delay);
                    }
                }
            }

            while (!selected)
            {
                Console.SetCursorPosition(0, cursorY);
                Console.Write($"{prompt}\n");

                for (int i = 0; i < options.Length; i++)
                {
                    if (i == index)
                    {
                        Console.Write($"{selectedIdentifier} {options[i]}\n");
                    }
                    else
                    {
                        Console.Write($"{string.Concat(Enumerable.Repeat(" ", selectedIdentifier.Length))}  {options[i]}\n");
                    }
                }

                ConsoleKeyInfo key = Console.ReadKey();

                if(key.Key == ConsoleKey.UpArrow || key.Key == ConsoleKey.W)
                {
                    index--;
                    if(index < 0) { index = options.Length - 1; }
                }
                else if (key.Key == ConsoleKey.DownArrow || key.Key == ConsoleKey.S)
                {
                    index++;
                    if (index > options.Length - 1) { index = 0; }
                }else if(key.Key == ConsoleKey.Enter) { selected = true; }
            }

            return options[index];
        }

        /// <summary>
        /// Display a list of selectable options and return the selected index
        /// </summary>
        /// <param name="prompt">message prompt</param>
        /// <param name="options">options array</param>
        /// <param name="delay">animated display</param>
        /// <param name="selectedIdentifier">selected option identifier</param>
        /// <returns>Selected index</returns>
        public static int GetOptionIndex(string prompt, string[] options, int delay, string selectedIdentifier = ">")
        {
            int index = 0;
            bool selected = false;
            int cursorY = Console.CursorTop;
            Console.CursorVisible = false;

            Console.SetCursorPosition(0, cursorY);
            if (delay <= 0) { Console.Write($"{prompt}\n"); } else { Out(prompt, delay); }

            for (int i = 0; i < options.Length; i++)
            {
                if(delay <= 0)
                {
                    if (i == index)
                    {
                        Console.Write($"{selectedIdentifier} {options[i]}\n", delay);
                    }
                    else
                    {
                        Console.Write($"{string.Concat(Enumerable.Repeat(" ", selectedIdentifier.Length))} {options[i]}", delay);
                    }
                }
                else
                {
                    if (i == index)
                    {
                        Out($"{selectedIdentifier} {options[i]}\n", delay);
                    }
                    else
                    {
                        Out($"{string.Concat(Enumerable.Repeat(" ", selectedIdentifier.Length))} {options[i]}", delay);
                    }
                }
            }

            while (!selected)
            {
                Console.SetCursorPosition(0, cursorY);
                Console.Write($"{prompt}\n");

                for (int i = 0; i < options.Length; i++)
                {
                    if (i == index)
                    {
                        Console.Write($"{selectedIdentifier} {options[i]}\n");
                    }
                    else
                    {
                        Console.Write($"{string.Concat(Enumerable.Repeat(" ", selectedIdentifier.Length))}  {options[i]}\n");
                    }
                }

                ConsoleKeyInfo key = Console.ReadKey();

                if (key.Key == ConsoleKey.UpArrow || key.Key == ConsoleKey.W)
                {
                    index--;
                    if (index < 0) { index = options.Length - 1; }
                }
                else if (key.Key == ConsoleKey.DownArrow || key.Key == ConsoleKey.S)
                {
                    index++;
                    if (index > options.Length - 1) { index = 0; }
                }
                else if (key.Key == ConsoleKey.Enter) { selected = true; }
            }

            return index;
        }

    }

    // Managed IO functions, callbacks, etc.
    public class IOManager
    {

    }
}
