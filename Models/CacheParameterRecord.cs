namespace Contrib.Cache.Models
{
    public class CacheParameterRecord
    {
        public virtual int Id { get; set; }
        public virtual string RouteKey { get; set; }
        public virtual int Duration { get; set; }
        public virtual int MaxAge { get; set; }
    }

}