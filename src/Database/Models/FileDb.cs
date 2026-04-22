namespace Miniblog.Core.Database.Models
{
    using System;

    public class FileDb
    {
        public Guid ID { get; set; }
        public string FileName { get; set; } = null!;
        public byte[] Content { get; set; } = null!;
    }
}
