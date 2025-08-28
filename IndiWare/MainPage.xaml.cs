using IndiWare.Models;
using IndiWare.Service;
using System.Diagnostics;

namespace IndiWare
{
    public partial class MainPage : ContentPage
    {
        public List<FileItem> _results { get; set; } = [];
        BulkObservableCollection<FileItem> Results = [];
        private readonly DatabaseService _db;
        private int _currentPage = 0;
        private const int _pageSize = 100;

        public MainPage(DatabaseService db)
        {
            InitializeComponent();
            _db = db;
            ResultsView.ItemsSource = Results;
        }

        private async void OnIndexFilesClicked(object sender, EventArgs e)
        {
            // MAKE LOADING OVERLAY VISIBLE
            LoadingOverlay.IsVisible = true;

            // CLEAR PREVIOUS RESULTS
            _results.Clear();
            Results.Clear();

            try
            {
                // CHECK IF DATABASE IS EMPTY
                if (await _db.IsDatabaseEmptyAsync())
                {
                    // IF EMPTY, PROMPT TO START INDEXING
                    await DisplayAlert("Indexing", "Starting file indexing. This may take a while depending on the number of files.", "OK");
                }
                else
                {
                    // IF NOT EMPTY, CONFIRM REINDEXING
                    bool confirm = await DisplayAlert("Reindex", "The database is not empty. Do you want to reindex all files? This will erase existing data.", "Yes", "No");

                    // IF USER DECLINES, EXIT
                    if (!confirm)
                    {
                        LoadingOverlay.IsVisible = false;
                        return;
                    }
                }

                // DELAY TO ALLOW UI TO UPDATE
                await Task.Delay(50);

                // GET LIST OF ACCESSIBLE DRIVES
                var rootPaths = GetAccessibleDrives();

                // IF NO DRIVES FOUND, ALERT AND EXIT
                if (rootPaths.Count == 0)
                {
                    await DisplayAlert("No Drives", "No accessible drives found.", "OK");
                    return;
                }

                // SCAN EACH DRIVE AND COLLECT FILES
                foreach (var rootPath in rootPaths)
                {
                    // SAFE FILE SCAN TO HANDLE PERMISSION ERRORS
                    List<string> files = await SafeFileScan(rootPath);

                    // PROCESS EACH FILE AND ADD TO RESULTS, HANDLING ERRORS
                    foreach (var file in files)
                    {
                        try
                        {
                            // CREATE FileItem OBJECT AND ADD TO RESULTS LIST
                            _results.Add(new FileItem
                            {
                                FilePath = file,
                                FileName = Path.GetFileName(file),
                                FileExtension = Path.GetExtension(file),
                                FileSize = new FileInfo(file).Length,
                                LastModified = File.GetLastWriteTime(file),
                                Created = File.GetCreationTime(file)
                            });
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error processing file {file}: {ex.Message}");
                        }
                    }
                }

                // SAVE ALL FILE RECORDS TO DATABASE
                await _db.SaveManyAsync(_results);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                // HIDE Loading Overlay WHEN DONE
                LoadingOverlay.IsVisible = false;
            }
        }

        // SAFE FILE SCAN METHOD TO HANDLE PERMISSION ERRORS
        private async Task<List<string>> SafeFileScan(string rootPath)
        {
            List<string> list = [];

            try
            {
                // GET FILES IN CURRENT DIRECTORY
                foreach (var file in Directory.GetFiles(rootPath))
                {
                    // ADD EACH FILE TO LIST
                    list.Add(file);
                }

                // RECURSIVELY SCAN SUBDIRECTORIES
                foreach (var dir in Directory.GetDirectories(rootPath))
                {
                    // RECURSIVE CALL TO SCAN SUBDIRECTORY
                    var subItems = await SafeFileScan(dir);
                    // ADD SUBDIRECTORY FILES TO MAIN LIST
                    list.AddRange(subItems);
                }
            }
            catch (Exception)
            {
                // IGNORE ERRORS AND CONTINUE
            }

            return list;
        }

        private static List<string> GetAccessibleDrives()
        {
            // GET ALL DRIVES ON THE SYSTEM
            var drives = DriveInfo.GetDrives();
            var accessibleDrives = new List<string>();

            // CHECK EACH DRIVE FOR ACCESSIBILITY
            foreach (var drive in drives)
            {
                // SKIP IF DRIVE IS NOT READY AND NOT FIXED
                if (!drive.IsReady && drive.DriveType != DriveType.Fixed)
                    continue;

                try
                {
                    // TEST ACCESSIBILITY BY LISTING DIRECTORIES
                    var _ = drive.RootDirectory.GetDirectories();
                    accessibleDrives.Add(drive.RootDirectory.FullName);
                }
                catch
                {
                    // IGNORE NOT ACCESSIBLE DRIVES
                }
            }

            return accessibleDrives;
        }

        // EVENT HANDLER FOR SEARCH BUTTON CLICK
        private void OnSearchFileClicked(object sender, EventArgs e)
        {
            // START ASYNC SEARCH OPERATION
            SearchFileAsync();
        }

        // ASYNC METHOD TO PERFORM FILE SEARCH
        private async void SearchFileAsync()
        {
            try
            {
                LoadingOverlay.IsVisible = true;
                Results.Clear();
                _results.Clear();
                _currentPage = 0;

                // GET SEARCH TEXT FROM INPUT
                string searchText = SearchEntry.Text;

                // VALIDATE INPUT
                if (string.IsNullOrEmpty(searchText))
                {
                    await DisplayAlert("Input Required", "Please enter a search term.", "OK");
                    return;
                }
                ;

                searchText = searchText.ToLowerInvariant().Trim();

                // PERFORM DATABASE SEARCH
                _results = await _db.GetSearchedAsync(searchText).ConfigureAwait(false);

                // LOAD FIRST PAGE OF RESULTS
                var page = _results.Take(_pageSize).ToList();

                // UPDATE UI ON MAIN THREAD
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Results.Clear();
                    Results.AddRange(page);
                });

                _currentPage = 1;
            }
            catch (Exception ex)
            {
                // LOG ERROR AND ALERT USER
                Debug.WriteLine($"Error searching: {ex.Message}");
                await DisplayAlert("Error", "Error searching for files.", "OK");
            }
            finally
            {
                // UPDATE UI ON MAIN THREAD
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert("Search Complete", $"Found {_results.Count} items.", "OK");
                    LoadingOverlay.IsVisible = false;

                    // MAKE Load More BUTTON VISIBLE IF MORE PAGES EXIST
                    LoadMoreButton.IsVisible = true;
                });
            }
        }

        // EVENT HANDLER FOR CLICKED A FILE ITEM
        public void OnViewFileClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is FileItem file)
            {
               LoadSelectedItem(file);
            }
        }

        // OPEN FILE LOCATION IN EXPLORER
        private void LoadSelectedItem(FileItem file)
        {

            // VALIDATE FILE PATH
            if (!string.IsNullOrEmpty(file.FilePath) && File.Exists(file.FilePath))
            {
                try
                {
                    // OPEN FILE LOCATION IN EXPLORER AND SELECT FILE
                    Process.Start("explorer.exe", $"/select,\"{file.FilePath}\"");
                }
                catch (Exception ex)
                {
                    // LOG ERROR IF PROCESS FAILS
                    Debug.WriteLine($"Error opening file: {ex.Message}");
                }
            }
            else
            {
                // LOG IF FILE PATH IS INVALID OR FILE DOESN'T EXIST
                Debug.WriteLine("Invalid file path or non-existent file.");
            }

        }

        // EVENT HANDLER FOR LOAD MORE BUTTON CLICK
        public void OnLoadMoreClicked(object sender, EventArgs e)
        {
            // START ASYNC LOAD MORE ITEMS OPERATION
            _ = LoadMoreItemsAsync();

        }

        // ASYNC METHOD TO LOAD MORE ITEMS INTO THE RESULTS
        private async Task LoadMoreItemsAsync()
        {
            try
            {
                LoadingOverlay.IsVisible = true;

                // GET NEXT PAGE OF ITEMS
                var nextItems = _results
                    .Skip(_currentPage * _pageSize)
                    .Take(_pageSize)
                    .ToList();

                if (nextItems.Count > 0)
                {
                    // DELAY TO ALLOW UI TO UPDATE
                    await Task.Delay(50);

                    // UPDATE UI ON MAIN THREAD
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        Results.AddRange(nextItems);
                    });

                    // INCREMENT CURRENT PAGE
                    _currentPage++;
                }
                else
                {
                    // HIDE LOAD MORE BUTTON IF NO MORE ITEMS
                    LoadMoreButton.IsVisible = false;
                }
            }
            finally
            {
                // HIDE LOADING OVERLAY WHEN DONE
                LoadingOverlay.IsVisible = false;
            }
        }
    }
}