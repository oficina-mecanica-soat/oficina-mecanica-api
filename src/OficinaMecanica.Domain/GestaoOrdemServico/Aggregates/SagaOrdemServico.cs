using OficinaMecanica.Domain.GestaoOrdemServico.Enums;
using OficinaMecanica.Domain.GestaoOrdemServico.Messages;
using OficinaMecanica.Domain.Shared.Exceptions;

namespace OficinaMecanica.Domain.GestaoOrdemServico.Aggregates;

public sealed class SagaOrdemServico
{
    private SagaOrdemServico()
    {
    }

    public Guid Id { get; private set; }
    public Guid OrdemServicoId { get; private set; }
    public int VersaoPreparacao { get; private set; }
    public EstadoSagaOrdemServico Estado { get; private set; }
    public Guid? PreparacaoId { get; private set; }
    public Guid? OrcamentoId { get; private set; }
    public int VersaoOrcamento { get; private set; }
    public decimal? ValorOrcamento { get; private set; }
    public DecisaoOrcamento? Decisao { get; private set; }
    public bool InicioFisicoConfirmado { get; private set; }
    public DateTimeOffset? FinalizadoEm { get; private set; }
    public Guid? PagamentoId { get; private set; }
    public bool PreparacaoCancelada { get; private set; }
    public bool OrcamentoCancelado { get; private set; }

    public static SagaOrdemServico Iniciar(Guid ordemServicoId, int versaoPreparacao, int versaoOrcamento)
    {
        ValidarIdentificador(ordemServicoId);
        ValidarVersao(versaoPreparacao);
        ValidarVersao(versaoOrcamento);

        return new SagaOrdemServico
        {
            Id = Guid.NewGuid(),
            OrdemServicoId = ordemServicoId,
            VersaoPreparacao = versaoPreparacao,
            VersaoOrcamento = versaoOrcamento,
            Estado = EstadoSagaOrdemServico.Preparando
        };
    }

    public void RegistrarPreparacao(Guid preparacaoId, int versaoPreparacao)
    {
        ValidarIdentificador(preparacaoId);
        ExigirVersaoPreparacao(versaoPreparacao);

        if (PreparacaoId == preparacaoId)
        {
            return;
        }

        ExigirEstado(EstadoSagaOrdemServico.Preparando);
        PreparacaoId = preparacaoId;
        Estado = EstadoSagaOrdemServico.AguardandoOrcamento;
    }

    public void RegistrarRecusaPreparacao(int versaoPreparacao)
    {
        ExigirVersaoPreparacao(versaoPreparacao);
        if (Estado == EstadoSagaOrdemServico.PreparacaoRecusada)
        {
            return;
        }

        ExigirEstado(EstadoSagaOrdemServico.Preparando);
        Estado = EstadoSagaOrdemServico.PreparacaoRecusada;
    }

    public void RegistrarOrcamento(Guid orcamentoId, int versaoOrcamento, decimal valorTotal)
    {
        ValidarIdentificador(orcamentoId);
        ValidarVersao(versaoOrcamento);
        if (versaoOrcamento != VersaoOrcamento)
        {
            throw new DomainException(SagaOrdemServicoErrorMessages.VersaoInvalida);
        }
        // Mesma representação SQL (18,2) já usada pelos valores da OS, sem arredondamento silencioso.
        if (valorTotal <= 0 || valorTotal > 9999999999999999.99m || decimal.Round(valorTotal, 2) != valorTotal)
        {
            throw new DomainException(SagaOrdemServicoErrorMessages.ValorInvalido);
        }

        if (OrcamentoId == orcamentoId && VersaoOrcamento == versaoOrcamento && ValorOrcamento == valorTotal)
        {
            return;
        }

        if (Estado is not (EstadoSagaOrdemServico.AguardandoOrcamento or EstadoSagaOrdemServico.CompensacaoPendente))
        {
            throw new DomainException(SagaOrdemServicoErrorMessages.TransicaoInvalida);
        }

        if (OrcamentoId.HasValue)
        {
            throw new DomainException(SagaOrdemServicoErrorMessages.ResultadoDivergente);
        }

        OrcamentoId = orcamentoId;
        ValorOrcamento = valorTotal;
        if (Estado != EstadoSagaOrdemServico.CompensacaoPendente)
        {
            Estado = EstadoSagaOrdemServico.AguardandoAprovacao;
        }
    }

    public void RegistrarDecisao(Guid orcamentoId, int versaoOrcamento, DecisaoOrcamento decisao)
    {
        ExigirOrcamento(orcamentoId, versaoOrcamento);
        if (!Enum.IsDefined(decisao))
        {
            throw new DomainException(SagaOrdemServicoErrorMessages.DecisaoInvalida);
        }

        if (Decisao == decisao)
        {
            return;
        }

        ExigirEstado(EstadoSagaOrdemServico.AguardandoAprovacao);
        Decisao = decisao;
        Estado = decisao == DecisaoOrcamento.Aprovado
            ? EstadoSagaOrdemServico.ExecucaoAutorizada
            : EstadoSagaOrdemServico.CompensacaoPendente;
    }

    public void RegistrarInicioFisico(Guid preparacaoId, int versaoOrcamento)
    {
        ExigirPreparacaoEOrcamento(preparacaoId, versaoOrcamento);
        if (InicioFisicoConfirmado)
        {
            return;
        }

        if (Estado == EstadoSagaOrdemServico.CompensacaoPendente
            && !PreparacaoCancelada && Decisao == DecisaoOrcamento.Aprovado)
        {
            // O início pode ter vencido no Execução antes de a OS receber seu evento.
            InicioFisicoConfirmado = true;
            Estado = EstadoSagaOrdemServico.RecuperacaoPendente;
            return;
        }

        ExigirEstado(EstadoSagaOrdemServico.ExecucaoAutorizada);
        InicioFisicoConfirmado = true;
        Estado = EstadoSagaOrdemServico.EmExecucao;
    }

    public void RegistrarFinalizacao(Guid preparacaoId, int versaoOrcamento, DateTimeOffset finalizadoEm)
    {
        ExigirPreparacaoEOrcamento(preparacaoId, versaoOrcamento);
        if (finalizadoEm == default || finalizadoEm.Offset != TimeSpan.Zero)
        {
            throw new DomainException(SagaOrdemServicoErrorMessages.DataFinalizacaoInvalida);
        }

        if (FinalizadoEm == finalizadoEm)
        {
            return;
        }

        ExigirEstado(EstadoSagaOrdemServico.EmExecucao);
        FinalizadoEm = finalizadoEm;
        Estado = EstadoSagaOrdemServico.AguardandoPagamento;
    }

    public void RegistrarPagamento(Guid orcamentoId, int versaoOrcamento, Guid pagamentoId, decimal valor)
    {
        ExigirOrcamento(orcamentoId, versaoOrcamento);
        ValidarIdentificador(pagamentoId);
        if (valor != ValorOrcamento)
        {
            throw new DomainException(SagaOrdemServicoErrorMessages.ValorInvalido);
        }

        if (PagamentoId == pagamentoId)
        {
            return;
        }

        ExigirEstado(EstadoSagaOrdemServico.AguardandoPagamento);
        PagamentoId = pagamentoId;
        Estado = EstadoSagaOrdemServico.PagamentoConfirmado;
    }

    public void IniciarCompensacao()
    {
        if (InicioFisicoConfirmado)
        {
            throw new DomainException(SagaOrdemServicoErrorMessages.CompensacaoAposInicioFisico);
        }

        if (Estado is EstadoSagaOrdemServico.CompensacaoPendente or EstadoSagaOrdemServico.Compensada)
        {
            return;
        }

        if (Estado is not (EstadoSagaOrdemServico.AguardandoOrcamento
            or EstadoSagaOrdemServico.AguardandoAprovacao or EstadoSagaOrdemServico.ExecucaoAutorizada))
        {
            throw new DomainException(SagaOrdemServicoErrorMessages.TransicaoInvalida);
        }

        Estado = EstadoSagaOrdemServico.CompensacaoPendente;
    }

    public void ConfirmarCancelamentoPreparacao(Guid preparacaoId, int versaoPreparacao)
    {
        ExigirVersaoPreparacao(versaoPreparacao);
        if (PreparacaoId != preparacaoId || preparacaoId == Guid.Empty)
        {
            throw new DomainException(SagaOrdemServicoErrorMessages.ResultadoDivergente);
        }

        ExigirCompensacao();
        PreparacaoCancelada = true;
        ConcluirCompensacaoSeConfirmada();
    }

    public void ConfirmarCancelamentoOrcamento(int versaoOrcamento)
    {
        // CancelarOrcamento também pode ser endereçado por OS + versão antes da geração.
        if (versaoOrcamento != VersaoOrcamento)
        {
            throw new DomainException(SagaOrdemServicoErrorMessages.VersaoInvalida);
        }
        ExigirCompensacao();
        OrcamentoCancelado = true;
        ConcluirCompensacaoSeConfirmada();
    }

    private void ConcluirCompensacaoSeConfirmada()
    {
        if (PreparacaoCancelada && OrcamentoCancelado)
        {
            Estado = EstadoSagaOrdemServico.Compensada;
        }
    }

    private void ExigirCompensacao()
    {
        if (Estado is not (EstadoSagaOrdemServico.CompensacaoPendente or EstadoSagaOrdemServico.Compensada))
        {
            throw new DomainException(SagaOrdemServicoErrorMessages.TransicaoInvalida);
        }
    }

    private void ExigirPreparacaoEOrcamento(Guid preparacaoId, int versaoOrcamento)
    {
        if (preparacaoId == Guid.Empty || PreparacaoId != preparacaoId || VersaoOrcamento != versaoOrcamento)
        {
            throw new DomainException(SagaOrdemServicoErrorMessages.ResultadoDivergente);
        }
    }

    private void ExigirOrcamento(Guid orcamentoId, int versaoOrcamento)
    {
        if (orcamentoId == Guid.Empty || OrcamentoId != orcamentoId || VersaoOrcamento != versaoOrcamento)
        {
            throw new DomainException(SagaOrdemServicoErrorMessages.ResultadoDivergente);
        }
    }

    private void ExigirVersaoPreparacao(int versao)
    {
        if (versao != VersaoPreparacao)
        {
            throw new DomainException(SagaOrdemServicoErrorMessages.VersaoInvalida);
        }
    }

    private void ExigirEstado(EstadoSagaOrdemServico estado)
    {
        if (Estado != estado)
        {
            throw new DomainException(SagaOrdemServicoErrorMessages.TransicaoInvalida);
        }
    }

    private static void ValidarIdentificador(Guid identificador)
    {
        if (identificador == Guid.Empty)
        {
            throw new DomainException(SagaOrdemServicoErrorMessages.IdentificadorObrigatorio);
        }
    }

    private static void ValidarVersao(int versao)
    {
        if (versao <= 0)
        {
            throw new DomainException(SagaOrdemServicoErrorMessages.VersaoInvalida);
        }
    }
}
