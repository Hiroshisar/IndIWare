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
    }
}
