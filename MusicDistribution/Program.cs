using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TagLib;
using File = System.IO.File;

internal class Options
{
    public string Source { get; set; } = "";
    public string Dest { get; set; } = "";
    public string Pattern { get; set; } =
        "{artist_name}/{album_name} ({release_year})/{multi_disc_path}{track_num} - {track_name}.mp3";
    public bool Move { get; set; } = false;
    public bool Overwrite { get; set; } = false;
    public bool DryRun { get; set; } = false;
}

internal sealed class Plan
{
    public required string AbsTarget { get; init; }
    public required string RelativeTarget { get; init; }
}

class Program
{
    static int Main(string[] args)
    {
        try
        {
            var opt = ParseArgs(args);
            if (string.IsNullOrWhiteSpace(opt.Source) || string.IsNullOrWhiteSpace(opt.Dest))
            {
                Console.WriteLine("Usage:");
                Console.WriteLine("  --src <sourceFolder> --dst <destFolder> [--pattern \"...\"] [--move] [--overwrite] [--dryrun]");
                return 2;
            }

            if (!Directory.Exists(opt.Source))
            {
                Console.WriteLine($"Source not found: {opt.Source}");
                return 3;
            }

            Console.WriteLine($"Scanning: {opt.Source}");
            var files = Directory.EnumerateFiles(opt.Source, "*.mp3", SearchOption.AllDirectories).ToList();
            Console.WriteLine($"Found {files.Count} mp3 file(s).");

            int processed = 0, skipped = 0, errors = 0;

            foreach (var src in files)
            {
                try
                {
                    var plan = BuildPlan(src, opt);
                    if (plan is null)
                    {
                        skipped++;
                        continue;
                    }

                    Console.WriteLine($"{Path.GetFileName(src)}");
                    Console.WriteLine($"  -> {plan.RelativeTarget}");

                    if (!opt.DryRun)
                    {
                        var targetDir = Path.GetDirectoryName(plan.AbsTarget);
                        if (!string.IsNullOrEmpty(targetDir))
                            Directory.CreateDirectory(targetDir);

                        string absTarget = plan.AbsTarget;

                        if (File.Exists(absTarget))
                        {
                            if (opt.Overwrite)
                            {
                                File.Delete(absTarget);
                            }
                            else
                            {
                                absTarget = ResolveCollision(absTarget);
                            }
                        }

                        if (opt.Move) File.Move(src, absTarget);
                        else File.Copy(src, absTarget, overwrite: false);
                    }

                    processed++;
                }
                catch (Exception ex)
                {
                    errors++;
                    Console.WriteLine($"  ! Error: {ex.Message}");
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Done. Processed: {processed}, Skipped: {skipped}, Errors: {errors}");
            return errors > 0 ? 1 : 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return 1;
        }
    }

    private static string ResolveCollision(string absTarget)
    {
        var dir = Path.GetDirectoryName(absTarget) ?? "";
        var name = Path.GetFileNameWithoutExtension(absTarget);
        var ext = Path.GetExtension(absTarget);
        int i = 2;
        string candidate;
        do
        {
            candidate = Path.Combine(dir, $"{name} ({i}){ext}");
            i++;
        } while (File.Exists(candidate));
        return candidate;
    }

    private static Plan? BuildPlan(string src, Options opt)
    {
        using var file = TagLib.File.Create(src);

        var title = NullOrTrim(file.Tag.Title) ?? Path.GetFileNameWithoutExtension(src);
        var artists = (file.Tag.Performers?.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray()
                       ?? Array.Empty<string>());
        var artist = artists.FirstOrDefault() ?? "Unknown Artist";
        var allArtists = artists.Length > 0 ? string.Join(", ", artists) : artist;

        var album = NullOrTrim(file.Tag.Album) ?? "Unknown Album";
        var year = file.Tag.Year > 0 ? file.Tag.Year.ToString() : "";
        var releaseDate = GuessReleaseDate(file);
        var track = file.Tag.Track > 0 ? (int)file.Tag.Track : 0;
        var disc = file.Tag.Disc > 0 ? (int)file.Tag.Disc : 0;

        var replacements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["{track_name}"] = San(title, forFile: true),
            ["{artist_name}"] = San(artist),
            ["{all_artist_names}"] = San(allArtists),
            ["{album_name}"] = San(album),
            ["{track_num}"] = track > 0 ? track.ToString("00") : "00",
            ["{release_year}"] = year,
            ["{release_date}"] = releaseDate,
            ["{multi_disc_path}"] = disc > 1 ? $"CD{disc}/" : "",
            ["{multi_disc_paren}"] = disc > 1 ? $"CD{disc}" : "",
            ["{playlist_name}"] = "",
            ["{context_name}"] = "",
            ["{context_index}"] = "",
            ["{canvas_id}"] = "",
        };

        var relative = ApplyPattern(opt.Pattern, replacements);
        if (string.IsNullOrWhiteSpace(relative)) return null;

        if (!relative.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
            relative += ".mp3";

        relative = NormalizeAndSanitizePath(relative);
        var abs = Path.Combine(opt.Dest, relative);

        return new Plan { AbsTarget = abs, RelativeTarget = relative };
    }

    private static string NormalizeAndSanitizePath(string path)
    {
        var parts = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => San(p, forFile: true))
                        .ToArray();
        return string.Join(Path.DirectorySeparatorChar, parts);
    }

    private static string ApplyPattern(string pattern, IDictionary<string, string> repl)
    {
        var result = pattern;
        foreach (var kv in repl)
            result = result.Replace(kv.Key, kv.Value ?? "", StringComparison.OrdinalIgnoreCase);
        return result.Trim();
    }

    private static string San(string input, bool forFile = false)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        var invalid = forFile ? Path.GetInvalidFileNameChars() : Path.GetInvalidPathChars();
        var sb = new StringBuilder(input.Length);
        foreach (var ch in input.Trim())
        {
            if (invalid.Contains(ch) || ch == ':' || ch == '|' || ch == '?' || ch == '*'
                || ch == '"' || ch == '<' || ch == '>')
                sb.Append(' ');
            else
                sb.Append(ch);
        }
        var s = sb.ToString();
        while (s.Contains("  ")) s = s.Replace("  ", " ");
        return s.Trim().Trim('.');
    }

    private static string? NullOrTrim(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string GuessReleaseDate(TagLib.File f)
    {
        if (f.Tag.Year > 0) return f.Tag.Year.ToString(CultureInfo.InvariantCulture);
        return "";
    }

    private static Options ParseArgs(string[] args)
    {
        var o = new Options();
        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            switch (a)
            {
                case "--src":
                    o.Source = Next(args, ref i);
                    break;
                case "--dst":
                case "--dest":
                    o.Dest = Next(args, ref i);
                    break;
                case "--pattern":
                    o.Pattern = Next(args, ref i);
                    break;
                case "--move":
                    o.Move = true;
                    break;
                case "--overwrite":
                    o.Overwrite = true;
                    break;
                case "--dryrun":
                    o.DryRun = true;
                    break;
                default:
                    // ignore unknowns so you can pass future flags safely
                    break;
            }
        }
        return o;

        static string Next(string[] argv, ref int i)
        {
            if (i + 1 >= argv.Length)
                throw new ArgumentException($"Missing value for {argv[i]}");
            i++;
            return argv[i];
        }
    }
}
