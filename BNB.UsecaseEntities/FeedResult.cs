namespace BNB.UsecaseEntities
{
    public class FeedResult
    {
        public int TotalRecords { get; set; }
        public int ValidRecords { get; set; }
        public int InvalidRecords { get; set; }
        public int Persisted { get; set; }
    }
}
