using DAL;

namespace CheckChallengeTime
{
    public class ResultEntryDetails
    {
        public Test Test { get; set; }
        public long FinalResultId { get; set; }
        public double? FinalResultValue { get; set; }
    }
}