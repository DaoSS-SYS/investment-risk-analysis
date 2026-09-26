using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiskAnalysis.Domain.Entities;

namespace RiskAnalysis.Infrastructure.Persistence.Configurations;

public class AuditRecordConfiguration : IEntityTypeConfiguration<AuditRecord>
{
    public void Configure(EntityTypeBuilder<AuditRecord> builder)
    {
        builder.ToTable("audit_records", t =>
            t.HasComment("Журнал действий пользователей, изменяющих состояние системы"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Action).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.EntityType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.EntityId).HasMaxLength(64);
        builder.Property(x => x.Description).HasMaxLength(1024).IsRequired();
        builder.Property(x => x.IpAddress).HasMaxLength(64);

        builder.HasIndex(x => x.Timestamp);
        builder.HasIndex(x => new { x.EntityType, x.EntityId });
        builder.HasIndex(x => x.UserId);
    }
}
