using System.Diagnostics;
using System.Runtime.InteropServices;

using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

using FlaUIApplication = FlaUI.Core.Application;

namespace MissingFiles.UiTests;

/// <summary>
/// The real desktop app in its own process, driven through UI Automation.
/// </summary>
/// <remarks>
/// Controls are used through their automation patterns, not with the mouse and keyboard,
/// so the tests do not need the window to have focus. Message boxes and file dialogs are
/// found by control ID, as their button texts depend on the language of Windows.
/// </remarks>
internal sealed class AppSession : IDisposable
{
    public const string DataFolderVariable = "MISSINGFILES_DATA_FOLDER";

    // Windows gives the only button of a message box the ID of Cancel.
    private const string MessageBoxOk = "2";
    private const string ConfirmOk = "1";
    private const string ConfirmCancel = "2";
    private const string FileDialogName = "1148";
    private const string FileDialogOpen = "1";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan FileDialogRetryInterval = TimeSpan.FromSeconds(1);

    private readonly UIA3Automation _automation = new();
    private readonly Process _process;
    private readonly FlaUIApplication _application;

    /// <summary>Starts the app with the test's data folder, which keeps it away from the real settings (REQ-04a).</summary>
    public AppSession(TestFolder folder)
        : this(folder, folder.DataFolder)
    {
        try
        {
            // The size the window gets on a CI runner (1024 x 768), so that a test behaves the same on every desktop.
            MainWindow.Patterns.Transform.Pattern.Resize(1024, 688);

            // Stop here if it did not work, as closing the window would save over the real settings.
            var fileTypes = TextBoxValue("FileTypesFile");
            if (!fileTypes.StartsWith(folder.DataFolder, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"The app does not use the test data folder: {fileTypes}");
            }
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>
    /// Starts the app with any value of the variable, and waits for the first window. A value other than
    /// a folder in the test can reach the real settings, so such a test must not use <see cref="Close"/>.
    /// </summary>
    public AppSession(TestFolder folder, string dataFolderValue)
    {
        var start = new ProcessStartInfo(Path.Join(AppContext.BaseDirectory, "MissingFiles.exe"))
        {
            UseShellExecute = false,
            WorkingDirectory = folder.Root,
        };
        start.Environment[DataFolderVariable] = dataFolderValue;
        _process = Process.Start(start)!;
        _application = FlaUIApplication.Attach(_process.Id);
        try
        {
            MainWindow = _application.GetMainWindow(_automation, Timeout) ?? throw new TimeoutException("The main window did not appear.");
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public Window MainWindow { get; }

    /// <summary>The exit code, after the app has ended.</summary>
    public int ExitCode => _process.ExitCode;

    /// <summary>Enters the folders. The output folder is always set, so nothing goes to the real Documents folder.</summary>
    public void SetFolders(string source, string destination, string output)
    {
        SetText("SourceFolder", source);
        SetText("DestinationFolder", destination);
        SetText("OutputFolder", output);
    }

    public void SetText(string id, string text) => Find(id).AsTextBox().Text = text;

    public string TextBoxValue(string id) => Find(id).AsTextBox().Text;

    /// <summary>The name of a control, which is the text of a text block.</summary>
    public string Text(string id) => Find(id).Name;

    public bool Exists(string id) => FindNow(MainWindow, id) is not null;

    public AutomationElement Find(string id) => Find(MainWindow, id);

    public AutomationElement Find(AutomationElement parent, string id) =>
        WaitFor($"control '{id}'", () => FindNow(parent, id));

    public bool IsEnabled(string id) => Find(id).IsEnabled;

    /// <summary>Presses a button when it is enabled. The app handles this after the call has returned.</summary>
    public void Press(string id) => Press(MainWindow, id);

    public void Press(AutomationElement parent, string id)
    {
        var button = Find(parent, id);
        WaitUntil($"button '{id}' is enabled", () => button.IsEnabled);
        button.AsButton().Invoke();
    }

    public CheckBox GroupCheckBox(string name) =>
        WaitFor($"check box '{name}'", () => MainWindow.FindFirstDescendant(c => c.ByControlType(ControlType.CheckBox).And(c.ByName(name)))?.AsCheckBox());

    /// <summary>Starts a scan and waits for its result.</summary>
    public void Scan()
    {
        Press("StartScan");
        WaitUntil("the scan has finished", () => Status.StartsWith("Scan finished", StringComparison.Ordinal));
    }

    public string Status => Text("StatusMessage");

    /// <summary>
    /// The cells of the rows the list of missing files has created, one row per array. A cell without
    /// text is named "Item: ...". The first column always has text, so it shows that a row is ready.
    /// </summary>
    public string[][] MissingFileRows() =>
        WaitFor("the rows of the list", () =>
        {
            var rows = Find("MissingFiles").AsGrid().Rows.Select(row => row.Cells.Select(cell => cell.Value).ToArray()).ToArray();
            return rows.Any(cells => cells[0].StartsWith("Item: ", StringComparison.Ordinal)) ? null : rows;
        });

    /// <summary>Scrolls the page of the main window, from 0 (top) to 100 (bottom) percent.</summary>
    public void ScrollPage(double percent)
    {
        const double NoHorizontalScroll = -1;
        Find("Page").Patterns.Scroll.Pattern.SetScrollPercent(NoHorizontalScroll, percent);
    }

    /// <summary>Waits for a dialog of the main window with this title.</summary>
    public Window Dialog(string title) => WaitFor($"dialog '{title}'", () => FindDialog(title));

    public bool DialogIsOpen(string title) => FindDialog(title) is not null;

    /// <summary>All text in a dialog.</summary>
    public static string[] DialogTexts(Window dialog) =>
        [.. dialog.FindAllDescendants(c => c.ByControlType(ControlType.Text)).Select(e => e.Name)];

    /// <summary>Waits for a message box from the app, returns its text and closes it with OK.</summary>
    public string DismissMessage(string title)
    {
        var dialog = Dialog(title);
        var text = string.Join('\n', DialogTexts(dialog));
        Press(dialog, MessageBoxOk);
        WaitUntil($"dialog '{title}' is closed", () => !DialogIsOpen(title));
        return text;
    }

    /// <summary>
    /// For an app that shows a message box instead of its main window: returns the text,
    /// closes the box with OK and waits until the app has exited.
    /// </summary>
    public string DismissStartError()
    {
        var text = string.Join('\n', DialogTexts(MainWindow));
        Press(MainWindow, MessageBoxOk);
        WaitUntil("the app has exited", () => _application.HasExited);
        return text;
    }

    /// <summary>Waits for the OK/Cancel question from the app and answers it.</summary>
    public string AnswerConfirmation(string title, bool ok)
    {
        var dialog = Dialog(title);
        var text = string.Join('\n', DialogTexts(dialog));
        Press(dialog, ok ? ConfirmOk : ConfirmCancel);
        WaitUntil($"dialog '{title}' is closed", () => !DialogIsOpen(title));
        return text;
    }

    /// <summary>Waits for the open file dialog with this title and opens a file in it.</summary>
    public void ChooseFile(string title, string path)
    {
        var dialog = Dialog(title);
        var name = WaitFor("the file name box", () => dialog.FindFirstDescendant(c => c.ByControlType(ControlType.Edit).And(c.ByAutomationId(FileDialogName))));

        // A dialog that is still showing its first folder can ignore the file name, so try again until it closes.
        WaitUntil($"dialog '{title}' is closed", () =>
        {
            if (!DialogIsOpen(title))
            {
                return true;
            }

            name.AsTextBox().Text = path;
            Press(dialog, FileDialogOpen);
            Thread.Sleep(FileDialogRetryInterval);
            return false;
        });
    }

    /// <summary>Closes the main window like a user does, so the app saves its settings, and waits until it has exited.</summary>
    public void Close()
    {
        MainWindow.Close();
        WaitUntil("the app has exited", () => _application.HasExited);
    }

    /// <summary>Waits for a value; the error names what was expected and which windows are open.</summary>
    public T WaitFor<T>(string description, Func<T?> probe)
        where T : class
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < Timeout)
        {
            try
            {
                if (probe() is { } value)
                {
                    return value;
                }
            }
            catch (COMException)
            {
                // The element went away while it was read, e.g. a closing dialog.
            }

            Thread.Sleep(PollInterval);
        }

        throw new TimeoutException($"Waited {Timeout.TotalSeconds:N0} s for {description}. {DescribeWindows()}");
    }

    public void WaitUntil(string description, Func<bool> condition) =>
        WaitFor<object>(description, () => condition() ? new object() : null);

    public void Dispose()
    {
        // Not a normal close: this also ends an app that is stuck in a dialog after a failed test.
        _application.Kill();
        _application.Dispose();
        _process.Dispose();
        _automation.Dispose();
    }

    private static AutomationElement? FindNow(AutomationElement parent, string id) =>
        parent.FindFirstDescendant(c => c.ByAutomationId(id));

    private Window? FindDialog(string title) =>
        MainWindow.FindFirstChild(c => c.ByControlType(ControlType.Window).And(c.ByName(title)))?.AsWindow();

    private string DescribeWindows()
    {
        try
        {
            var dialogs = MainWindow.FindAllChildren(c => c.ByControlType(ControlType.Window));
            var texts = dialogs.SelectMany(d => new[] { $"[{d.Name}]" }.Concat(DialogTexts(d.AsWindow())));
            return $"Open dialogs: {string.Join(' ', texts)}";
        }
        catch (COMException ex)
        {
            return $"Open dialogs unknown: {ex.Message}";
        }
    }
}
