using Content.Shared.Administration;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client.UserInterface.Controls;

// mfw they ported input() from BYOND

/// <summary>
/// Client-side dialog with multiple prompts.
/// Used by admin tools quick dialog system among other things.
/// </summary>
[GenerateTypedNameReferences]
public sealed partial class DialogWindow : FancyWindow
{
    /// <summary>
    /// Action for when the ok button is pressed or the last field has enter pressed.
    /// Results maps prompt FieldIds to the LineEdit's text contents.
    /// </summary>
    public Action<Dictionary<string, string>>? OnConfirmed;

    /// <summary>
    /// Action for when the cancel button is pressed or the window is closed.
    /// </summary>
    public Action? OnCancelled;

    /// <summary>
    /// Used to ensure that only one output action is invoked.
    /// E.g. Pressing cancel will invoke then close the window, but OnClose will not invoke.
    /// </summary>
    private bool _finished;

    private List<(string, LineEdit)> _promptLines;

    /// <summary>
    /// Create and open a new dialog with some prompts.
    /// </summary>
    /// <param name="title">String to use for the window title.</param>
    /// <param name="entries">Quick dialog entries to create prompts with.</param>
    /// <param name="ok">Whether to have an Ok button.</param>
    /// <param name="cancel">Whether to have a Cancel button. Closing the window will still cancel it.</param>
    /// <remarks>
    /// Won't do anything on its own, you need to handle or network with <see cref="OnConfirmed"/> and <see cref="OnCancelled"/>.
    /// </remarks>
    public DialogWindow(string title, List<QuickDialogEntry> entries, bool ok = true, bool cancel = true)
    {
        RobustXamlLoader.Load(this);

        Title = title;

        OkButton.Visible = ok;
        CancelButton.Visible = cancel;

        _promptLines = new(entries.Count);

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];

            var box = new BoxContainer();
            box.AddChild(new Label() { Text = entry.Prompt, HorizontalExpand = true, SizeFlagsStretchRatio = 0.5f });

            var edit = new LineEdit() { HorizontalExpand = true };

            (Func<string, bool>, string) pair = entry.Type switch
            {
                QuickDialogEntryType.Integer => (VerifyInt, "integer"),
                QuickDialogEntryType.Float => (VerifyFloat, "float"),
                QuickDialogEntryType.ShortText => (VerifyShortText, "short-text"),
                QuickDialogEntryType.LongText => (VerifyLongText, "long-text"),
                _ => throw new ArgumentOutOfRangeException()
            };
            var (valid, name) = pair;

            edit.IsValid += valid;
            // try use placeholder from the caller, fall back to the generic one for whatever type is being validated.
            edit.PlaceHolder = entry.Placeholder ?? Loc.GetString($"quick-dialog-ui-{name}");

            // Last text box gets enter confirmation.
            // Only the last so you don't accidentally confirm early.
            if (i == entries.Count - 1)
                edit.OnTextEntered += _ => Confirm();

            _promptLines.Add((entry.FieldId, edit));
            box.AddChild(edit);
            Prompts.AddChild(box);
        }

        OkButton.OnPressed += _ => Confirm();

        CancelButton.OnPressed += _ =>
        {
            _finished = true;
            OnCancelled?.Invoke();
            Close();
        };

        OnClose += () =>
        {
            if (!_finished)
                OnCancelled?.Invoke();
        };

        MinWidth *= 2; // Just double it.

        OpenCentered();
    }

    protected override void Opened()
    {
        base.Opened();
        
        // Grab keyboard focus for the first dialog entry
        _promptLines[0].Item2.GrabKeyboardFocus();
    }

    private void Confirm()
    {
        var results = new Dictionary<string, string>();
        foreach (var (field, edit) in _promptLines)
        {
            results[field] = edit.Text;
        }

        _finished = true;
        OnConfirmed?.Invoke(results);
        Close();
    }

    #region Input validation


    private bool VerifyInt(string input)
    {
        return int.TryParse(input, out var _);
    }

    private bool VerifyFloat(string input)
    {
        return float.TryParse(input, out var _);
    }

    private bool VerifyShortText(string input)
    {
        return input.Length <= 100;
    }

    private bool VerifyLongText(string input)
    {
        return input.Length <= 2000;
    }

    #endregion
}
