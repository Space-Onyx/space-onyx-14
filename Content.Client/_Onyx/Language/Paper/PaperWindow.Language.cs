using Content.Client._Onyx.Language.Paper;
using Content.Client.RichText;
using Content.Shared._Onyx.Language.Paper;
using Content.Shared.Paper;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client.Paper.UI;

public sealed partial class PaperWindow
{
    public event Action<uint, ulong, List<PaperLanguageEditOperation>>? OnLanguageSaved;

    private static readonly Type[] PaperAllowedTags =
        [..UserFormattableTags.BaseAllowedTags, typeof(PaperLanguageTag)];

    private uint _revision;
    private ulong _viewGeneration;
    private string _baseText = string.Empty;
    private string _lastText = string.Empty;
    private bool _populating;
    private bool _applyingLanguageView;
    private readonly List<PaperLanguageEditOperation> _operations = new();

    private void InitializeLanguageView()
    {
        PaperLoadingIndicator.SetMessage(Loc.GetString("paper-language-loading"), null, DefaultTextColor);
        BlankPaperIndicator.Visible = false;
        WrittenTextLabel.Visible = false;
        InputContainer.Visible = false;
        EditButtons.Visible = false;
    }

    private void SaveLanguageText(string text)
    {
        OnLanguageSaved?.Invoke(_revision, _viewGeneration, new List<PaperLanguageEditOperation>(_operations));
    }

    public void PopulateLanguage(PaperLanguageViewMessage state)
    {
        if (state.PreserveEditor && InputContainer.Visible)
        {
            if (state.ViewGeneration < _viewGeneration)
                return;
            _revision = state.Revision;
            _viewGeneration = state.ViewGeneration;
            UpdateFillState();
            return;
        }

        if (state.ViewGeneration < _viewGeneration)
            return;

        if (InputContainer.Visible && state.Mode == PaperComponent.PaperAction.Write &&
            state.Revision == _revision && state.EditableText == _baseText)
        {
            _viewGeneration = state.ViewGeneration;
            return;
        }

        _populating = true;
        _applyingLanguageView = true;
        _revision = state.Revision;
        _viewGeneration = state.ViewGeneration;
        _operations.Clear();
        _baseText = state.EditableText;
        _lastText = state.EditableText;

        Populate(new PaperComponent.PaperBoundUserInterfaceState(state.Text, state.StampedBy, state.Mode));
        Input.TextRope = Rope.Leaf.Empty;
        Input.CursorPosition = new TextEdit.CursorPos();
        Input.InsertAtCursor(state.EditableText);
        PaperLoadingIndicator.Visible = false;
        _applyingLanguageView = false;
        _populating = false;
    }

    private void TrackLanguageEdit(string text, int selectionStart, int selectionEnd)
    {
        if (_populating)
            return;

        if (text == _lastText)
        {
            if (selectionStart < selectionEnd)
            {
                _operations.Add(new PaperLanguageEditOperation(
                    selectionStart,
                    selectionEnd - selectionStart,
                    text.Substring(selectionStart, selectionEnd - selectionStart)));
            }
            return;
        }

        var start = CommonPrefix(_lastText, text);
        var suffix = CommonSuffix(_lastText, text, start);
        var deleteLength = _lastText.Length - start - suffix;
        var insertLength = text.Length - start - suffix;
        _operations.Add(new PaperLanguageEditOperation(start, deleteLength, text.Substring(start, insertLength)));
        _lastText = text;
    }

    private static int CommonPrefix(string left, string right)
    {
        var length = Math.Min(left.Length, right.Length);
        var prefix = 0;
        while (prefix < length && left[prefix] == right[prefix])
            prefix++;
        return prefix;
    }

    private static int CommonSuffix(string left, string right, int prefix)
    {
        var limit = Math.Min(left.Length, right.Length) - prefix;
        var suffix = 0;
        while (suffix < limit && left[^(suffix + 1)] == right[^(suffix + 1)])
            suffix++;
        return suffix;
    }
}
