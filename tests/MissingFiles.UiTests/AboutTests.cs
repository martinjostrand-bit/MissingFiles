using System.Runtime.InteropServices;

using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

using MissingFiles.Core;

namespace MissingFiles.UiTests;

/// <summary>The About dialog in the real app (spec REQ-36, REQ-37, FT-11).</summary>
public sealed partial class AboutTests : IDisposable
{
    private const string Title = "About MissingFiles";

    private const uint WmKeyDown = 0x0100;
    private const uint WmKeyUp = 0x0101;
    private const int VkEscape = 0x1B;
    private const int EscapeScanCode = 0x01;

    private readonly TestFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void AboutShowsTheAboutInformationAndCloses() // REQ-36, REQ-37, REQ-39, FT-11
    {
        using var app = new AppSession(_folder);

        app.Press("About");
        var dialog = app.Dialog(Title);

        // The same text as --about, as both come from the core library.
        var text = app.Find(dialog, "AboutText").AsTextBox();
        Assert.Equal(ProductInfo.AboutText, text.Text);
        Assert.True(text.IsReadOnly);
        Assert.Contains($"Revision: {ProductInfo.Version}", text.Text, StringComparison.Ordinal);
        Assert.Contains("Author: Martin Strand", text.Text, StringComparison.Ordinal);
        Assert.Contains("Copyright © 2026 Martin Strand", text.Text, StringComparison.Ordinal);
        Assert.Contains("License: Free to use.", text.Text, StringComparison.Ordinal);
        Assert.EndsWith(ProductInfo.LicenseText, text.Text, StringComparison.Ordinal);

        // The text can be selected, and so copied. The clipboard is not used, as it belongs to the user.
        var textPattern = text.Patterns.Text.Pattern;
        Assert.NotEqual(SupportedTextSelection.None, textPattern.SupportedTextSelection);
        textPattern.DocumentRange.Select();
        Assert.Equal(ProductInfo.AboutText, Assert.Single(textPattern.GetSelection()).GetText(-1));

        app.Press(dialog, "CloseButton");
        app.WaitUntil("the About dialog is closed", () => !app.DialogIsOpen(Title));
    }

    [Fact]
    public void AboutIsModal() // REQ-37
    {
        using var app = new AppSession(_folder);

        app.Press("About");
        var dialog = app.Dialog(Title);

        Assert.True(dialog.Patterns.Window.Pattern.IsModal);
        Assert.Equal(dialog.Name, app.MainWindow.ModalWindows.Single().Name);
    }

    [Fact]
    public void EscClosesTheAboutDialog() // REQ-37, FT-11
    {
        using var app = new AppSession(_folder);

        app.Press("About");
        var dialog = app.Dialog(Title);

        PressEscape(dialog);

        app.WaitUntil("the About dialog is closed", () => !app.DialogIsOpen(Title));
    }

    /// <summary>
    /// Sends Esc to the window itself, not with the keyboard, so the test does not need the
    /// keyboard focus of the desktop and cannot press Esc in another program.
    /// </summary>
    private static void PressEscape(Window window)
    {
        var handle = window.Properties.NativeWindowHandle.Value;
        var lParam = 1 | (EscapeScanCode << 16); // Repeat count 1.
        Assert.True(PostMessage(handle, WmKeyDown, VkEscape, lParam));
        Assert.True(PostMessage(handle, WmKeyUp, VkEscape, unchecked((int)0xC0000000) | lParam)); // Key was down, is released.
    }

    [LibraryImport("user32.dll", EntryPoint = "PostMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostMessage(nint window, uint message, nint wParam, nint lParam);
}
