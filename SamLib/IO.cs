namespace SamLib.IO
{
    using System.Linq;

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
        public static void Out(string message, int delay = 90, bool newLine = true, bool showCursor = false, float punctuationMultiplier = 1.4f)
        {
            bool cursorVisibility = Console.CursorVisible;
            Console.CursorVisible = showCursor;

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
            Console.CursorVisible = cursorVisibility;
        }

        /// <summary>
        /// Display a list of selectable options and return the selected string
        /// </summary>
        /// <param name="prompt">message prompt</param>
        /// <param name="options">options array</param>
        /// <param name="delay">animated display</param>
        /// <param name="selectedIdentifier">selected option identifier</param>
        /// <returns>Selected string</returns>
        public static string GetOption(string prompt, string[] options, int delay = 90, string selectedIdentifier = ">", bool colored = false, ConsoleColor color = ConsoleColor.Cyan)
        {
            int index = 0;
            bool selected = false;
            int cursorY = Console.CursorTop;
            Console.CursorVisible = false;
            ConsoleColor baseColor = Console.ForegroundColor;

            Console.SetCursorPosition(0, cursorY);
            if (delay <= 0) { Console.Write($"{prompt}\n"); } else { Out(prompt, delay); }

            for (int i = 0; i < options.Length; i++)
            {
                if (delay <= 0)
                {
                    if (i == index)
                    {
                        if (colored) Console.ForegroundColor = color;
                        Console.Write($"{selectedIdentifier} {options[i]}\n", delay);
                    }
                    else
                    {
                        if (colored) Console.ForegroundColor = baseColor;
                        Console.Write($"{string.Concat(Enumerable.Repeat(" ", selectedIdentifier.Length))} {options[i]}\n", delay);
                    }
                }
                else
                {
                    if (i == index)
                    {
                        if (colored) Console.ForegroundColor = color;
                        Out($"{selectedIdentifier} {options[i]}", delay);
                    }
                    else
                    {
                        if (colored) Console.ForegroundColor = baseColor;
                        Out($"{string.Concat(Enumerable.Repeat(" ", selectedIdentifier.Length))} {options[i]}", delay);
                    }
                }
            }

            while (!selected)
            {
                if (colored) Console.ForegroundColor = baseColor;
                Console.SetCursorPosition(0, cursorY);
                Console.Write($"{prompt}\n");

                for (int i = 0; i < options.Length; i++)
                {
                    if (i == index)
                    {
                        if (colored) Console.ForegroundColor = color;
                        Console.Write($"{selectedIdentifier} {options[i]}\n");
                    }
                    else
                    {
                        if (colored) Console.ForegroundColor = baseColor;
                        Console.Write($"{string.Concat(Enumerable.Repeat(" ", selectedIdentifier.Length))} {options[i]}\n");
                    }
                }

                ConsoleKeyInfo key = Console.ReadKey(true);

                if (key.Key == ConsoleKey.UpArrow || key.Key == ConsoleKey.W)
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
        public static int GetOptionIndex(string prompt, string[] options, int delay = 90, string selectedIdentifier = ">", bool colored = false, ConsoleColor color = ConsoleColor.Cyan)
        {
            int index = 0;
            bool selected = false;
            int cursorY = Console.CursorTop;
            Console.CursorVisible = false;
            ConsoleColor baseColor = Console.ForegroundColor;

            Console.SetCursorPosition(0, cursorY);
            if (delay <= 0) { Console.Write($"{prompt}\n"); } else { Out(prompt, delay); }

            for (int i = 0; i < options.Length; i++)
            {
                if(delay <= 0)
                {
                    if (i == index)
                    {
                        if (colored) Console.ForegroundColor = color;
                        Console.Write($"{selectedIdentifier} {options[i]}\n", delay);
                    }
                    else
                    {
                        if (colored) Console.ForegroundColor = baseColor;
                        Console.Write($"{string.Concat(Enumerable.Repeat(" ", selectedIdentifier.Length))} {options[i]}\n", delay);
                    }
                }
                else
                {
                    if (i == index)
                    {
                        if (colored) Console.ForegroundColor = color;
                        Out($"{selectedIdentifier} {options[i]}", delay);
                    }
                    else
                    {
                        if (colored) Console.ForegroundColor = baseColor;
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
                        if (colored) Console.ForegroundColor = color;
                        Console.Write($"{selectedIdentifier} {options[i]}\n");
                    }
                    else
                    {
                        if (colored) Console.ForegroundColor = baseColor;
                        Console.Write($"{string.Concat(Enumerable.Repeat(" ", selectedIdentifier.Length))} {options[i]}\n");
                    }
                }

                ConsoleKeyInfo key = Console.ReadKey(true);

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

        /// <summary>
        /// Display scrollable messages (D and A)
        /// </summary>
        /// <param name="messages">Messages array</param>
        public static void HorizontalScrollDisplay(string[] messages)
        {
            int index = 0;
            int cursorY = Console.CursorTop;
            bool finished = false;

            while (!finished)
            {
                Console.SetCursorPosition(0, cursorY);
                Console.Write(string.Concat(Enumerable.Repeat("  ", messages.OrderByDescending(s => s.Length).FirstOrDefault().Length)));
                Console.SetCursorPosition(0, cursorY);
                string head = (index!=0?"< ":"");
                string tail = (index!=messages.Length-1?" >":"");
                Console.Write($"{head}{messages[index]}{tail}");

                ConsoleKeyInfo key = Console.ReadKey(true);

                if (key.Key == ConsoleKey.D || key.Key == ConsoleKey.RightArrow)
                {
                    if (index != messages.Length - 1) { index++; }
                }
                else if (key.Key == ConsoleKey.A || key.Key == ConsoleKey.LeftArrow)
                {
                    if (index != 0) { index--; }
                }
                else if(key.Key == ConsoleKey.Enter)
                {
                    finished = true;
                }
            }
        }

        /// <summary>
        /// Display scrollable messages (D and A)
        /// </summary>
        /// <param name="messages">Messages array</param>
        /// <returns>Selected string</returns>
        public static string HorizontalScrollSelect(string[] messages)
        {
            int index = 0;
            int cursorY = Console.CursorTop;
            bool finished = false;

            while (!finished)
            {
                Console.SetCursorPosition(0, cursorY);
                Console.Write(string.Concat(Enumerable.Repeat("  ", messages.OrderByDescending(s => s.Length).FirstOrDefault().Length)));
                Console.SetCursorPosition(0, cursorY);
                string head = (index != 0 ? "< " : "");
                string tail = (index != messages.Length - 1 ? " >" : "");
                Console.Write($"{head}{messages[index]}{tail}");

                ConsoleKeyInfo key = Console.ReadKey(true);

                if (key.Key == ConsoleKey.D || key.Key == ConsoleKey.RightArrow)
                {
                    if (index != messages.Length - 1) { index++; }
                }
                else if (key.Key == ConsoleKey.A || key.Key == ConsoleKey.LeftArrow)
                {
                    if (index != 0) { index--; }
                }
                else if (key.Key == ConsoleKey.Enter)
                {
                    finished = true;
                }
            }
            return messages[index];
        }

        /// <summary>
        /// Display scrollable messages (D and A)
        /// </summary>
        /// <param name="messages">Messages array</param>
        /// <returns>Selected index</returns>
        public static int HorizontalScrollSelectIndex(string[] messages)
        {
            int index = 0;
            int cursorY = Console.CursorTop;
            bool finished = false;

            while (!finished)
            {
                Console.SetCursorPosition(0, cursorY);
                Console.Write(string.Concat(Enumerable.Repeat("  ", messages.OrderByDescending(s => s.Length).FirstOrDefault().Length)));
                Console.SetCursorPosition(0, cursorY);
                string head = (index != 0 ? "< " : "");
                string tail = (index != messages.Length - 1 ? " >" : "");
                Console.Write($"{head}{messages[index]}{tail}");

                ConsoleKeyInfo key = Console.ReadKey(true);

                if (key.Key == ConsoleKey.D || key.Key == ConsoleKey.RightArrow)
                {
                    if (index != messages.Length - 1) { index++; }
                }
                else if (key.Key == ConsoleKey.A || key.Key == ConsoleKey.LeftArrow)
                {
                    if (index != 0) { index--; }
                }
                else if (key.Key == ConsoleKey.Enter)
                {
                    finished = true;
                }
            }
            return index;
        }
    }
}
