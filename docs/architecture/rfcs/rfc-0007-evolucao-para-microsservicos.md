# RFC-0007 — Evolução para microsserviços

## Status

Contrato de implementação definido em 09/11/2026 conforme ADR-0024, ADR-0025 e ADR-0026. Os itens inicialmente abertos abaixo são resolvidos pelas especificações seguintes. Mudanças incompatíveis exigem revisão deste RFC antes de implementação.

## Contexto

O enunciado da Fase 4 exige ao menos três microsserviços independentes, repositório, infraestrutura e banco por serviço, SQL e NoSQL, mensageria, Saga com compensação, testes e deploy em Kubernetes. A aplicação existente é um monólito modular com contextos de Atendimento, Administrativo, Gestão de Estoque e Gestão de Ordem de Serviço.

## Direção acordada

- Evoluir a aplicação para microsserviços, preservando as regras de domínio e avaliando a sucessão da ADR-0001.
- Utilizar DynamoDB e SQS; avaliar SNS apenas quando um evento precisar de múltiplos consumidores. Verificar as permissões de criação e operação no AWS Academy antes do provisionamento.
- Preservar a infraestrutura e as esteiras existentes como referência, observando independência e ownership de cada serviço.
- Manter bancos isolados por serviço, com ao menos um SQL e um NoSQL. Nenhum serviço acessa diretamente o banco de outro.

## Aspectos cobertos pelo contrato abaixo

- Delimitar serviços, responsabilidades, migração de dados, APIs e ownership de bancos sem perder os fluxos atuais.
- Definir Saga, estados, comandos, eventos, compensações, falhas, repetição e idempotência; validar a integração com Mercado Pago.
- Definir contratos, segurança, correlação, observabilidade, execução local, testes e fluxo completo de demonstração.
- Detalhar implantação e destruição, custos e limites do laboratório; avaliar o impacto sobre autenticação, API Gateway e consumidores existentes.

## Critérios de aceite

- Fronteiras, decisões, contratos e critérios de falha revisados e acordados; ADRs necessárias registradas sem apagar o histórico.
- Backlog dividido por dependências e responsáveis; cada serviço com CI/CD independente, testes, cobertura, documentação e evidências exigidas pelo enunciado.

## Contrato de mensagens v1

Envelope JSON: `mensagemId` (UUID), `tipo`, `versao` (1), `sagaId`, `ordemServicoId`, `correlationId`, `causationId`, `ocorridoEm` (UTC ISO-8601) e `dados`. IDs de domínio são UUIDs; valores monetários usam decimal BRL, nunca float. Não transportar JWT, CPF/CNPJ ou credenciais. Propagar também o contexto de trace suportado pelo Datadog. A chave de idempotência de negócio é operação + OS + versão de negócio, além de `mensagemId`; a versão do envelope não compõe essa versão de negócio.

| Origem → destino | Tipo e dados mínimos | Resultado para OS |
| --- | --- | --- |
| OS → Execução | PrepararExecucao: versao, filialId, veiculoId, mecanicoId, itens [{itemId, quantidade}], servicos [{servicoId, servicoCatalogoId, descricao, valor}] | ExecucaoPreparada: preparacaoId, versao, itens reservados e diagnóstico; ou PreparacaoRecusada: versao, codigoMotivo |
| OS → Orçamento/Pagamento | GerarOrcamento: versao, clienteId, itens e serviços com quantidade e valor unitário congelados | OrcamentoGerado: orcamentoId, versao, valorTotal |
| Orçamento/Pagamento → OS | OrcamentoDecidido: orcamentoId, versao, decisao (Aprovado/Recusado) | OS valida versão e estado antes de avançar |
| OS → Execução | AutorizarExecucao: preparacaoId, versaoOrcamento | ExecucaoIniciada; depois ExecucaoFinalizada com finalizadoEm |
| OS → Orçamento/Pagamento | CriarCobranca: orcamentoId, versao | CobrancaCriada; PagamentoConfirmado com pagamentoId e valor; ou PagamentoRecusado |
| OS → Execução | CancelarPreparacao: preparacaoId, motivo | PreparacaoCancelada, inclusive se não houver reserva |
| OS → Orçamento/Pagamento | CancelarOrcamento: orcamentoId ou OS + versao, motivo | OrcamentoCancelado, preservando histórico |

Publicações de resultado usam a fila de OS; comandos usam a fila do destinatário. Cada handler valida tipo/versão e dados; mensagem desconhecida vai para investigação via DLQ, sem processamento permissivo.

### Snapshot e compensação de orçamento

Detalhamento dos dados mínimos para os produtores e consumidores de OS e Orçamento/Pagamento:

- `GerarOrcamento.dados = { versao, clienteId, servicos: [{ servicoId, servicoCatalogoId, descricao, valor }], itens: [{ itemId, descricao, quantidade, valorUnitario }] }`. `itemId` é a referência ao catálogo de peças/insumos; o consumidor a mapeia para `PecaInsumoCatalogoId`. Cada serviço representa uma instância da OS, com quantidade implícita de um. Preservar os preços congelados, sem consultar ou recalcular preços no catálogo de outro serviço.
- `CancelarOrcamento.dados` contém `motivo` e `orcamentoId`, ou `versao` quando endereçado por OS do envelope. Pode conter ambos; nesse caso devem identificar o mesmo orçamento/versão e a mesma OS/Saga.
- `OrcamentoCancelado.dados = { orcamentoId, versao }`. O identificador é nulo se o cancelamento precedeu a geração. O consumidor confirma a proibição de gerar essa OS/versão, sem inventar orçamento ou excluir histórico. Geração tardia compatível é recebida sem reabrir a versão cancelada.
- Repetir operação + OS + versão exige o mesmo conteúdo de negócio e Saga. Reutilizar `mensagemId` com outra operação/conteúdo é conflito; outro ID de transporte não permite alterar a operação. Contexto de trace e tentativa de entrega não geram novo efeito.
- O resultado de geração preserva a causação do comando original; OS também confere o total contra o snapshot que solicitou. Recebimento, transição e novas mensagens são confirmados em uma transação local. Falha antes do commit não confirma processamento.

O transporte por arquivos utilizado nas provas locais não emula FIFO, DLQ, IAM ou visibilidade do SQS. Publicação/consumo em AWS, ativação nas rotas de OS e corte do ownership continuam sujeitos à integração e à janela autorizada; estes testes não concluem o fluxo distribuído implantado.

### Contexto técnico de rastreamento — F4-11

Alinhamento explícito com Gabriel em 10/11/2026: o envelope v1 pode incluir `traceContext` opcional, fora de `dados`. Neste incremento, sua única chave permitida é `traceparent`, no formato W3C versão `00`, com trace-id hexadecimal minúsculo de 32 caracteres, parent-id de 16, flags de 2 e IDs não nulos. Exemplo: `{ "traceparent": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01" }`. Campo ausente/nulo mantém o contrato anterior. Não incluir baggage, tracestate, JWT, documento, credenciais ou cabeçalhos livres.

O contexto é metadado técnico: não participa da chave/hash de negócio ou da decisão de estoque/Saga. Campos não permitidos e valores de traceparent inválidos são descartados do carrier, sem fabricar IDs; o formato estrutural JSON do envelope continua obrigatório. Reentrega com outro contexto não cria nova operação nem substitui o contexto do resultado/outbox já persistido.

O resultado captura o contexto técnico ativo e o persiste junto da transação de negócio/outbox; na ausência de um contexto ativo válido, pode preservar somente o contexto de origem validado. Após reinício, o publicador retoma o contexto persistido e preserva mensagemId, agrupamento/deduplicação e payload nas tentativas. O consumidor restaura esse carrier pelo SDK nativo Datadog; não criar framework de eventos ou dependência Datadog no Domain.

As ações manuais podem gerar resultados ligados ao contexto da requisição HTTP atual; correlação funcional (correlationId), Saga e causationId permanecem independentes e preservados. A F4-11 utiliza Datadog nativo, sem adicionar OpenTelemetry. O cabeçalho HTTP `traceparent` validado pode ser preservado como metadado, e `Activity` já fornecida pelo runtime HTTP pode ser lida como carrier local quando o profiler não estiver presente; isso não representa exportação ou span Datadog validado.

Produtores/consumidores estritos precisam aceitar o campo opcional antes do corte integrado. Gabriel adapta Execução; Geovanna adapta OS/cobrança em suas frentes. O merge deste contrato não comprova propagação entre os três serviços, profiler carregado, ingestão Datadog, métricas, painéis ou alertas. A prova exige a janela autorizada; AWS permanece bloqueada.

### Identidades dos serviços no snapshot

Alinhamento explícito com Gabriel em 10/11/2026: preservar as duas identidades do modelo DDD. `servicoId` identifica a instância de `Servico` da OS; `servicoCatalogoId` identifica sua referência ao catálogo, mantido no serviço de OS. Ambos são UUIDs obrigatórios e não vazios. O Execução preserva esses IDs, descrição e valor do snapshot; não gera outro ID para a mesma instância nem consulta o banco/catálogo de OS. O resultado `ExecucaoPreparada.diagnostico.servicos` preserva os dois campos.

`servicoId` deve ser único dentro da preparação. Duas instâncias distintas podem referenciar o mesmo `servicoCatalogoId`; a identidade usada nas ações individuais do mecânico é `servicoId`, não a referência ao catálogo. Mudar o catálogo de uma instância altera o snapshot e conflita com a repetição da mesma preparação/versão; não remapear IDs silenciosamente.

Este ajuste explicita um campo obrigatório no contrato v1 antes da integração real; produtor OS e consumidores precisam ser atualizados de forma coordenada na F4-06/F4-10. Não há compatibilidade permissiva para snapshots sem `servicoCatalogoId`, nem substituição por `servicoId`. Preparações antigas persistidas sem esse campo exigem reconciliação antes de autorizar, sem fabricar vínculo de catálogo; o ajuste não executa migração ou descarte de dados. O merge do documento não comprova implantação dos consumidores.

Os resultados do ciclo físico usam `ExecucaoIniciada.dados = { preparacaoId, versaoOrcamento }` e `ExecucaoFinalizada.dados = { preparacaoId, versaoOrcamento, finalizadoEm }`, além do envelope v1 com OS/Saga/correlação. `finalizadoEm` é UTC. As ações manuais mantêm um identificador de operação estável nas repetições, usado como `causationId` do evento que produzirem; não são novos tipos de comando SQS. Contrato HTTP e autenticação dessas ações permanecem na integração correspondente.

### Versão de contrato e versão da preparação

- `envelope.versao` identifica o formato da mensagem e permanece `1` neste contrato. Não é contador de tentativas, versão de orçamento ou versão de preparação.
- `PrepararExecucao.dados.versao` é um inteiro positivo que identifica o snapshot de preparação solicitado para a OS. O serviço de OS é dono desse número: inicia em `1` e persiste a versão junto do estado da Saga e do comando na outbox.
- Reenvio, timeout ou resposta incerta preservam a versão e o snapshot. Uma nova tentativa de transporte não cria uma nova preparação de negócio. `ExecucaoPreparada.dados.versao` e `PreparacaoRecusada.dados.versao` devolvem exatamente a versão solicitada, permitindo a OS rejeitar resultados antigos.
- A chave de idempotência de preparação é `PrepararExecucao + ordemServicoId + dados.versao`. Repetir essa chave com os mesmos dados não reserva novamente, mesmo com outro `mensagemId`; mantém a preparação e o resultado persistidos. A mesma chave com dados diferentes é conflito de contrato e exige investigação, sem sobrescrever o snapshot ou movimentar estoque.
- Cada `preparacaoId` identifica uma única combinação de OS e versão. `CancelarPreparacao` continua usando `preparacaoId` e `motivo`: o serviço de Execução resolve OS/versão pelo registro persistido e usa `CancelarPreparacao + ordemServicoId + versão persistida` como chave de negócio. Não é necessário acrescentar versão ao comando de cancelamento.
- Uma versão cancelada não pode ser reaberta por reenvio. Se o fluxo exigir outro snapshot, OS solicita uma versão maior, mantendo o histórico; a nova reserva só pode ocorrer após confirmação da liberação da preparação anterior e antes do início físico da execução. Não há duas preparações ativas da mesma OS. Mensagens antigas não substituem uma versão mais recente.
- `versaoOrcamento` continua identificando o orçamento aprovado. Sua associação à preparação deve ser validada pelo estado da Saga; não presumir que seus números são iguais. Versões técnicas usadas no controle de concorrência do banco também são separadas das versões de contrato e negócio.

Exemplo: uma segunda preparação usa `envelope.versao = 1` e `dados.versao = 2`. Seu reenvio mantém ambos os valores. O resultado devolve `dados.versao = 2`; cancelar usa o `preparacaoId` desse resultado, sem alterar o formato do envelope.

### Autorização e início físico manual

Esclarecimento alinhado com Gabriel em 10/11/2026, preservando o modelo DDD e o fluxo atual de ações do mecânico:

- `AutorizarExecucao` libera a preparação para o trabalho após a aprovação do orçamento; não inicia serviços automaticamente e não significa que o reparo físico começou. A associação entre preparação e orçamento aprovado continua validada pela Saga, sem pressupor igualdade entre suas versões.
- O mecânico inicia cada serviço manualmente. `Servico` preserva as transições `Pendente` → `EmExecucao` → `Finalizado`; iniciar exige estado pendente e finalizar exige estado em execução. Autorização não substitui essas ações.
- O primeiro início manual, após autorização válida, registra o início físico e `ExecucaoIniciada` na mesma transação local com a deduplicação/outbox. Iniciar outros serviços não gera outro evento de início da execução. Repetição não altera a data original nem reabre serviço concluído.
- A finalização física também permanece explícita, como no modelo atual: exige todos os serviços finalizados e ao menos um serviço. Somente após confirmar atomicamente a baixa das reservas, o estado final e a outbox, publicar `ExecucaoFinalizada` com `finalizadoEm`. Finalizar um serviço individual não equivale a finalizar toda a execução nem a confirmar pagamento.
- Antes do início físico, inclusive quando já autorizada mas ainda sem serviço iniciado, o cancelamento pode liberar reservas conforme o fluxo da Saga. A disputa entre cancelamento e primeiro início deve comparar o mesmo estado/revisão: ambos não podem confirmar. Se cancelamento vencer, um início posterior falha; se início vencer, o cancelamento não libera estoque nem confirma compensação fictícia.
- Após o início físico, falha técnica exige recuperação e preserva as reservas/execução; não desfaz reparo por reentrega de `CancelarPreparacao`. Eventos e estado não regridem por mensagens antigas ou duplicadas.

Este esclarecimento não cria outro comando SQS, não muda o envelope v1 e não define novas rotas públicas. Os handlers, persistência, autenticação e encaminhamento das ações do mecânico precisam ser implementados e integrados nas tarefas correspondentes. O fluxo público atual da API permanece até o corte coordenado; registrar autorização não conclui F4-05 nem comprova execução física.

## Estados, compensação e recuperação

1. Abrir OS persiste Recebida e Saga Preparando; emite PrepararExecucao.
2. Execução realiza diagnóstico e reserva atômica; sem saldo, retorna recusa sem reserva parcial.
3. OS solicita orçamento; ao recebê-lo, aguarda decisão. Recusa aciona CancelarPreparacao e CancelarOrcamento. Só registra Saga Compensada após ambas as confirmações.
4. Aprovação da versão atual libera execução. Conclusão registra Finalizada e solicita cobrança.
5. Pagamento confirmado pelo provedor libera Entregar OS; consulta de pagamento é local ao serviço de cobrança, nunca ao banco dele por OS.
6. Falha permanente anterior à execução física aciona compensação. Falha de compensação permanece CompensacaoPendente, com repetição e alerta; não anunciar rollback concluído sem confirmação.

Estado da Saga e versão são persistidos separadamente do enum legado. Aplicar comparação de versão na transição; mensagens antigas/duplicadas não regridem estado. Resultado recebido após cancelamento não reabre execução e deve ser reconciliado.

Filas FIFO: MessageGroupId = ordemServicoId, deduplicação = mensagemId. Visibility timeout inicial 60 s, long polling 20 s, maxReceiveCount 5, retenção 4 dias e DLQ 14 dias; ajustar por medição. Operações longas renovam visibilidade. Outbox com repetição e backoff; expiração técnica e falha permanente geram ocorrência persistida. Reprocessamento de DLQ é operação controlada pela esteira e deve ser idempotente.

### Reenvio individual controlado da DLQ do Execução

Proposta alinhada com Gabriel em 12/11/2026 para F4-04, sujeita à aprovação deste PR e à implementação posterior. Não habilita reprocessamento nem declara validação real na AWS.

O consumidor atual exige `MessageGroupId = ordemServicoId` e `MessageDeduplicationId = mensagemId`. A AWS substitui o identificador de deduplicação ao mover uma mensagem FIFO para a DLQ; por isso, o redrive nativo em lote não é o mecanismo desta proposta. Preservar a validação do consumidor e reenviar individualmente o corpo original, reconstruindo esses dois atributos a partir do envelope. O `MessageId` atribuído pelo SQS não substitui o `mensagemId` de negócio. Referência: [redrive de DLQ FIFO no SQS](https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-configure-dead-letter-queue-redrive.html).

Preparar a operação manual no workflow AWS existente do Execução, separada de apply/destroy, seguindo environments, credenciais, gates e concorrência das esteiras atuais. Execução somente em `development`, com janela AWS explicitamente autorizada e bloqueio compartilhado com deploy/destroy. Push, merge ou promoção não reenviam mensagens; a operação não altera `terraform-action.env`. Não criar outra esteira, pasta de scripts, endpoint público, recurso AWS ou contrato SSM.

O procedimento terá fases explícitas de inspeção, reenvio e confirmação:

1. Resolver fila de entrada, DLQ e tabela do próprio Execução pelos contratos SSM existentes; conferir conta, região, ARNs e associação entre as filas. Não aceitar destino arbitrário nem operar filas de OS/cobrança. As credenciais ficam no runner, sem transferência para Pods ou alteração de IAM.
2. Selecionar uma mensagem pelo `mensagemId` e pelo SHA-256 esperado do corpo original. Limitar cada inspeção a um lote de até dez mensagens, sem varredura ilimitada. Receber da DLQ altera visibilidade e contagem de recebimentos; restaurar prontamente a visibilidade das mensagens não selecionadas, sem excluí-las. Não registrar corpos ou receipts.
3. Reenviar somente envelope e payload aceitos pelo contrato vigente, sem corrigir conteúdo no workflow. Preservar corpo, IDs, versão e contexto técnico originais; reaproveitar a leitura/validação existente da aplicação. Mensagem inválida ou incompatível permanece na DLQ para investigação pelo produtor responsável. Correção do payload exige o fluxo do produtor e uma identidade compatível com as regras de idempotência, sem reutilizar ID com conteúdo divergente.
4. Enviar à fila de entrada com os atributos FIFO de negócio reconstruídos. Registrar separadamente aceitação pelo transporte e confirmação de processamento. O SQS pode aceitar uma repetição sem entregá-la durante a janela de deduplicação de cinco minutos; portanto, sucesso de `SendMessage` não autoriza excluir a cópia da DLQ. Referência: [deduplicação no SQS FIFO](https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/using-messagededuplicationid-property.html).
5. Confirmar por leitura fortemente consistente do registro existente `MENSAGEM#{mensagemId}` / `RECEBIDA` no DynamoDB do Execução, gravado na mesma transação dos efeitos locais. Exigir correspondência do hash de negócio, reutilizando a normalização e o cálculo da aplicação. O SHA-256 do corpo usado na seleção não equivale a esse hash. Um registro compatível já existente comprova o commit local anterior, sem exigir novos efeitos por reentrega; não comprova publicação da outbox, consumo pela OS ou conclusão da Saga.
6. Somente após essa confirmação persistida, receber novamente a cópia selecionada na DLQ, conferir novamente ID e digest do corpo e excluir usando o receipt mais recente. Ausência ou divergência de confirmação, mensagem não localizada, timeout, falha de acesso ou resposta incerta impedem anunciar remoção confirmada. Não apagar outra mensagem nem usar purge/redrive em lote. Referência: [identificadores e receipts do SQS](https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-fifo-queue-message-identifiers.html).

Se houver interrupção após o envio ou resposta incerta na exclusão, repetir a reconciliação com a mesma identidade de negócio. Preservar a cópia enquanto não houver prova do commit; a idempotência existente impede duplicar reserva, baixa ou eventos. A ausência de confirmação ao fim da tentativa significa pendência, não falha de negócio compensada. O reenvio entra na posição atual da fila e não restaura a ordem histórica: continuam valendo versão, estado, compensação e início manual pelo mecânico, sem bypass das regras do consumidor.

Auditar somente ação, conta/ambiente, execução da esteira, IDs, digests e resultados de envio/confirmação/remoção, sem nova tabela de auditoria. Não expor corpo, receipt, credenciais, tokens ou baggage em logs/artifacts. Antes de habilitar a operação, validar localmente seleção, duplicidade, divergência de hash, aceitação sem entrega, commit seguido de interrupção e falha na exclusão. A prova posterior em AWS exige nova janela autorizada e execução isolada do verificador, sem processo auxiliar dentro do Pod da aplicação. A implementação e essa prova permanecem pendentes; este contrato não conclui F4-04.

## HTTP, segurança e pagamentos

Preservar rotas públicas existentes da OS e autorização por papel/cliente. APIs novas usam /api/v1/orcamentos e /api/v1/pagamentos no serviço de cobrança e /api/v1/execucoes no serviço de execução. Atualizar Gateway, OpenAPI e Postman na mesma tarefa de integração. Operações assíncronas retornam 202 com identificador e endpoint de consulta; não devolver sucesso de negócio antes da confirmação.

### Ações manuais HTTP do Execução

Contrato alinhado explicitamente com Gabriel em 10/11/2026. Base do recurso: `/api/v1/execucoes/{ordemServicoId}/preparacoes/{preparacaoId}`. Os IDs são UUIDs, preservados da OS/preparação. Mantém os sufixos e os perfis usados pelas ações equivalentes de `OrdensServicoController`:

| Método e sufixo da base | Operação | Acesso |
| --- | --- | --- |
| `GET` na base | Consultar o estado local da preparação/execução, inclusive antes da autorização ou após cancelamento | Administrador ou Mecanico |
| `POST /servicos/{servicoId}/iniciar-execucao` | Iniciar manualmente um serviço após autorização | Administrador ou Mecanico |
| `POST /servicos/{servicoId}/finalizar` | Finalizar um serviço individual | Administrador ou Mecanico |
| `POST /finalizar` | Finalizar explicitamente a execução, com baixa atômica | Administrador ou Mecanico |

O corpo dos POSTs é somente `{ "operacaoId": "UUID" }`, obrigatório e não vazio, estável na repetição da mesma ação. Reutilizar esse ID em outra ação/payload conflita; IDs de OS/preparação/serviço vêm da rota e não podem ser sobrescritos pelo corpo. Não há rota HTTP de autorização por orçamento: `AutorizarExecucao` continua sendo comando da Saga.

Após confirmar a transação local, POST retorna `202` com `operacaoId`, `resultado` (preparacaoId, versaoOrcamento e estado da execução) e cabeçalho `Location` apontando ao GET da base. Falha antes do commit não retorna `202`. A confirmação é local: não afirma recebimento do evento pela OS, estado final da Saga, pagamento ou entrega. Repetição idempotente retorna o estado local atual, preservando datas e eventos anteriores.

GET retorna `200` com `ordemServicoId`, `preparacaoId`, `versaoPreparacao`, `versaoOrcamento` (nula antes da autorização), `reservada`, `cancelada` e `execucao` (nula antes da autorização). O ciclo contém serviços com IDs da instância/catálogo, valor, status e datas UTC, mais `finalizadaEm` da execução. Status de serviço usa os nomes `Pendente`, `EmExecucao`, `Finalizado`. `reservada` registra a confirmação histórica da reserva, não o saldo reservado atual. O GET não é recibo de processamento pela OS e não expõe envelope de autorização, hashes ou credenciais.

Reutilizar o contrato de erros da API (`mensagem`, `tipo`, `erros` opcional): `400` para corpo/IDs inválidos, `401` para JWT ausente/inválido, `403` para perfil sem acesso, `404` para preparação ausente ou incompatível com a OS da rota, `409` para conflito de operação/persistência e `422` para regra de negócio. Falha técnica retorna `500` genérico, sem detalhes do SDK ou segredos. A autenticação/role antecede a resolução da persistência; Cliente/Atendente não acionam essas rotas.

O host preserva o modo local como padrão: sem modo remoto configurado, estas rotas retornam `503` para identidade autorizada quando o processamento local não está habilitado em Development, antes de resolver persistência. A composição remota é opt-in, conforme exceção acadêmica da ADR-0025: valida configuração de conta/região/tabela/filas e identidade STS antes de registrar os clientes e o worker remoto. Configuração inválida, modos simultâneos ou falha de identidade impedem a inicialização, sem fallback local. Somente após compor o modo remoto o filtro admite as mesmas ações HTTP nesse modo; não altera perfis, payloads ou confirmações após commit. A configuração padrão/manifests atuais não o habilitam. Isso não comprova permissões de escrita, consumo FIFO/DLQ ou execução real no Pod. Healthcheck permanece público e verifica somente HTTP.

Gabriel prepara as entradas do Execução, testes e seus contratos OpenAPI/Postman locais. Integração de emissores/audiences/chaves JWT, autorização compartilhada, produtor OS, roteamento Gateway e retirada/encaminhamento das rotas antigas permanecem coordenados com Geovanna na F4-06/F4-09/F4-10. Não ampliar emissores confiáveis nem presumir que o JWT de Cliente autorize ações do mecânico. Não alterar ou publicar o Gateway como efeito deste preparo; o contrato público legado permanece até o corte integrado. AWS continua bloqueada, sem deploy por aprovação de código.

Decisão de orçamento exige identidade autorizada e versão atual. Consulta de cliente para a Lambda é privada, autenticada por identidade técnica segregada, e não pode ficar acessível por rota pública do Gateway. Mantém o contrato público do RFC-0001.

Mercado Pago será integrado em ambiente de teste com credenciais próprias. Criar cobrança vinculada à OS e versão, persistir referência do provedor e chave idempotente antes da chamada; em resposta incerta consultar o provedor antes de repetir. Notificação não é prova de pagamento: validar autenticidade conforme API escolhida, consultar o pagamento no provedor e conferir referência, valor, moeda e estado. Duplicatas não confirmam duas vezes. Escolher e validar o produto de checkout no primeiro spike de integração, mantendo o contrato interno acima. Nenhuma cobrança real faz parte do teste.

## Infraestrutura, execução local e CI/CD

Alinhamento com Gabriel em 11/11/2026: a identidade do Execução no AWS Academy segue a exceção acadêmica da ADR-0025, utilizando explicitamente a identidade existente dos nodes (`LabRole`), sem isolamento IAM por aplicação. A configuração e o acesso real dentro do Pod ainda precisam de implementação e validação; simulação de permissões não comprova o runtime. Não copiar credenciais do runner para o workload ou ativar fallback silencioso. A concessão não muda contratos de mensagens/HTTP, ownership de bancos/filas ou permissões das outras frentes e não autoriza deploy por merge.

Cada serviço mantém Dockerfile, manifests, workload Terraform, CI/CD e README próprios; dados e filas seguem ADR-0025. Workloads no EKS existente, com validação JWT, health checks, requests/limits e observabilidade conforme API. Nenhum serviço acessa banco alheio. Recursos reais somente por esteira.

Ambiente local: SQL Server em Docker conforme projeto atual, DynamoDB Local e adaptador de mensageria substituível para testes de processo. Testes de integração com SQS real usam ambiente de teste provisionado pela esteira; não afirmar equivalência completa com execução local. Cada serviço deve rodar e testar isoladamente.

Replicar os workflows atuais, preservando detecção de escopo, validação de branches, quality gate, promoção por PR, publicação de imagem e validação de rollout. Adaptar exclusivamente o que pertence ao serviço. Não copiar o acoplamento de banco do monólito nem criar deploy manual.

## Evidências de aceite

- Fluxo BDD completo: OS → diagnóstico/reserva → orçamento aprovado → execução → pagamento de teste confirmado → entrega.
- Recusa libera saldo exatamente uma vez; falha ao gerar orçamento compensa reserva; compensação interrompida retoma após reinício.
- Mensagens duplicadas, resultado tardio, reinício entre commit e envio e falha do provedor têm testes.
- Cada serviço tem cobertura mínima de 80%, análise Sonar ou equivalente no CI e evidências reais no README.
- Traces/logs correlacionados, painel de erros da Saga, DLQ e health dos três serviços demonstrados.
- Vídeo de até 15 minutos, PDF com participantes, links, arquitetura e justificativas.
