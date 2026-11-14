# Backlog técnico

## Execução da Fase 4 — 09 a 17/11/2026

Fonte de arquitetura: ADR-0024 a ADR-0026 e RFC-0007. Esta seção prevalece para o escopo atual; itens históricos abaixo não viram trabalho automático. Responsável indica quem conduz a tarefa com seu Codex. Contratos são compartilhados; alterações incompatíveis devem ser alinhadas antes de implementação.

| ID | Responsável | Dependências | Entrega e aceite | Acompanhamento em 14/11/2026 |
| --- | --- | --- | --- | --- |
| F4-01 | Gabriel | — | Validar ambiente atual via esteiras e Postman; registrar impedimentos e evidências, sem provisionamento direto. | Validações AWS anteriores registradas no README do Execução; endpoints/fluxos isolados não equivalem a aceite completo dos três serviços. Laboratório offline. |
| F4-02 | Geovanna | — | Revisar ADR-0024/25/26 e RFC-0007, conferir enunciado e fechar contratos usados pelas duas frentes. | Decisões-base ADR-0024/25/26 e contratos RFC-0007 aprovados; novos produtores/consumidores devem ser conferidos na integração. |
| F4-03 | Gabriel | F4-01, F4-02 | Criar repositório Execução com AGENTS, Clean Architecture, testes, Dockerfile, CI/CD e governança derivados das referências; CI verde e execução local. | Implementado: repositório, camadas, testes, Dockerfile, manifests, CI/CD e gates. Organização Terraform #59 e limpeza/apresentação do Execução #62 integradas em develop; sem nova implantação nesta retomada. |
| F4-04 | Gabriel | F4-03 | Provisionar DynamoDB, fila de entrada e DLQ pela esteira; validar leitura/escrita/consumo pelo workload e publicar contratos SSM. Sem SNS inicialmente. | Infraestrutura e consumo/publicação pelo workload validados nas janelas anteriores; DLQ e retomada de outbox têm provas com limites registrados. Recuperação de payload corrigido e demais provas remotas pendentes. |
| F4-05 | Gabriel | F4-03, F4-02 | Extrair diagnóstico, estoque, reserva/liberação e execução preservando regras e testes. Garantir reserva atômica, idempotência, outbox e compensação; consumir os contratos v1 do RFC. Pode usar doubles enquanto F4-04 ocorre. | Estoque/reserva/liberação, autorização, início manual, finalização/baixa, HTTP/JWT e outbox implementados. Ciclo com estoque e cancelamento validados isoladamente na AWS; corte integrado ainda pendente. |
| F4-06 | Geovanna | F4-02 | Adaptar OS existente, estados persistidos da Saga, outbox, consumidor de resultados e testes; remover transação entre bancos de serviços. | API #369 integrado em develop: Saga, recebimentos, outbox e produtor de snapshot pela OS, com valores congelados e linhas de peças preservados. 517 testes locais aprovados, cobertura 93,2%. Código de contratos conferido por Gabriel (92f5d1e, sem diferença de produção para 732268c). Incremento integrado em develop no [API #384](https://github.com/oficina-mecanica-soat/oficina-mecanica-api/pull/384): solicitação de preparação pela OS, snapshot persistido antes da espera, recebimento de confirmação/recusa e geração atômica do comando de orçamento. 527 testes locais aprovados, nenhum ignorado, cobertura 93,3%. Ciclo físico/financeiro integrado no [API #387](https://github.com/oficina-mecanica-soat/oficina-mecanica-api/pull/387), com CI aprovado. Worker SQS integrado no [API #388](https://github.com/oficina-mecanica-soat/oficina-mecanica-api/pull/388), com CI aprovado e ainda sem aceite AWS; corte das rotas pendente. |
| F4-07 | Geovanna | F4-02 | Criar serviço Orçamento/Pagamento, CI/CD independente, banco SQL isolado e fila/DLQ pela esteira; snapshot de orçamento e decisão versionada. | Orçamento/Pagamento #1 integrado no develop 7b28ae8: núcleo, mensagens, migrações, Docker e CI derivados da API. Gabriel repetiu 93 testes locais aprovados, incluindo SQL Server descartável; cobertura publicada 94,4%. Proteções ativas/AWS bloqueada segundo a frente responsável. Cobrança e Webhook integrados no #4, com CI aprovado. Worker SQS integrado no [Orçamento/Pagamento #5](https://github.com/oficina-mecanica-soat/oficina-mecanica-orcamento-pagamento/pull/5), com CI aprovado; CD/infraestrutura, ativação remota e aceite integrado pendentes. |
| F4-08 | Geovanna | F4-07 | Validar produto de checkout Mercado Pago com credenciais de teste; implementar cobrança, notificação verificada, consulta ao provedor e idempotência. Bloqueio de credenciais é P0 imediato. | Adaptador Checkout Pro integrado em Orçamento/Pagamento #3 e contrato no API #386. Incremento integrado com CI aprovado no [Orçamento/Pagamento #4](https://github.com/oficina-mecanica-soat/oficina-mecanica-orcamento-pagamento/pull/4): agregado/persistência próprios de pagamento, intenção antes da chamada HTTP, conciliação de criação incerta sem segundo POST, notificação HMAC com consulta ao provedor, deduplicação/Outbox e consulta JWT por cliente. Caso de uso interno e resultados financeiros ligados ao ciclo físico da Saga no [API #387](https://github.com/oficina-mecanica-soat/oficina-mecanica-api/pull/387), integrado com CI aprovado. Credenciais ainda não configuradas, conforme confirmado em 14/11; checkout/Webhook reais de teste, ativação SQS em AWS, corte público e implantação permanecem pendentes. Não concluído. |
| F4-09 | Geovanna | F4-06, F4-07 | Adequar Lambda ao contrato privado de clientes, JWT nos três serviços e roteamento Gateway; testes 401/403 e acesso por cliente. | HTTP/JWT próprios do Execução preparados. Consulta privada integrada nos PRs API #390 e Lambda #46, com CI aprovado e ativação desligada; no modo novo a Lambda não recebe conexão/acesso SQL. Configurar a credencial técnica, validar rede pela esteira, rotear os novos owners no Gateway e efetivar o corte coordenado continuam pendentes. |
| F4-10 | Gabriel | F4-05, F4-06, F4-07 | Integrar fluxo assíncrono, BDD e testes de duplicata, reinício, recusa e compensação; saldo e estados consistentes. Geovanna revisa o contrato e resultados. | Produtores reais de Orçamento/OS e validador do Execução conferidos em verificador com portas da OS simuladas. Autorização/cancelamento, reentrega, causação/trace e versões 2/3 verificados. Regressões integradas em develop no [Execução #63](https://github.com/oficina-mecanica-soat/oficina-mecanica-execucao/pull/63); 334 testes locais do Execução aprovados. Sem prova persistida completa dos três serviços, SQS ou corte HTTP. |
| F4-11 | Gabriel | F4-04, F4-05 | Instrumentar serviço Execução e fluxos distribuídos no Datadog; painéis, logs seguros e falhas da Saga/DLQ observáveis. Geovanna instrumenta OS e cobrança nas respectivas tarefas. | Logs/APM, correlação e contexto persistido na outbox preparados no Execução. Instrumentação nativa dos consumidores/publicadores e captura do contexto na Outbox de OS e Orçamento/Pagamento implementadas localmente: API 627 testes/92,9% e Orçamento/Pagamento 286/93,9%, sem ignorados. Publicado no [Orçamento/Pagamento #6](https://github.com/oficina-mecanica-soat/oficina-mecanica-orcamento-pagamento/pull/6); CI/merge deste incremento ainda pendentes. Ingestão, traces dos três serviços, painel Saga/DLQ/health e recuperação observável ainda pendentes. |
| F4-12 | Ambos | F4-08 a F4-11 | Rodar deploy completo por esteiras; cobertura ≥80% por serviço, Sonar/equivalente e checks verdes; registrar links reais. | CI/deploy independente do Execução e controles operacionais entregues; deploy e aceite integrado dos três serviços ainda pendentes. Promoções retidas em develop. |
| F4-13 | Geovanna | F4-12 | Atualizar diagrama geral final, README, OpenAPI/Postman, roteiro e PDF; Gabriel revisa execução seguindo os documentos. Revisão completa dos modelos DDD/C4 fica em AUD-015. | Acompanhamento consolidado neste backlog (#377/#379). Limpeza de planos/alinhamentos auxiliares integrada no Execução #62; o README recebe apenas correções de referências e remoção de progresso nessa limpeza. Diagrama final, collection integrada, PDF e revisão conjunta ainda pendentes. |
| F4-14 | Ambos | F4-13 | Gravar vídeo ≤15 min com sucesso, compensação, CI/CD e traces; conferir acesso aos repositórios e links; enviar até 17/11 23h59. | Gravação, conferência de links e envio até 17/11 23h59 permanecem pendentes; dependem da prova integrada. |

**Consulta de andamento e descoberta da cobrança — 14/11/2026:** rota aditiva autenticada da OS retorna o estado real da Saga e os identificadores de orçamento/pagamento, sem consultar outro serviço. Cliente só acessa a OS vinculada ao seu veículo; claims ausentes/inválidas são recusadas. A Saga preserva a identidade da cobrança antes da confirmação; conhecer `pagamentoId` não equivale a quitação e uma confirmação de outra identidade é rejeitada. Não altera schema nem substitui o status HTTP legado/corte público. Validação local: 610 testes aprovados, nenhum ignorado, cobertura de linhas 92,9%, incluindo HTTP/JWT, SQL e regressão da confirmação após a criação. Integrado no [API #389](https://github.com/oficina-mecanica-soat/oficina-mecanica-api/pull/389), após CI remoto aprovado; sem implantação.

**Consulta privada de clientes — 14/11/2026:** API e Auth Lambda implementam o contrato privado do RFC-0007, preservando autenticação pública e JWT. No modo privado a Lambda usa somente HTTP e não recebe conexão ou regras SQL; credencial técnica segregada, timeout e falhas sem fallback. Ativação padrão falsa; esteiras preparadas com resolução do NLB existente e validação após apply. Validação local: 620 testes API (93,0% de linhas), 72 Lambda (95,2%), seis planos Terraform simulados na API, três na Lambda e geração de tfvars exercitada sem AWS. Integrado após CI remoto aprovado nos PRs [API #390](https://github.com/oficina-mecanica-soat/oficina-mecanica-api/pull/390) e [Lambda #46](https://github.com/oficina-mecanica-soat/oficina-mecanica-auth-lambda/pull/46); CDs conferidos com deploy e promoção pulados. Corte, configuração do secret e prova de rede real continuam pendentes.

**Workers SQS — 14/11/2026:** adaptadores, roteamento de mensagens e loops independentes de consumo/publicação preparados para OS e Orçamento/Pagamento, derivados de Execução. Habilitação padrão falsa; fonte explícita LabRole, conferência STS e validação de conta/região/filas. SDK substituído + SQL real verificam commit antes de ack/envio, renovação/cancelamento de visibilidade, falha de consumo sem bloquear Outbox e recuperação de envio incerto. Validação local: API com 583 testes aprovados e cobertura 92,8%; Orçamento/Pagamento com 279 testes aprovados e cobertura 94,1%, sem ignorados. Infraestrutura, configuração de workload e prova real FIFO/DLQ/IAM ainda necessárias; nenhum recurso AWS foi alterado. Integrado após CI aprovado nos PRs [API #388](https://github.com/oficina-mecanica-soat/oficina-mecanica-api/pull/388) e [Orçamento/Pagamento #5](https://github.com/oficina-mecanica-soat/oficina-mecanica-orcamento-pagamento/pull/5). CD da API conferido com deploy/promoção pulados pelo bloqueio AWS.

**Ciclo físico e financeiro — 14/11/2026:** handlers da OS processam início/finalização física, solicitam cobrança atomicamente após a finalização e conferem os resultados financeiros contra o comando, orçamento, valor e contexto originais. Reentregas não duplicam cobrança nem regridem pagamento. Validação local: API com 542 testes aprovados e 93,2% de cobertura; Orçamento/Pagamento #4 com 239 testes aprovados e 95,1%, sem testes ignorados. Restore/build, formatação e gate de estrutura verificados. Prova adicional com dois bancos SQL, Outboxes/publicadores reais e arquivos chegou a `PagamentoConfirmado` na aprovação e `Compensada` na recusa. O ciclo físico foi simulado com contratos reais de Execução; o provedor foi substituto local. Não comprova DynamoDB/estoque, SQS, credenciais, Webhook real, deploy ou corte das rotas públicas. Orçamento/Pagamento #4 e API #387 foram integrados após CI remoto aprovado; AWS permanece bloqueada e o arquivo de ação Terraform da API permanece em `destroy`.

**Integração local integrada no [API #384](https://github.com/oficina-mecanica-soat/oficina-mecanica-api/pull/384):** `feat/integracao-preparacao-ordens-servico` conecta preparação à geração do orçamento com snapshot persistido antes da espera. Conferência com dois bancos SQL descartáveis, publicadores/outboxes reais e arquivos locais confirmou aprovação e recusa; preparação e cancelamento do Execução foram simulados usando DTOs e validador reais, sem executar estoque/DynamoDB. Entrada por caso de uso interno com filial explícita; rotas HTTP atuais preservadas. Migração adiciona coluna opcional à Saga, sem reescrever dados históricos. `TERRAFORM_ACTION=destroy` e AWS bloqueada permanecem por solicitação da equipe. CD do Orçamento/Pagamento, SQS e corte público continuam pendentes.

**Correção de organização e estilo integrada em 14/11:** reaproximar a integração dos padrões da API original: interfaces dos repositories de agregados no Domain, processamento em UseCases, portas técnicas IInbox/IOutbox na Application e implementações SQL na Infrastructure. A interface da Saga foi separada dos contratos de snapshots e Outbox; contexto de integração do orçamento foi separado da publicação. Mapeamentos EF permanecem em Configurations. O [mapa de responsabilidades e o fluxo transacional](../architecture/rfcs/rfc-0007-evolucao-para-microsservicos.md#organização-da-integração-e-padrões-de-mensageria) estão na RFC-0007. Mensagens permanece como agrupamento técnico de contratos/comunicação, sem criar bounded context ou entidade de negócio. Contratos em arquivos próprios e blocos de código expandidos; preservados campos JSON, regras e schemas. Regressão local: API 527 testes e Orçamento/Pagamento 93, nenhum ignorado; coberturas 93,1% e 93,2%, respectivamente. Sem diferenças de modelo EF ou deploy. Alterações integradas em develop nos PRs [API #385](https://github.com/oficina-mecanica-soat/oficina-mecanica-api/pull/385) e [Orçamento/Pagamento #2](https://github.com/oficina-mecanica-soat/oficina-mecanica-orcamento-pagamento/pull/2); não alteram o repositório Execução.

O acompanhamento acima incorpora os PRs integrados até 13/11: controles AUD-001/002/003/005 e correções AUD-006/007 estão detalhados nas tabelas seguintes. Em 13/11, Orçamento/Pagamento foi publicado no develop (7b28ae8) e a Saga API #369 foi integrada durante a validação (head 732268c). O código foi lido/testado sem AWS; publicação e suítes individuais não concluem a integração. A sequência de entrega e os critérios permanecem neste backlog e na [RFC-0007](../architecture/rfcs/rfc-0007-evolucao-para-microsservicos.md), sem plano particular no serviço.

**Manutenção do backlog:** em cada entrega, atualizar esta fonte com progresso, evidência/PR, dependências e aceite restante. PR aberto não equivale a merge, CI aprovado não equivale a deploy, e prova isolada não conclui a integração. Não registrar progresso, conversas, alinhamentos ou listas paralelas nos READMEs ou em planos particulares dos serviços. READMEs devem receber as conclusões finais quando as tarefas forem encerradas; o README da API não deve ser alterado nesta frente.

### Triagem da auditoria — 12/11/2026

**Decisão de escopo: continuar as implementações da entrega; não executar a auditoria inteira antes de 17/11.** Esta triagem é o ponto de acompanhamento, sem necessidade de consultar o relatório completo. Para os achados auditados, os horizontes abaixo substituem a priorização inicial da auditoria. Não reabre decisões aceitas nem autoriza operações AWS.

A revisão local dos snapshots de `develop` confirmou 806 testes .NET aprovados, sem falhas ou ignorados: API 450, Auth Lambda 39 e Execução 317. Também passaram 63 testes operacionais, validação Terraform dos sete repositórios e 12 planos com providers simulados. Os 33 testes HTTP antes impedidos na nuvem passaram na suíte completa de 57. Isso permite aproveitar a base do Gabriel, mas não comprova Saga integrada, pagamento, cobertura atual ou implantação AWS. Foram identificados defeitos novos e legados; não atribuir tudo ao trabalho recente.

#### Obrigatório até 17/11 — caminho crítico

As tarefas existentes continuam sendo a fonte de responsabilidade e aceite; não criar uma segunda lista para as mesmas entregas.

| Referência | Responsável | Próximo resultado necessário | Status atualizado em 13/11 |
| --- | --- | --- | --- |
| F4-06 | Geovanna | Saga persistida na OS, outbox e consumo de resultados; aprovação versionada antes de autorizar execução e pagamento confirmado antes de entregar. Compensação real, sem transação entre bancos. | Em andamento: orçamento e ciclo físico/financeiro integrados em código; workers SQS preparados e validados localmente. Corte público e aceite implantado pendentes. |
| F4-07 e F4-08 | Geovanna | Terceiro serviço Orçamento/Pagamento com banco próprio, CI/CD e Mercado Pago em teste. Antecipar prova de credenciais/checkout; callback manual não substitui integração com o provedor. | Orçamento, cobrança e Webhook integrados com CI aprovado; workers SQS preparados. CD/infra, ativação remota e prova Mercado Pago com credenciais permanecem pendentes. |
| F4-05 e F4-09 | Gabriel / Geovanna, conforme divisão original | Preservar diagnóstico, reserva, início físico manual e baixa; fazer corte de rotas e autenticação sem banco alheio nem dois owners para a mesma mutação. | Execução isolada validada; corte integrado pendente |
| F4-10 a F4-12 | Ambos, conforme divisão original | Fluxo dos três serviços, BDD completo, compensação, duplicata/reinício, cobertura por serviço e análise de qualidade; deploy pelas esteiras e observabilidade distribuída demonstrados. Preservar gate de 90% existente. | A fazer na integração; não confundir testes locais com aceite completo |
| F4-13 e F4-14 | Ambos, conforme divisão original | Documentação e collection do fluxo real, diagrama, vídeo de até 15 minutos e PDF; conferir acesso e enviar até 17/11 às 23h59. | A fazer após prova integrada; preparar roteiro em paralelo |

Atualização de acompanhamento em 13/11/2026: os status AUD abaixo incorporam os PRs efetivamente integrados em `develop`. Os registros de 12/11 e suas contagens permanecem históricos. O laboratório está offline e os sete repositórios mantêm `AWS_DEPLOY_ENABLED=false`; nenhuma nova implantação ou promoção para `release/main` foi autorizada.

#### Controles antes da operação afetada — sem bloquear desenvolvimento local

Progresso local em 12/11: 513 testes aprovados na API (93% de cobertura de linhas) e 89 no Orçamento/Pagamento (94,5%), sem falhas ou ignorados. Prova adicional com transporte por arquivos e bancos SQL separados confirmou aprovação e recusa; os comandos foram aceitos pelo validador real do Execução, mas a confirmação de cancelamento da preparação foi simulada. Testes cobrem reentrega, conteúdo divergente, cancelamento antecipado, rollback, lease e interrupção após envio. Não houve push/deploy, execução física de estoque, prova SQS ou pagamento; não tratar este resultado como conclusão da entrega distribuída. O aviso legado SSH.NET da API permanece rastreado em AUD-008.

São correções focadas para conduzir as próximas operações com segurança. Não exigir reestruturação completa das esteiras ou da infraestrutura antes de programar os serviços.

| ID | Prioridade / horizonte | Responsável | Item e critério de aceite | Status |
| --- | --- | --- | --- | --- |
| `AUD-001` | P0 / antes da próxima operação AWS pelos caminhos afetados | Gabriel | Uniformizar bloqueio `AWS_DEPLOY_ENABLED` em Auth Lambda, Gateway e entrada manual Datadog, incluindo caller/callee quando existentes. Ausente/false bloqueia mutação; true continua sujeito a ref, environment e janela autorizada. Testes negativos locais. | Implementado em develop; testes locais/CI aprovados. Lambda #43, Gateway #38 e Kubernetes #76; AWS bloqueada, sem nova prova operacional |
| `AUD-002` | P0 / antes do próximo destroy RDS | Gabriel | Cumprir RFC-0005: bloquear destroy com consumidores API/Lambda ativos, conferindo antes do plano e da aplicação. Ausência deve ser comprovada; AccessDenied bloqueia; recuperação preserva state e plano destrutivo. | Implementado no RDS #53; testes/CI aprovados e destroy da janela anterior concluído. Repetir conferência de consumidores em cada novo destroy |
| `AUD-003` | P0 / antes de promover conteúdo pendente | Gabriel | Corrigir promoção cumulativa: código sem deploy confirmado seguido de commit documental não pode liberar promoção. Testar essa sequência e a de código já implantado seguido de docs; manter PR e merge humano. Não duplicar o problema de ancestralidade de PIPE-005. | Implementado nos sete repositórios; testes/CI aprovados. API #375, Execução #60, VPC #59, RDS #55, Lambda #44, Gateway #39 e Kubernetes #77. Sem promoção autorizada nesta rodada |
| `AUD-004` | P0 / antes de executar código não confiável com acesso aos caches | Geovanna, com apoio Gabriel | Definir contenção do state sensível em cache Actions: mapear quem pode restaurar/executar código de PR, restringir a fronteira de confiança ou adotar armazenamento protegido viável. Não extrair valores como prova, não considerar aprovação de environment suficiente e não apagar o único state. Migração durável fica em AUD-011/OPS-011. Não houve incidente comprovado. | A fazer |
| `AUD-005` | P1 / antes de rerun operacional que dependa desses artefatos | Gabriel | Alinhar plano/pacote/cache por `run_id` e `run_attempt`: VPC/Kubernetes no plano e cache; Lambda/Gateway nos artefatos restantes. Download usa a tentativa correta e falha parcial preserva state. | Implementado em develop; testes locais/CI aprovados. VPC #60, Kubernetes #78, Lambda #45 e Gateway #40; RDS já isolava tentativa. Rerun AWS real não executado neste ajuste |

#### Correções pequenas — encaixar na frente afetada antes da entrega

P1 significa fazer se não ameaçar o caminho crítico. Se o problema impedir um cenário obrigatório, passa a bloquear aquela integração e deve ser corrigido antes de seu aceite. Não alterar contratos ou limites de negócio unilateralmente para contornar um teste.

| ID | Prioridade / horizonte | Responsável | Item e critério de aceite | Status |
| --- | --- | --- | --- | --- |
| `AUD-006` | P1 / junto da F4-05 e integração | Gabriel | Corrigir decimal válido rejeitado em `ResultadosPreparacao.Normalizar`: `0.00001m` passa no validator e gera FormatException. Testar normalização e equivalência de hash; preservar a faixa monetária acordada. | Corrigido no Execução #53; testes locais/CI aprovados, faixa e hash preservados. Não equivale a aceite dos três serviços |
| `AUD-007` | P1 / junto do produtor OS e consumidor Execução | Gabriel, contrato com Geovanna | Validar tamanho persistível do snapshot e classificar rejeição permanente. Entrada Unicode de 200.686 bytes foi aceita e gerou documento de reprodução de 600.428 bytes, acima dos 400 KiB por item DynamoDB. Testar serialização final e limites sem retry infinito ou saldo parcial. | Contrato API #371 e proteção Execução #55 integrados; testes locais/CI aprovados. Compatibilidade do produtor OS e prova integrada continuam pendentes |
| `AUD-008` | P1 / antes da demonstração, sem atualização ampla de dependências | Geovanna | Corrigir pontualmente dependências sinalizadas na API: PKCS 9.0.4 e SSH.NET 2025.1.0; este último pertence à cadeia de testes. Usar atualização compatível, validar SQL/JWT/testcontainers e registrar avisos reais, sem suprimir NU1903. Política transversal de scan continua em CODE-008. | Correções na branch do PR API #369: PKCS 10.0.14, SSH.NET 2026.0.0 e AutoMapper 16.3.0 (dois avisos adicionais encontrados em 13/11). Restore/build sem avisos, 517 testes aprovados e consulta NuGet sem vulnerabilidades conhecidas nos projetos avaliados. Aguarda merge; não comprova ausência de toda vulnerabilidade. |

#### Pós-17/11 — não bloqueiam o início nem ampliam a entrega

Continuar seguindo os padrões existentes nas implementações novas. Adiar uma refatoração não autoriza copiar o desvio, criar pastas proibidas ou enfraquecer checks. Reavaliar prioridade se aparecer falha no fluxo obrigatório ou necessidade de dados reais/produção.

| ID | Prioridade / horizonte | Responsável inicial | Item e critério de aceite | Status |
| --- | --- | --- | --- | --- |
| `AUD-009` | P2 / pós-17/11 | Gabriel | Padronizar código existente do Execução: DTO público separado de `CicloExecucaoPersistido`, Result/erro tipado em vez de comparar Exception.Message, validators por campo, mensagens centralizadas e log sanitizado com causa útil. Preservar JSON/contratos e testes; PRs pequenos. | Não priorizado agora |
| `AUD-010` | P2 / pós-17/11 | Gabriel / Geovanna nos respectivos componentes | Corrigir overflow herdado em ItemEstoque e dívidas da API: numeração MAX+1 concorrente, remoção de catálogo referenciado e exemplo 503 da Lambda. Testes de fronteira/SQL, sem alterar regras silenciosamente. Overflow foi reproduzido; corrida/FK continuam achados estáticos. | Não priorizado agora |
| `AUD-011` | P2 / pós-17/11, antes de produção | Ambos | Evoluir lifecycle e recuperação: separar roots/states/permissões de dados, workload e registries; backend durável com locking, backup/PITR e restauração ensaiada; reduzir vínculo RDS ao SG efêmero do cluster. Complementa OPS-009/OPS-011/OPS-020; não cria repo por recurso nem executa migração de state por automatismo. | Não priorizado agora |
| `AUD-012` | P2 / pós-17/11 | Gabriel | Evoluir mensageria conforme necessidade medida: isolamento da outbox global, lease/envio demorado, retenção e compatibilidade de documentos versus protocolo. Testar recuperação sem perder dedup/idempotência; não adicionar TTL indiscriminadamente. Outbox serial atual é concessão acadêmica explícita. | Não priorizado agora |
| `AUD-013` | P2 / pós-17/11 | Gabriel | Convergir organização/gates dos cinco owners com `.github/scripts`, sem perder testes e proteções. Novas alterações já seguem a regra vigente; não criar pasta alternativa. | Não priorizado agora |
| `AUD-014` | P2 / pós-17/11 | Geovanna | Refinar AGENTS/skill atuais com exemplos reais, invariantes e evidências; não instalar rascunhos nem importar a auditoria inteira. Reconciliar autorização/publicação e fontes acessíveis, preservando padrão e estrutura. | Não priorizado agora |
| `AUD-015` | P2 / pós-17/11 | Geovanna, revisão conjunta | Atualizar integralmente mapa de contextos, design estratégico/tático, Domain Storytelling, C4 detalhado e explicações didáticas para refletir os microsserviços. Preservar a modelagem histórica e indicar sua fase; reconciliar responsabilidades, agregados e termos com o implementado. Não substitui a documentação mínima de F4-13/F4-14. | Não priorizado agora |

**Escopo documental acordado em 12/11:** priorizar implementação e requisitos da Fase 4. Até 17/11, manter diagrama geral final com serviços/bancos/comunicação, justificativa da divisão e da Saga, documentação de execução e APIs por serviço, evidências, vídeo e PDF. Atualizar ADR/RFC somente quando a implementação mudar decisão ou contrato relevante; confirmar regras, responsabilidades e contratos antes da parte dependente. Novos nomes/controles técnicos precisam de explicação curta no PR, sem reescrever toda a modelagem. Identificar os desenhos antigos como históricos quando usados como referência da entrega, sem apresentá-los como arquitetura final. A revisão completa em AUD-015 não bloqueia o desenvolvimento; testes verdes não substituem a conferência de aderência ao domínio. A direção das ADRs-0024/25/26 permanece, sujeita às provas de integração, isolamento, compensação e pagamento.

A correção de egress HTTPS da Lambda e a prova de sua ingestão Datadog continuam em F3-008/F3-009/OPS-016, pós-entrega enquanto não impedirem F4-09 ou o fluxo de observabilidade exigido. A prova distribuída dos três serviços em F4-11/F4-12 permanece obrigatória até 17/11. Não adiar dashboards/métricas necessários à demonstração sob o rótulo genérico de hardening.

**Ordem prática:** Geovanna prioriza F4-07/08 e F4-06; Gabriel fecha os controles da operação afetada e a preparação de integração F4-10/11. Não aguardar AUD-009 a AUD-015 para iniciar. Antecipar integração, reservar 16/11 para evidências/documentação/gravação e 17/11 para conferência/envio. Não marcar tarefas inteiras como concluídas pela existência de código, teste isolado ou workflow verde.

Validação local de contratos em 13/11, sem AWS: 93 testes do Orçamento/Pagamento develop, 517 da OS no checkout isolado do PR #369 e 334 do Execução, sem falhas/ignorados nas suítes finais. SQL Server e DynamoDB Local foram descartáveis em Docker; são suítes individuais. O verificador cruzado utilizou agregados/processadores/fábricas de resultados reais, com persistência/recebimentos da OS simulados em memória; verificou envelopes, reentrega sem comando extra, causação/trace e compensação ainda não confirmada. Não prova transação SQL integrada, broker FIFO/DLQ, rotas públicas, Mercado Pago ou ingestão Datadog. Não houve alteração no repositório Orçamento/Pagamento nem no código da Saga. Testes de regressão do consumidor em revisão no Execução #63; nenhum README foi alterado.

Dependências a coordenar com Geovanna: ligar resultados de preparação e ciclo físico na OS; concluir adaptador/worker SQS e CD/infra próprios do Orçamento/Pagamento; completar pagamento real em teste. Mantida a pendência de credenciais Mercado Pago registrada pela responsável. Não alterar essa frente ou contratos para destravar testes. Nenhuma incompatibilidade de payload foi encontrada nos dois comandos atualmente emitidos e verificados; isso não cobre tipos ainda não ligados. AWS não foi utilizada; eventual janela fica para amanhã, após preparação e autorização de Gabriel.

### Marcos e coordenação

- 09/11: decisões e contratos revisáveis; ambiente atual; início dos dois scaffolds e código independente.
- 10–11/11: serviços isolados e infraestrutura mínima; spike Mercado Pago e permissões AWS resolvidos.
- 12–13/11: integração da Saga e compensações, autenticação/Gateway e pagamento de teste.
- 14–15/11: testes, cobertura, Sonar, observabilidade e deploy demonstrável.
- 16/11: documentação, PDF e gravação; congelar funcionalidades.
- 17/11: buffer, conferência de links e envio; evitar deixar gravação/upload para o limite.

Cada tarefa deve ter PR com ID, critérios de aceite, validações reais e referência de contrato. Gabriel pode iniciar F4-03 depois de F4-02; não precisa aguardar todas as frentes. Não editar a mesma branch; compartilhar alterações de contrato antes de avançar nos dois lados. Cada pessoa revisa seus diffs e checks mesmo usando bypass de aprovação. Promoções continuam automáticas conforme as esteiras.

### Alinhamento da infraestrutura da fila de OS — 10/11/2026

Gabriel prepara em PR separado a entrada FIFO/DLQ de resultados de OS e seus contratos SSM no repositório API, conforme alinhamento explícito do responsável pelo projeto. Saga, outbox SQL, consumidor de resultados e transições de OS continuam com Geovanna na F4-06; a preparação da fila não conclui essa tarefa. O contrato proposto e os limites de ativação estão em [Mensageria de OS](../deploy/mensageria-os.md). Recursos reais e IAM dependem da janela de laboratório autorizada.

### Sequência por dependências — frente Geovanna, 12/11/2026

| Ordem | Trabalho | Dependência e limite de ativação |
| --- | --- | --- |
| 1 | F4-06: modelo de domínio e persistência da Saga na OS | ADR-0026/RFC-0007; pode ser testado com SQL local sem depender do deploy de Gabriel. Versões de preparação e orçamento são independentes. Não ativa SQS nem altera o fluxo público legado. |
| 2 | F4-07/08: Orçamento/Pagamento e prova Mercado Pago | Detalhar payloads de cobrança/decisão antes dos handlers; preservar o envelope aceito. Banco/credencial próprios pelo owner RDS e registry pelo owner Kubernetes são pré-requisitos de deploy, não dos testes locais. Credenciais de teste do provedor precisam de prova antecipada. |
| 3 | F4-06: outbox, deduplicação e handlers; F4-10: ligação dos serviços | Estado + deduplicação + comandos na mesma transação SQL. Confirmar contratos e consumidores de Execução/cobrança antes de ativar produtores. Infraestrutura `ready` não comprova processamento ou conclusão da Saga. |
| 4 | F4-09: autenticação/Gateway e corte dos owners antigos | Depende de Saga e serviços integrados; validar diagnóstico, aprovação, reserva, início físico, pagamento e compensação. Sem dual write ou acesso a banco alheio. Não transferir trabalho do Gabriel sem coordenação. |
| 5 | F4-10 a F4-14: prova completa, implantação e entrega | BDD com sucesso/compensação, qualidade/cobertura, traces e documentação do implantado. Reservar 16/11 para materiais e 17/11 para conferência/envio. Recursos reais exclusivamente pelas esteiras e janela autorizada. |

A base persistente não encerra F4-06: ainda serão necessários coordenação transacional com outbox/deduplicação, resultados antigos/divergentes, avanço de novas versões e reconciliação. Cancelamento só termina após as confirmações de ambos os serviços. Se o início físico vencer antes de seu evento chegar à OS, a Saga registra recuperação pendente, sem confirmar estorno fictício. Não usar esse modelo isolado como prova da Saga distribuída.

### Cortes e riscos

F4-04, alinhamento em 11/11/2026: Gabriel aprovou a exceção acadêmica da ADR-0025 para identidade do Execução pela `LabRole` dos nodes, reconhecendo ausência de isolamento IAM entre aplicações. Implementação explícita e prova dentro do Pod permanecem pendentes; não confundir simulação de políticas com acesso real ou transportar credenciais do runner para o workload. AWS continua bloqueada até a janela específica de implantação. A exceção não amplia automaticamente o escopo das frentes da Geovanna.

Preparação da F4-11 alinhada com Gabriel em 10/11/2026: Execução reutiliza Serilog/APM nativo da referência e prepara `traceContext` opcional persistido na outbox, conforme RFC-0007, sem alterar idempotência de negócio. Metadados do workload seguem a plataforma existente; não criar outro Agent, OpenTelemetry ou esteira. Métricas/painéis/alertas de Saga/DLQ e prova real de ingestão permanecem pendentes da integração e janela autorizada; não definir limites sem medição.

Alinhamento da entrada HTTP da F4-05 em 10/11/2026: Gabriel prepara as ações manuais e consulta local do Execução no contrato detalhado da RFC-0007, preservando Administrador/Mecanico e os sufixos atuais. Inclui OpenAPI/Postman locais e testes de 401/403, commit antes de 202 e persistência. Não transfere a F4-09: emissores JWT, Gateway e corte das rotas antigas continuam coordenados com Geovanna. Não declarar integração pública ou AWS concluída por esse preparo.

Alinhamento adicional da F4-05 em 10/11/2026: o snapshot de serviços mantém `servicoId` (instância na OS) e inclui `servicoCatalogoId` (referência ao catálogo), conforme o DDD. A RFC-0007 detalha obrigatoriedade, repetição e limites de compatibilidade. Gabriel adapta Execução; Geovanna adapta o produtor OS na F4-06, antes do corte integrado F4-10. Não inferir igualdade entre os IDs nem consultar banco de outro serviço.

Esclarecimento da F4-05 alinhado com Gabriel em 10/11/2026: preservar o início manual pelo mecânico. `AutorizarExecucao` somente libera o trabalho; `ExecucaoIniciada` corresponde ao primeiro serviço efetivamente iniciado. Finalização física explícita exige todos os serviços finalizados, baixa e outbox atômicas. A barreira de compensação é o início físico, não o recebimento da autorização. A seção correspondente da RFC-0007 detalha a disputa cancelamento/início; implementação e corte público permanecem pendentes nas frentes responsáveis.

Não incluir SNS sem múltiplos consumidores, novos serviços de cadastro, framework compartilhado, migração de Git Flow, plataforma de templates ou hardening corporativo nesta entrega. Não cortar Saga real, integração Mercado Pago, isolamento de bancos, BDD, cobertura, CI/CD ou observabilidade exigidos.

Permissões DynamoDB/SQS, acesso Mercado Pago, conta Sonar e limite do laboratório são riscos P0 e devem ser resolvidos até 11/11. Compensação não pode ser simulada como sucesso. Provisionamento de bancos isolados precisa ser provado; qualquer inviabilidade muda ADR-0025 por decisão explícita, sem acesso cruzado a banco.

## Preparação da próxima entrega

Os itens abaixo são pontos de partida e não substituem decisões arquiteturais aprovadas. O enunciado exige ao menos três microsserviços independentes, bancos próprios com SQL e NoSQL, mensageria, Saga com compensação, cobertura mínima de 80% por serviço, BDD, CI/CD, Kubernetes e observabilidade.

| Prioridade | Frente | Trabalho e critério de aceite | Responsável inicial |
| --- | --- | --- | --- |
| P0 | Ambiente | Executar a solução atual e conhecer os fluxos no Postman; registrar bloqueios sem alterar infraestrutura remota. | Gabriel |
| P0 | Arquitetura | Definir limites dos serviços e evolução do monólito em ADR sucessora; detalhar contratos e Saga em RFC antes de integrar. | Geovanna |
| P0 | AWS | Verificar permissão de criação, uso e limpeza de DynamoDB e SQS no laboratório antes do provisionamento. SNS apenas se necessário. | A definir |
| P0 | Serviços | Planejar três serviços com bancos próprios, fluxo ponta a ponta e integração Mercado Pago; decompor por contratos aprovados. | A definir |
| P0 | Qualidade e entrega | Preservar esteiras existentes como referência; garantir testes, cobertura, BDD, Sonar, deploy, observabilidade, documentação, vídeo e PDF exigidos. | Ambos |

Este backlog guarda melhorias técnicas, itens de código e evoluções operacionais que não precisam entrar imediatamente na entrega principal. A ideia é separar bem o que é escopo do Tech Challenge do que é maturidade de produção para depois.

## Como usar

| Campo | Regra |
| --- | --- |
| Prioridade | `P0` obrigatório para a entrega, `P1` importante se couber, `P2` pós-entrega, `P3` oportunidade futura. |
| Horizonte | `Fase 3`, `Antes da demo`, `Pós-entrega` ou `Pesquisa`. |
| Status | `A fazer`, `Em andamento`, `Bloqueado`, `Concluído` ou `Não priorizado agora`. |
| Critério de aceite | Evidência objetiva de que o item saiu do backlog. |

## Visão de prioridade

| Prioridade | Significado | Decisão prática |
| --- | --- | --- |
| `P0` | Necessário para cumprir o Tech Challenge com segurança. | Fazer antes da entrega. |
| `P1` | Melhora clareza, demonstração ou confiabilidade. | Fazer se não ameaçar o prazo. |
| `P2` | Padrão mais próximo de empresa em produção. | Guardar para depois da entrega. |
| `P3` | Ideia técnica, pesquisa ou refinamento. | Reavaliar quando o produto estiver estável. |

## Escopo da Fase 3

| ID | Prioridade | Horizonte | Área | Item | Critério de aceite | Status |
| --- | --- | --- | --- | --- | --- | --- |
| `F3-001` | `P0` | Fase 3 | Autenticação | Criar uma Lambda de autenticação por documento, em repositório próprio, emitindo JWT somente para Cliente ativo. | `POST /auth/documento` valida CPF ou CNPJ, consulta por `Documento + TipoDocumento` e emite JWT com os claims aprovados; o cenário CPF foi adotado na demonstração. | Concluído |
| `F3-002` | `P0` | Fase 3 | API | Alinhar a API ao contrato único de JWT e manter a autorização dentro da aplicação. | JWT de Cliente usa `sub`, `cliente_id`, `role`, `jti`, `iss`, `aud` e expiração, sem documento ou hash. | Concluído |
| `F3-003` | `P0` | Fase 3 | Banco | Usar banco gerenciado no RDS, com credenciais e rede compatíveis com os consumidores da Fase 3. | RDS provisionado por Terraform, credenciais em Secrets Manager e contratos não sensíveis publicados no SSM. | Concluído |
| `F3-004` | `P0` | Fase 3 | Kubernetes | Executar a API em Kubernetes com escalabilidade. | API publicada no EKS, healthcheck funcional e HPA evidenciado. | Concluído |
| `F3-005` | `P0` | Fase 3 | API Gateway | Expor a entrada pública via API Gateway. | HTTP API roteia `POST /auth/documento` para Lambda e `ANY /api/{proxy+}` para a API no Kubernetes via VPC Link. | Concluído |
| `F3-006` | `P0` | Fase 3 | Terraform | Separar infraestrutura em repositórios/esteiras por recurso. | Seis repositórios possuem ownership explícito, Terraform/implementação, CI/CD e instruções operacionais. | Concluído |
| `F3-007` | `P0` | Fase 3 | CI/CD | Manter branch protegida, PR obrigatório e quality gate. | Esteiras e promoções Git Flow implementadas nos repositórios da solução. | Concluído |
| `F3-008` | `P0` | Pós-entrega | Observabilidade | Enviar logs, métricas e traces para Datadog. | API e Kubernetes possuem métricas, logs e traces funcionando. Auth Lambda está instrumentada, mas a ingestão Datadog da Lambda não foi evidenciada no ambiente acadêmico. | Em andamento / evolução pós-entrega |
| `F3-009` | `P0` | Pós-entrega | Observabilidade | Criar dashboards e alertas pedidos no enunciado. | Implementados: latência API, CPU, memória, healthcheck, error rate, logs ao vivo e dashboard Datadog. Restam métricas de negócio, alerta específico de falha de OS e visibilidade completa da Lambda. | Em andamento / evolução pós-entrega |
| `F3-010` | `P0` | Fase 3 | Documentação | Consolidar diagramas, ADRs/RFCs e documentação final. | READMEs, diagramas, ADRs/RFCs, execução, arquitetura e limitações finais estão documentados sem afirmar evidências inexistentes. | Concluído |
| `F3-011` | `P0` | Fase 3 | Rede | Garantir entrada pública única pelo API Gateway e backend privado da API. | Internet → API Gateway → VPC Link → NLB interno → NodePort → API no EKS; autenticação segue para a Auth Lambda. | Concluído |
| `F3-012` | `P0` | Fase 3 | Rede / Cutover | Remover a exposição pública direta da API após validação E2E do private ingress. | VPC Link, NLB interno, NodePort e rotas do Gateway validados; API Gateway é a entrada pública única. | Concluído |

## Setup inicial de novos repositórios

| ID | Prioridade | Horizonte | Área | Item | Critério de aceite | Status |
| --- | --- | --- | --- | --- | --- | --- |
| `SETUP-001` | `P0` | Fase 3 | Repositório | Criar repositório com nome padronizado `oficina-mecanica-*`. | Nome reflete o recurso e aparece no plano da Fase 3. | Em andamento |
| `SETUP-002` | `P0` | Fase 3 | Branches | Criar `main`, `develop` e `release` quando aplicável. | Branches existem antes do primeiro fluxo de PR. | Em andamento |
| `SETUP-003` | `P0` | Fase 3 | Branch protection | Aplicar proteção nas branches principais. | Dois rulesets ativos: Git Flow sem bypass exige PR, validação de origem, Quality gate e bloqueio de push direto, force push e deleção; aprovação humana separada permite bypass via PR para os integrantes autorizados e maintain/admin. Repetir o padrão em cada novo repositório e revisar alterações nos workflows de validação. | Em andamento |
| `SETUP-004` | `P0` | Fase 3 | CI/CD | Criar workflow de CI mínimo para cada tipo de repo. | PR executa validação compatível com o repositório: `.NET`, Terraform, Lambda ou manifests; a Lambda usa `ci.yml`, quality gate e jobs equivalentes às esteiras maduras. | Em andamento |
| `SETUP-005` | `P0` | Fase 3 | Documentação | Criar README inicial com objetivo, stack, execução e deploy. | README permite entender o papel do repo sem depender de conversa externa. | Em andamento |
| `SETUP-006` | `P1` | Antes da demo | Operação | Padronizar repository variables, secrets e environments. | Variáveis obrigatórias documentadas e cadastradas antes da esteira real. | A fazer |
| `SETUP-007` | `P1` | Antes da demo | Governança | Confirmar acesso do usuário `soat-architecture`. | Usuário aparece com acesso exigido em todos os repositórios da entrega. | A fazer |
| `SETUP-008` | `P0` | Fase 3 | CI/CD | Padronizar os novos fluxos de CI/CD pelo padrão maduro atual. | VPC, Kubernetes, API e a nova Lambda usam `ci.yml` com os estágios compatíveis (`🧪 CI Development`, `🔎 CI Release`, `🛡️ CI Production`, `🚀 CD Development`, `☁️ AWS Deploy`, `🔀 CD Release` e `🏁 CD Production`); o check obrigatório no ruleset corresponde ao Quality gate. A divergência legada do RDS é tratada em `PIPE-001` a `PIPE-004`, sem bloquear a Lambda. | Em andamento |
| `SETUP-009` | `P1` | Antes da demo | Documentação | Revisar acentuação e português dos textos das esteiras. | Workflows, READMEs e docs de todas as esteiras ficam legíveis em português, sem palavras sem acento por padronização manual ou mojibake. | A fazer |
| `SETUP-010` | `P1` | Antes da demo | Higiene | Completar `.gitignore` do repositório RDS. | Arquivos Terraform gerados, crash logs, overrides e arquivos de IDE ficam alinhados, sem remover as proteções já existentes. | A fazer |

## Backlog de código e qualidade

| ID | Prioridade | Horizonte | Área | Item | Critério de aceite | Status |
| --- | --- | --- | --- | --- | --- | --- |
| `CODE-001` | `P0` | Fase 3 | Domínio | Adicionar `StatusCliente` para suportar autenticação de Cliente por documento. | Aggregate/modelo, mapeamento EF, migration e snapshot são atualizados; seed/demo possui cliente ativo e inativo; testes cobrem os documentos CPF e CNPJ nos dois estados. A representação física do status é documentada antes da migration. | Concluído |
| `CODE-002` | `P0` | Fase 3 | Segurança | Evitar CPF/CNPJ puro em logs, respostas técnicas e JWT. | Testes e implementação garantem respostas sanitizadas e JWT sem documento ou hash. | Concluído |
| `CODE-003` | `P0` | Fase 3 | Testes | Cobrir fluxo de autenticação e autorização por documento. | Testes cobrem CPF/CNPJ, entradas inválidas, cliente inexistente/inativo, claims e autorização. | Concluído |
| `CODE-004` | `P1` | Pós-entrega | Contratos | Atualizar OpenAPI e Postman versionados para os fluxos da Fase 3. | Artefatos versionados ainda refletem a Fase 2; Swagger runtime representa a API atual. | Evolução pós-entrega |
| `CODE-005` | `P1` | Fase 3 | Observabilidade | Padronizar `X-Correlation-Id`, `dd.trace_id` e `dd.span_id`. | API e Lambda propagam correlação e a API injeta identificadores Datadog nos logs. | Concluído |
| `CODE-006` | `P2` | Pós-entrega | Banco | Mover migrations e seed inicial do startup da API para Kubernetes Job versionado. | Deploy da API não executa migration automaticamente no startup. | Não priorizado agora |
| `CODE-007` | `P2` | Pós-entrega | Banco | Avaliar lock distribuído para migrations concorrentes. | Estratégia definida, por exemplo com `sp_getapplock`, antes de escalar réplicas com migration automática. | Não priorizado agora |
| `CODE-008` | `P2` | Pós-entrega | Qualidade | Adicionar análise de dependências e vulnerabilidades. | Pipeline publica resultado de auditoria de pacotes sem bloquear indevidamente a entrega acadêmica. | Não priorizado agora |
| `CODE-009` | `P0` | Fase 3 | API | Criar a rota do próprio cliente para consultar suas ordens de serviço. | `GET /api/v1/clientes/me/ordens-servico` usa somente `cliente_id` validado do JWT e possui testes de autorização. | Concluído |
| `CODE-010` | `P1` | Antes da demo | Segredos locais | Substituir o segredo local versionado no manifesto Kubernetes. | `k8s/api-secret.yaml`, hoje com credencial local fixa versionada, é trocado por estratégia de exemplo/template sem segredo real ou local versionado, preservando a facilidade de execução local e as validações existentes. | A fazer |
| `CODE-011` | `P2` | Pós-entrega | Qualidade | Tratar vulnerabilidade reportada no SSH.NET 2025.1.0. | Atualização da dependência é avaliada com impactos e regressões antes da correção. | A fazer |

## Decisões e contratos pendentes da autenticação

| ID | Prioridade | Horizonte | Área | Item | Critério de aceite | Status |
| --- | --- | --- | --- | --- | --- | --- |
| `DEC-001` | `P0` | Fase 3 | Contrato JWT | Resolver a divergência entre o `cpf_hash` do ADR-0019 e o contrato do RFC-0001. | ADR-0019 e RFC-0001 definem o mesmo claim set: `sub = cliente_id`, `cliente_id`, `role`, `jti`, `iss`, `aud` e expiração; CPF/CNPJ e hashes de documento ficam fora do JWT. | Concluído |
| `DEC-002` | `P0` | Fase 3 | Contrato HTTP | Formalizar o contrato de `POST /auth/documento` e sua integração com o API Gateway. | HTTP API payload format 2.0 com `APIGatewayHttpApiV2ProxyRequest`; request por `documento`; `200` com token mínimo; `400` genérico para entrada inválida; `401` idêntico para inexistente/inativo; `503` genérico para RDS/Secrets Manager; sem exposição de documento ou infraestrutura. | Concluído |
| `RDS-001` | `P0` | Fase 3 | Segredos | Provisionar e publicar a referência das credenciais do RDS. | `oficina-mecanica-infra-rds` é o dono do segredo gerenciado no AWS Secrets Manager; o endpoint e o ARN do segredo são publicados pelo SSM em `/oficina-mecanica/development/rds/endpoint` e `/oficina-mecanica/development/rds/master_secret_arn`. API e Auth Lambda resolvem as credenciais no deploy, sem GitHub Secret de connection string da Lambda. | Concluído |
| `RDS-002` | `P0` | Fase 3 | Rede | Restringir o acesso SQL Server do RDS conforme ADR-0018. | RDS privado aceita TCP/1433 somente dos componentes autorizados; EKS e Auth Lambda usam regras por security group. | Concluído |

## Dependências P0 da autenticação

| Item | Repositório responsável | Depende de | Bloqueia |
| --- | --- | --- | --- |
| `DEC-001` | `oficina-mecanica-api` | — | — (concluído) |
| `DEC-002` | `oficina-mecanica-infra-api-gateway` com contrato revisado em `oficina-mecanica-api` | — | — (concluído) |
| `CODE-001` | `oficina-mecanica-api` | definição física do status | `F3-001`, `CODE-003`, `CODE-009` |
| `F3-002` | `oficina-mecanica-api` | contrato JWT consolidado em `DEC-001` | `CODE-003`, `CODE-009`, testes ponta a ponta |
| `RDS-001` | `oficina-mecanica-infra-rds` | decisão de publicar ARN não secreto via SSM | `F3-001`, testes ponta a ponta |
| `RDS-002` | `oficina-mecanica-infra-rds` (SG do RDS) e `oficina-mecanica-auth-lambda` (regra de integração) | SG do RDS publicado via SSM e SG próprio da Lambda | deploy funcional da Lambda |
| `F3-001` | `oficina-mecanica-auth-lambda` | `CODE-001`, `RDS-001` | `RDS-002`, `F3-005`, testes ponta a ponta |
| `CODE-009` | `oficina-mecanica-api` | `CODE-001`, `F3-002` | demonstração de autorização do cliente |
| `F3-005` | `oficina-mecanica-infra-api-gateway` | `F3-001`, endpoint privado da API | testes ponta a ponta |

## Dívida de CI/CD observada na auditoria

| ID | Prioridade | Horizonte | Área | Item | Critério de aceite | Status |
| --- | --- | --- | --- | --- | --- | --- |
| `PIPE-001` | `P2` | Pós-entrega | CI/CD | Convergir a organização do RDS para o padrão `ci.yml` adotado por API, VPC e Kubernetes. | A decisão de unificar ou manter os três workflows do RDS é registrada e, se unificada, preserva os quality gates sem regressão. | Não priorizado agora |
| `PIPE-002` | `P2` | Pós-entrega | CI/CD | Avaliar reutilização do mesmo `tfplan` no apply do RDS. | Revalidado em 12/11: `aws-deploy.yml` do RDS publica/recupera o plano salvo e o aplica, sem regenerá-lo. Isto não encerra o guardrail de consumidores registrado em AUD-002 nem comprova uma execução AWS nesta revisão. | Concluído |
| `PIPE-003` | `P2` | Pós-entrega | Documentação | Corrigir a referência de `infra-action.env` onde o padrão real é `terraform-action.env`. | RFCs, planos e READMEs citam o nome real, ou a alteração de nome é aprovada e aplicada de forma coordenada. | Não priorizado agora |
| `PIPE-004` | `P2` | Pós-entrega | Qualidade | Normalizar finais de linha no RDS, caso a alteração ainda exista após nova verificação. | O diff não contém alteração apenas de line ending e `.gitattributes` ou padrão equivalente evita recorrência. | Não priorizado agora |
| `PIPE-005` | `P2` | Pós-entrega / executar hoje somente se todos os P0 estiverem concluídos e houver folga | CI/CD / Git Flow | Corrigir definitivamente a política de promoção `develop -> release -> main` nos seis repositórios da Fase 3, preservando ancestralidade Git. | A configuração vulnerável identificada em `oficina-mecanica-api`, `oficina-mecanica-infra-vpc`, `oficina-mecanica-infra-kubernetes`, `oficina-mecanica-infra-rds`, `oficina-mecanica-auth-lambda` e `oficina-mecanica-infra-api-gateway` é corrigida conforme o diagnóstico detalhado da issue #268. `feature/* -> develop` pode continuar permitindo squash; `develop -> release` e `release -> main` devem usar merge commit real. Squash e rebase não devem ser usados em promoções entre branches de estágio enquanto os workflows dependerem de ancestralidade ou `git rev-list`. Preferir a branch fixa `release` neste projeto e remover `release/*` dos rulesets e workflows se releases versionadas não forem usadas; se `release/*` for mantido, corrigir o deadlock atual de required status checks aplicados na criação da branch. Não reconciliar nem regravar o histórico dos demais repositórios quando o grafo já estiver saudável. Validar ao final com `git merge-base --is-ancestor` e compare. | Não priorizado agora |

## Backlog operacional pós-entrega

### Itens não bloqueantes para a Fase 3

Os itens desta seção não bloqueiam a entrega atual da Fase 3.

### Estado aceito de secrets e configuração na entrega

- As credenciais do RDS têm fonte da verdade adequada: `username` e `password` são gerenciados pelo RDS no AWS Secrets Manager; o Parameter Store publica somente contratos não sensíveis, como endpoint, ARN, IDs, nomes e status.
- API e Auth Lambda obtêm endpoint e ARN do segredo via SSM e resolvem as credenciais no Secrets Manager durante o deploy. A Auth Lambda não utiliza `AUTH_LAMBDA_CONNECTION_STRING` como GitHub Secret.
- Por limitação operacional do AWS Academy/lab, `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY` e `AWS_SESSION_TOKEN` permanecem temporariamente no GitHub Environment para autenticar o runner de CI/CD na AWS. Não são credenciais da aplicação.
- O segredo de assinatura JWT continua no GitHub Environment durante a Fase 3. Esta é uma decisão temporária de entrega, não o estado alvo de produção.
- `AWS_REGION`, issuer, audience, expiração e nome da execution role podem permanecer como GitHub Variables na Fase 3; são configurações não sensíveis.

### Regra arquitetural para evolução futura

- **Parameter Store:** configuração e contratos não sensíveis, como IDs, ARNs, endpoints, nomes, status e parâmetros operacionais.
- **Secrets Manager:** material sensível, como passwords, signing keys, tokens e API keys.
- **GitHub:** código, workflows e a configuração mínima necessária ao bootstrap do CI/CD. Em produção, evitar secrets permanentes no GitHub quando OIDC/federação e Secrets Manager puderem substituí-los.

| ID | Prioridade | Horizonte | Área | Item | Critério de aceite | Status |
| --- | --- | --- | --- | --- | --- | --- |
| `OPS-009` | `P2` | Pós-entrega | Registry | Avaliar mover o ownership do ECR da API de `oficina-mecanica-infra-kubernetes` para `oficina-mecanica-api`, aproximando o registry do ciclo de vida da aplicação. | Decisão registrada e migração planejada sem interromper a entrega atual. | Não priorizado agora |
| `OPS-010` | `P2` | Pós-entrega | Kubernetes | Avaliar Helm ou manifests centralizados em `oficina-mecanica-infra-kubernetes` após a entrega. | Estratégia de workloads definida sem duplicação e com migração segura. | Não priorizado agora |
| `OPS-011` | `P2` | Pós-entrega | Segredos | Consumir secrets em runtime pelos workloads. | Auth Lambda recebe somente ARN/referência e consulta o Secrets Manager em runtime via AWS SDK/IAM; a API adota estratégia equivalente adequada ao Kubernetes. A evolução evita valores sensíveis em Terraform state e environment variables; `sensitive = true` oculta a apresentação, mas não remove o valor do state. | Não priorizado agora |
| `OPS-012` | `P2` | Pós-entrega | Solution | Migrar a solution `.sln` para `.slnx`. | Migração validada sem alterar o comportamento da aplicação. | Não priorizado agora |
| `OPS-013` | `P1` | Assim que possível após o primeiro apply seguro | Infraestrutura da API | Concluir a limpeza transicional da API desacoplada: remover módulos legados VPC/EKS/RDS/ECR e `state-ownership.tf`, deixando somente namespace, configmap, secret, deployment, service, HPA, providers/data sources e pipeline. | Após o primeiro apply seguro, não há ruído nem ownership de infraestrutura compartilhada na pasta `infra/terraform` da API. | Concluído |
| `OPS-014` | `P1` | Pós-entrega | Ambientes | Auditar hardcoded de ambiente e paths SSM em todas as esteiras. | Workflows reutilizáveis não apontam silenciosamente para development; a relação entre branch, GitHub Environment, pasta Terraform e paths SSM está parametrizada ou explicitamente documentada. | A fazer |
| `OPS-015` | `P2` | Pós-entrega | Kubernetes | Restringir o endpoint administrativo do EKS. | Avaliar `cluster_endpoint_public_access_cidrs`, eliminar `0.0.0.0/0` e usar endpoint privado e/ou runner self-hosted dentro da VPC em produção. Não bloqueia a Fase 3. | A fazer |
| `OPS-016` | `P2` | Pós-entrega | Rede | Evoluir a estratégia de egress privado. | Avaliar manter NAT Gateway único, usar NAT por AZ para alta disponibilidade ou substituir parte do tráfego por VPC Endpoints (ECR, S3, Secrets Manager, CloudWatch, STS e similares). O NAT atual permanece intencional na Fase 3: EKS e Lambda estão em subnets privadas e precisam de saída, inclusive para Secrets Manager e Datadog conforme ADR-0018. | A fazer |
| `OPS-017` | `P3` | Pós-entrega | Rede | Reavaliar subnets públicas após remover o Load Balancer público da API. | Não remover subnets na Fase 3. Hoje o NAT usa `public[0]`; a segunda subnet pode ser útil em uma evolução para NAT por AZ e não há benefício suficiente para alterar a topologia imediatamente antes da entrega. | A fazer |
| `OPS-018` | `P2` | Pós-entrega | Segredos | Centralizar a JWT signing key no AWS Secrets Manager. | Existe uma única fonte da verdade compartilhada por Auth Lambda e API; elimina duplicação entre `JWT_SECRET` e `AUTH_LAMBDA_JWT_SECRET`, não expõe valor em logs, documenta ownership e suporta rotação. O owner compartilhado deve ser definido antes da implementação para não criar dependência circular entre API e Auth Lambda. | Não priorizado agora |
| `OPS-019` | `P2` | Pós-entrega | Segredos | Migrar `WEBHOOK_TOKEN` para o AWS Secrets Manager. | Valor deixa o GitHub Secrets; referência/configuração é controlada e nenhum segredo aparece em código, tfvars versionados ou logs. | Não priorizado agora |
| `OPS-020` | `P2` | Pós-entrega | CI/CD | Migrar a autenticação GitHub Actions → AWS para OIDC. | GitHub OIDC Provider e IAM Role usam trust policy restrita a repositório, branch e environment, menor privilégio e credenciais STS temporárias; não permanecem access keys no GitHub. Pode ser inviável no AWS Academy/voclabs por restrições de IAM: as credenciais temporárias atuais são concessão operacional acadêmica, não desenho de produção. | Não priorizado agora |
| `OPS-021` | `P3` | Pós-entrega | Configuração | Centralizar configurações não sensíveis no Parameter Store. | Avaliar região quando aplicável, issuer, audience, expiração e demais configurações de ambiente no SSM. Não é requisito de segurança — GitHub Variables não são secrets — e busca governança/centralização sem competir com itens funcionais da Fase 3. | Não priorizado agora |
| `ECR-001` | `P2` | Pós-entrega | Registry | Criar Lifecycle Policy do ECR para imagens publicadas com tags imutáveis `sha-$GITHUB_SHA`. | Existe `aws_ecr_lifecycle_policy` que mantém as últimas 10–20 imagens `sha-*`, remove imagens untagged antigas após alguns dias, preserva rollback e não apaga imediatamente a imagem anterior ao deploy. | A fazer |
| `DOC-README-001` | `P2` | Pós-entrega / documentação final | Documentação | Revisar os READMEs dos seis repositórios para refletir o estado final real. | Conteúdo obsoleto foi corrigido sem reescrever a documentação madura. | Concluído |
| `DOC-README-002` | `P2` | Pós-entrega / documentação final | Documentação | Revisar links quebrados nos seis repositórios. | Links relativos alterados foram validados e referências removidas foram ajustadas. | Concluído |
| `DOC-README-003` | `P2` | Pós-entrega / documentação final | Documentação | Padronizar conteúdo mínimo dos READMEs sem apagar o estilo próprio de cada repositório. | Cada README documenta responsabilidade, dependências, execução, CI/CD, configuração e retorno ao README principal. | Concluído |
| `DOC-API-001` | `P2` | Pós-entrega / documentação final | Contratos | Atualizar OpenAPI e Postman após o código final. | Documentação cobre `POST /auth/documento`, `GET /api/v1/clientes/me/ordens-servico`, JWT/autorização e endpoints finais; referências ao LoadBalancer público e fluxos obsoletos foram removidas. | A fazer |
| `DOC-API-002` | `P2` | Pós-entrega / documentação final | Demonstração | Criar collection Postman exclusiva para o vídeo. | Collection contém health, autenticação por documento, `/me` sem JWT (`401`) e com JWT Cliente (`200`), rota Admin com JWT Cliente (`403`), abertura de OS e chamada para logs/traces; variável/script captura automaticamente o JWT de `/auth/documento` para as requests seguintes. | A fazer |

## Status atual da entrega

- VPC, Kubernetes/EKS, RDS, API, Auth Lambda e API Gateway possuem esteiras separadas e contratos SSM documentados.
- API Gateway é a entrada pública; a API no EKS é alcançada por VPC Link, NLB interno e NodePort.
- Autenticação por documento e rota protegida do próprio cliente estão implementadas.
- Datadog da API/Kubernetes possui métricas, logs e traces; a telemetria completa da Auth Lambda permanece como evolução.

| ID | Prioridade | Horizonte | Área | Item | Critério de aceite | Status |
| --- | --- | --- | --- | --- | --- | --- |
| `OPS-001` | `P2` | Pós-entrega | Hotfix | Definir fluxo `hotfix/*` a partir de `main`. | Hotfix entra por PR para `main`, passa por CI/aprovação e depois é sincronizado para `develop` e `release`. | Não priorizado agora |
| `OPS-002` | `P2` | Pós-entrega | Rollback | Criar rollback manual por versão/tag de imagem. | Esteira permite redeploy de uma versão anterior conhecida como boa. | Não priorizado agora |
| `OPS-003` | `P2` | Pós-entrega | Rollback | Avaliar rollback automático após falha de healthcheck. | Rollback automático é limitado a deploy de aplicação e nunca reverte banco de forma destrutiva sem aprovação humana. | Não priorizado agora |
| `OPS-004` | `P2` | Pós-entrega | Banco | Definir política de rollback de migrations. | Migrations possuem estratégia segura para forward fix, compatibilidade ou rollback manual controlado. | Não priorizado agora |
| `OPS-005` | `P2` | Pós-entrega | Infra | Evoluir RDS para Multi-AZ, backups e snapshot final. | Ambiente deixa de depender das simplificações da AWS Academy. | Não priorizado agora |
| `OPS-006` | `P2` | Pós-entrega | Segredos | Formalizar rotação de secrets e chaves JWT. | Segredos possuem dono, periodicidade e procedimento de rotação. | Não priorizado agora |
| `OPS-007` | `P2` | Pós-entrega | DNS | Trocar hostname bruto do Load Balancer por DNS amigável. | API usa domínio próprio com Route 53 ou provedor equivalente. | Não priorizado agora |
| `OPS-008` | `P3` | Pesquisa | Plataforma | Avaliar trunk-based development como alternativa ao Git Flow. | Decisão documentada somente se houver ganho real para o contexto do time. | Não priorizado agora |

## Evoluções pós-entrega identificadas na Fase 3

- evidenciar a telemetria completa da Auth Lambda no Datadog;
- medir o volume diário de ordens de serviço por evento de negócio;
- medir o tempo médio por status de Diagnóstico, Execução e Finalização;
- criar alerta específico de falha de processamento de ordem de serviço;
- atualizar a collection Postman versionada para os fluxos da Fase 3;
- atualizar o OpenAPI versionado, preservando o Swagger runtime como referência atual;
- persistir o histórico de transições de status necessário às métricas de negócio;
- criar monitores e alertas mais específicos conforme dados reais de operação;
- reavaliar as melhorias de maturidade P2/P3 já mantidas neste backlog.

## Fora do escopo imediato

Estes itens ficam registrados para não serem esquecidos, mas não devem competir com a entrega da Fase 3:

- rollback automático completo;
- fluxo formal de hotfix;
- banco Multi-AZ com política completa de backup;
- DNS com domínio próprio;
- rotação corporativa de segredos;
- esteira corporativa com change management;
- plataforma compartilhada de templates para todos os repositórios.

## Próxima revisão

Antes de iniciar uma nova frente, revisar primeiro os itens `P0`. Depois da entrega, reabrir os itens `P2` e decidir o que vira melhoria real do projeto e o que continua apenas como referência de arquitetura.

### Padronização obrigatória de estrutura — 10/11/2026

Por orientação explícita de Gabriel, as pastas de scripts foram removidas da API e do Execução. Lógica operacional e testes são mantidos nos workflows existentes; arquivos temporários são gerados fora do conteúdo versionado. O gate de Git Flow impede pastas de scripts e novos diretórios de topo em todo PR. Não acrescentar estruturas auxiliares sem alinhamento. Esta reorganização não altera recursos de infraestrutura, contratos de mensagens ou regras de negócio; AWS permanece desabilitada.
