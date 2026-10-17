namespace Marbles;

/// <summary>
/// An item of a list with folders: a folder, or an item that can be in the folder above it
/// </summary>
internal interface IFolderItem
{
    bool IsFolder { get; }

    /// <summary>
    /// An item in the folder above it in the list
    /// </summary>
    bool InFolder { get; set; }

    /// <summary>
    /// A folder that is closed: the items in it are not shown
    /// </summary>
    bool Collapsed { get; set; }
}

/// <summary>
/// A list in the order set by the user, with folders: the items of a folder follow it in the list, and are marked
/// InFolder. The favorites of the file browser and the bookmarks of the Jump menu are kept in such lists. Items are
/// moved into and out of folders, a folder moves with its items, and what was removed last can be put back.
///
/// This class does not touch the console.
/// </summary>
internal sealed class FolderList<T> where T : class, IFolderItem
{
    /// <summary>
    /// The items and folders, in their order
    /// </summary>
    internal List<T> Items { get; } = new();

    // What was removed last, with the places it had in the list: an item, a folder with its items, or several items
    private List<(int Index, List<T> Items)>? _removed;

    /// <summary>
    /// Remove an item, or a folder with the items in it
    /// </summary>
    internal void RemoveAt(int index)
    {
        if (index < 0 || index >= Items.Count) return;
        var count = FolderEnd(index) - index + 1;
        // Kept so that it can be put back
        _removed = new List<(int, List<T>)> { (index, Items.GetRange(index, count)) };
        Items.RemoveRange(index, count);
    }

    /// <summary>
    /// Remove several items (folders are not removed this way)
    /// </summary>
    /// <returns>The number removed</returns>
    internal int RemoveMany(ICollection<T> items)
    {
        var removed = new List<(int, List<T>)>();
        for (var i = 0; i < Items.Count; i++)
        {
            if (!Items[i].IsFolder && items.Contains(Items[i])) removed.Add((i, new List<T> { Items[i] }));
        }

        if (removed.Count == 0) return 0;
        Items.RemoveAll(item => !item.IsFolder && items.Contains(item));
        _removed = removed;
        return removed.Count;
    }

    /// <summary>
    /// Nothing can be put back (the list was emptied)
    /// </summary>
    internal void ForgetRemoved()
    {
        _removed = null;
    }

    /// <summary>
    /// Put back what was removed last, where it was
    /// </summary>
    /// <param name="canReturn">Tells if an item can be put back (one that is in the list again is not)</param>
    /// <returns>The place in the list of the first that was put back, or -1 if there is nothing to put back</returns>
    internal int UndoRemove(Func<T, bool> canReturn)
    {
        if (_removed == null) return -1;
        var first = -1;
        // From the top, so that each gets the place it had
        foreach (var (place, removed) in _removed)
        {
            var items = removed.Where(item => item.IsFolder || canReturn(item)).ToList();
            if (items.Count == 0) continue;
            var index = Math.Min(place, Items.Count);
            // Not into the middle of a folder that it was not in
            if (!items[0].InFolder) index = index < Items.Count ? FolderStart(index) : index;
            // An item that was in a folder goes back into it only if the folder is still there
            if (items[0].InFolder && (index == 0 || !(Items[index - 1].IsFolder || Items[index - 1].InFolder)))
            {
                items[0].InFolder = false;
                index = index < Items.Count ? FolderStart(index) : index;
            }

            Items.InsertRange(index, items);
            if (first < 0) first = index;
        }

        _removed = null;
        return first;
    }

    /// <summary>
    /// Move items into a folder (last in it, in the order they have in the list), or out of their folders
    /// </summary>
    /// <param name="items">The items</param>
    /// <param name="folder">The folder, or null to place them last in the list, outside the folders</param>
    /// <returns>The place in the list of the first of them, or -1 if nothing was moved</returns>
    internal int MoveToFolder(ICollection<T> items, T? folder)
    {
        if (folder != null && (!folder.IsFolder || !Items.Contains(folder))) return -1;
        var moving = Items.Where(item => !item.IsFolder && items.Contains(item)).ToList();
        if (moving.Count == 0) return -1;
        Items.RemoveAll(item => moving.Contains(item));
        var index = Items.Count;
        if (folder != null)
        {
            index = FolderEnd(Items.IndexOf(folder)) + 1;
            // Opened, so that what was moved into it can be seen
            folder.Collapsed = false;
        }

        foreach (var item in moving) item.InFolder = folder != null;
        Items.InsertRange(index, moving);
        return index;
    }

    /// <summary>
    /// Move items (or folders, with the items in them) to the top or the end: an item in a folder to the top or
    /// the end of its folder, the others to the top or the end of the list. Their order is kept
    /// </summary>
    /// <returns>The number moved</returns>
    internal int MoveToEnd(ICollection<T> items, bool top)
    {
        var moving = Items.Where(items.Contains).ToList();
        // By where they are: the folder they are in (null: the list)
        var groups = moving.GroupBy(item => item.InFolder ? Items[FolderStart(Items.IndexOf(item))] : null).ToList();
        foreach (var group in groups)
        {
            var blocks = new List<T>();
            foreach (var item in group)
            {
                var index = Items.IndexOf(item);
                var count = item.IsFolder ? FolderEnd(index) - index + 1 : 1;
                blocks.AddRange(Items.GetRange(index, count));
                Items.RemoveRange(index, count);
            }

            int at;
            if (group.Key == null)
            {
                at = top ? 0 : Items.Count;
            }
            else
            {
                var folder = Items.IndexOf(group.Key);
                at = top ? folder + 1 : FolderEnd(folder) + 1;
            }

            Items.InsertRange(at, blocks);
        }

        return moving.Count;
    }

    #region Folders

    /// <summary>
    /// Add a folder in front of an item of the list (in front of its folder, if it is in one)
    /// </summary>
    /// <returns>The place of the folder in the list</returns>
    internal int AddFolder(T folder, int before)
    {
        var index = before < 0 || before >= Items.Count ? Items.Count : FolderStart(before);
        Items.Insert(index, folder);
        return index;
    }

    /// <summary>
    /// Open or close all the folders
    /// </summary>
    /// <returns>The number of folders in the list</returns>
    internal int SetAllCollapsed(bool collapsed)
    {
        var folders = 0;
        foreach (var item in Items)
        {
            if (!item.IsFolder) continue;
            item.Collapsed = collapsed;
            folders++;
        }

        return folders;
    }

    /// <summary>
    /// The folder an item is in (the item itself if it is not in a folder)
    /// </summary>
    internal int FolderOf(int index)
    {
        return index >= 0 && index < Items.Count ? FolderStart(index) : index;
    }

    /// <summary>
    /// The number of items in a folder
    /// </summary>
    internal int FolderCount(int index)
    {
        return index >= 0 && index < Items.Count && Items[index].IsFolder ? FolderEnd(index) - index : 0;
    }

    // The last item of a folder (the folder itself if it is empty, or the item if it is not a folder)
    private int FolderEnd(int index)
    {
        if (!Items[index].IsFolder) return index;
        var end = index;
        while (end + 1 < Items.Count && Items[end + 1].InFolder) end++;
        return end;
    }

    // Where the folder of an item starts (the item itself if it is not in a folder)
    private int FolderStart(int index)
    {
        while (index > 0 && Items[index].InFolder) index--;
        return index;
    }

    #endregion

    /// <summary>
    /// Move an item or a folder one place up or down in the list.
    /// An item moved onto a folder goes into it, and one moved past the first or last item of its folder leaves
    /// the folder. A folder is closed and moves with its items, past the item or folder next to it.
    /// </summary>
    /// <returns>False if it is at the end of the list</returns>
    internal bool Move(int index, bool up)
    {
        if (index < 0 || index >= Items.Count) return false;
        var item = Items[index];
        if (item.IsFolder)
        {
            item.Collapsed = true;
            var end = FolderEnd(index);
            var count = end - index + 1;
            int target;
            if (up)
            {
                if (index == 0) return false;
                target = FolderStart(index - 1);
            }
            else
            {
                if (end == Items.Count - 1) return false;
                // After the item or folder that follows
                target = FolderEnd(end + 1) + 1 - count;
            }

            var block = Items.GetRange(index, count);
            Items.RemoveRange(index, count);
            Items.InsertRange(target, block);
            return true;
        }

        if (up)
        {
            if (index == 0) return false;
            var previous = Items[index - 1];
            if (item.InFolder)
            {
                if (previous.IsFolder)
                {
                    // The first of the folder: out of it, above it
                    Swap(index - 1, index);
                    item.InFolder = false;
                }
                else
                {
                    Swap(index - 1, index);
                }
            }
            else if (previous.IsFolder || previous.InFolder)
            {
                // Into the folder above, as the last in it
                item.InFolder = true;
                Items[FolderStart(index - 1)].Collapsed = false;
            }
            else
            {
                Swap(index - 1, index);
            }

            return true;
        }

        if (item.InFolder)
        {
            if (index + 1 < Items.Count && Items[index + 1].InFolder)
            {
                Swap(index, index + 1);
            }
            else
            {
                // The last of the folder: out of it, below it
                item.InFolder = false;
            }

            return true;
        }

        if (index + 1 >= Items.Count) return false;
        var next = Items[index + 1];
        Swap(index, index + 1);
        if (next.IsFolder)
        {
            // Into the folder below, as the first in it
            item.InFolder = true;
            next.Collapsed = false;
        }

        return true;
    }

    private void Swap(int a, int b)
    {
        (Items[a], Items[b]) = (Items[b], Items[a]);
    }
}
