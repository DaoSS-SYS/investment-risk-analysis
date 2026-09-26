using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiskAnalysis.Infrastructure.Identity;

namespace RiskAnalysis.Infrastructure.Persistence.Configurations;

/// <summary>
/// Настройка сущностей подсистемы удостоверения личности.
///
/// Стандартные таблицы подсистемы получают наименования в стиле snake_case
/// с приставкой, обозначающей принадлежность к подсистеме: без явного
/// указания они назывались бы AspNetUsers, AspNetRoles и подобным образом,
/// что расходится с принятым в базе данных соглашением об именовании.
/// </summary>
public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("users", t => t.HasComment("Пользователи системы"));

        builder.Property(x => x.FullName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.Position).HasMaxLength(128);

        builder.HasIndex(x => x.IsActive);
    }
}

public class ApplicationRoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
{
    public void Configure(EntityTypeBuilder<ApplicationRole> builder)
    {
        builder.ToTable("roles", t => t.HasComment("Роли пользователей системы"));

        builder.Property(x => x.Description).HasMaxLength(512);
    }
}
