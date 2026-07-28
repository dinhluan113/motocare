using MongoDB.Driver;
using MotoCare.Api.Contracts;
using MotoCare.Api.Domain;
using MotoCare.Api.Infrastructure;

namespace MotoCare.Api.Services;

public sealed class LoyaltyService(MongoDbContext context)
{
    public async Task<LoyaltyRedemptionPreview> PreviewRedemptionAsync(
        LoyaltyRedemptionPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var account = await context.Collection<LoyaltyAccount>()
            .Find(x => x.CustomerId == request.CustomerId && !x.IsDeleted && x.Status == "Active")
            .FirstOrDefaultAsync(cancellationToken);
        var rule = await GetActiveRuleAsync(null, cancellationToken);
        var available = account?.AvailablePoints ?? 0;
        var maxDiscount = decimal.Floor(request.InvoiceAmount * rule.MaximumRedemptionRate);
        var maxPointsByInvoice = (long)decimal.Floor(maxDiscount / rule.RedemptionValue);
        var allowed = Math.Min(Math.Min(available, request.RequestedPoints), maxPointsByInvoice);
        if (allowed < rule.MinimumRedemptionPoints)
        {
            allowed = 0;
        }

        return new LoyaltyRedemptionPreview(
            available,
            request.RequestedPoints,
            allowed,
            rule.RedemptionValue,
            allowed * rule.RedemptionValue,
            maxDiscount);
    }

    public async Task<LoyaltyTransaction> AdjustAsync(
        string customerId,
        LoyaltyAdjustmentRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (request.Points == 0)
        {
            throw new InvalidOperationException("Số điểm điều chỉnh phải khác 0.");
        }

        using var session = await context.Client.StartSessionAsync(cancellationToken: cancellationToken);
        session.StartTransaction();
        try
        {
            var customer = await context.Collection<Customer>()
                .Find(session, x => x.Id == customerId && !x.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new KeyNotFoundException("Không tìm thấy khách hàng.");
            var account = await GetOrCreateAccountAsync(session, customer, cancellationToken);
            var transaction = await ApplyPointChangeAsync(
                session,
                account,
                request.Points,
                request.Points > 0 ? LoyaltyTransactionType.Adjust : LoyaltyTransactionType.Adjust,
                $"adjust:{customerId}:{request.IdempotencyKey}",
                userId,
                request.Reason.Trim(),
                null,
                null,
                cancellationToken);
            await session.CommitTransactionAsync(cancellationToken);
            return transaction;
        }
        catch
        {
            await session.AbortTransactionAsync(cancellationToken);
            throw;
        }
    }

    public async Task<decimal> RedeemWithinTransactionAsync(
        IClientSessionHandle session,
        string customerId,
        string invoiceId,
        decimal invoiceAmount,
        long requestedPoints,
        string idempotencyKey,
        string userId,
        CancellationToken cancellationToken)
    {
        if (requestedPoints <= 0)
        {
            return 0;
        }

        var existingKey = $"redeem:{invoiceId}:{idempotencyKey}";
        var existing = await context.Collection<LoyaltyTransaction>()
            .Find(session, x => x.IdempotencyKey == existingKey)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            return existing.MonetaryValue;
        }

        var customer = await context.Collection<Customer>()
            .Find(session, x => x.Id == customerId && !x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy khách hàng.");
        var account = await GetOrCreateAccountAsync(session, customer, cancellationToken);
        var rule = await GetActiveRuleAsync(session, cancellationToken);
        if (requestedPoints < rule.MinimumRedemptionPoints)
        {
            throw new InvalidOperationException(
                $"Số điểm đổi tối thiểu là {rule.MinimumRedemptionPoints}.");
        }

        var maxDiscount = decimal.Floor(invoiceAmount * rule.MaximumRedemptionRate);
        var maxPoints = (long)decimal.Floor(maxDiscount / rule.RedemptionValue);
        if (requestedPoints > maxPoints)
        {
            throw new InvalidOperationException(
                $"Chỉ được dùng tối đa {maxPoints} điểm cho hóa đơn này.");
        }

        await ApplyPointChangeAsync(
            session,
            account,
            -requestedPoints,
            LoyaltyTransactionType.Redeem,
            existingKey,
            userId,
            $"Đổi điểm cho hóa đơn {invoiceId}",
            invoiceId,
            requestedPoints * rule.RedemptionValue,
            cancellationToken);
        return requestedPoints * rule.RedemptionValue;
    }

    public async Task EarnWithinTransactionAsync(
        IClientSessionHandle session,
        string customerId,
        string invoiceId,
        decimal eligibleAmount,
        string userId,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = $"earn:{invoiceId}";
        if (await context.Collection<LoyaltyTransaction>()
            .Find(session, x => x.IdempotencyKey == idempotencyKey)
            .AnyAsync(cancellationToken))
        {
            return;
        }

        var customer = await context.Collection<Customer>()
            .Find(session, x => x.Id == customerId && !x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy khách hàng.");
        var account = await GetOrCreateAccountAsync(session, customer, cancellationToken);
        var rule = await GetActiveRuleAsync(session, cancellationToken);
        var tier = await context.Collection<LoyaltyTier>()
            .Find(session, x => x.Code == account.CurrentTierCode && x.IsActive && !x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
        var earnRate = tier?.EarnRate ?? 1;
        var points = (long)decimal.Floor(eligibleAmount / rule.SpendPerPoint * earnRate);
        if (points <= 0)
        {
            return;
        }

        var balanceBefore = account.AvailablePoints;
        var newSpend = account.EligibleSpend + eligibleAmount;
        var newTier = await context.Collection<LoyaltyTier>()
            .Find(session, x =>
                x.IsActive
                && !x.IsDeleted
                && x.MinEligibleSpend <= newSpend)
            .SortByDescending(x => x.Rank)
            .FirstOrDefaultAsync(cancellationToken);
        var newTierCode = newTier?.Code ?? account.CurrentTierCode;

        var accountUpdate = Builders<LoyaltyAccount>.Update
            .Inc(x => x.AvailablePoints, points)
            .Inc(x => x.LifetimeEarnedPoints, points)
            .Inc(x => x.EligibleSpend, eligibleAmount)
            .Set(x => x.CurrentTierCode, newTierCode)
            .Set(x => x.TierUpdatedAt, DateTime.UtcNow)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);
        await context.Collection<LoyaltyAccount>().UpdateOneAsync(
            session,
            x => x.Id == account.Id,
            accountUpdate,
            cancellationToken: cancellationToken);
        await context.Collection<Customer>().UpdateOneAsync(
            session,
            x => x.Id == customerId,
            Builders<Customer>.Update
                .Inc(x => x.LoyaltyPointBalance, points)
                .Set(x => x.LoyaltyTierCode, newTierCode)
                .Set(x => x.UpdatedAt, DateTime.UtcNow),
            cancellationToken: cancellationToken);

        var transaction = new LoyaltyTransaction
        {
            TransactionCode = NewTransactionCode(),
            IdempotencyKey = idempotencyKey,
            LoyaltyAccountId = account.Id,
            CustomerId = customerId,
            InvoiceId = invoiceId,
            Type = LoyaltyTransactionType.Earn,
            Points = points,
            MonetaryValue = eligibleAmount,
            BalanceBefore = balanceBefore,
            BalanceAfter = balanceBefore + points,
            ExpiresAt = rule.PointExpiryDays.HasValue
                ? DateTime.UtcNow.AddDays(rule.PointExpiryDays.Value)
                : null,
            CreatedBy = userId,
            Reason = $"Tích điểm từ hóa đơn {invoiceId}"
        };
        await context.Collection<LoyaltyTransaction>()
            .InsertOneAsync(session, transaction, cancellationToken: cancellationToken);
    }

    public async Task ReverseInvoiceWithinTransactionAsync(
        IClientSessionHandle session,
        string invoiceId,
        string userId,
        CancellationToken cancellationToken)
    {
        var originals = await context.Collection<LoyaltyTransaction>()
            .Find(session, x =>
                x.InvoiceId == invoiceId
                && (x.Type == LoyaltyTransactionType.Earn || x.Type == LoyaltyTransactionType.Redeem)
                && !x.IsDeleted)
            .ToListAsync(cancellationToken);
        foreach (var original in originals)
        {
            var account = await context.Collection<LoyaltyAccount>()
                .Find(session, x => x.Id == original.LoyaltyAccountId && !x.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new KeyNotFoundException("Không tìm thấy tài khoản loyalty.");
            await ApplyPointChangeAsync(
                session,
                account,
                -original.Points,
                LoyaltyTransactionType.Reverse,
                $"reverse:{original.Id}",
                userId,
                $"Đảo giao dịch {original.TransactionCode} do hoàn/hủy hóa đơn",
                invoiceId,
                -original.MonetaryValue,
                cancellationToken);

            if (original.Type == LoyaltyTransactionType.Earn && original.MonetaryValue > 0)
            {
                await context.Collection<LoyaltyAccount>().UpdateOneAsync(
                    session,
                    x => x.Id == account.Id,
                    Builders<LoyaltyAccount>.Update
                        .Inc(x => x.EligibleSpend, -original.MonetaryValue)
                        .Set(x => x.UpdatedAt, DateTime.UtcNow),
                    cancellationToken: cancellationToken);
            }
        }
    }

    private async Task<LoyaltyTransaction> ApplyPointChangeAsync(
        IClientSessionHandle session,
        LoyaltyAccount account,
        long signedPoints,
        LoyaltyTransactionType type,
        string idempotencyKey,
        string userId,
        string reason,
        string? invoiceId,
        decimal? monetaryValue,
        CancellationToken cancellationToken)
    {
        var transactions = context.Collection<LoyaltyTransaction>();
        var existing = await transactions.Find(session, x => x.IdempotencyKey == idempotencyKey)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var filter = Builders<LoyaltyAccount>.Filter.Eq(x => x.Id, account.Id);
        if (signedPoints < 0 && type != LoyaltyTransactionType.Reverse)
        {
            filter &= Builders<LoyaltyAccount>.Filter.Gte(x => x.AvailablePoints, -signedPoints);
        }

        var update = Builders<LoyaltyAccount>.Update
            .Inc(x => x.AvailablePoints, signedPoints)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);
        if (type == LoyaltyTransactionType.Redeem)
        {
            update = update.Inc(x => x.LifetimeRedeemedPoints, -signedPoints);
        }
        else if (type == LoyaltyTransactionType.Earn && signedPoints > 0)
        {
            update = update.Inc(x => x.LifetimeEarnedPoints, signedPoints);
        }

        var updated = await context.Collection<LoyaltyAccount>().FindOneAndUpdateAsync(
            session,
            filter,
            update,
            new FindOneAndUpdateOptions<LoyaltyAccount, LoyaltyAccount>
            {
                ReturnDocument = ReturnDocument.After
            },
            cancellationToken)
            ?? throw new InvalidOperationException("Số dư điểm không đủ.");

        await context.Collection<Customer>().UpdateOneAsync(
            session,
            x => x.Id == account.CustomerId,
            Builders<Customer>.Update
                .Inc(x => x.LoyaltyPointBalance, signedPoints)
                .Set(x => x.UpdatedAt, DateTime.UtcNow),
            cancellationToken: cancellationToken);

        var transaction = new LoyaltyTransaction
        {
            TransactionCode = NewTransactionCode(),
            IdempotencyKey = idempotencyKey,
            LoyaltyAccountId = account.Id,
            CustomerId = account.CustomerId,
            InvoiceId = invoiceId,
            Type = type,
            Points = signedPoints,
            MonetaryValue = monetaryValue ?? 0,
            BalanceBefore = account.AvailablePoints,
            BalanceAfter = updated.AvailablePoints,
            CreatedBy = userId,
            Reason = reason
        };
        await transactions.InsertOneAsync(session, transaction, cancellationToken: cancellationToken);
        return transaction;
    }

    private async Task<LoyaltyAccount> GetOrCreateAccountAsync(
        IClientSessionHandle session,
        Customer customer,
        CancellationToken cancellationToken)
    {
        var accounts = context.Collection<LoyaltyAccount>();
        var account = await accounts.Find(session, x => x.CustomerId == customer.Id && !x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
        if (account is not null)
        {
            return account;
        }

        account = new LoyaltyAccount
        {
            CustomerId = customer.Id,
            MemberCode = $"MEM-{DateTime.UtcNow:yyyyMMdd}-{customer.Id[^6..].ToUpperInvariant()}"
        };
        await accounts.InsertOneAsync(session, account, cancellationToken: cancellationToken);
        await context.Collection<Customer>().UpdateOneAsync(
            session,
            x => x.Id == customer.Id,
            Builders<Customer>.Update
                .Set(x => x.LoyaltyAccountId, account.Id)
                .Set(x => x.LoyaltyTierCode, account.CurrentTierCode)
                .Set(x => x.LoyaltyPointBalance, 0)
                .Set(x => x.UpdatedAt, DateTime.UtcNow),
            cancellationToken: cancellationToken);
        return account;
    }

    private async Task<LoyaltyRule> GetActiveRuleAsync(
        IClientSessionHandle? session,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var filter = Builders<LoyaltyRule>.Filter.And(
            Builders<LoyaltyRule>.Filter.Eq(x => x.IsActive, true),
            Builders<LoyaltyRule>.Filter.Eq(x => x.IsDeleted, false),
            Builders<LoyaltyRule>.Filter.Lte(x => x.EffectiveFrom, now),
            Builders<LoyaltyRule>.Filter.Or(
                Builders<LoyaltyRule>.Filter.Eq(x => x.EffectiveTo, null),
                Builders<LoyaltyRule>.Filter.Gte(x => x.EffectiveTo, now)));
        var find = session is null
            ? context.Collection<LoyaltyRule>().Find(filter)
            : context.Collection<LoyaltyRule>().Find(session, filter);
        return await find.SortByDescending(x => x.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Chưa cấu hình chính sách loyalty đang hiệu lực.");
    }

    private static string NewTransactionCode() =>
        $"LTX-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}"[..31].ToUpperInvariant();
}
