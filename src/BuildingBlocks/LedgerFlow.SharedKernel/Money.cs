namespace LedgerFlow.SharedKernel;

/// <summary>A signed USD value represented in cents. Transfer amounts require additional validation.</summary>
public sealed record Money
{
    public long MinorUnits { get; }
    public string Currency { get; }
    public decimal Amount => MinorUnits / 100m;

    private Money(long minorUnits, string currency)
    {
        MinorUnits = minorUnits;
        Currency = currency;
    }

    public static Money FromMinorUnits(long minorUnits, string currency = "USD")
    {
        if (currency != "USD")
            throw new ArgumentException("Only USD is supported.", nameof(currency));

        return new Money(minorUnits, currency);
    }

    /// <summary>Converts major units to cents without rounding. Throws on fractional cents or overflow.</summary>
    public static Money FromDecimal(decimal amount, string currency = "USD")
    {
        var cents = amount * 100m;
        if (cents != decimal.Truncate(cents))
            throw new ArgumentException("Amounts must not contain fractional cents.", nameof(amount));

        return FromMinorUnits(checked((long)cents), currency);
    }

    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return FromMinorUnits(checked(left.MinorUnits + right.MinorUnits), left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return FromMinorUnits(checked(left.MinorUnits - right.MinorUnits), left.Currency);
    }

    private static void EnsureSameCurrency(Money left, Money right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        if (left.Currency != right.Currency)
            throw new ArgumentException("Arithmetic requires matching currencies.");
    }
}
