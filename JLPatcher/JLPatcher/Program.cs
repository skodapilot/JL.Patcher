using System.Collections.Concurrent;
using Spectre.Console;

const string name = "JL.patcher";

Console.Title = name;
Console.ResetColor();
Console.Clear();

Environment.ExitCode = -1;

var header = new Rule
{
    Justification = Justify.Left,
    Title = name
};
header.RuleStyle("green");
AnsiConsole.Write(header);

var input = AnsiConsole.Prompt(
    new TextPrompt<string>("Enter SN:")
        .Validate(s => int.TryParse(s, out _)
            ? ValidationResult.Success()
            : ValidationResult.Error("Only digits are allowed!"))
);

int number = int.Parse(input);

var options = new EnumerationOptions
{
    RecurseSubdirectories = true,
    IgnoreInaccessible = true,
    MatchCasing = MatchCasing.CaseInsensitive
};

var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "JLink_x64.dll",
    "JLinkARM.dll"
};

var pending = new ConcurrentStack<string>();
pending.Push(@"C:\");

Parallel.ForEach(
    Partitioner.Create(pending),
    dir =>
    {
        while (pending.TryPop(out var current))
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(current))
                {
                    if (targets.Contains(Path.GetFileName(file)))
                        Console.WriteLine(file);
                }

                foreach (var sub in Directory.EnumerateDirectories(current))
                    pending.Push(sub);
            }
            catch (UnauthorizedAccessException)
            {
                // silently skip
            }
            catch (PathTooLongException)
            {
            }
        }
    });

