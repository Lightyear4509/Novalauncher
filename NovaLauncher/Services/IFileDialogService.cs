using System.Threading.Tasks;

namespace NovaLauncher.Services;

public interface IFileDialogService
{
    Task<string?> PickExecutableAsync();
    Task<string?> PickCoverImageAsync();
}