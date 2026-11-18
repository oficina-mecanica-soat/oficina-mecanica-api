using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OficinaMecanica.Infrastructure.GestaoOrdemServico.Mensagens;

namespace OficinaMecanica.Infrastructure.GestaoOrdemServico.Configurations;

public sealed class RecebimentoMensagemConfiguration : IEntityTypeConfiguration<RecebimentoMensagem>
{
    public void Configure(EntityTypeBuilder<RecebimentoMensagem> builder)
    {
        builder.ToTable("RecebimentosMensagem", "GestaoOrdemServico");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.ChaveOperacao).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Hash).HasMaxLength(64).IsRequired();
        builder.HasIndex(r => r.ChaveOperacao);
    }
}
