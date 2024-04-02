namespace TopupBeneficiary.Models
{
    public class TopUpTransaction
    {
        public int Id { get; set; }
        public int BeneficiaryId { get; set; }
        public int TopUpDataId { get; set; }
        public decimal Amount { get; set; }
        public DateTime DateTime { get; set; }
    }
}
