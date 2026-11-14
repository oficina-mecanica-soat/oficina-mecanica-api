using System.Text.Json;
using OficinaMecanica.Application.GestaoOrdemServico.Interfaces;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens.Contratos;

namespace OficinaMecanica.Application.GestaoOrdemServico.OrdemServicoUseCases.ProcessarResultadoPreparacao;

public sealed class ProcessarResultadoPreparacaoUseCase
{
    private readonly IRecebimentoMensagemRepository _recebimentoMensagemRepository;
    private readonly ISagaIntegracaoRepository _sagaIntegracaoRepository;
    private readonly TimeProvider _relogio;

    public ProcessarResultadoPreparacaoUseCase(IRecebimentoMensagemRepository recebimentos, ISagaIntegracaoRepository sagas, TimeProvider relogio)
    {
        _recebimentoMensagemRepository = recebimentos;
        _sagaIntegracaoRepository = sagas;
        _relogio = relogio;
    }

    public async Task ExecuteAsync(string json, CancellationToken cancellationToken = default)
    {
        var mensagem = JsonContrato.LerEnvelope(json);
        object dados;
        int versao;
        switch (mensagem.Tipo)
        {
            case "ExecucaoPreparada":
                var preparada = JsonContrato.Ler<ExecucaoPreparadaDados>(mensagem.Dados.GetRawText());
                if (preparada.PreparacaoId == Guid.Empty || preparada.Diagnostico is null || preparada.Diagnostico.Servicos is null ||
                    preparada.Diagnostico.Servicos.Any(s => s is null) || preparada.ItensReservados is null || preparada.ItensReservados.Any(i => i is null))
                {
                    throw new ContratoMensagemException("PREPARACAO_INVALIDA");
                }
                dados = preparada with
                {
                    ItensReservados = preparada.ItensReservados.OrderBy(i => i.ItemId).ToArray(),
                    Diagnostico = preparada.Diagnostico with
                    {
                        Servicos = preparada.Diagnostico.Servicos.OrderBy(s => s.ServicoId).ToArray()
                    }
                };
                versao = preparada.Versao;
                break;
            case "PreparacaoRecusada":
                var recusada = JsonContrato.Ler<PreparacaoRecusadaDados>(mensagem.Dados.GetRawText());
                if (string.IsNullOrWhiteSpace(recusada.CodigoMotivo))
                {
                    throw new ContratoMensagemException("MOTIVO_INVALIDO");
                }
                dados = recusada;
                versao = recusada.Versao;
                break;
            default:
                throw new ContratoMensagemException("TIPO_DESCONHECIDO");
        }
        if (versao <= 0)
        {
            throw new ContratoMensagemException("VERSAO_INVALIDA");
        }
        var chave = $"{mensagem.Tipo}:{mensagem.OrdemServicoId:D}:{versao}";
        var hash = JsonContrato.Hash(mensagem, dados);
        await _recebimentoMensagemRepository.ExecutarEmTransacaoAsync(async cancellationTokenTransacao =>
        {
            // Valida a origem também em reentregas; causationId não integra o hash de negócio.
            var saga = await _sagaIntegracaoRepository.ObterAsync(mensagem.SagaId, mensagem.OrdemServicoId, cancellationTokenTransacao)
                ?? throw new ContratoMensagemException("SAGA_NAO_ENCONTRADA");
            var comando = await _sagaIntegracaoRepository.ObterComandoAsync("PrepararExecucao", mensagem.OrdemServicoId, versao, cancellationTokenTransacao)
                ?? throw new ContratoMensagemException("PREPARACAO_NAO_SOLICITADA");
            if (comando.SagaId != mensagem.SagaId || comando.MensagemId != mensagem.CausationId || comando.CorrelationId != mensagem.CorrelationId)
            {
                throw new ContratoMensagemException("CAUSACAO_DIVERGENTE");
            }
            if (!await _recebimentoMensagemRepository.VerificarRepeticaoAsync(mensagem.MensagemId, chave, hash, cancellationTokenTransacao))
            {
                if (dados is ExecucaoPreparadaDados resultado)
                {
                    var solicitado = JsonContrato.Ler<PrepararExecucaoDados>(comando.Dados.GetRawText());
                    if (resultado.Diagnostico.VeiculoId != solicitado.VeiculoId || resultado.Diagnostico.MecanicoId != solicitado.MecanicoId ||
                        !resultado.Diagnostico.Servicos.SequenceEqual(solicitado.Servicos.OrderBy(s => s.ServicoId)) ||
                        !resultado.ItensReservados.SequenceEqual(solicitado.Itens.OrderBy(i => i.ItemId)))
                    {
                        throw new ContratoMensagemException("SNAPSHOT_DIVERGENTE");
                    }
                    var snapshot = await _sagaIntegracaoRepository.ObterSnapshotAsync(saga, cancellationTokenTransacao)
                        ?? throw new ContratoMensagemException("SNAPSHOT_NAO_ENCONTRADO");
                    if (snapshot.Versao != saga.VersaoOrcamento)
                    {
                        throw new ContratoMensagemException("VERSAO_INVALIDA");
                    }
                    saga.RegistrarPreparacao(resultado.PreparacaoId, versao);
                    await _sagaIntegracaoRepository.AdicionarComandoAsync(new(Guid.CreateVersion7(), "GerarOrcamento", 1, saga.Id, saga.OrdemServicoId,
                        mensagem.CorrelationId, mensagem.MensagemId, _relogio.GetUtcNow(),
                        JsonSerializer.SerializeToElement(snapshot, JsonContrato.Options), ContextoTrace.Filtrar(mensagem.TraceContext)),
                        saga.VersaoOrcamento, "OrcamentoPagamento", cancellationTokenTransacao);
                }
                else
                {
                    saga.RegistrarRecusaPreparacao(versao);
                }
            }
            await _recebimentoMensagemRepository.RegistrarAsync(mensagem.MensagemId, chave, hash, cancellationTokenTransacao);
        }, cancellationToken);
    }
}
