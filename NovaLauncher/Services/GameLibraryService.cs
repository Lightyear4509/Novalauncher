using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using NovaLauncher.Models;

namespace NovaLauncher.Services;

public class GameLibraryService
{
    private readonly string _libraryFilePath;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public GameLibraryService()
    {
        string applicationDataFolder = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);

        string novaLauncherFolder = Path.Combine(
            applicationDataFolder,
            "NovaLauncher");

        Directory.CreateDirectory(novaLauncherFolder);

        _libraryFilePath = Path.Combine(
            novaLauncherFolder,
            "games.json");
    }

    public List<Game> LoadGames()
    {
        if (!File.Exists(_libraryFilePath))
        {
            return [];
        }

        try
        {
            string json = File.ReadAllText(_libraryFilePath);

            List<Game>? games = JsonSerializer.Deserialize<List<Game>>(
                json,
                _jsonOptions);

            return games ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
    }

    public bool SaveGames(IEnumerable<Game> games)
    {
        try
        {
            string json = JsonSerializer.Serialize(
                games,
                _jsonOptions);

            File.WriteAllText(_libraryFilePath, json);

            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}