using System;
using System.IO;

namespace BifyLang.Exceptions
{
    public class SimpleErrorWrapper(Exception exception)
    {

        public void PrintStackTrace(int contextLines = 2)
        {
            ConsoleColor originalColor = Console.ForegroundColor;

            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("Traceback (most recent call last):");
            Console.WriteLine();

            PrintException(exception, contextLines);

            Console.ForegroundColor = originalColor;
        }

        private void PrintException(Exception ex, int contextLines)
        {
            if (!string.IsNullOrEmpty(ex.StackTrace))
            {
                string[] lines = ex.StackTrace.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    string trimmed = line.Trim();
                    if (trimmed.Contains(" in ") && trimmed.Contains(":line "))
                    {
                        int inIndex = trimmed.IndexOf(" in ");
                        int lineIndex = trimmed.IndexOf(":line ");
                        string methodInfo = trimmed.Substring(0, inIndex);
                        string filePath = trimmed.Substring(inIndex + 4, lineIndex - (inIndex + 4));
                        string lineNumberStr = trimmed.Substring(lineIndex + 6).Trim();

                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Write("  File \"");
                        Console.ForegroundColor = ConsoleColor.DarkCyan;
                        Console.Write(filePath);
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Write("\", line ");
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.Write(lineNumberStr);
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Write(", in ");
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine(methodInfo);

                        if (File.Exists(filePath) && int.TryParse(lineNumberStr, out int lineNumber))
                        {
                            PrintCodeSnippet(filePath, lineNumber, contextLines);
                        }
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine("  " + trimmed);
                    }
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("  (No stack trace available)");
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"{ex.GetType().Name}: {ex.Message}");
            Console.WriteLine();

            if (ex.InnerException != null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Inner Exception:");
                PrintException(ex.InnerException, contextLines);
            }
        }


        private void PrintCodeSnippet(string filePath, int errorLine, int contextLines)
        {
            try
            {
                string[] sourceLines = File.ReadAllLines(filePath);
                int targetIndex = errorLine - 1;
                int startIndex = Math.Max(0, targetIndex - contextLines);
                int endIndex = Math.Min(sourceLines.Length - 1, targetIndex + contextLines);

                for (int i = startIndex; i <= endIndex; i++)
                {
                    if (i == targetIndex)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        Console.Write("> ");
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Gray;
                        Console.Write("  ");
                    }
                    Console.WriteLine($"{i + 1,4}: {sourceLines[i]}");
                }
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine("  (Could not read source file: " + ex.Message + ")");
            }
        }
    }
}
