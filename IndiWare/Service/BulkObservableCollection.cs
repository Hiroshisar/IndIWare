using System.Collections.ObjectModel;
using System.Collections.Specialized;

public class BulkObservableCollection<T> : ObservableCollection<T>
{
    private bool _suppressNotification = false;

    public void AddRange(IEnumerable<T> items)
    {
        if (items == null) return;

        _suppressNotification = true;

        foreach (var item in items)
            Add(item);

        _suppressNotification = false;

        // SEND A SINGLE NOTIFICATION TO THE UI TO FORCE A REFRESH
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (!_suppressNotification)
            base.OnCollectionChanged(e);
    }
}
