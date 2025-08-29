namespace IndiWare.Models;

public partial class DriveSelectionPage : ContentPage
{
    private TaskCompletionSource<List<string>> _tcs = new();

    public DriveSelectionPage(IEnumerable<string> availableDrives)
    {
        InitializeComponent();

        // POPULATE DRIVE LIST
        foreach (var drive in availableDrives)
        {
            var chk = new CheckBox { BindingContext = drive };
            var lbl = new Label { Text = drive, VerticalOptions = LayoutOptions.Center };

            var layout = new HorizontalStackLayout { Spacing = 10 };
            layout.Add(chk);
            layout.Add(lbl);

            // ADD TO STACK
            DriveListStack.Add(layout);
        }
    }

    private async void OnConfirmClicked(object sender, EventArgs e)
    {
        var selectedDrives = new List<string>();

        // COLLECT SELECTED DRIVES FROM CHECKBOXES
        foreach (var layout in DriveListStack.Children.OfType<HorizontalStackLayout>())
        {
            var checkBox = layout.Children.OfType<CheckBox>().FirstOrDefault();
            if (checkBox?.IsChecked == true && checkBox.BindingContext is string drive)
            {
                selectedDrives.Add(drive);
            }
        }

        _tcs.TrySetResult(selectedDrives);
        // CLOSE MODAL PAGE
        await Navigation.PopModalAsync();
    }

    // WAIT USER SELECTION ASYNC
    public Task<List<string>> WaitForSelectionAsync() => _tcs.Task;
}
