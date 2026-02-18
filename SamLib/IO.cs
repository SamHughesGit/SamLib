namespace SamLib.IO
{
    using Microsoft.Xna.Framework;
    using System.Linq;
    using System.Runtime.InteropServices.Swift;

    // Static IO functions
    public static class IO
    {
        public static readonly string punctuation = "!?,.-:;";

        /// <summary>
        /// Animated type-writer display
        /// </summary>
        /// <param name="message">text to display</param>
        /// <param name="delay">delay in ms between character outputs</param>
        /// <param name="newLine">new line after output</param>
        /// <param name="punctuationMultiplier">multiplier for punctuation delay, (delay * puncMutliplier)</param>
        public static void Type(string message, int delay = 90, bool newLine = true, bool showCursor = false, float punctuationMultiplier = 1.4f)
        {
            // Store cursor visibilty before changing it so it can be reverted at the end
            bool cursorVisibility = Console.CursorVisible;
            Console.CursorVisible = showCursor;

            // If delay is <= 0, just write the message instantly
            if (delay <= 0) { Console.Write(message); }
            else
            {
                // For each character, write it out
                foreach (char c in message)
                {
                    Console.Write(c);

                    // If current character is in the punctuation list (just a string but they function like a list of chars)
                    // then sleep longer (base delay * punctuation multiplier), otherwise sleep for regular time
                    if (punctuation.Contains(c)) { Thread.Sleep((int)(delay * punctuationMultiplier)); }
                    else { Thread.Sleep(delay); }
                }
            }
            // Optional new line
            if (newLine) Console.WriteLine();

            // Restore cursor visibility settings
            Console.CursorVisible = cursorVisibility;
        }

        /// <summary>
        /// Get an input from an array of valid options
        /// </summary>
        /// <param name="options">String array of options</param>
        /// <param name="prompt">Option prompt which is output before requesting an input</param>
        /// <returns>string option</returns>
        public static string GetOption(string[] options, bool isCaseSensitive = false, string prompt = null)
        {
            // Start input off as invalid option
            string input = "";

            // If isCaseSensitive is false, standardise the array by converting each element to lowercase and trim leading or proceeding white spaces 
            if(!isCaseSensitive) options = options.Select(s => s.ToLower().Trim()).ToArray();

            // While input is not in the list (starts off as true, so loop starts)
            while (!options.Contains(input))
            {
                // If user passed an optional prompt, display it before input each time
                if (!(prompt == null || prompt == "")) { Console.WriteLine(prompt); }

                // Get new (standardised) input
                input = Console.ReadLine().ToLower().Trim();
            }

            // Loop ends when inptu is valid, return input
            return input;
        }

        /// <summary>
        /// Get an input from an array of valid options
        /// </summary>
        /// <param name="options">String array of options</param>
        /// <param name="prompt">Option prompt which is output before requesting an input</param>
        /// <returns>index of option</returns>
        // Repeat of last function but returns the position in the array of the option selected
        public static int GetOptionIndex(string[] options, string prompt = null)
        {
            string input = "";
            while (!options.Contains(input))
            {
                if (!(prompt == null || prompt == "")) { Console.WriteLine(prompt); }
                input = Console.ReadLine();
            }
            return options.IndexOf(input);
        }

        /// <summary>
        /// Display a list of selectable options and return the selected string
        /// </summary>
        /// <param name="prompt">message prompt</param>
        /// <param name="options">options array</param>
        /// <param name="delay">animated display</param>
        /// <param name="selectedIdentifier">selected option identifier</param>
        /// <returns>Selected string</returns>
        public static string GetOptionDropdown(string prompt, string[] options, int delay = 90, string selectedIdentifier = ">", bool colored = false, ConsoleColor color = ConsoleColor.Cyan)
        {
            int index = 0;
            bool selected = false;
            int cursorY = Console.CursorTop;
            Console.CursorVisible = false;
            ConsoleColor baseColor = Console.ForegroundColor;

            Console.SetCursorPosition(0, cursorY);
            if (delay <= 0) { Console.Write($"{prompt}\n"); } else { Type(prompt, delay); }

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
                        Type($"{selectedIdentifier} {options[i]}", delay);
                    }
                    else
                    {
                        if (colored) Console.ForegroundColor = baseColor;
                        Type($"{string.Concat(Enumerable.Repeat(" ", selectedIdentifier.Length))} {options[i]}", delay);
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
        public static int GetOptionIndexDropdown(string prompt, string[] options, int delay = 90, string selectedIdentifier = ">", bool colored = false, ConsoleColor color = ConsoleColor.Cyan)
        {
            int index = 0;
            bool selected = false;
            int cursorY = Console.CursorTop;
            Console.CursorVisible = false;
            ConsoleColor baseColor = Console.ForegroundColor;

            Console.SetCursorPosition(0, cursorY);
            if (delay <= 0) { Console.Write($"{prompt}\n"); } else { Type(prompt, delay); }

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
                        Type($"{selectedIdentifier} {options[i]}", delay);
                    }
                    else
                    {
                        if (colored) Console.ForegroundColor = baseColor;
                        Type($"{string.Concat(Enumerable.Repeat(" ", selectedIdentifier.Length))} {options[i]}", delay);
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
        /// Display scrollable messages (D and A or Left and Right)
        /// </summary>
        /// <param name="messages">Messages array</param>
        public static void HorizontalScrollDisplay(string[] messages, bool newLine = true)
        {
            int index = 0;
            int cursorY = Console.CursorTop;
            bool finished = false;

            if (messages == null) return;

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
            } if (newLine) Console.WriteLine();
        }

        /// <summary>
        /// Display scrollable messages (D and A)
        /// </summary>
        /// <param name="messages">Messages array</param>
        /// <returns>Selected string</returns>
        public static string HorizontalScrollSelect(string[] messages, bool newLine = true)
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
            } if (newLine) Console.WriteLine();
            return messages[index];
        }

        /// <summary>
        /// Display scrollable messages (D and A)
        /// </summary>
        /// <param name="messages">Messages array</param>
        /// <returns>Selected index</returns>
        public static int HorizontalScrollSelectIndex(string[] messages, bool newLine = true)
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
            if (newLine) Console.WriteLine();
            return index;
        }

        // --- Text Effects ---
        public enum FadeType { LEFT_TO_RIGHT,  RIGHT_TO_LEFT, MIDDLE_OUT, EDGE_IN };

        /// <summary>
        /// Render obfuscated text and then fade it out
        /// </summary>
        /// <param name="text">Text to be displayed</param>
        /// <param name="identifier">Identifier to indicate an obfuscated section of text</param>
        /// <param name="fadeType">Type of fade for reveal</param>
        public static void ObfuscateReveal (string text, string identifier, FadeType fadeType, int typeSpeed = 60, int waitUntilReveal = 1000)
        {
            bool initialState = Console.CursorVisible;
            Console.CursorVisible = false;
            Random rng = new Random();
            char[] glitchChars = "$%#@*&0123456789".ToCharArray();

            bool[] glitchMap = new bool[text.Length];
            bool isInside = false;
            for (int i = 0; i <= text.Length - identifier.Length; i++)
            {
                if (text.Substring(i, identifier.Length) == identifier)
                {
                    isInside = !isInside;
                    i += identifier.Length - 1;
                    continue;
                }
                glitchMap[i] = isInside;
            }

            int cursorX = Console.CursorLeft;
            int cursorY = Console.CursorTop;

            for (int visibleCount = 0; visibleCount <= text.Length; visibleCount++)
            {
                RenderFrame(text, identifier, glitchMap, visibleCount, cursorX, cursorY, glitchChars, rng);
                Thread.Sleep(typeSpeed);
            }

            List<int> glitchIndices = new List<int>();
            for (int i = 0; i < glitchMap.Length; i++) if (glitchMap[i]) glitchIndices.Add(i);

            switch (fadeType)
            {
                case FadeType.RIGHT_TO_LEFT:
                    glitchIndices.Reverse();
                    break;
                case FadeType.MIDDLE_OUT:
                    int mid = text.Length / 2;
                    glitchIndices = glitchIndices.OrderBy(x => Math.Abs(x - mid)).ToList();
                    break;
                case FadeType.EDGE_IN:
                    int center = text.Length / 2;
                    glitchIndices = glitchIndices.OrderByDescending(x => Math.Abs(x - center)).ToList();
                    break;
            }

            // Wait for x time after finished type-write effect
            long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            long time = 0;
            while (time < waitUntilReveal)
            {
                time = DateTimeOffset.Now.ToUnixTimeMilliseconds() - now;
                RenderFrame(text, identifier, glitchMap, text.Length, cursorX, cursorY, glitchChars, rng);
                Thread.Sleep(typeSpeed);
            }

            // Reveal
            foreach (int indexToReveal in glitchIndices)
            {
                glitchMap[indexToReveal] = false;
                RenderFrame(text, identifier, glitchMap, text.Length, cursorX, cursorY, glitchChars, rng);
                Thread.Sleep(typeSpeed); 
            }

            Console.WriteLine();
            Console.CursorVisible = initialState;
        }

        // Obfuscate no reveal
        public static void Obfuscate(string text, string identifier, int typeSpeed = 60, int waitUntilReveal = 1000)
        {
            bool initialState = Console.CursorVisible;
            Console.CursorVisible = false;
            Random rng = new Random();
            char[] glitchChars = "$%#@*&0123456789".ToCharArray();

            bool[] glitchMap = new bool[text.Length];
            bool isInside = false;
            for (int i = 0; i <= text.Length - identifier.Length; i++)
            {
                if (text.Substring(i, identifier.Length) == identifier)
                {
                    isInside = !isInside;
                    i += identifier.Length - 1;
                    continue;
                }
                glitchMap[i] = isInside;
            }

            int cursorX = Console.CursorLeft;
            int cursorY = Console.CursorTop;

            for (int visibleCount = 0; visibleCount <= text.Length; visibleCount++)
            {
                RenderFrame(text, identifier, glitchMap, visibleCount, cursorX, cursorY, glitchChars, rng);
                Thread.Sleep(typeSpeed);
            }

            long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            while (DateTimeOffset.Now.ToUnixTimeMilliseconds() - now < waitUntilReveal)
            {
                RenderFrame(text, identifier, glitchMap, text.Length, cursorX, cursorY, glitchChars, rng);
                Thread.Sleep(typeSpeed);
            }

            Console.WriteLine();
            Console.CursorVisible = initialState;
        }
        private static void RenderFrame(string text, string identifier, bool[] glitchMap, int visibleCount, int x, int y, char[] glitchChars, Random rng)
        {
            Console.SetCursorPosition(x, y);
            for (int i = 0; i < visibleCount; i++)
            {
                if (i <= text.Length - identifier.Length && text.Substring(i, identifier.Length) == identifier)
                {
                    i += identifier.Length - 1;
                    continue;
                }

                if (glitchMap[i])
                    Console.Write(glitchChars[rng.Next(glitchChars.Length)]);
                else
                    Console.Write(text[i]);
            }
        }

        /// <summary>
        /// Write colored text using string parsing
        /// </summary>
        /// <param name="text">Text</param>
        /// <param name="identifier">Identifier</param>
        /// <param name="colors">Colors</param>
        /// <param name="newLine">Do new line?</param>
        public static void RichText(string text, string identifier, ConsoleColor[] colors, bool newLine = true)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (colors == null || colors.Length == 0)
            {
                Console.Write(text);
                return;
            }

            string[] parts = text.Split(new[] { identifier }, StringSplitOptions.None);
            ConsoleColor defaultColor = Console.ForegroundColor;
            int colorIndex = 0;

            for (int i = 0; i < parts.Length; i++)
            {
                if (i % 2 != 0)
                {
                    int activeColorIndex = Math.Min(colorIndex, colors.Length - 1);
                    Console.ForegroundColor = colors[activeColorIndex];

                    Console.Write(parts[i]);

                    Console.ForegroundColor = defaultColor;
                    colorIndex++; 
                }
                else
                {
                    Console.Write(parts[i]);
                }
            } if(newLine) Console.WriteLine();
        }

        /// <summary>
        /// TypeWrite colored text using string parsing
        /// </summary>
        /// <param name="text">Text</param>
        /// <param name="identifier">Identifier</param>
        /// <param name="colors">Colors</param>
        /// <param name="newLine">Do new line?</param>
        /// <param name="delay">Per char delay</param>
        public static void RichType(string text, string identifier, ConsoleColor[] colors, int delay = 90, bool newLine = true, bool cursor = false)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (colors == null || colors.Length == 0)
            {
                Console.Write(text);
                return;
            }

            bool cursorVisibility = Console.CursorVisible;
            Console.CursorVisible = cursor;

            string[] parts = text.Split(new[] { identifier }, StringSplitOptions.None);
            ConsoleColor defaultColor = Console.ForegroundColor;
            int colorIndex = 0;

            for (int i = 0; i < parts.Length; i++)
            {
                if (i % 2 != 0)
                {
                    int activeColorIndex = Math.Min(colorIndex, colors.Length - 1);
                    Console.ForegroundColor = colors[activeColorIndex];

                    Type(parts[i], delay, false);

                    Console.ForegroundColor = defaultColor;
                    colorIndex++;
                }
                else
                {
                    Type(parts[i], delay, false);
                }
            }
            Console.CursorVisible = cursorVisibility;
            if (newLine) Console.WriteLine();
        }
    }
}
