using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WinLevers.App.Services;

/// <summary>The save and open dialogs, parented the way an unpackaged app has to.</summary>
/// <remarks>
/// An unpackaged app has no window of its own as far as a picker is concerned,
/// so it has to be told which HWND to parent itself to. Without that call the
/// picker throws rather than opening. Every view that offers a file goes
/// through here so that rule is written once.
/// </remarks>
internal static class FileDialogs
{
    /// <summary>Asks where to put a file, then writes it.</summary>
    /// <param name="root">The view asking, for the error dialog.</param>
    /// <param name="kind">The file type's label in the dialog.</param>
    /// <param name="extension">Its extension, with the dot.</param>
    /// <param name="suggestedName">The name offered.</param>
    /// <param name="contentFor">The text to write, given the name the user chose.</param>
    /// <returns>Whether a file was written.</returns>
    public static async Task<bool> SaveTextAsync(
        XamlRoot root,
        string kind,
        string extension,
        string suggestedName,
        Func<string, string> contentFor)
    {
        try
        {
            var picker = new global::Windows.Storage.Pickers.FileSavePicker
            {
                SuggestedStartLocation = global::Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary,
                SuggestedFileName = suggestedName,
            };

            picker.FileTypeChoices.Add(kind, [extension]);
            ParentToWindow(picker);

            var file = await picker.PickSaveFileAsync();

            if (file is null)
            {
                return false;
            }

            await global::Windows.Storage.FileIO.WriteTextAsync(
                file, contentFor(Path.GetFileNameWithoutExtension(file.Name)));

            return true;
        }
        catch (Exception exception)
        {
            AppDiagnostics.Log("SaveText", exception);
            await MessageAsync(root, "Could not save the file", exception.Message);
            return false;
        }
    }

    /// <summary>Asks for a file, then reads it.</summary>
    /// <returns>Its text, or null if the user cancelled.</returns>
    public static async Task<string?> OpenTextAsync(XamlRoot root, string extension)
    {
        try
        {
            var picker = new global::Windows.Storage.Pickers.FileOpenPicker
            {
                SuggestedStartLocation = global::Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary,
            };

            picker.FileTypeFilter.Add(extension);
            ParentToWindow(picker);

            var file = await picker.PickSingleFileAsync();

            return file is null ? null : await global::Windows.Storage.FileIO.ReadTextAsync(file);
        }
        catch (Exception exception)
        {
            AppDiagnostics.Log("OpenText", exception);
            await MessageAsync(root, "Could not open the file", exception.Message);
            return null;
        }
    }

    /// <summary>A one-button dialog.</summary>
    public static Task MessageAsync(XamlRoot root, string title, string message) =>
        new ContentDialog
        {
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            CloseButtonText = "OK",
        }.In(root).ShowAsync().AsTask();

    private static void ParentToWindow(object picker)
    {
        if (App.Shell is not null)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Shell);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        }
    }
}
