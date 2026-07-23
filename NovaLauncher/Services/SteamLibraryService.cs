using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using NovaLauncher.Models;

namespace NovaLauncher.Services;

public sealed class SteamLibraryService
{
    public string? FindSteamDirectory()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        string? registryPath = Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Valve\Steam",
            "SteamPath",
            null) as string;

        if (IsValidSteamDirectory(registryPath))
        {
            return Path.GetFullPath(registryPath!);
        }

        string? registryInstallPath = Registry.GetValue(
            @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam",
            "InstallPath",
            null) as string;

        if (IsValidSteamDirectory(registryInstallPath))
        {
            return Path.GetFullPath(registryInstallPath!);
        }

        string[] commonPaths =
        {
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ProgramFilesX86),
                "Steam"),

            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ProgramFiles),
                "Steam")
        };

        return commonPaths.FirstOrDefault(IsValidSteamDirectory);
    }

    public IReadOnlyList<string> FindSteamLibraries()
    {
        string? steamDirectory = FindSteamDirectory();

        if (steamDirectory is null)
        {
            return Array.Empty<string>();
        }

        HashSet<string> libraries =
            new(StringComparer.OrdinalIgnoreCase)
            {
                steamDirectory
            };

        string libraryFile = Path.Combine(
            steamDirectory,
            "steamapps",
            "libraryfolders.vdf");

        if (!File.Exists(libraryFile))
        {
            return libraries.ToList();
        }

        try
        {
            string fileContents = File.ReadAllText(libraryFile);

            MatchCollection matches = Regex.Matches(
                fileContents,
                "\"path\"\\s+\"(?<path>[^\"]+)\"",
                RegexOptions.IgnoreCase);

            foreach (Match match in matches)
            {
                string libraryPath = match.Groups["path"].Value
                    .Replace(@"\\", @"\");

                if (IsValidSteamDirectory(libraryPath))
                {
                    libraries.Add(Path.GetFullPath(libraryPath));
                }
            }
        }
        catch
        {
            // The main Steam directory is still returned if this file
            // cannot be read.
        }

        return libraries.ToList();
    }

    public IReadOnlyList<SteamGameInfo> FindInstalledGames()
    {
        List<SteamGameInfo> games = new();

        foreach (string library in FindSteamLibraries())
        {
            string steamAppsDirectory =
                Path.Combine(library, "steamapps");

            if (!Directory.Exists(steamAppsDirectory))
            {
                continue;
            }

            foreach (string manifestPath in Directory.EnumerateFiles(
                         steamAppsDirectory,
                         "appmanifest_*.acf"))
            {
                SteamGameInfo? game = ParseManifest(
                    manifestPath,
                    library);

                if (game is not null)
                {
                    games.Add(game);
                }
            }
        }

        return games
            .GroupBy(game => game.AppId)
            .Select(group => group.First())
            .OrderBy(game => game.Name)
            .ToList();
    }

    private static SteamGameInfo? ParseManifest(
        string manifestPath,
        string libraryDirectory)
    {
        try
        {
            string manifest = File.ReadAllText(manifestPath);

            string? appId = ReadQuotedValue(manifest, "appid");
            string? name = ReadQuotedValue(manifest, "name");
            string? installDirectoryName =
                ReadQuotedValue(manifest, "installdir");

            if (string.IsNullOrWhiteSpace(appId) ||
                string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(installDirectoryName))
            {
                return null;
            }

            string installDirectory = Path.Combine(
                libraryDirectory,
                "steamapps",
                "common",
                installDirectoryName);

            return new SteamGameInfo
            {
                AppId = appId,
                Name = name,
                InstallDirectory = installDirectory
            };
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadQuotedValue(
        string text,
        string key)
    {
        Match match = Regex.Match(
            text,
            $"\"{Regex.Escape(key)}\"\\s+\"(?<value>[^\"]*)\"",
            RegexOptions.IgnoreCase);

        return match.Success
            ? match.Groups["value"].Value
            : null;
    }

    private static bool IsValidSteamDirectory(string? path)
    {
        return !string.IsNullOrWhiteSpace(path) &&
               Directory.Exists(path) &&
               Directory.Exists(Path.Combine(path, "steamapps"));
    }
}