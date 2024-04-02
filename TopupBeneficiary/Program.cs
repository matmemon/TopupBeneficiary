using System.Text;
using TopupBeneficiary.Models;

/// <summary>
/// Topup Service to perform all relevant operations
/// </summary>
public class TopUpService
{
    private const string BalanceApiUrl = "https://localhost:7033/balance"; // Update with your API URL
    private const decimal MaxMonthlyTopUpUnverified = 1000m;
    private const decimal MaxMonthlyTopUpVerified = 500m;
    private const decimal MaxTotalMonthlyTopUp = 3000m;
    private const decimal TopUpTransactionCharge = 1m;

    private decimal _userBalance;
    private HttpClient _httpClient;

    public decimal UserBalance
    {
        get => _userBalance;
        set => _userBalance = value >= 0 ? value : throw new ArgumentOutOfRangeException("Balance cannot be negative.");
    }

    public List<TopUpBeneficiary> ActiveBeneficiaries { get; set; }
    public List<decimal> AvailableTopUpOptions { get; } = new List<decimal> { 5m, 10m, 20m, 30m, 50m, 75m, 100m };

    public TopUpService()
    {
        _userBalance = 0;
        ActiveBeneficiaries = new List<TopUpBeneficiary>();
        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri(BalanceApiUrl);
    }
    /// <summary>
    /// Credit Balance API Call
    /// </summary>
    /// <param name="amount"></param>
    /// <returns></returns>
    public async Task<decimal?> CreditBalanceAsync(decimal amount)
    {
        try
        {
            HttpResponseMessage response = await _httpClient.PostAsync("credit", new StringContent(amount.ToString(), Encoding.UTF8, "application/json"));

            if (response.IsSuccessStatusCode)
            {
                string responseBody = await response.Content.ReadAsStringAsync();
                if (decimal.TryParse(responseBody, out decimal updatedBalance))
                {
                    return updatedBalance;
                }
                else
                {
                    Console.WriteLine($"Error: Failed to parse updated balance from the server response: {responseBody}");
                    return null;
                }
            }
            else
            {
                Console.WriteLine($"Error: Failed to debit user balance. Status code: {response.StatusCode}");
                return null;
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Error: Failed to connect to the server. {ex.Message}");
            return null;
        }
    }
    /// <summary>
    /// Debit Balance API Call
    /// </summary>
    /// <param name="amount"></param>
    /// <returns></returns>
    public async Task<decimal?> DebitBalanceAsync(decimal amount)
    {
        try
        {
            HttpResponseMessage response = await _httpClient.PostAsync("debit", new StringContent(amount.ToString(), Encoding.UTF8, "application/json"));

            if (response.IsSuccessStatusCode)
            {
                string responseBody = await response.Content.ReadAsStringAsync();
                if (decimal.TryParse(responseBody, out decimal updatedBalance))
                {
                    return updatedBalance;
                }
                else
                {
                    Console.WriteLine($"Error: Failed to parse updated balance from the server response: {responseBody}");
                    return null;
                }
            }
            else
            {
                Console.WriteLine($"Error: Failed to debit user balance. Status code: {response.StatusCode}");
                return null;
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Error: Failed to connect to the server. {ex.Message}");
            return null;
        }
    }
    /// <summary>
    /// Get User Balance API Call
    /// </summary>
    /// <returns></returns>
    public async Task<decimal?> GetUserBalanceAsync()
    {
        try
        {
            // Call the API to fetch the balance
            using (HttpClient client = new HttpClient())
            {
                HttpResponseMessage response = await client.GetAsync(BalanceApiUrl);

                if (response.IsSuccessStatusCode)
                {
                    string balanceString = await response.Content.ReadAsStringAsync();
                    if (decimal.TryParse(balanceString, out decimal balance))
                    {
                        UserBalance = balance;
                        return balance;
                    }
                    else
                    {
                        Console.WriteLine("Error: Unable to parse balance from the server response.");
                    }
                }
                else
                {
                    Console.WriteLine($"Error: Failed to fetch user balance from the server. Status code: {response.StatusCode}");
                }
            }
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"Error: Failed to connect to the server. {ex.Message}");
        }

        return 0;
    }
    /// <summary>
    /// Check If User Can Topup
    /// </summary>
    /// <param name="amount"></param>
    /// <param name="isUserVerified"></param>
    /// <returns></returns>
    public bool CanTopUp(decimal amount, bool isUserVerified)
    {
        // Calculate the maximum monthly top-up amount per beneficiary based on user verification status
        decimal maxMonthlyTopUpPerBeneficiary = isUserVerified ? MaxMonthlyTopUpVerified : MaxMonthlyTopUpUnverified;

        // Calculate the total top-up amount for all beneficiaries for the current month
        decimal totalTopUpForAllBeneficiaries = ActiveBeneficiaries
            .Where(b => b.Transactions != null)
            .Sum(b => b.Transactions?.Where(t => t.DateTime.Year == DateTime.Now.Year && t.DateTime.Month == DateTime.Now.Month).Sum(t => t.Amount) ?? 0);

        // Calculate the remaining available monthly top-up amount for all beneficiaries
        decimal remainingMonthlyTopUpAmount = MaxTotalMonthlyTopUp - totalTopUpForAllBeneficiaries;

        // Calculate total top-up amount for the current month including the new top-up
        DateTime startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        DateTime endDate = startDate.AddMonths(1).AddDays(-1);
        decimal totalTopUpThisMonth = ActiveBeneficiaries
            .Where(b => b.IsUserVerified == isUserVerified)
            .SelectMany(b => b.Transactions ?? Enumerable.Empty<TopUpTransaction>())
            .Where(t => t.DateTime >= startDate && t.DateTime <= endDate)
            .Sum(t => t.Amount);
        decimal totalTopUpIncludingNewTopUp = totalTopUpThisMonth + amount;

        // Check if the total top-up amount for all beneficiaries exceeds the maximum total allowed
        if (totalTopUpIncludingNewTopUp > MaxTotalMonthlyTopUp)
        {
            Console.WriteLine($"Error: Total monthly top-up limit reached for all beneficiaries ({MaxTotalMonthlyTopUp} AED).");
            return false;
        }

        // Check if the total top-up amount for the beneficiary exceeds the maximum allowed
        if (totalTopUpIncludingNewTopUp > maxMonthlyTopUpPerBeneficiary)
        {
            Console.WriteLine($"Error: User Status {isUserVerified}.\n " +
                $"Total Topup {totalTopUpIncludingNewTopUp} =   totalTopUpThisMonth {totalTopUpThisMonth} + amount {amount}\n" +
                $"Maximum monthly top-up limit per beneficiary ({maxMonthlyTopUpPerBeneficiary} AED) exceeded.\n");
            return false;
        }

        // Check if the user has sufficient balance to perform the top-up
        if (amount > UserBalance)
        {
            Console.WriteLine("Error: Insufficient balance to perform top-up.");
            return false;
        }

        return true;
    }
    /// <summary>
    /// Topup Method
    /// </summary>
    /// <param name="beneficiaryNickname"></param>
    /// <param name="amount"></param>
    /// <param name="isUserVerified"></param>
    /// <returns></returns>
    public bool TopUp(string beneficiaryNickname, decimal amount, bool isUserVerified)
    {
        decimal? userBalance = GetUserBalanceAsync().GetAwaiter().GetResult();
        Console.WriteLine($"Current User Balance is {userBalance}");
        if (!userBalance.HasValue)
        {
            Console.WriteLine("Error: Failed to fetch user balance.");
            return false;
        }

        if (amount + TopUpTransactionCharge > UserBalance)
        {
            Console.WriteLine("Error: Insufficient balance to perform top-up.");
            return false;
        }

        TopUpBeneficiary? beneficiary = ActiveBeneficiaries.FirstOrDefault(b => b.Nickname == beneficiaryNickname);

        if (beneficiary == null)
        {
            Console.WriteLine($"Error: Beneficiary '{beneficiaryNickname}' not found.");
            return false;
        }

        if (!CanTopUp(amount, isUserVerified))
        {
            Console.WriteLine("Error: Cannot perform top-up due to monthly limits.");
            return false;
        }

        // Debit the balance first
        //UserBalance -= amount;
        UserBalance -= Convert.ToDecimal(DebitBalanceAsync(amount).GetAwaiter().GetResult());
        var debiteduserBalance = GetUserBalanceAsync().GetAwaiter().GetResult();
        Console.WriteLine($"user Balance after amount {amount} debited is  {debiteduserBalance}");

        // Apply charge
        //UserBalance -= TopUpTransactionCharge;
        UserBalance -= Convert.ToDecimal(DebitBalanceAsync(TopUpTransactionCharge).GetAwaiter().GetResult());
        var transactionuserBalance = GetUserBalanceAsync().GetAwaiter().GetResult();
        Console.WriteLine($"user Balance after TopUpTransactionCharge {TopUpTransactionCharge} debited is  {transactionuserBalance}");

        // Create a new top-up transaction
        var newTransaction = new TopUpTransaction
        {
            BeneficiaryId = beneficiary.Id,
            Amount = amount,
            DateTime = DateTime.Now
        };

        // Add the transaction to the beneficiary's collection
        beneficiary.Transactions ??= new List<TopUpTransaction>();
        beneficiary.Transactions.Add(newTransaction);
        beneficiary.TotalBalanceAdded += amount;

        Console.WriteLine($"TopUp Completed.Successfully topped up {amount} to {beneficiaryNickname} with TotalbalancedAdded {beneficiary.TotalBalanceAdded}.");
        return true;
    }
    /// <summary>
    /// Store Topup Beneficiary Details
    /// </summary>
    /// <param name="beneficiary"></param>
    /// <returns></returns>
    public bool StoreTopUpBeneficiary(TopUpBeneficiary beneficiary)
    {
        if (string.IsNullOrEmpty(beneficiary.Nickname) || beneficiary.Nickname.Length > 20)
        {
            Console.WriteLine("Error: Nickname must be between 1 and 20 characters in length.");
            return false;
        }

        if (ActiveBeneficiaries.Count >= 5)
        {
            Console.WriteLine("Error: Maximum limit of 5 beneficiaries reached. Cannot add more.");
            return false;
        }
        beneficiary.Transactions = new List<TopUpTransaction>();

        ActiveBeneficiaries.Add(beneficiary);
        Console.WriteLine($"Beneficiary '{beneficiary.Nickname}' added successfully in StoreTopUpBeneficiary.");
        return true;
    }
    /// <summary>
    /// View Top Up beneficiary
    /// </summary>
    public void ViewTopUpBeneficiaries()
    {

        Console.WriteLine("Available Top-Up Beneficiaries:");
        foreach (var beneficiary in ActiveBeneficiaries)
        {
            Console.WriteLine($"Beneficiary ID: {beneficiary.Id}, Nickname: {beneficiary.Nickname} , IsVerifiedUser : {beneficiary.IsUserVerified}");
        }
    }
    /// <summary>
    /// View Available Topup options
    /// </summary>
    public void ViewAvailableTopUpOptions()
    {
        Console.WriteLine("Available Top-Up Options:");
        foreach (var option in AvailableTopUpOptions)
        {
            Console.WriteLine($"AED {option}");
        }
    }
}


/// <summary>
/// Main Program
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {

        Console.WriteLine("------------InitializeTopUpService--------");
        var topUpService = InitializeTopUpService();

        Console.WriteLine("------------ViewAvailableTopUpOptions--------");
        topUpService.ViewAvailableTopUpOptions();

        Console.WriteLine("------------AddBeneficiariesValidation--------");
        AddBeneficiariesValidation(topUpService);

        Console.WriteLine("------------ViewBeneficiaries--------");
        ViewBeneficiaries(topUpService);
        Console.WriteLine("------------TryTopUpExistingBeneficiary--------");
        TryTopUpExistingBeneficiary(topUpService);

        Console.WriteLine("----Additional Methods-------");
        Console.WriteLine("------------DisplayUserBalance1--------");
        await DisplayUserBalance(topUpService);
        Console.WriteLine("------------PerformCreditTransaction--------");
        await PerformCreditTransaction(topUpService);
        Console.WriteLine("------------DisplayUserBalance2--------");
        await DisplayUserBalance(topUpService);
        Console.WriteLine("------------PerformDebitTransaction--------");
        await PerformDebitTransaction(topUpService);
        Console.WriteLine("------------DisplayUserBalance3--------");
        await DisplayUserBalance(topUpService);
        Console.WriteLine("------------End of Program--------");
        Console.ReadLine();

    }
    /// <summary>
    /// Initialize TopUp Service and add beneficiaries
    /// </summary>
    /// <returns></returns>
    static TopUpService InitializeTopUpService()
    {
        var topUpService = new TopUpService();

        // Add 5 beneficiaries with valid nicknames
        for (int i = 1; i <= 5; i++)
        {
            var beneficiaryToAdd = new TopUpBeneficiary();

            //Adding isUserVerified to False when i = 4
            if (i == 4)
            {
                beneficiaryToAdd.Id = i;
                beneficiaryToAdd.Nickname = $"User{i}";
                beneficiaryToAdd.IsUserVerified = false;
            }
            else
            {
                beneficiaryToAdd.Id = i;
                beneficiaryToAdd.Nickname = $"User{i}";
                beneficiaryToAdd.IsUserVerified = true;
            }

            topUpService.StoreTopUpBeneficiary(beneficiaryToAdd);
        }

        return topUpService;
    }
    /// <summary>
    /// Check Validation of Beneficiaries
    /// </summary>
    /// <param name="topUpService"></param>
    static void AddBeneficiariesValidation(TopUpService topUpService)
    {
        // Try to add another beneficiary (should fail)
        var extraBeneficiary = new TopUpBeneficiary { Id = 6, Nickname = "ExtraUser", IsUserVerified = false };
        topUpService.StoreTopUpBeneficiary(extraBeneficiary);

        // Try to add a beneficiary with a nickname exceeding 20 characters (should fail)
        var longNicknameBeneficiary = new TopUpBeneficiary { Id = 7, Nickname = "LongNicknameOver20Characters", IsUserVerified = false };
        topUpService.StoreTopUpBeneficiary(longNicknameBeneficiary);
    }
    /// <summary>
    /// Try Updaing beneficiaries with verified and unverified status
    /// </summary>
    /// <param name="topUpService"></param>
    static void TryTopUpExistingBeneficiary(TopUpService topUpService)
    {
        //Checking for User1 - verfiedUser
        Console.WriteLine("---------------------------user1---------------------------------------");
        CheckVerifiedUser(topUpService);
        //Check for user4 - IsUserVerifiedFalse 
        Console.WriteLine("----------------------------user4--------------------------------------");
        CheckNotVerifiedUser(topUpService);

    }
    /// <summary>
    /// Check not verified status user
    /// </summary>
    /// <param name="topUpService"></param>
    private static void CheckNotVerifiedUser(TopUpService topUpService)
    {
        string beneficiaryNickname = "User4";
        decimal topUpAmount = 0m;
        var existingBeneficiary = GetExistingBeneficiary(topUpService, beneficiaryNickname);
        if (existingBeneficiary != null)
        {
            //Check When Balance is null
            for (int i = 1; i <= 50; i++)
            {
                Console.WriteLine($"--------User Transaction History- {beneficiaryNickname}------------");
                var random = new Random();
                topUpAmount = topUpService.AvailableTopUpOptions[random.Next(topUpService.AvailableTopUpOptions.Count)];
                //topUpAmount = 100;
                Console.WriteLine($"Selected top-up amount: {topUpAmount}");

                if (topUpService.TopUp(beneficiaryNickname, topUpAmount, existingBeneficiary.IsUserVerified))
                {
                    Console.WriteLine($"Successfully topped up {topUpAmount} to {beneficiaryNickname} (Attempt {i}).");
                }
                else
                {
                    Console.WriteLine($"Failed to top up {topUpAmount} to {beneficiaryNickname} (Attempt {i}).");
                }
            }
        }
        else
        {
            Console.WriteLine($"Beneficiary '{beneficiaryNickname}' not found.");
        }

        TryPerformTopUp(topUpService, existingBeneficiary, topUpAmount);
    }
    /// <summary>
    /// Check verified status of user
    /// </summary>
    /// <param name="topUpService"></param>
    private static void CheckVerifiedUser(TopUpService topUpService)
    {
        string beneficiaryNickname = "User1";
        decimal topUpAmount = 0m;
        var existingBeneficiary = GetExistingBeneficiary(topUpService, beneficiaryNickname);
        if (existingBeneficiary != null)
        {
            //Check When Balance is null
            for (int i = 1; i <= 50; i++)
            {
                Console.WriteLine($"--------User Transaction History- {beneficiaryNickname}------------");
                var random = new Random();
                topUpAmount = topUpService.AvailableTopUpOptions[random.Next(topUpService.AvailableTopUpOptions.Count)];
                //topUpAmount = 100;
                Console.WriteLine($"Selected top-up amount: {topUpAmount}");

                if (topUpService.TopUp(beneficiaryNickname, topUpAmount, existingBeneficiary.IsUserVerified))
                {
                    Console.WriteLine($"Successfully topped up {topUpAmount} to {beneficiaryNickname} (Attempt {i}).");
                }
                else
                {
                    Console.WriteLine($"Failed to top up {topUpAmount} to {beneficiaryNickname} (Attempt {i}).");
                }
            }
        }
        else
        {
            Console.WriteLine($"Beneficiary '{beneficiaryNickname}' not found.");
        }

        TryPerformTopUp(topUpService, existingBeneficiary, topUpAmount);
    }
    /// <summary>
    /// Get Existing Beneficiary
    /// </summary>
    /// <param name="topUpService"></param>
    /// <param name="beneficiaryNickname"></param>
    /// <returns></returns>
    static TopUpBeneficiary GetExistingBeneficiary(TopUpService topUpService, string beneficiaryNickname)
    {
        return topUpService.ActiveBeneficiaries.FirstOrDefault(b => b.Nickname == beneficiaryNickname)!;
    }
    /// <summary>
    /// TRy TopUp Operations
    /// </summary>
    /// <param name="topUpService"></param>
    /// <param name="beneficiary"></param>
    /// <param name="amount"></param>
    static void TryPerformTopUp(TopUpService topUpService, TopUpBeneficiary beneficiary, decimal amount)
    {
        if (topUpService.TopUp(beneficiary.Nickname, amount, beneficiary.IsUserVerified))
        {
            Console.WriteLine($"Successfully topped up {amount} to {beneficiary.Nickname}.");
        }
        else
        {
            Console.WriteLine($"Failed to top up {amount} to {beneficiary.Nickname}.");
        }
    }
    /// <summary>
    /// View Beneficiaries
    /// </summary>
    /// <param name="topUpService"></param>
    static void ViewBeneficiaries(TopUpService topUpService)
    {
        topUpService.ViewTopUpBeneficiaries();
    }
    /// <summary>
    /// Display User Balance
    /// </summary>
    /// <param name="topUpService"></param>
    /// <returns></returns>
    static async Task DisplayUserBalance(TopUpService topUpService)
    {
        decimal? balance = await topUpService.GetUserBalanceAsync();
        if (balance.HasValue)
        {
            Console.WriteLine($"User balance: {balance}");
        }
        else
        {
            Console.WriteLine("Failed to fetch user balance.");
        }
    }
    /// <summary>
    /// Perform Credit Operation
    /// </summary>
    /// <param name="topUpService"></param>
    /// <returns></returns>
    static async Task PerformCreditTransaction(TopUpService topUpService)
    {
        decimal creditAmount = 100.0m;
        decimal? balanceAmt = await topUpService.CreditBalanceAsync(creditAmount);
        if (balanceAmt > 0)
        {
            Console.WriteLine($"Credited {creditAmount} to user balance successfully Current balance is {balanceAmt}.");
        }
        else
        {
            Console.WriteLine("Failed to credit user balance.");
        }
    }
    /// <summary>
    /// Perform Debit operation
    /// </summary>
    /// <param name="topUpService"></param>
    /// <returns></returns>
    static async Task PerformDebitTransaction(TopUpService topUpService)
    {
        decimal debitAmount = 50.0m;
        decimal? balanceAmt = await topUpService.DebitBalanceAsync(debitAmount);
        if (balanceAmt > 0)
        {
            Console.WriteLine($"Debited {debitAmount} from user balance successfully Current balance is {balanceAmt}.");
        }
        else
        {
            Console.WriteLine("Failed to debit user balance.");
        }
    }
}