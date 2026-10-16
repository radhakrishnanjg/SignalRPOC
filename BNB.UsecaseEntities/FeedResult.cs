namespace BNB.UsecaseEntities
{
    /// <summary>
    /// Counts returned after a batch is saved: total, valid, invalid and persisted records.
    /// </summary>
    public class FeedResult
    {
        public int TotalRecords { get; set; }
        public int ValidRecords { get; set; }
        public int InvalidRecords { get; set; }
        public int Persisted { get; set; }
    }
}
