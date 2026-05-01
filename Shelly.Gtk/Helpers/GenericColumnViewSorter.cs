using Gtk;
using Gio;
using Shelly.Gtk.Enums;
using Shelly.Gtk.UiModels.PackageManagerObjects.GObjects;

namespace Shelly.Gtk.Helpers;

public static class GenericColumnViewSorter
{

    public static void Sort(
        Gio.ListStore listStore,
        List<AlpmPackageGObject> items,
        PackageSortColumn column,
        SortType order)
    {
        Comparison<AlpmPackageGObject> comparison = column switch
        {
            PackageSortColumn.Name =>
                (a, b) => Compare(a.Package?.Name, b.Package?.Name),

            PackageSortColumn.Repo =>
                (a, b) => Compare(a.Package?.Repository, b.Package?.Repository),

            PackageSortColumn.Version =>
                (a, b) => Compare(a.Package?.Version, b.Package?.Version),

            _ => (_, _) => 0
        };

        if (order == SortType.Descending)
        {
            var baseComp = comparison;

            comparison = (a, b) =>
                -baseComp(a, b);
        }

        items.Sort(comparison);

        SpliceReplace(
            listStore,
            items
        );
    }

    private static int Compare(
        string? a,
        string? b)
    {
        return string.Compare(
            a,
            b,
            StringComparison.OrdinalIgnoreCase
        );
    }
    
    private static void SpliceReplace(
        Gio.ListStore listStore,
        List<AlpmPackageGObject> items)
    {
        var array = new GObject.Object[items.Count];

        for (int i = 0; i < items.Count; i++)
        {
            array[i] = items[i];
        }

        listStore.Splice(
            0,
            listStore.GetNItems(),
            array,
            (uint)array.Length
        );
    }
}