using IndiWare.Models;
using IndiWare.Service;
using System.Diagnostics;

namespace IndiWare
{
    public partial class MainPage : ContentPage
    {
        public List<FileItem> _results { get; set; } = new();
        BulkObservableCollection<FileItem> Results = new();
        private readonly DatabaseService _db;
        private int _currentPage = 0;
        private const int _pageSize = 100;
        private bool _isLoadingMore = false;

        public MainPage(DatabaseService db)
        {
            InitializeComponent();
            _db = db;
            ResultsView.ItemsSource = Results;
        }

        private async void OnIndexFilesClicked(object sender, EventArgs e)
        {
            LoadingOverlay.IsVisible = true;

            _results.Clear();
            Results.Clear();

            try
            {
                if (await _db.IsDatabaseEmptyAsync())
                {
                    await DisplayAlert("Indexing", "Starting file indexing. This may take a while depending on the number of files.", "OK");
                }
                else
                {
                    bool confirm = await DisplayAlert("Reindex", "The database is not empty. Do you want to reindex all files? This will erase existing data.", "Yes", "No");
                    if (!confirm)
                    {
                        LoadingOverlay.IsVisible = false;
                        return;
                    }
                }

                await Task.Delay(50);

                var rootPaths = GetAccessibleDrives();

                if (rootPaths.Count == 0)
                {
                    await DisplayAlert("No Drives", "No accessible drives found.", "OK");
                    return;
                }

                foreach (var rootPath in rootPaths)
                {
                    List<string> files = await SafeFileScan(rootPath);

                    foreach (var file in files)
                    {
                        try
                        {
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
                await _db.SaveManyAsync(_results);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                LoadingOverlay.IsVisible = false;
            }
        }



        public async Task<List<string>> SafeFileScan(string rootPath)
        {
            List<string> list = [];

            try
            {
                foreach (var file in Directory.GetFiles(rootPath))
                {
                    list.Add(file);
                }

                foreach (var dir in Directory.GetDirectories(rootPath))
                {
                    var subItems = await SafeFileScan(dir);
                    list.AddRange(subItems);
                }
            }
            catch (Exception)
            {

            }


            return list;
        }

        public static List<string> GetAccessibleDrives()
        {
            var drives = DriveInfo.GetDrives();
            var accessibleDrives = new List<string>();

            foreach (var drive in drives)
            {
                if (!drive.IsReady && drive.DriveType != DriveType.Fixed)
                    continue; // Evita CD vuoti, dischi non pronti o periferiche esterne

                try
                {
                    // Testa l’accesso al root del drive
                    var _ = drive.RootDirectory.GetDirectories();
                    accessibleDrives.Add(drive.RootDirectory.FullName);
                }
                catch
                {
                    // Ignora drive non accessibili
                }
            }

            return accessibleDrives;
        }

        private void OnSearchFileClicked(object sender, EventArgs e)
        {
            _ = SearchFileAsync();
        }

        private async Task SearchFileAsync()
        {
            try
            {
                LoadingOverlay.IsVisible = true;
                Results.Clear();
                _results.Clear();
                _currentPage = 0;

                string searchText = SearchEntry.Text;

                if (string.IsNullOrEmpty(searchText))
                {
                    await DisplayAlert("Input Required", "Please enter a search term.", "OK");
                    return;
                }
                ;

                searchText = searchText.ToLowerInvariant().Trim();

                _results = await _db.GetSearchedAsync(searchText).ConfigureAwait(false);

                var page = _results.Take(_pageSize).ToList();

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Results.Clear();
                    Results.AddRange(page);
                });

                _currentPage = 1;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Errore durante la ricerca: {ex.Message}");
                await DisplayAlert("Errore", "Errore durante la ricerca dei file.", "OK");
            }
            finally
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert("Search Complete", $"Found {_results.Count} items.", "OK");
                    LoadingOverlay.IsVisible = false;

                    // Making Load More button visible if there are more items to load
                    LoadMoreButton.IsVisible = true;
                });
            }
        }


        private void OnViewFileTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is FileItem file)
            {
                if (!string.IsNullOrEmpty(file.FilePath) && File.Exists(file.FilePath))
                {
                    try
                    {
                        Process.Start("explorer.exe", $"/select,\"{file.FilePath}\"");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Errore nell'apertura del file: {ex.Message}");
                    }
                }
                else
                {
                    Debug.WriteLine("Percorso file non valido o file inesistente.");
                }
            }
        }

        public void OnLoadMoreClicked(object sender, EventArgs e)
        {
            _ = LoadMoreItemsAsync();

        }

        private async Task LoadMoreItemsAsync()
        {
            try
            {
                if (_isLoadingMore) return;

                _isLoadingMore = true;
                LoadingOverlay.IsVisible = true;

                var nextItems = _results
                    .Skip(_currentPage * _pageSize)
                    .Take(_pageSize)
                    .ToList();

                if (nextItems.Count > 0)
                {
                    // break for responsive UI
                    await Task.Delay(50);

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        Results.AddRange(nextItems);
                    });

                    _currentPage++;
                }
                else
                {
                    // Hiding Load More button if no more items
                    LoadMoreButton.IsVisible = false;
                }
            }
            finally
            {
                LoadingOverlay.IsVisible = false;
                _isLoadingMore = false;
            }
        }
    }
}