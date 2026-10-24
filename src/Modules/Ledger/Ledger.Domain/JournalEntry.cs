namespace LedgerFlow.Modules.Ledger.Domain;

/// <summary>An immutable balanced entry; corrections use a separate reversing entry.</summary>
public sealed class JournalEntry
{
    public Guid Id { get; }
    public Guid OperationId { get; }
    public Guid? ReversesEntryId { get; }
    public IReadOnlyList<Posting> Postings { get; }

    private JournalEntry(Guid operationId, Posting[] postings, Guid? reversesEntryId)
    {
        Id = Guid.NewGuid();
        OperationId = operationId;
        Postings = Array.AsReadOnly(postings);
        ReversesEntryId = reversesEntryId;
    }

    public static JournalEntry Create(Guid operationId, IEnumerable<Posting> postings)
        => CreateValidated(operationId, postings, null);

    private static JournalEntry CreateValidated(Guid operationId, IEnumerable<Posting> postings, Guid? reversesEntryId)
    {
        if (operationId == Guid.Empty) throw new ArgumentException("Operation is required.", nameof(operationId));
        ArgumentNullException.ThrowIfNull(postings);
        var snapshot = postings.ToArray();
        if (snapshot.Length < 2 || snapshot.Any(p => p is null))
            throw new ArgumentException("At least two non-null postings are required.", nameof(postings));
        if (snapshot.Select(p => p.AccountId).Distinct().Count() < 2)
            throw new ArgumentException("At least two distinct accounts are required.", nameof(postings));
        if (snapshot.Select(p => p.Amount.Currency).Distinct().Count() != 1)
            throw new ArgumentException("Postings must use the same currency.", nameof(postings));

        // Decimal safely accumulates Int64 cents without overflowing at Int64.MaxValue.
        var debits = snapshot.Where(p => p.Side == PostingSide.Debit).Sum(p => (decimal)p.Amount.MinorUnits);
        var credits = snapshot.Where(p => p.Side == PostingSide.Credit).Sum(p => (decimal)p.Amount.MinorUnits);
        if (debits != credits)
            throw new ArgumentException("Debits and credits must balance.", nameof(postings));
        return new JournalEntry(operationId, snapshot, reversesEntryId);
    }

    public JournalEntry Reverse(Guid reversalOperationId)
        => CreateValidated(reversalOperationId, Postings.Select(p => new Posting(p.AccountId,
            p.Side == PostingSide.Debit ? PostingSide.Credit : PostingSide.Debit, p.Amount)), Id);
}
