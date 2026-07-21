using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace NovaLauncher.Services;

public sealed class FileDialogService : IFileDialogService
{
    private readonly Window _window;

    public FileDialogService(Window window)
    {
        _window = window;
    }

    public async Task<string?> PickExecutableAsync()
    {
        IReadOnlyList<IStorageFile> selectedFiles =
            await _window.StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    Title = "Choose a game executable",
                    AllowMultiple = false,
                    FileTypeFilter =
                    [
                        new FilePickerFileType(
                            "Windows executable")
                        {
                            Patterns = ["*.exe"]
                        }
                    ]
                });

        if (selectedFiles.Count == 0)
        {
            return null;
        }

        return selectedFiles[0].TryGetLocalPath();
    }

    public async Task<string?> PickCoverImageAsync()
    {
        IReadOnlyList<IStorageFile> selectedFiles =
            await _window.StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    Title = "Choose cover image",
                    AllowMultiple = false,
                    FileTypeFilter =
                    [
                        new FilePickerFileType("Image files")
                        {
                            Patterns =
                            [
                                "*.png",
                                "*.jpg",
                                "*.jpeg",
                                "*.webp",
                                "*.bmp"
                            ]
                        }
                    ]
                });

        if (selectedFiles.Count == 0)
        {
            return null;
        }

        return selectedFiles[0].TryGetLocalPath();
    }
}