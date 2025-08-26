public class User {
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string? Email { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Wallet? Wallet { get; set; }
    public ICollection<Bet> Bets { get; set; } = new List<Bet>();
}

public class Wallet {
    public long Id { get; set; }
    public long UserId { get; set; }
    public long BalanceCents { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public User? User { get; set; }
    public ICollection<WalletTransaction> Transactions { get; set; } = new List<WalletTransaction>();
}

public class WalletTransaction {
    public long Id { get; set; }
    public long WalletId { get; set; }
    public string Type { get; set; } = "RECHARGE"; // RECHARGE | DEBIT | ADJUST
    public long AmountCents { get; set; }
    public string? MetaJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Wallet? Wallet { get; set; }
}

public class Draw {
    public long Id { get; set; }
    public string Market { get; set; } = "PTM";
    public DateOnly DrawDate { get; set; }
    public TimeOnly DrawTime { get; set; }
    public List<string> Prizes { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<BetResult> Results { get; set; } = new List<BetResult>();
}

public class Bet {
    public long Id { get; set; }
    public long UserId { get; set; }
    public string Modality { get; set; } = "";
    public string PositionsCsv { get; set; } = "";
    public string PrizeWindow { get; set; } = "1_5";
    public long StakeCents { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "PENDING";
    public long? EvaluationId { get; set; }

    public User? User { get; set; }
    public ICollection<BetResult> Results { get; set; } = new List<BetResult>();
}

public class BetResult {
    public long Id { get; set; }
    public long BetId { get; set; }
    public long DrawId { get; set; }
    public bool Matched { get; set; }
    public string? DetailsJson { get; set; }
    public long GrossPayoutCents { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Bet? Bet { get; set; }
    public Draw? Draw { get; set; }
}

public class PayoutTable {
    public long Id { get; set; }
    public string Modality { get; set; } = "";
    public string Key { get; set; } = "BASE";
    public int MultiplierBp { get; set; } // 10000 = 100x
}
