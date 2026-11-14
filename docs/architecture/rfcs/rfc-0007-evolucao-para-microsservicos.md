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

## Organização da integração e padrões de mensageria

Mensageria é um mecanismo de integração entre os contextos existentes. `Mensagens` não é outro bounded context, agregado ou entidade de negócio. A separação em serviços preserva a organização interna por Domain, Application, Infrastructure e API.

Os padrões adotados são **Transactional Outbox** e **consumidor idempotente**. Neste projeto, **Inbox** nomeia a persistência dos recebimentos usada na idempotência; não é uma fila adicional nem um novo serviço. Essas referências definem responsabilidades e garantias, não uma estrutura universal de diretórios.

| Componente | Responsabilidade e localização |
| --- | --- |
| Repository de agregado | Interface no **Domain/Interfaces** do contexto; implementação na **Infrastructure/Repositories** correspondente. `ISagaOrdemServicoRepository` persiste/consulta a Saga; não serializa mensagens nem publica eventos. `IOrcamentoRepository` permanece no Domain de Orçamento/Pagamento. |
| Caso de uso | Em **Application/...UseCases**, conforme a organização existente. Coordena a operação, chama o domínio e registra os efeitos necessários; não acessa SQS, banco de outro serviço ou SDK de provedor. |
| Inbox | Contrato técnico `IInbox` na Application e implementação SQL `SqlInbox` na Infrastructure/Mensagens. Confere mensagem/operação/hash, registra recebimentos e delimita a transação local de processamento. Não contém regra de aprovação, estoque ou estado da Saga. |
| Outbox | Contrato técnico `IOutbox` na Application e implementação `SqlOutbox` na Infrastructure/Mensagens. Grava mensagens pendentes junto dos efeitos locais. A publicação é responsabilidade separada de `PublicadorOutbox`, por uma abstração de transporte. |
| Contratos de mensagens | DTOs do envelope e dos comandos/eventos em **Application/Mensagens** — dentro do contexto na API — com payloads em **Mensagens/Contratos**. Representam o protocolo v1, sem virar entidades do Domain ou compartilhar assemblies entre serviços. Leitura JSON, hash e filtragem de trace são suporte técnico do contrato, sem regras de negócio. |
| Snapshots e correlação | Consulta do snapshot da própria OS, armazenamento do snapshot congelado e armazenamento do contexto de integração são portas técnicas específicas da Application, com implementações na Infrastructure. Não são repositories de agregados e não compartilham a interface da Outbox. |
| Mapeamentos EF | Em **Infrastructure/.../Configurations**, separados dos registros persistidos e dos repositories. Os registros SQL de Inbox/Outbox são metadados técnicos, sem classificação como novas entidades DDD. |

### Fluxo e limite da transação

1. Validar envelope, tipo, versão e dados pelo contrato v1. Contexto técnico não determina a regra de negócio.
2. Na transação local, conferir a Inbox; uma repetição compatível não repete os efeitos, e uma divergência não sobrescreve o processamento anterior.
3. Executar o caso de uso, consultar o repository do próprio agregado e aplicar suas regras de domínio.
4. Persistir **estado local + recebimento na Inbox + mensagens na Outbox** no mesmo banco/transação. Falha reverte os três; recebimento não equivale a confirmação antecipada da mensagem na fila.
5. Após o commit, o publicador envia a mensagem persistida pela abstração de transporte. Falha ou resposta incerta preserva a mensagem para repetição com o mesmo identificador e conteúdo.

A Outbox não promete entrega exatamente uma vez: reentregas são previstas, por isso o destinatário também precisa de idempotência. No consumidor SQS, a exclusão/confirmação só pode ocorrer após o processamento confirmado. Snapshot congelado, causação, correlação e versões de negócio permanecem sujeitos às regras abaixo.

Esta organização não comprova implantação ou funcionamento do transporte SQS. A implementação e o aceite distribuído são acompanhados no backlog; validação por arquivos locais não substitui essas provas.

Referências: [AWS — Transactional outbox pattern](https://docs.aws.amazon.com/prescriptive-guidance/latest/cloud-design-patterns/transactional-outbox.html), [Microsoft — Infrastructure persistence layer](https://learn.microsoft.com/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-design) e [Microsoft — DDD-oriented microservice](https://learn.microsoft.com/dotnet/standard/microservices-architecture/microservice-ddd-cqrs-patterns/ddd-oriented-microservice).

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

### Cobrança e confirmação financeira

`CriarCobranca.dados = { orcamentoId, versao }` é emitido pela OS somente após processar `ExecucaoFinalizada`. Não é uma operação HTTP pública. O serviço Orçamento/Pagamento obtém cliente e valor do orçamento aprovado em seu próprio banco; não aceita esses valores no comando.

Os resultados `CobrancaCriada`, `PagamentoConfirmado` e `PagamentoRecusado` usam `dados = { pagamentoId, orcamentoId, versao, valor }`. `pagamentoId` é o UUID do agregado local, diferente do identificador numérico do provedor. O valor é decimal em BRL, congelado na cobrança. OS verifica o comando original, Saga, correlação, orçamento, versão e valor. `CobrancaCriada` preserva a causação do comando; os resultados financeiros usam a operação de conferência como causação. A criação do checkout não confirma pagamento, e uma recusa não compensa o trabalho físico nem impede uma aprovação posterior.

Antes de criar a preferência externa, Orçamento/Pagamento confirma a intenção local e o estado `CriacaoEmAndamento`. A chamada HTTP acontece fora da transação SQL. A preferência utiliza o UUID local como `external_reference`; repetições pesquisam essa referência. Como a API de preferências não oferece neste contrato uma garantia documentada de idempotência por header, uma criação incerta não autoriza outro POST automático. Consulta sem resultado, resultado ambíguo ou falha após a intenção exigem conciliação; não se inventa confirmação nem se libera uma segunda cobrança. Uma falha anterior à tentativa, como configuração ausente na consulta inicial, mantém a intenção `Solicitado`, permitindo retomada após corrigir a configuração.

A notificação HTTP exige assinatura HMAC e coerência do identificador entre query e corpo. Ela não é prova de pagamento: o caso de uso consulta o recurso no provedor e confere identidade, referência, moeda e valor. Somente o estado aprovado confirma o agregado; autorização, pendência e cancelamento não significam pagamento. Confirmação, deduplicação e Outbox são atômicas. Recusas e pendências tardias não regridem um pagamento confirmado; estorno, contestação e estado desconhecido exigem conciliação manual, sem rollback automático da execução física.

`GET /api/v1/pagamentos/{pagamentoId}` consulta somente o banco próprio e aplica JWT e restrição ao cliente proprietário. `POST /api/v1/pagamentos/notificacoes/mercado-pago` dispensa JWT, mas exige a assinatura do provedor; responde 200 somente após a conferência e o commit locais. Indisponibilidade externa responde 503 para permitir reentrega. Segue a [documentação de notificações do Checkout Pro](https://www.mercadopago.com.br/developers/pt/docs/checkout-pro-preferences/payment-notifications). Testes locais usam um provedor substituto e não comprovam recebimento real de Webhooks, credenciais ou implantação.

### Snapshot e compensação de orçamento

Detalhamento dos dados mínimos para os produtores e consumidores de OS e Orçamento/Pagamento:

- `GerarOrcamento.dados = { versao, clienteId, servicos: [{ servicoId, servicoCatalogoId, descricao, valor }], itens: [{ itemId, descricao, quantidade, valorUnitario }] }`. `itemId` é a referência ao catálogo de peças/insumos; o consumidor a mapeia para `PecaInsumoCatalogoId`. Cada serviço representa uma instância da OS, com quantidade implícita de um. Preservar os preços congelados, sem consultar ou recalcular preços no catálogo de outro serviço.
- A OS permite lançamentos distintos da mesma peça, inclusive com preços diferentes. O snapshot de orçamento preserva essas linhas e seus valores, sem média, agrupamento ou nova consulta de preço. O consumidor não usa `itemId` como chave única das linhas do orçamento. A identidade técnica de persistência dessas linhas não altera os identificadores do catálogo ou o contrato de transporte.
- `CancelarOrcamento.dados` contém `motivo` e `orcamentoId`, ou `versao` quando endereçado por OS do envelope. Pode conter ambos; nesse caso devem identificar o mesmo orçamento/versão e a mesma OS/Saga.
- `OrcamentoCancelado.dados = { orcamentoId, versao }`. O identificador é nulo se o cancelamento precedeu a geração. O consumidor confirma a proibição de gerar essa OS/versão, sem inventar orçamento ou excluir histórico. Geração tardia compatível é recebida sem reabrir a versão cancelada.
- Repetir operação + OS + versão exige o mesmo conteúdo de negócio e Saga. Reutilizar `mensagemId` com outra operação/conteúdo é conflito; outro ID de transporte não permite alterar a operação. Contexto de trace e tentativa de entrega não geram novo efeito.
- O resultado de geração preserva a causação do comando original; OS também confere o total contra o snapshot que solicitou. Recebimento, transição e novas mensagens são confirmados em uma transação local. Falha antes do commit não confirma processamento.

O transporte por arquivos utilizado nas provas locais não emula FIFO, DLQ, IAM ou visibilidade do SQS. Publicação/consumo em AWS, ativação nas rotas de OS e corte do ownership continuam sujeitos à integração e à janela autorizada; estes testes não concluem o fluxo distribuído implantado.

### Composição dos workers SQS

OS e Orçamento/Pagamento mantêm `ProcessamentoRemoto:Habilitado=false` por padrão. O host somente cria clientes e workers quando a implantação oficial habilita o modo remoto. A conta (`ContaEsperada`), a `Regiao` e as URLs FIFO são injetadas pela esteira, a partir dos contratos publicados pelos owners da infraestrutura. OS recebe `FilaEntradaUrl`, `FilaExecucaoUrl` e `FilaOrcamentoPagamentoUrl`; Orçamento/Pagamento recebe `FilaEntradaUrl` e `FilaResultadosOsUrl`. As filas de cada composição são distintas, da mesma conta e região. A fonte de credenciais e a verificação STS da `LabRole` seguem a implementação acadêmica do Execução, sem fallback para credenciais locais.

`IProcessadorMensagem` é uma porta técnica da Application. O caso de uso de entrada valida o envelope e encaminha somente os tipos conhecidos aos casos de uso existentes; não decide transições, valores ou compensações. O consumidor SQS fica na Infrastructure, confere `MessageGroupId=ordemServicoId` e `MessageDeduplicationId=mensagemId`, e exclui a entrada somente após a conclusão do processamento. Processamento pendente de conciliação não confirma a entrada.

Os loops de consumo e publicação usam escopos de DI independentes por operação, sem reusar contexto SQL após falha. Long polling de 20 segundos não bloqueia a Outbox. A visibilidade inicial é de 60 segundos, com renovação a cada 20; uma falha de renovação cancela o processamento e não confirma a entrada. O cliente SQS usa timeout de 30 segundos e nenhuma repetição interna do SDK, preservando o controle de falhas e novas tentativas no worker/Outbox. Respostas sem confirmação HTTP válida não equivalem a fila vazia, envio concluído ou exclusão bem-sucedida. Esses controles seguem as referências de [visibilidade](https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/working-with-visibility-timeouts.html) e [recebimento/exclusão](https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/step-receive-delete-message.html) do SQS; a configuração de redrive/DLQ e a prova operacional pertencem à implantação.

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

### Proposta AUD-007 — snapshot persistível e rejeição técnica

Proposta para revisão de Gabriel e Geovanna, anterior à implementação. O tamanho do envelope recebido não comprova que o snapshot cabe na persistência: a serialização JSON dos documentos pode expandir caracteres Unicode e repetir dados nos registros de preparação, resultado e outbox. O [limite físico do DynamoDB](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/Constraints.html) é 400 KiB por item, incluindo nomes e valores de atributos; não apenas o campo de conteúdo.

O Execução deve validar a representação final dos itens antes de submeter qualquer escrita da transação, usando a mesma serialização da persistência e contabilizando chaves, revisão e conteúdo. Também deve verificar os documentos derivados do snapshot na preparação e sua utilização no ciclo, evitando aceitar uma preparação cujo crescimento previsível impeça autorização/finalização. A guarda no ponto de commit permanece para todas as transições. O detalhamento deve reutilizar os documentos/casos de uso existentes, sem nova biblioteca compartilhada ou consulta a banco alheio.

Não truncar descrições, remover serviços/itens, arredondar valores ou modificar o hash para fazer um documento caber. Esta proposta não escolhe um novo máximo arbitrário de caracteres, serviços ou bytes do envelope; qualquer restrição adicional de entrada precisa ser alinhada com o produtor OS antes do corte. Preservar limites transacionais já existentes. A OS continua dona da construção/correção do snapshot; o Execução, dos documentos e da verificação de persistibilidade.

Documento não persistível deve ser classificado como rejeição técnica permanente, distinta de indisponibilidade/concorrência. Proposta de código interno: `SNAPSHOT_NAO_PERSISTIVEL`, sem novo campo/tipo de evento no envelope v1. Não tratar como estoque insuficiente, não emitir `PreparacaoRecusada` ou compensação fictícia e não gravar reserva, recebimento ou outbox parcial. Não repetir a mesma transação em loop interno para esse erro. Falhas transitórias continuam com o tratamento existente.

Sem commit, o consumidor não confirma nem apaga a mensagem. Mantém a política FIFO/redrive existente, com tentativas limitadas pelo `maxReceiveCount` e encaminhamento à DLQ; não altera a fila nem cria repetição ilimitada. A ocorrência deve ser identificável por código/IDs, sem registrar o corpo. Corrigir o conteúdo é responsabilidade do produtor, respeitando nova identidade quando o payload mudar e as regras de versão/idempotência. O procedimento de reenvio da DLQ não corrige mensagens e continua exigindo contrato válido e commit compatível antes da remoção. Não confundir o código interno com um contrato de erro público HTTP já aprovado.

Aceite da implementação: reproduzir o caso Unicode do AUD-007; testar itens finais imediatamente abaixo/acima do limite e crescimento dos documentos do ciclo; demonstrar rejeição antes de qualquer escrita/alteração de saldo, ausência de ACK e classificação permanente; preservar mensagens pequenas, hashes, estoque e repetição transitória. Testes locais não comprovam IAM/SQS reais. O PR deste texto não implementa a guarda, altera API/Execução ou encerra AUD-007; implementação e integração permanecem em PRs próprios, somente para `develop` nesta rodada, com promoções pendentes de confirmação de Gabriel.

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

Mercado Pago será integrado em ambiente de teste com credenciais próprias. Criar cobrança vinculada à OS e versão, persistir intenção, referência local e chave idempotente antes da chamada; o identificador retornado pelo provedor só é vinculado após validar a resposta. Em resposta incerta consultar o provedor antes de repetir. Notificação não é prova de pagamento: validar autenticidade conforme API escolhida, consultar o pagamento no provedor e conferir referência, valor, moeda e estado. Duplicatas não confirmam duas vezes. Escolher e validar o produto de checkout no primeiro spike de integração, mantendo o contrato interno acima. Nenhuma cobrança real faz parte do teste.

### Adaptador Mercado Pago — spike de Checkout Pro

O spike de 14/11 usa Checkout Pro pela API de Preferências; aceite do produto exige prova posterior com contas/credenciais de teste. O adaptador fica na Infrastructure/Pagamentos, atrás de `IProvedorPagamento` na Application. Os DTOs do provedor e seus nomes JSON permanecem na Infrastructure; respostas da porta usam contratos próprios. `Pagamentos` agrupa essa funcionalidade dentro do serviço Orçamento/Pagamento e não declara outro bounded context. A composição fica desabilitada por padrão.

| Operação do adaptador | API externa e limite |
| --- | --- |
| Criar checkout | `POST /checkout/preferences`, com referência local UUID em `external_reference`, valor BRL e `notification_url` HTTPS. Retornar somente endereço sandbox. Não assumir suporte a `X-Idempotency-Key` nesse endpoint: a garantia documentada para Payments não se transfere automaticamente a Preferências. |
| Reconciliar criação | `GET /checkout/preferences/search` pela referência e consulta do detalhe por ID. Ausência não dispara novo POST; múltiplos resultados são ambiguidade, sem escolha arbitrária. A política de recuperação pertence ao caso de uso que possui a intenção persistida. |
| Consultar pagamento | `GET /v1/payments/{id}`. Exigir o ID solicitado, recebedor configurado, referência UUID, valor válido, BRL e `live_mode=false`. A resposta não confirma o estado local: o caso de uso ainda precisa confrontá-la com o valor e a versão da cobrança persistida. Autorizado, pendente e estados desconhecidos não equivalem a aprovado. |

O transporte usa destino HTTPS fixo, timeout finito e não segue redirecionamentos ou repete automaticamente a criação. Falha de rede, timeout ou resposta inválida de criação permanecem resultados incertos; não representam recusa financeira. Cancelamento do chamador também não prova ausência de criação remota. Não colocar a chamada HTTP dentro de uma transação SQL aberta.

O validador de notificações usa `HMAC-SHA256` com a chave de Webhooks e o manifesto `id:{data.id};request-id:{x-request-id};ts:{ts};`, comparando os hashes em tempo constante. Exige os campos necessários ao evento de pagamento. A API futura deverá conferir consistência entre os IDs de query/body e tipo de evento antes de consultar o recurso; não confiar em valor/status enviados no webhook. Autenticidade e idempotência são verificações distintas: o validador não registra recebimento, não altera cobrança e não emite `PagamentoConfirmado`. Timestamp integra o manifesto; não foi adotada janela de expiração neste adaptador.

O spike não expõe rotas de pagamento/notificação, não cria persistência de cobrança nem liga os comandos financeiros à Saga. Essas partes precisam ser implementadas com Domain, repository de agregado no Domain, casos de uso e Inbox/Outbox conforme a organização definida acima. Testes HTTP com handler local e vetor HMAC não substituem o sandbox ou o aceite integrado. Progresso e credenciais pendentes ficam no backlog canônico.

Referências: [criação de preferências](https://www.mercadopago.com.br/developers/en/reference/online-payments/checkout-pro-preferences/create-preference/post), [pesquisa de preferências](https://www.mercadopago.com.br/developers/en/reference/online-payments/checkout-pro-preferences/search-preferences/get), [referência Checkout Pro](https://www.mercadopago.com.br/developers/pt/reference/online-payments/checkout-pro-preferences/overview) e [assinatura das notificações](https://www.mercadopago.com.br/developers/en/docs/checkout-pro-preferences/additional-settings/optional-notifications).

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
