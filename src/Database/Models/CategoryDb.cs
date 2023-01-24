namespace Miniblog.Core.Database.Models
{
    public class CategoryDb
    {
        public int ID { get; set; }
        public string? Name { get; set; }

        public virtual PostDb Post { get; set; }
    }
}
