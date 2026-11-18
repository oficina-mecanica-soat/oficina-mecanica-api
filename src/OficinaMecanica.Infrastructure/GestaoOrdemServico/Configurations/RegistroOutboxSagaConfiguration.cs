using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OficinaMecanica.Infrastructure.GestaoOrdemServico.Mensagens;

namespace OficinaMecanica.Infrastructure.GestaoOrdemServico.Configurations;

public sealed class RegistroOutboxSagaConfiguration : IEntityTypeConfiguration<RegistroOutboxSaga>
{
    public void Configure(EntityTypeBuilder<RegistroOutboxSaga> builder)
    {
        builder.ToTable("OutboxSaga", "GestaoOrdemServico");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Tipo).HasMaxLength(64).IsRequired();
        builder.Property(r => r.Destino).HasMaxLength(32).IsRequired();
        builder.Property(r => r.Corpo).IsRequired();
        builder.HasIndex(r => new { r.Tipo, r.OrdemServicoId, r.VersaoNegocio }).IsUnique();
        builder.HasIndex(r => r.PublicadoEm);
    }
}
