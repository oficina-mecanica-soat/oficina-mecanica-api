# Validação integrada e demonstração

Este roteiro orienta o aceite dos três serviços. Não constitui evidência de execução: registrar resultados reais e links no [backlog canônico](../projeto/backlog-tecnico.md). Contratos e decisões: [RFC-0007](../architecture/rfcs/rfc-0007-evolucao-para-microsservicos.md), ADR-0024 a ADR-0026 e [RFC de observabilidade](../architecture/rfcs/rfc-0006-observabilidade-centralizada-com-datadog.md).

## Antes da janela

- Separar credenciais temporárias do laboratório, credenciais de teste do Mercado Pago e acesso ao Datadog. Não exportar valores em collections, relatórios, prints ou vídeo.
- Conferir conta, região e eventual drift após reset; state preservado não comprova existência dos recursos. Credencial expirada ou AccessDenied não significa recurso ausente.
- Usar os Environments e as esteiras oficiais, respeitando dependências e guardrails. Nenhum comando local deve provisionar, reiniciar ou destruir recursos como atalho.
- Os quatro emissores/validadores devem usar a chave JWT compartilhada atual de `development`. Tokens antigos precisam ser emitidos novamente. Não trocar a chave para resolver um teste sem investigar issuer, audience, perfil e expiração.
- Preparar cliente ativo, veículo associado, mecânico, catálogo, filial e saldo de peças no serviço responsável. Não inserir dados diretamente no banco alheio nem usar o estoque legado da OS para abastecer Execução. Registrar como os dados foram preparados pelo mecanismo suportado pelo serviço.
- Obter a URL pública atual do Gateway pelos outputs/contratos oficiais; não reutilizar hostname de Load Balancer de uma fase anterior.

## Ordem de ativação coordenada

Somente após autorização explícita da janela: infraestrutura compartilhada → bancos/registry/filas próprios → workloads → autenticação privada → corte do Gateway. As dependências entre contratos podem exigir uma primeira aplicação sem os workers e outra com os destinos publicados. Usar os gates dos workflows, não supor uma ordem linear que ignore dependências circulares de configuração.

Antes do corte, verificar `status/api-aplicacao`, `status/execucao` e `status/orcamento-pagamento-aplicacao`, flags distribuídas dos workloads, Lambda sem conexão SQL quando usa consulta privada e listeners privados saudáveis. `ready` de fila não equivale a `ready` da aplicação. O Gateway só deve encaminhar o fluxo distribuído quando seus guardrails aprovarem todos os responsáveis.

Para cada serviço, guardar link do CI e CD, SHA da imagem, cobertura e análise de qualidade. Confirmar workload, banco/credencial próprios, saúde HTTP, versão e identidade AWS observada dentro do Pod. A LabRole compartilhada é exceção acadêmica expressa; não comprova isolamento IAM entre Pods.

## Postman e dados da jornada

Importar a [collection distribuída](../postman/oficina-mecanica-distribuida.postman_collection.json) e um [environment](postman.md). Usar variáveis locais para senhas/tokens. O endpoint público é o mesmo Gateway para os três serviços; em execução local, preencher os endereços individuais e não presumir que Docker Compose legado inicialize todos os serviços e workers.

Executar uma requisição por vez: a collection não faz polling nem espera automaticamente pela mensageria. `202` confirma aceitação, não a conclusão de todo o processo. Repetir consultas com prazo operacional definido e registrar erro quando não houver convergência; não clicar em ações seguintes antes do estado necessário.

`servicoId` identifica o serviço dentro da OS; `servicoCatalogoId` identifica o catálogo. Para múltiplos serviços, repetir início/finalização para cada instância e usar `operacaoId` próprio por ação. Repetições da mesma ação conservam seu ID; nova ação/jornada deve limpar os IDs locais da anterior.

## Cenários de aceite

### BDD automatizado local

A suíte `tests/OficinaMecanica.FluxoDistribuido.IntegrationTests` executa preparação, orçamento/decisão, ações físicas, pagamento e entrega usando código real dos três participantes, dois bancos SQL isolados e DynamoDB Local. Outros cenários cobrem recusa compensada, estoque insuficiente, cancelamento público repetido antes do início e recusa do cancelamento após início físico confirmado. A compensação solicitada permanece pendente até receber os resultados dos dois participantes; início físico preserva a reserva e o orçamento aprovado. Cada mensagem é reapresentada com o mesmo JSON e os processadores usam novos escopos; ações manuais também são repetidas com o mesmo ID.

O job `🔗 04.1 · Validar jornada distribuída` no CI da API fixa os commits de Execução e Orçamento/Pagamento e exige dezenove cenários aprovados sem ignorados. Atualizar esses commits é uma alteração explícita da matriz de compatibilidade. Antes do aceite, comparar os SHAs da matriz com as versões implantadas; aprovação dessa combinação não comprova outra combinação de versões. As referências a outros projetos existem somente nessa suíte de aceitação; não são dependências de produção. Ela é executada separadamente da solution da OS para preservar a autonomia dos testes unitários, a cobertura por serviço e os builds independentes.

Localmente, com Docker disponível e clones irmãos nas versões do job:

```powershell
dotnet test tests/OficinaMecanica.FluxoDistribuido.IntegrationTests/OficinaMecanica.FluxoDistribuido.IntegrationTests.csproj --logger trx
```

As propriedades MSBuild `RepositorioExecucao` e `RepositorioOrcamentoPagamento` permitem apontar os clones existentes em outro local. A fixture prepara dados sintéticos por jornada: cliente ativo, veículo, mecânico, serviço de R$ 150,00, peça de R$ 25,00, filial e saldo inicial de dez unidades; reserva três unidades ou solicita onze no caso de insuficiência. Dados, bancos, tabela e diretório temporário são exclusivos do teste. Não reutilizar essa fixture como carga direta da AWS.

Os cenários de aprovação, recusa e estoque insuficiente também executam com falha injetada após cada primeiro envio local, antes da confirmação da Outbox. Cada publicador é recriado em novo escopo; o teste confere tentativa persistida, lease liberada e próximo envio agendado. Um relógio controlado somente dos publicadores permite a retomada sem esperas artificiais; os prazos de negócio não são alterados. O reenvio deve preservar ID e JSON, terminar com outboxes drenadas e manter reserva/baixa/cobrança únicas. Isso é erro controlado de transporte com releitura persistida, não encerramento ou reinício real de processo.

Quatro jornadas adicionais exercitam traceContext ausente, válido, com campos proibidos e com traceparent inválido. A fixture injeta somente metadados técnicos sintéticos na mensagem inicial persistida; não modifica dados de negócio nem representa captura HTTP/profiler. Consumidores e publicadores reais recebem/republicam envelopes; a reentrega mantém o ID e troca o contexto técnico. O teste confere filtragem, trace-id quando há origem válida, corpos das outboxes SQL e primeira pendência DynamoDB antes/depois da reentrega, retomada e efeitos únicos até a entrega. Contexto de resultado já persistido não deve ser substituído. Sem origem válida, não se exige ausência de um novo contexto gerado legitimamente pelo tracer; exige-se que qualquer carrier publicado seja válido e limitado a traceparent.

Seis jornadas financeiras exercitam respostas pendente, recusada e desconhecida, indisponibilidade na consulta, moeda divergente e valor divergente. Cada consulta é repetida; nenhuma dessas condições confirma pagamento na OS ou permite entrega, nem desfaz o consumo físico já concluído. Após corrigir a resposta do provedor substituto, a confirmação válida repetida deve permitir entrega com cobrança única e saldo preservado. Esses cenários não validam HMAC/webhook, HTTP do provedor, políticas de retry em rede ou credenciais Mercado Pago.

O cenário de interrupção real compila a API Execução fixada na matriz e a inicia como processo separado, com worker por arquivos e DynamoDB Local. Bloqueia a saída com um arquivo temporário para observar reserva e falha de publicação persistidas; encerra o processo à força, reapresenta o mesmo comando, remove o bloqueio e inicia outro PID sobre a mesma tabela/diretório. Exige o ID de resultado original recebido uma única vez pela OS, reserva 7/3 preservada e compensação pelos dois participantes com saldo 10/0, sem cobrança. O worker filho usa relógio real e pode aguardar a expiração natural da lease; seu publicador/consumidor não são executados pelo processo pai nesse cenário. PID e ID técnico ficam no TRX, sem logs brutos ou JWT. A referência à API existe somente no projeto de aceitação, sem importar sua assembly para o teste ou criar dependências de produção. Isso comprova kill/reinício do processo local do Execução com a reserva já persistida, não queda do banco/node nem interrupção no meio do commit.

O transporte é o adaptador local por arquivos e o provedor financeiro é um substituto de teste. A suíte não valida HTTP/JWT integrado, assinatura de webhook, SQS/DLQ/IAM, crash de processo, rede AWS ou Mercado Pago real; essas provas continuam nos cenários abaixo. Reabrir escopos comprova releitura persistida, não reinício de processo. A cobertura de cada serviço continua sendo medida por sua própria esteira, sem somar esta suíte aos percentuais de produção.

| Cenário | Ação e resultado esperado | Evidência mínima |
| --- | --- | --- |
| Autorização | Sem JWT: `401` em consulta protegida; JWT Cliente: `/me` permitido e rota administrativa `403`. Cliente diferente não consulta OS/orçamento/pagamento alheios. | Método, rota sem dados pessoais, status e perfil; nunca o JWT. |
| Abertura | POST de cadastro com filial e dados válidos retorna `202`, ID da OS e Location. Consulta mostra Saga; Execução prepara/reserva e OS solicita orçamento. | OS/Saga/preparação/orçamento correlacionados e estados persistidos. |
| Aprovação | Decisão da versão vigente retorna `202`; aguardar autorização no Execução. A aprovação não inicia fisicamente o serviço. | Orçamento aprovado e execução ainda aguardando ação do mecânico. |
| Trabalho físico | Mecânico inicia e finaliza cada serviço; depois finaliza a execução. OS converge para Finalizada e solicita cobrança. | Ações manuais e resultado processado pela OS, sem mutação pelo endpoint legado. |
| Cobrança real de teste | Consultar pagamento, abrir endereço sandbox, pagar com contas de teste compatíveis e aguardar notificação assinada mais consulta ao provedor. | Preferência/pagamento de teste e referência/valor/moeda/recebedor conferidos; nenhuma cobrança real. |
| Entrega | Antes da confirmação financeira: bloqueada. Após `pagamentoConfirmado=true` e OS Finalizada: entrega permitida. | Estado antes/depois; não usar callback falso ou confirmação manual como prova. |
| Recusa | Nova OS preparada; recusar orçamento antes de trabalho físico. Aguardar confirmações de cancelamento dos dois participantes. | Saga Compensada, orçamento cancelado e saldo restaurado uma única vez. |
| Cancelamento | Nova OS com preparação confirmada, antes do início físico: solicitar cancelamento e repetir a mesma intenção. | `202` pendente e convergência após ambas as confirmações; sem compensação antecipada. |
| Estoque insuficiente | Nova OS com necessidade superior ao saldo disponível. | Recusa sem reserva parcial e estado coerente; saldo anterior preservado. |
| Idempotência | Repetir decisão/ação com o mesmo ID e conteúdo; reapresentar resultado pelo mecanismo controlado existente. | Sem segunda reserva, cobrança ou transição. Mesmo ID com conteúdo divergente deve ser rejeitado. |
| Recuperação | Em janela combinada, usar teste automatizado existente ou mecanismo oficial para interromper entre persistência/publicação e retomar. | Outbox retomada, ausência de perda/efeito duplicado. Não afirmar prova de crash apenas por repetir HTTP. |
| Falha de pagamento | Provedor indisponível, pendente ou resposta inválida não confirma pagamento nem libera entrega. | Erro/estado pendente e recuperação segura, sem criação remota repetida às cegas. |
| SQS/DLQ | Conferir consumo real, atributos FIFO, reentrega e DLQ. Reprocessamento somente pelo fluxo controlado existente e com autorização. | Filas/contagens aproximadas, envelope válido e efeito idempotente; não fazer purge para “limpar” evidência. |
| Corte legado | Com token autorizado e modo distribuído, mutações antigas de estoque/execução/orçamento retornam `409`. | Novo responsável funciona; não há dois serviços alterando o mesmo negócio. |

Para cada execução registrar: cenário, data/hora e fuso, SHAs, ambiente/conta, IDs técnicos, esperado, observado, link de evidência e defeito associado quando houver. Remover CPF, e-mail, senha, Authorization, cookies, assinatura de webhook e valores de secrets. Dados sintéticos também não autorizam publicar tokens válidos.

## Observabilidade: o que mostrar e o que falta medir

Reutilizar a plataforma e o [painel Saga versionado no Kubernetes](https://github.com/oficina-mecanica-soat/oficina-mecanica-infra-kubernetes/blob/develop/observability/datadog/saga-dashboard.json). Não instalar outro Agent nem introduzir OpenTelemetry. Importar/alterar recursos reais apenas no fluxo autorizado para a janela; este documento não executa essa configuração.

| Visão | Origem e construção | Como validar |
| --- | --- | --- |
| Saúde dos serviços | APM por `env`, `service`, `version`; latência, taxa de erros e volume HTTP. Complementar com health real e réplicas disponíveis. | Gerar requisições nos três serviços e conferir janela/SHA; réplica disponível não prova negócio saudável. |
| Jornada distribuída | Logs e spans `os.processar/publicar`, `execucao.processar/publicar`, `orcamento_pagamento.processar/publicar`, correlacionados por OS/Saga e contexto de trace. | Abrir uma jornada de sucesso e outra compensada; mostrar passagem entre serviços e outbox. |
| Recuperação e falhas | Painel existente filtra `operation` e `outcome:falha`; verificar se Serilog está em `Properties` ou campos remapeados. | Produzir falha controlada, localizar IDs e mostrar retomada. Tentativas técnicas não são Sagas únicas fracassadas. |
| Kubernetes | Agent/Cluster Agent: CPU, memória, reinícios, réplicas desejadas/disponíveis por namespace/deployment. | Conferir tags reais e métricas sem misturar workloads ou clusters antigos. |
| Filas e DLQs | Integração AWS/SQS: mensagens visíveis/em voo e idade aproximada nas filas próprias. | Conferir nomes/conta/região. Sem integração AWS, painel vazio é lacuna, não fila vazia. |
| Autenticação, Gateway e RDS | Preservar painéis da Fase 3; conferir ingestão Lambda, erros/duração, acesso Gateway, CPU/conexões RDS. | Exibir dados atuais. A instrumentação da Lambda preparada não equivale a ingestão comprovada. |
| Negócio | Volume diário de OS e tempo por status exigem evento de transição único ou fonte persistida com timestamps. | Não usar contagem de `processar/publicar` como OS criada nem inferir duração de estado pela latência do span. Até existir fonte validada, declarar a lacuna da Fase 3. |

Tags de serviço: `oficina-mecanica-api`, `oficina-mecanica-execucao`, `oficina-mecanica-orcamento-pagamento`, `oficina-mecanica-auth-lambda`. Manter `env:development` e a versão real de cada imagem. Não criar dimensão de métrica por OS, Saga, cliente ou mensagem; esses IDs servem para investigação de logs/traces.

O painel versionado também reúne CPU em nanocores, memória em bytes, contador de reinícios e p95 HTTP pela distribuição `trace.aspnet_core.request`. `hits/errors` medem requisições/erros por intervalo, não OS únicas. Os eventos de abertura e entrega já emitidos pela API podem ser investigados no stream de negócio; não substituem uma fonte validada para volume total ou tempo por status. Conferir ingestão e acesso às métricas na janela antes de usar esses gráficos como evidência.

Na abertura distribuída, enviar `X-Correlation-Id` e conferir o mesmo valor na resposta HTTP, no comando persistido e nos resultados dos participantes. O middleware sanitiza o header; sem header válido, o ID seguro gerado deve ser o usado pela Outbox. Isso não substitui a comprovação do contexto de trace e dos IDs Datadog exportados.

Sequência para fechar os alertas da RFC-0006:

1. Conferir ingestão e parsing antes de escrever consultas ou concluir que faltam erros.
2. Medir baseline de saúde, latência, HTTP 5xx, pods, Lambda, RDS e atraso de fila na janela.
3. Configurar monitores de indisponibilidade, erro de processamento, backlog/DLQ e reinícios com escopo do ambiente e limites derivados da medição; registrar janela de avaliação, atraso de ingestão e comportamento sem dados. Não atribuir `0` a ausência de telemetria para deixar o painel verde.
4. Provocar uma falha controlada autorizada, comprovar alerta e recuperação e guardar consulta/configuração e horários. Não disparar mensagens externas ou configurar destinatários sem alinhamento.

Fontes oficiais: [integração SQS](https://docs.datadoghq.com/integrations/amazon-sqs/), [diagnóstico de ausência de dados](https://docs.datadoghq.com/monitors/guide/troubleshooting-no-data/) e [configuração de monitores](https://docs.datadoghq.com/monitors/configuration/). As pendências históricas F3-008/F3-009 continuam rastreadas no backlog; não encerrar itens por existir apenas um JSON de dashboard.

## Roteiro do vídeo — 14 minutos e 30 segundos

Preparar abas, IDs e evidências antes de gravar. Usar cortes para esperas assíncronas, deixando claro o intervalo; não apresentar gravação anterior como execução da versão atual. Mostrar os dois participantes e explicar a divisão de responsabilidades.

| Tempo | Conteúdo e fala orientadora |
| --- | --- |
| 00:00–01:30 | Problema e arquitetura: “Separamos OS, Execução e Orçamento/Pagamento. Cada serviço tem repositório, workload e banco próprios. OS e cobrança usam SQL; Execução usa DynamoDB. Nenhum acessa banco alheio.” Mostrar diagrama final e explicar SQS e exceção acadêmica IAM. |
| 01:30–02:30 | Clean Architecture e Saga: mostrar camadas de um serviço, porta de pagamento e adaptador Infrastructure; explicar orquestração persistida pela OS, Inbox/Outbox e compensação. Microsserviços descrevem a distribuição; Clean Architecture descreve a organização interna. |
| 02:30–06:30 | Fluxo feliz real: autenticar, abrir OS, orçamento/aprovação, ação manual do mecânico, finalização, checkout de teste Mercado Pago, pagamento verificado e entrega. Mostrar consultas dos três responsáveis e IDs da mesma jornada. |
| 06:30–08:30 | Nova OS recusada: mostrar saldo reservado, recusa, compensação dos dois participantes e saldo restaurado. Demonstrar reentrega/idempotência por evidência real, sem afirmar rollback instantâneo de operação física. |
| 08:30–10:30 | CI/CD: abrir execução real de pelo menos um serviço, mostrar build, testes, cobertura >=80%, qualidade e deploy Kubernetes da mesma versão; mostrar PR/checks e proteção. Deploy lógico ou job ignorado não atende à demonstração de implantação real. |
| 10:30–13:30 | Datadog: jornada nos três serviços, correlação, falha/recuperação, latência/erros, saúde Kubernetes e filas/DLQ. Mostrar alerta testado e explicar limites acadêmicos com transparência. |
| 13:30–14:30 | Encerrar com links dos três repositórios, documentação, evidências e justificativas. Conferir áudio, legibilidade e duração total menor que 15 minutos. |

## Conferência do PDF e do envio

Diagrama geral preparado: [SVG vetorial](../architecture/diagrams/distributed-architecture.svg), [PNG](../architecture/diagrams/distributed-architecture.png) e [fonte editável draw.io](../architecture/diagrams/distributed-architecture.drawio). Representa o fluxo distribuído definido pelos contratos vigentes; conferir a correspondência com a implantação observada antes da exportação final do PDF. Os modelos detalhados históricos C4/DDD não foram reescritos por este desenho geral.

- Nome e identificação dos participantes, links públicos dos três microsserviços e infraestrutura relacionada.
- Link YouTube/Vimeo público ou não listado, acessível em janela anônima e com duração de até 15 minutos.
- Diagrama geral da arquitetura final com serviços, bancos próprios, filas, autenticação, Gateway, Kubernetes, Mercado Pago e observabilidade; não reutilizar desenho do monólito como arquitetura final.
- Descrição e justificativa da Saga orquestrada, compensação, limites físicos e escolha das tecnologias/divisão dos serviços.
- Em cada repositório: fonte, Dockerfile/manifests, CI/CD, arquitetura, cobertura real no README e Swagger ou Postman atualizado. A atualização final do README da API é uma etapa de fechamento, não parte deste roteiro.
- Conferir BDD completo, cobertura e qualidade de cada serviço, branch main protegida e links dos runs. Teste de agregado isolado com nome Given/When/Then não substitui fluxo completo entre serviços.
- Guardar o PDF final e comprovante de envio até **17/11/2026 às 23h59 (America/Sao_Paulo)**. Não marcar entrega concluída antes do upload e conferência de acesso.
