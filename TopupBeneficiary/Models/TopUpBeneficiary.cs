namespace TopupBeneficiary.Models
{
    public class TopUpBeneficiary
    {
        public int Id { get; set; }
        public string Nickname { get; set; } = string.Empty;
        public bool IsUserVerified { get; set; }
        public ICollection<TopUpTransaction>? Transactions { get; set; }

        public decimal TotalBalanceAdded { get; set; }
    }
}
