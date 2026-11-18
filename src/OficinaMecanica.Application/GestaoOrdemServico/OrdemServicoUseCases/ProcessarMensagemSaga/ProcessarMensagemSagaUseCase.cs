using OficinaMecanica.Application.GestaoOrdemServico.Interfaces;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens;
using OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.ProcessarResultadoExecucao;
using OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.ProcessarResultadoOrcamento;
using OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.ProcessarResultadoPagamento;
using OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.ProcessarResultadoPreparacao;

namespace OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.ProcessarMensagemSaga;

public sealed class ProcessarMensagemSagaUseCase : IProcessadorMensagem
{
    private readonly ProcessarResultadoPreparacaoUseCase _processarResultadoPreparacaoUseCase;
    private readonly ProcessarResultadoOrcamentoUseCase _processarResultadoOrcamentoUseCase;
    private readonly ProcessarResultadoExecucaoUseCase _processarResultadoExecucaoUseCase;
    private readonly ProcessarResultadoPagamentoUseCase _processarResultadoPagamentoUseCase;

    public ProcessarMensagemSagaUseCase(
        ProcessarResultadoPreparacaoUseCase processarResultadoPreparacaoUseCase,
        ProcessarResultadoOrcamentoUseCase processarResultadoOrcamentoUseCase,
        ProcessarResultadoExecucaoUseCase processarResultadoExecucaoUseCase,
        ProcessarResultadoPagamentoUseCase processarResultadoPagamentoUseCase)
    {
        _processarResultadoPreparacaoUseCase = processarResultadoPreparacaoUseCase;
        _processarResultadoOrcamentoUseCase = processarResultadoOrcamentoUseCase;
        _processarResultadoExecucaoUseCase = processarResultadoExecucaoUseCase;
        _processarResultadoPagamentoUseCase = processarResultadoPagamentoUseCase;
    }

    public Task ProcessarAsync(string corpo, CancellationToken cancellationToken = default)
    {
        var mensagem = JsonContrato.LerEnvelope(corpo);
        return mensagem.Tipo switch
        {
            "ExecucaoPreparada" or "PreparacaoRecusada" => _processarResultadoPreparacaoUseCase.ExecuteAsync(corpo, cancellationToken),
            "OrcamentoGerado" or "OrcamentoDecidido" or "OrcamentoCancelado" or "PreparacaoCancelada" =>
                _processarResultadoOrcamentoUseCase.ExecuteAsync(corpo, cancellationToken),
            "ExecucaoIniciada" or "ExecucaoFinalizada" => _processarResultadoExecucaoUseCase.ExecuteAsync(corpo, cancellationToken),
            "CobrancaCriada" or "PagamentoConfirmado" or "PagamentoRecusado" => _processarResultadoPagamentoUseCase.ExecuteAsync(corpo, cancellationToken),
            _ => throw new ContratoMensagemException("TIPO_DESCONHECIDO")
        };
    }
}
