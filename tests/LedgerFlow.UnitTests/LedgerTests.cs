using LedgerFlow.Modules.Ledger.Domain;
using LedgerFlow.SharedKernel;

namespace LedgerFlow.UnitTests;

public class LedgerTests
{
    private static Posting Line(Guid account, PostingSide side, long cents)
        => new(account, side, Money.FromMinorUnits(cents));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Posting_amount_must_be_positive(long cents)
        => Assert.Throws<ArgumentException>(() => Line(Guid.NewGuid(), PostingSide.Debit, cents));

    [Fact]
    public void Posting_requires_account_and_valid_side()
    {
        Assert.Throws<ArgumentException>(() => Line(Guid.Empty, PostingSide.Debit, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Line(Guid.NewGuid(), (PostingSide)99, 1));
    }

    [Fact]
    public void Journal_rejects_unbalanced_or_incomplete_entries()
    {
        var source = Guid.NewGuid();
        var destination = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => JournalEntry.Create(Guid.NewGuid(), []));
        Assert.Throws<ArgumentException>(() => JournalEntry.Create(Guid.NewGuid(), [Line(source, PostingSide.Debit, 100)]));
        Assert.Throws<ArgumentException>(() => JournalEntry.Create(Guid.NewGuid(),
            [Line(source, PostingSide.Debit, 100), Line(destination, PostingSide.Credit, 90)]));
        Assert.Throws<ArgumentException>(() => JournalEntry.Create(Guid.NewGuid(),
            [Line(source, PostingSide.Debit, 100), Line(source, PostingSide.Credit, 100)]));
        Assert.Throws<ArgumentException>(() => JournalEntry.Create(Guid.Empty,
            [Line(source, PostingSide.Debit, 100), Line(destination, PostingSide.Credit, 100)]));
    }

    [Fact]
    public void Journal_snapshots_inputs_and_reversal_preserves_original()
    {
        var lines = new[] { Line(Guid.NewGuid(), PostingSide.Debit, 100), Line(Guid.NewGuid(), PostingSide.Credit, 100) };
        var original = JournalEntry.Create(Guid.NewGuid(), lines);
        lines[0] = Line(Guid.NewGuid(), PostingSide.Debit, 999);
        Assert.Equal(100, original.Postings[0].Amount.MinorUnits);
        Assert.Throws<NotSupportedException>(() => ((IList<Posting>)original.Postings).Clear());

        var reversalOperation = Guid.NewGuid();
        var reversal = original.Reverse(reversalOperation);
        Assert.NotEqual(original.Id, reversal.Id);
        Assert.Equal(original.Id, reversal.ReversesEntryId);
        Assert.Equal(reversalOperation, reversal.OperationId);
        Assert.Equal(PostingSide.Debit, original.Postings[0].Side);
        Assert.Equal(PostingSide.Credit, reversal.Postings[0].Side);
        Assert.Equal(PostingSide.Debit, reversal.Postings[1].Side);
        Assert.Equal(original.Postings[0].AccountId, reversal.Postings[0].AccountId);
        Assert.Equal(original.Postings[0].Amount, reversal.Postings[0].Amount);
    }

    [Fact]
    public void Funding_and_transfer_conserve_value_across_all_accounts()
    {
        var clearing = LedgerAccount.CreateClearing();
        var source = LedgerAccount.ForWallet(Guid.NewGuid());
        var destination = LedgerAccount.ForWallet(Guid.NewGuid());
        var accounts = new[] { clearing, source, destination }.ToDictionary(a => a.Id);
        var funding = JournalEntry.Create(Guid.NewGuid(),
            [Line(clearing.Id, PostingSide.Debit, 10000), Line(source.Id, PostingSide.Credit, 10000)]);
        foreach (var posting in funding.Postings) accounts[posting.AccountId].Apply(posting);

        var transfer = JournalEntry.Create(Guid.NewGuid(),
            [Line(source.Id, PostingSide.Debit, 3000), Line(destination.Id, PostingSide.Credit, 3000)]);
        foreach (var posting in transfer.Postings) accounts[posting.AccountId].Apply(posting);

        Assert.Equal(-10000, clearing.Balance.MinorUnits);
        Assert.Equal(7000, source.Balance.MinorUnits);
        Assert.Equal(3000, destination.Balance.MinorUnits);
        Assert.Equal(0, accounts.Values.Sum(a => a.Balance.MinorUnits));
    }

    [Fact]
    public void Insufficient_funds_leave_the_account_unchanged()
    {
        var account = LedgerAccount.ForWallet(Guid.NewGuid());
        account.Apply(Line(account.Id, PostingSide.Credit, 100));
        Assert.Throws<InvalidOperationException>(() => account.Apply(Line(account.Id, PostingSide.Debit, 101)));
        Assert.Equal(100, account.Balance.MinorUnits);
        account.Apply(Line(account.Id, PostingSide.Debit, 100));
        Assert.Equal(0, account.Balance.MinorUnits);
    }

    [Fact]
    public void Invalid_account_or_overflow_cannot_change_balance()
    {
        Assert.Throws<ArgumentException>(() => LedgerAccount.ForWallet(Guid.Empty));
        var account = LedgerAccount.ForWallet(Guid.NewGuid());
        Assert.Throws<ArgumentException>(() => account.Apply(Line(Guid.NewGuid(), PostingSide.Credit, 1)));
        account.Apply(Line(account.Id, PostingSide.Credit, long.MaxValue));
        Assert.Throws<OverflowException>(() => account.Apply(Line(account.Id, PostingSide.Credit, 1)));
        Assert.Equal(long.MaxValue, account.Balance.MinorUnits);
    }
}
