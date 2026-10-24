using LedgerFlow.SharedKernel;

namespace LedgerFlow.Modules.Transfers.Domain;

public enum TransferStatus { Pending = 1, Confirmed = 2, Rejected = 3 }
public enum TransferRejectionReason { InsufficientFunds = 1, WalletUnavailable = 2 }

public sealed class Transfer
{
    public Guid Id { get; }
    public Guid RequestedBy { get; }
    public Guid SourceWalletId { get; }
    public Guid DestinationWalletId { get; }
    public Money Amount { get; }
    public TransferStatus Status { get; private set; }
    public Guid? JournalEntryId { get; private set; }
    public TransferRejectionReason? RejectionReason { get; private set; }

    private Transfer(Guid requestedBy, Guid sourceWalletId, Guid destinationWalletId, Money amount)
    {
        Id = Guid.NewGuid();
        RequestedBy = requestedBy;
        SourceWalletId = sourceWalletId;
        DestinationWalletId = destinationWalletId;
        Amount = amount;
        Status = TransferStatus.Pending;
    }

    public static Transfer Request(Guid requestedBy, Guid sourceWalletId, Guid destinationWalletId, Money amount)
    {
        if (requestedBy == Guid.Empty) throw new ArgumentException("Requester is required.", nameof(requestedBy));
        if (sourceWalletId == Guid.Empty) throw new ArgumentException("Source is required.", nameof(sourceWalletId));
        if (destinationWalletId == Guid.Empty) throw new ArgumentException("Destination is required.", nameof(destinationWalletId));
        if (sourceWalletId == destinationWalletId) throw new ArgumentException("Wallets must be distinct.", nameof(destinationWalletId));
        ArgumentNullException.ThrowIfNull(amount);
        if (amount.MinorUnits <= 0) throw new ArgumentException("Transfer amount must be positive.", nameof(amount));
        return new Transfer(requestedBy, sourceWalletId, destinationWalletId, amount);
    }

    public void Confirm(Guid journalEntryId)
    {
        EnsurePending();
        if (journalEntryId == Guid.Empty) throw new ArgumentException("Journal entry is required.", nameof(journalEntryId));
        JournalEntryId = journalEntryId;
        Status = TransferStatus.Confirmed;
    }

    public void Reject(TransferRejectionReason reason)
    {
        EnsurePending();
        if (!Enum.IsDefined(reason)) throw new ArgumentOutOfRangeException(nameof(reason));
        RejectionReason = reason;
        Status = TransferStatus.Rejected;
    }

    private void EnsurePending()
    {
        if (Status != TransferStatus.Pending)
            throw new InvalidOperationException("Only pending transfers can be confirmed or rejected.");
    }
}
