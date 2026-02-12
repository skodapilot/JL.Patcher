using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Spectre.Console;

const string name = "J-link.patcher";

Console.OutputEncoding = Encoding.UTF8;

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

var origSn = AnsiConsole.Prompt(
    new TextPrompt<string>("Orig SN:")
        .DefaultValue("941000024")
        .Validate(s => int.TryParse(s, out _)
            ? ValidationResult.Success()
            : ValidationResult.Error("Only digits are allowed!"))
);

int sn = int.Parse(origSn);

var fakeSn = AnsiConsole.Prompt(
    new TextPrompt<string>("Fake SN:")
        .DefaultValue("123456")
        .Validate(s => int.TryParse(s, out _)
            ? ValidationResult.Success()
            : ValidationResult.Error("Only digits are allowed!"))
);

int fsn = int.Parse(fakeSn);

var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "JLink_x64.dll",
    "JLinkARM.dll"
};

var pending = new ConcurrentStack<string>();

pending.Push(@"c:\");

await AnsiConsole.Status()
    .Spinner(Spinner.Known.Default)
    .StartAsync("Scanning for DLL...", ctx =>
    {
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
                            if (!targets.Contains(Path.GetFileName(file))) continue;

                            var info = FileVersionInfo.GetVersionInfo(file);

                            var fileInfo = new FileInfo(file);

                            if (PatchDll(file))
                            {
                                AnsiConsole.WriteLine(
                                    $"Patched: {fileInfo.Name} v.{info.FileVersion} in {fileInfo.Directory?.FullName}");
                            }
                            else
                            {
                                AnsiConsole.WriteLine(
                                    $"Skipped: {fileInfo.Name} v.{info.FileVersion} in {fileInfo.Directory?.FullName}");
                            }
                        }

                        foreach (var sub in Directory.EnumerateDirectories(current))
                        {
                            pending.Push(sub);
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // silently skip
                    }
                    catch (PathTooLongException)
                    {
                        // silently skip
                    }
                }
            });
        return Task.CompletedTask;
    });

AnsiConsole.WriteLine(@"Done!");


static List<int> FindAll(ReadOnlySpan<byte> buffer, int serial)
{
    var result = new List<int>();

    var pattern = BitConverter.GetBytes(serial);

    if (pattern.Length == 0 || buffer.Length < pattern.Length)
        return result;

    for (int i = 0; i <= buffer.Length - pattern.Length; i++)
    {
        if (buffer.Slice(i, pattern.Length).SequenceEqual(pattern))
        {
            result.Add(i);
            i += pattern.Length - 1; // пропускаем перекрытия
        }
    }

    return result;
}

bool PatchDll(string path)
{
    if (!File.Exists(path))
    {
        return false;
    }


    var bytes = File.ReadAllBytes(path);

    var positions = FindAll(bytes, sn);

    if (positions.Count == 0)
    {
        return false;
    }


    var newsn = BitConverter.GetBytes(fsn);

    foreach (var offset in positions)
    {
        Array.Copy(newsn, 0, bytes, offset, newsn.Length);
    }

    try
    {
        File.Copy(path, path + ".bak", true);
        File.WriteAllBytes(path, bytes);
        return true;
    }
    catch (Exception e)
    {
        AnsiConsole.WriteLine(e.Message);
    }

    return false;
}