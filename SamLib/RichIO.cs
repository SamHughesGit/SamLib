namespace SamLib.RichIO
{
    public static class IO
    {
        private static readonly Dictionary<string, string> Tags = new()
        {
            //                   --- Text Styles ---
            { "[b]",         "\x1b[1m" },    { "[/b]",         "\x1b[22m" }, // Bold
            { "[dim]",       "\x1b[2m" },    { "[/dim]",       "\x1b[22m" }, // Dim
            { "[i]",         "\x1b[3m" },    { "[/i]",         "\x1b[23m" }, // Italic
            { "[un]",        "\x1b[4m" },    { "[/un]",        "\x1b[24m" }, // Underline
            { "[blink]",     "\x1b[5m" },    { "[/blink]",     "\x1b[25m" }, // Blink
            { "[inv]",       "\x1b[7m" },    { "[/inv]",       "\x1b[27m" }, // Invert
            { "[hide]",      "\x1b[8m" },    { "[/hide]",      "\x1b[28m" }, // Hidden
            { "[strike]",    "\x1b[9m" },    { "[/strike]",    "\x1b[29m" }, // Strikethrough

            //           --- Foreground Colors (Standard) ---
            { "[black]",     "\x1b[30m" },   { "[/black]",     "\x1b[39m" },
            { "[red]",       "\x1b[31m" },   { "[/red]",       "\x1b[39m" },
            { "[green]",     "\x1b[32m" },   { "[/green]",     "\x1b[39m" },
            { "[yellow]",    "\x1b[33m" },   { "[/yellow]",    "\x1b[39m" },
            { "[blue]",      "\x1b[34m" },   { "[/blue]",      "\x1b[39m" },
            { "[magenta]",   "\x1b[35m" },   { "[/magenta]",   "\x1b[39m" },
            { "[cyan]",      "\x1b[36m" },   { "[/cyan]",      "\x1b[39m" },
            { "[white]",     "\x1b[37m" },   { "[/white]",     "\x1b[39m" },
            { "[default]",   "\x1b[39m" },   { "[/default]",   "\x1b[39m" },

            //      --- Foreground Colors (Bright/High Intensity) ---
            { "[gray]",      "\x1b[90m" },   { "[/gray]",      "\x1b[39m" },
            { "[bred]",      "\x1b[91m" },   { "[/bred]",      "\x1b[39m" },
            { "[bgreen]",    "\x1b[92m" },   { "[/bgreen]",    "\x1b[39m" },
            { "[byellow]",   "\x1b[93m" },   { "[/byellow]",   "\x1b[39m" },
            { "[bblue]",     "\x1b[94m" },   { "[/bblue]",     "\x1b[39m" },
            { "[bmagenta]",  "\x1b[95m" },   { "[/bmagenta]",  "\x1b[39m" },
            { "[bcyan]",     "\x1b[96m" },   { "[/bcyan]",     "\x1b[39m" },
            { "[bwhite]",    "\x1b[97m" },   { "[/bwhite]",    "\x1b[39m" },

            //             --- Background Colors (Standard) ---
            { "[bgblack]",   "\x1b[40m" },   { "[/bgblack]",   "\x1b[49m" },
            { "[bgred]",     "\x1b[41m" },   { "[/bgred]",     "\x1b[49m" },
            { "[bggreen]",   "\x1b[42m" },   { "[/bggreen]",   "\x1b[49m" },
            { "[bgyellow]",  "\x1b[43m" },   { "[/bgyellow]",  "\x1b[49m" },
            { "[bgblue]",    "\x1b[44m" },   { "[/bgblue]",    "\x1b[49m" },
            { "[bgmagenta]", "\x1b[45m" },   { "[/bgmagenta]", "\x1b[49m" },
            { "[bgcyan]",    "\x1b[46m" },   { "[/bgcyan]",    "\x1b[49m" },
            { "[bgwhite]",   "\x1b[47m" },   { "[/bgwhite]",   "\x1b[49m" },

            //              --- Background Colors (Bright) ---
            { "[bggray]",    "\x1b[100m" },  { "[/bggray]",    "\x1b[49m" },
            { "[bgbred]",    "\x1b[101m" },  { "[/bgbred]",    "\x1b[49m" },
            { "[bgbgreen]",  "\x1b[102m" },  { "[/bgbgreen]",  "\x1b[49m" },
            { "[bgbyellow]", "\x1b[103m" },  { "[/bgbyellow]", "\x1b[49m" },
            { "[bgbblue]",   "\x1b[104m" },  { "[/bgbblue]",   "\x1b[49m" },
            { "[bgbmagenta]","\x1b[105m" },  { "[/bgbmagenta]","\x1b[49m" },
            { "[bgbcyan]",   "\x1b[106m" },  { "[/bgbcyan]",   "\x1b[49m" },
            { "[bgbwhite]",  "\x1b[107m" },  { "[/bgbwhite]",  "\x1b[49m" },

            // --- Other ---
            { "[reset]",     "\x1b[0m" } // Reset all effects
        };
        private static string punctuation = ".,!?;:";   

        public static void TypeRich(string message, int delay = 50, bool newLine = true, bool hideCursor = true, bool doPunctiationDelay = true, float punctionationDelayMultiplier = 1.5f)
        {
            bool cursorVisibility = Console.CursorVisible;
            Console.CursorVisible = hideCursor;

            for (int i = 0; i < message.Length; i++)
            {
                // Check if the current character starts a tag
                if (message[i] == '[')
                {
                    int closingBracket = message.IndexOf(']', i);
                    if (closingBracket != -1)
                    {
                        string tag = message.Substring(i, closingBracket - i + 1);
                        if (Tags.ContainsKey(tag))
                        {
                            Console.Write(Tags[tag]); // Apply style immediately (no animate)
                            i = closingBracket;       // Skip the tag in the loop
                            continue;
                        }
                    }
                }

                // Write char
                Console.Write(message[i]);

                // Handle timing
                if (punctuation.Contains(message[i]) && doPunctiationDelay) 
                    Thread.Sleep((int)(delay * punctionationDelayMultiplier));
                else 
                    Thread.Sleep(delay);
            }

            if (newLine) Console.WriteLine();
            Console.Write("\x1b[0m"); // Reset all effects
            Console.CursorVisible = cursorVisibility;
        }

        public static string GetOptionDropdown(string prompt, string[] options, int delay = 90, string selectedIdentifier = ">", bool colored = false, ConsoleColor color = ConsoleColor.Cyan)
        {
            int index = 0;
            bool selected = false;
            int cursorY = Console.CursorTop;
            Console.CursorVisible = false;
            ConsoleColor baseColor = Console.ForegroundColor;

            Console.SetCursorPosition(0, cursorY);
            if (delay <= 0) { Console.Write($"{prompt}\n"); } else { TypeRich(prompt, delay); }

            for (int i = 0; i < options.Length; i++)
            {
                bool activeElement = i == index;
                string head = activeElement ? $"{selectedIdentifier} " : $"{string.Concat(Enumerable.Repeat(" ", selectedIdentifier.Length))} ";
                string text = $"{head} {options[i]}\n";
                if (colored) Console.ForegroundColor = activeElement ? color : baseColor;
                if (delay <= 0) Console.Write(text);
                else TypeRich(text, 90, false);
            }

            while (!selected)
            {
                if (colored) Console.ForegroundColor = baseColor;
                Console.SetCursorPosition(0, cursorY);
                Console.Write($"{prompt}\n");

                for (int i = 0; i < options.Length; i++)
                {
                    bool activeElement = i == index;
                    string head = activeElement ? $"{selectedIdentifier} " : $"{string.Concat(Enumerable.Repeat(" ", selectedIdentifier.Length))} ";
                    string text = $"{head} {options[i]}\n";
                    if (colored) Console.ForegroundColor = activeElement ? color : baseColor;
                    Console.Write(text);
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
                }
                else if(key.Key == ConsoleKey.Enter) { selected = true; }
            }

            return options[index];
        }
    }
}