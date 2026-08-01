using MotoCare.Api.Domain;
using MotoCare.Api.Infrastructure;
using MongoDB.Driver;

namespace MotoCare.Api.Services;

public sealed class AutoCodeService(SequenceService sequences, MongoDbContext context)
{
    public async Task EnsureAsync(BaseDocument entity, CancellationToken cancellationToken = default)
    {
        var target = entity switch
        {
            Employee value when string.IsNullOrWhiteSpace(value.EmployeeCode) =>
                new CodeTarget("employee", "NV", code => value.EmployeeCode = code),
            VehicleBrand value when string.IsNullOrWhiteSpace(value.Code) =>
                new CodeTarget("vehicle-brand", "HX", code => value.Code = code),
            VehicleModel value when string.IsNullOrWhiteSpace(value.Code) =>
                new CodeTarget("vehicle-model", "DX", code => value.Code = code),
            PartBrand value when string.IsNullOrWhiteSpace(value.Code) =>
                new CodeTarget("part-brand", "HPT", code => value.Code = code),
            PartCategory value when string.IsNullOrWhiteSpace(value.Code) =>
                new CodeTarget("part-category", "DMPT", code => value.Code = code),
            ServiceCategory value when string.IsNullOrWhiteSpace(value.Code) =>
                new CodeTarget("service-category", "DV", code => value.Code = code),
            Part value when string.IsNullOrWhiteSpace(value.Code) =>
                new CodeTarget("part", "PT", code => value.Code = code),
            Supplier value when string.IsNullOrWhiteSpace(value.Code) =>
                new CodeTarget("supplier", "NCC", code => value.Code = code),
            CashCategory value when string.IsNullOrWhiteSpace(value.Code) =>
                new CodeTarget("cash-category", "DMTG", code => value.Code = code),
            LoyaltyTier value when string.IsNullOrWhiteSpace(value.Code) =>
                new CodeTarget("loyalty-tier", "HTV", code => value.Code = code),
            Coupon value when string.IsNullOrWhiteSpace(value.Code) =>
                new CodeTarget("coupon", "CP", code => value.Code = code),
            _ => null
        };

        if (target is null)
        {
            return;
        }

        for (var attempt = 0; attempt < 100; attempt++)
        {
            var generated = await sequences.NextGlobalAsync(
                target.SequenceName,
                target.Prefix,
                cancellationToken: cancellationToken);
            if (!await CodeExistsAsync(entity, generated, cancellationToken))
            {
                target.Assign(generated);
                return;
            }
        }

        throw new InvalidOperationException("Không thể tạo mã duy nhất. Vui lòng thử lại.");
    }

    private Task<bool> CodeExistsAsync(
        BaseDocument entity,
        string code,
        CancellationToken cancellationToken) => entity switch
        {
            Employee => context.Collection<Employee>().Find(x => x.EmployeeCode == code).AnyAsync(cancellationToken),
            VehicleBrand => context.Collection<VehicleBrand>().Find(x => x.Code == code).AnyAsync(cancellationToken),
            VehicleModel => context.Collection<VehicleModel>().Find(x => x.Code == code).AnyAsync(cancellationToken),
            PartBrand => context.Collection<PartBrand>().Find(x => x.Code == code).AnyAsync(cancellationToken),
            PartCategory => context.Collection<PartCategory>().Find(x => x.Code == code).AnyAsync(cancellationToken),
            ServiceCategory => context.Collection<ServiceCategory>().Find(x => x.Code == code).AnyAsync(cancellationToken),
            Part => context.Collection<Part>().Find(x => x.Code == code).AnyAsync(cancellationToken),
            Supplier => context.Collection<Supplier>().Find(x => x.Code == code).AnyAsync(cancellationToken),
            CashCategory => context.Collection<CashCategory>().Find(x => x.Code == code).AnyAsync(cancellationToken),
            LoyaltyTier => context.Collection<LoyaltyTier>().Find(x => x.Code == code).AnyAsync(cancellationToken),
            Coupon => context.Collection<Coupon>().Find(x => x.Code == code).AnyAsync(cancellationToken),
            _ => Task.FromResult(false)
        };

    private sealed record CodeTarget(string SequenceName, string Prefix, Action<string> Assign);
}
