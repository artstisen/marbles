namespace Marbles;

/// <summary>
/// A split (":split"): the text in two panes side by side, with a column of "|" between them. The pane with the
/// focus gets the keys and is the one the top row, the mode on the divider (over that pane), the status line and the
/// command line are about; the other is drawn as it is, and follows the changes (EditorEngine.Split.cs). Tab and
/// Shift+Tab in Normal mode change the focus, ":q" closes the pane with the focus and ":only" the other one. The
/// pane with the focus is drawn with the fields of the text area (_engine, _left, _width ...); the other pane keeps
/// its own, and they are swapped when the focus changes
/// </summary>
public partial class TextArea
{
    // The other pane of a split: its editor, and where it is and what is shown of it
    private sealed class Pane
    {
        internal EditorEngine Engine = null!;
        internal int Left;
        internal int Width;
        internal int TextWidth;
        internal int Gutter;
        internal string?[] ShownRows = Array.Empty<string?>();
        internal int[] ShownCells = Array.Empty<int>();
    }

    private Pane? _otherPane;

    // The place of the text area when it is not split
    private int _wholeLeft;
    private int _wholeWidth;

    // ":split": two panes, or one again (as ":only")
    private void Split()
    {
        if (_otherPane != null)
        {
            CloseOtherPane();
            return;
        }

        // (A peek refuses the request, and a field of one line does not make it)
        if (_peek != null || _singleLine) return;

        _wholeLeft = _left;
        _wholeWidth = _width;
        // The pane with the focus at the left, the new one at the right, and a column between them
        var leftWidth = (_wholeWidth - 1) / 2;
        var rightWidth = _wholeWidth - leftWidth - 1;
        SetPlace(_wholeLeft, leftWidth);
        var other = new EditorEngine(_engine, rightWidth - 1, _height);
        _engine.InSplit = true;
        other.InSplit = true;
        other.StartFollowing();
        _otherPane = new Pane
        {
            Engine = other,
            Left = _wholeLeft + leftWidth + 1,
            Width = rightWidth,
            TextWidth = rightWidth - 1,
            Gutter = -1,
            ShownRows = new string?[_height],
            ShownCells = new int[_height]
        };
        RedrawAll();
        _engine.SetMessage(Lang.T("Tab: the other pane, :only: one pane"), false);
    }

    /// <summary>
    /// The other pane of a split, when the editor is closed: the index of its cursor, and whether it is at the
    /// left. Null when not split
    /// </summary>
    internal (int Index, bool OtherLeft)? SplitPlace =>
        _otherPane == null ? null : (_otherPane.Engine.GetCursorIndex(), _otherPane.Left < _left);

    // Opened split, as it was when it was closed: the other pane at its cursor, at the left or the right
    private void RestoreSplit(int index, bool otherLeft)
    {
        Split();
        if (_otherPane == null) return;
        var other = _otherPane.Engine;
        other.SetCursorIndex(index);
        other.StartFollowing();
        if (otherLeft)
        {
            // The panes change places (each keeps its editor)
            (_left, _otherPane.Left) = (_otherPane.Left, _left);
            (_width, _otherPane.Width) = (_otherPane.Width, _width);
            (_textWidth, _otherPane.TextWidth) = (_otherPane.TextWidth, _textWidth);
            _gutter = -1;
            _otherPane.Gutter = -1;
            RedrawAll();
        }
    }

    // Tab (Shift+Tab): the other pane gets the keys
    private void SwitchPane()
    {
        if (_otherPane == null)
        {
            _engine.SetMessage(Lang.T("One pane: :split shows the text in two"), false);
            return;
        }

        _engine.StartFollowing();
        SwapPanes();
        _engine.StopFollowing();
        _engine.TakeSmallRegister(_otherPane.Engine);
        // The mode is written over this pane, and the cursor is shown in it
        _shownModeLabel = null;
        _shownStatus = null;
    }

    // ":q" in a split (also ":q!" and ZQ; ":wq", ":x" and ZZ save first): the pane with the focus is closed, and the
    // other one shows the text
    private void ClosePane()
    {
        if (_otherPane == null) return;
        _engine.StartFollowing();
        SwapPanes();
        _engine.StopFollowing();
        _engine.TakeSmallRegister(_otherPane.Engine);
        CloseOtherPane();
    }

    // A box was drawn on top of the text: in a split it may cover both panes, which are drawn again with the whole
    // screen (with one pane, only the rows it covered)
    private bool _redrawWhole;

    private void Uncover(int firstRow, int rows, int right)
    {
        if (_otherPane != null)
        {
            _redrawWhole = true;
            return;
        }

        for (var r = firstRow; r < firstRow + rows && r < _height; r++)
        {
            _shownRows[r] = null;
            _shownCells[r] = Math.Max(_shownCells[r], right);
        }
    }

    // ":only": the other pane is closed, and the one with the focus takes the whole width
    private void CloseOtherPane()
    {
        if (_otherPane == null)
        {
            _engine.SetMessage(Lang.T("One pane: :split shows the text in two"), false);
            return;
        }

        _otherPane.Engine.Detach();
        _otherPane = null;
        _engine.InSplit = false;
        SetPlace(_wholeLeft, _wholeWidth);
        RedrawAll();
    }

    // The place of the pane with the focus: the text is wrapped in its width again (the line numbers are found
    // again, UpdateGutter)
    private void SetPlace(int left, int width)
    {
        _left = left;
        _width = width;
        _textWidth = width - 1;
        _gutter = -1;
    }

    // The pane with the focus and the other one change places in the fields
    private void SwapPanes()
    {
        var other = _otherPane!;
        (_engine, other.Engine) = (other.Engine, _engine);
        (_left, other.Left) = (other.Left, _left);
        (_width, other.Width) = (other.Width, _width);
        (_textWidth, other.TextWidth) = (other.TextWidth, _textWidth);
        (_gutter, other.Gutter) = (other.Gutter, _gutter);
        (_shownRows, other.ShownRows) = (other.ShownRows, _shownRows);
        (_shownCells, other.ShownCells) = (other.ShownCells, _shownCells);
    }

    // The rows of the other pane, as they are (no mode, status line or cursor of its own)
    private void DrawOtherPane()
    {
        if (_otherPane == null) return;
        _otherPane.Engine.UpdateFollowingView();
        SwapPanes();
        try
        {
            DrawRows();
        }
        finally
        {
            SwapPanes();
        }
    }

    // After the whole screen is drawn again: the other pane is written again, and the column between the panes
    private void RedrawSplit()
    {
        if (_otherPane == null) return;
        Array.Fill(_otherPane.ShownRows, null);
        Array.Fill(_otherPane.ShownCells, 0);
        // Just before the pane at the right
        var column = Math.Max(_left, _otherPane.Left) - 1;
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        for (var r = 0; r < _height; r++)
        {
            Tools.SetCursorEx(_top + r, column);
            Console.Write('|');
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    // The column the mode is centered on: the middle of the pane with the focus (of the window when not split)
    private int ModeCenter => _otherPane == null ? Init.LineWidth / 2 : _left + _width / 2;
}
