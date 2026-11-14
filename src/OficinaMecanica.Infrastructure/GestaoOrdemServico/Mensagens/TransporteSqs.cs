using System.Net;
using Amazon.SQS;
using Amazon.SQS.Model;
using OficinaMecanica.Application.GestaoOrdemServico.Interfaces;
using OficinaMecanica.Application.GestaoOrdemServico.Mensagens;

namespace OficinaMecanica.Infrastructure.GestaoOrdemServico.Mensagens;

public sealed class TransporteSqs : ITransporteMensagem
{
    private readonly IAmazonSQS _client;
    private readonly string _filaExecucaoUrl;
    private readonly string _filaOrcamentoPagamentoUrl;

    public TransporteSqs(IAmazonSQS client, string filaExecucaoUrl, string filaOrcamentoPagamentoUrl)
    {
        ArgumentNullException.ThrowIfNull(client);
        ContratoFilaSqs.ValidarUrl(filaExecucaoUrl);
        ContratoFilaSqs.ValidarUrl(filaOrcamentoPagamentoUrl);
        if (new Uri(filaExecucaoUrl) == new Uri(filaOrcamentoPagamentoUrl))
        {
            throw new ArgumentException("Os destinatários devem possuir filas distintas.");
        }
        _client = client;
        _filaExecucaoUrl = filaExecucaoUrl;
        _filaOrcamentoPagamentoUrl = filaOrcamentoPagamentoUrl;
    }

    public async Task PublicarAsync(string destino, string corpo, CancellationToken cancellationToken)
    {
        var fila = destino switch
        {
            "Execucao" => _filaExecucaoUrl,
            "OrcamentoPagamento" => _filaOrcamentoPagamentoUrl,
            _ => throw new ContratoMensagemException("DESTINO_COMANDO_INVALIDO")
        };
        var mensagem = JsonContrato.LerEnvelope(corpo);
        var resposta = await _client.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = fila,
            MessageBody = JsonContrato.Serializar(mensagem),
            MessageGroupId = mensagem.OrdemServicoId.ToString("D"),
            MessageDeduplicationId = mensagem.MensagemId.ToString("D")
        }, cancellationToken);
        if (resposta.HttpStatusCode != HttpStatusCode.OK || string.IsNullOrWhiteSpace(resposta.MessageId))
        {
            throw new InvalidOperationException("Publicação SQS sem confirmação válida; a Outbox deve repetir o envio.");
        }
    }
}
