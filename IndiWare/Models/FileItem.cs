using SQLite;

namespace IndiWare.Models
{
    public class FileItem
    {

        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string FilePath { get; set; }

        public string FileName { get; set; }

        public string FileExtension { get; set; }
        public long FileSize { get; set; }
        public DateTime LastModified { get; set; }
        public DateTime Created { get; set; }
    

    public string FileSizeReadable
        {
            get
            {
                if (FileSize < 1024) return $"{FileSize} B";
                if (FileSize < 1024 * 1024) return $"{FileSize / 1024.0:F2} KB";
                if (FileSize < 1024 * 1024 * 1024) return $"{FileSize / 1024.0 / 1024.0:F2} MB";
                return $"{FileSize / 1024.0 / 1024.0 / 1024.0:F2} GB";
            }
        }
    }
}