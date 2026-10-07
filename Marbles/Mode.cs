namespace marbles;

class Mode
{
    #region General modes
    // General program mode
    public static SelectedMode CurrentMode { get; set; }

    public enum SelectedMode
    {
        Login,
        Manage,
        Search,
        View,
        Edit
    }

    public static void SetMode(SelectedMode mode)
    {
        CurrentMode = mode;
    }


    // Recall mode
    public static RecallMode CurrentRecallMode { get; set; }

    public enum RecallMode
    {
        None,
        Search,
        Top20,
        Favorites,
        ListAll,
        Session
    }

    public static void SetRecallMode(RecallMode mode)
    {
        CurrentRecallMode = mode;
    }



    // Search mode
    public static SearchMode CurrentSearchMode { get; set; }

    public enum SearchMode
    {
        ListAll,
        Search,
        Top20,
        Favorites,
        Session
    }

    public static void SetSearchMode(SearchMode mode)
    {
        CurrentSearchMode = mode;
    }

    #endregion





    #region Editor modes

    public static EditorScrollIndicator CurrentEditorScrollIndicator { get; set; }

    public enum EditorScrollIndicator
    {
        Off,
        On
    }

    public static void SetEditorScrollIndicator(EditorScrollIndicator mode)
    {
        CurrentEditorScrollIndicator = mode;
    }

    public static EditorUndoState CurrentEditorUndoState { get; set; }

    public enum EditorUndoState
    {
        Off,
        On
    }

    public static void SetEditorUndoState(EditorUndoState mode)
    {
        CurrentEditorUndoState = mode;
    }

    #endregion

}