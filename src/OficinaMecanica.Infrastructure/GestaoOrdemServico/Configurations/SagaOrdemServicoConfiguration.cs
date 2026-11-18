using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OficinaMecanica.Domain.GestaoOrdemServico.Aggregates;

namespace OficinaMecanica.Infrastructure.GestaoOrdemServico.Configurations;

public sealed class SagaOrdemServicoConfiguration : IEntityTypeConfiguration<SagaOrdemServico>
{
    public void Configure(EntityTypeBuilder<SagaOrdemServico> builder)
    {
        builder.ToTable("SagasOrdemServico", "GestaoOrdemServico");
        builder.HasKey(saga => saga.Id);
        builder.Property(saga => saga.Id).ValueGeneratedNever();
        builder.Property(saga => saga.Estado).HasConversion<int>().IsRequired();
        builder.Property(saga => saga.Decisao).HasConversion<int?>();
        builder.Property(saga => saga.ValorOrcamento).HasPrecision(18, 2);
        builder.Property<byte[]>("RowVersion").IsRowVersion();
        // Metadado de integração: o agregado permanece independente de JSON e transporte.
        builder.Property<string?>("SnapshotOrcamento");
        builder.HasIndex(saga => saga.OrdemServicoId).IsUnique();
        builder.HasOne<OrdemServico>().WithMany()
            .HasForeignKey(saga => saga.OrdemServicoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
