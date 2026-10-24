using LedgerFlow.Modules.Transfers.Domain;
using LedgerFlow.SharedKernel;

namespace LedgerFlow.UnitTests;

public class TransferTests
{
    private static Transfer NewTransfer() => Transfer.Request(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Money.FromDecimal(30));

    [Fact]
    public void Request_captures_payment_details_and_starts_pending()
    {
        var requester = Guid.NewGuid();
        var source = Guid.NewGuid();
        var destination = Guid.NewGuid();
        var transfer = Transfer.Request(requester, source, destination, Money.FromDecimal(30));
        Assert.NotEqual(Guid.Empty, transfer.Id);
        Assert.Equal(requester, transfer.RequestedBy);
        Assert.Equal(source, transfer.SourceWalletId);
        Assert.Equal(destination, transfer.DestinationWalletId);
        Assert.Equal(Money.FromMinorUnits(3000), transfer.Amount);
        Assert.Equal(TransferStatus.Pending, transfer.Status);
        Assert.Null(transfer.JournalEntryId);
        Assert.Null(transfer.RejectionReason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Transfer_requires_positive_amount(long cents)
        => Assert.Throws<ArgumentException>(() => Transfer.Request(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Money.FromMinorUnits(cents)));

    [Fact]
    public void Invalid_participants_are_rejected()
    {
        var id = Guid.NewGuid();
        var amount = Money.FromMinorUnits(1);
        Assert.Throws<ArgumentException>(() => Transfer.Request(Guid.Empty, id, Guid.NewGuid(), amount));
        Assert.Throws<ArgumentException>(() => Transfer.Request(id, Guid.Empty, Guid.NewGuid(), amount));
        Assert.Throws<ArgumentException>(() => Transfer.Request(id, Guid.NewGuid(), Guid.Empty, amount));
        Assert.Throws<ArgumentException>(() => Transfer.Request(id, id, id, amount));
    }

    [Fact]
    public void Confirmation_requires_journal_reference_and_is_terminal()
    {
        var transfer = NewTransfer();
        Assert.Throws<ArgumentException>(() => transfer.Confirm(Guid.Empty));
        Assert.Equal(TransferStatus.Pending, transfer.Status);
        var entry = Guid.NewGuid();
        transfer.Confirm(entry);
        Assert.Equal(TransferStatus.Confirmed, transfer.Status);
        Assert.Equal(entry, transfer.JournalEntryId);
        Assert.Null(transfer.RejectionReason);
        Assert.Throws<InvalidOperationException>(() => transfer.Confirm(entry));
        Assert.Throws<InvalidOperationException>(() => transfer.Reject(TransferRejectionReason.InsufficientFunds));
    }

    [Fact]
    public void Rejection_records_reason_and_is_terminal()
    {
        var transfer = NewTransfer();
        Assert.Throws<ArgumentOutOfRangeException>(() => transfer.Reject((TransferRejectionReason)99));
        transfer.Reject(TransferRejectionReason.InsufficientFunds);
        Assert.Equal(TransferStatus.Rejected, transfer.Status);
        Assert.Equal(TransferRejectionReason.InsufficientFunds, transfer.RejectionReason);
        Assert.Null(transfer.JournalEntryId);
        Assert.Throws<InvalidOperationException>(() => transfer.Confirm(Guid.NewGuid()));
        Assert.Throws<InvalidOperationException>(() => transfer.Reject(TransferRejectionReason.WalletUnavailable));
    }
}
