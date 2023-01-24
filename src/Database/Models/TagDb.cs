namespace Miniblog.Core.Database.Models
{
    public class TagDb
    {
        public int ID { get; set; }
        public string? Name { get; set; }

        public virtual PostDb Post { get; set; }
    }
}
