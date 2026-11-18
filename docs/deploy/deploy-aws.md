# ☁️ Deploy na AWS

O ambiente físico é `development`. Homologation e production continuam promoções lógicas do Git Flow. Toda criação, atualização e destruição de recursos reais ocorre exclusivamente nas esteiras oficiais; comandos locais são somente de consulta, validação e planejamento.

## 🧭 Ordem das esteiras

A [sequência de implantação no README](../../README.md#deploy-aws) descreve os dez passos das oito esteiras.

VPC precede Kubernetes; RDS depende de Kubernetes para preparar seu banco próprio. A API publica primeiro sua fila e consulta privada. Execução e Orçamento/Pagamento publicam suas entradas e usam a fila OS. A API então recebe as filas dos participantes. Auth usa a consulta privada, e o Gateway sobe uma única vez após os três serviços estarem saudáveis. A URL pública permite completar o Webhook e atualizar somente a configuração do pagamento.

As passagens adicionais da API e de Orçamento/Pagamento são dependências da implementação atual. Não exigem destroy, criação de novos serviços ou troca de arquitetura. Não iniciar jornadas antes da configuração final.

## 🔑 Configuração da janela

- Usar credenciais temporárias da mesma conta/região nos oito Environments `development`.
- Preservar a chave JWT compartilhada e o contrato de issuer/audience dos três serviços e Auth.
- Manter secrets no GitHub Environment ou Secrets Manager; nunca imprimir valores, state, connection strings, JWT ou assinatura de Webhook.
- Configurar flags e dependências conforme o roteiro de referência. `AWS_DEPLOY_ENABLED` autoriza a operação, `TERRAFORM_ACTION` escolhe apply/destroy e `AUTO_PR_ENABLED` controla promoções; não são opções equivalentes.
- Durante o aceite, reter promoções para release/main. A configuração de uma Variable não dispara sozinha o deploy.

## 🚀 Execução pela esteira

1. Inventariar conta, recursos, contratos SSM e state com consultas read-only. Reset do laboratório não comprova limpeza.
2. Confirmar janela autorizada e dependências reais prontas.
3. Abrir branch de trabalho a partir de develop e PR com a ação operacional explícita no controle `infra/terraform/environments/dev/terraform-action.env`.
4. Exigir CI e quality gate aprovados antes do merge.
5. Acompanhar o CD: restore de state, validação, plano salvo, publicação da imagem validada quando aplicável, aplicação desse mesmo plano e verificação real.
6. Conferir conta/região, outputs SSM, imagem, rollout e health. Status de fila não substitui status da aplicação.

Quando uma configuração depender de recursos publicados pela passagem anterior, usar nova execução completa do CD autorizado, com plano da nova tentativa. Não repetir somente o job que aplica um plano antigo.

## 💾 States e planos

Cada owner mantém state separado por conta e ambiente. API, Execução, Auth e Orçamento/Pagamento cifram states e planos com AES-256-GCM e chave exclusiva do Environment. Contexto de repositório, conta e ambiente é autenticado; o plano também vincula commit, ação e tentativa. Os workflows determinam os prefixos de cache efetivos; não restaurar manualmente caches de outra conta ou material legado sem proteção.

Ausência ou corrupção de cache não autoriza esquecer recursos. Sem cache compatível, API, Execução, Auth e Orçamento/Pagamento exigem primeiro um destroy dedicado com inventário vazio comprovado, plano exclusivo de destroy e state final sem recursos gerenciados. Isso prepara o estado da conta; não autoriza destruir um ambiente existente para testar a aplicação. Se houver recursos, recuperar o state correto antes de prosseguir. Preservar caches, chaves e evidências; não usar state rm/import/mv como atalho.

O backend protegido e durável é evolução do projeto. A solução atual de cache continua sujeita às limitações documentadas no backlog e não deve ser apresentada como backend remoto definitivo.

## 🏗️ Ownership de rede e dados

O NLB interno e os listeners privados pertencem à esteira Kubernetes. As aplicações possuem Services NodePort privados: API 30080, Execução 30081 e Orçamento/Pagamento 30082. O Gateway usa VPC Link e encaminha as rotas ao responsável; não há Load Balancer público criado pelo Service da API.

RDS publica endpoint e metadados dos secrets. Orçamento/Pagamento usa somente seu banco/login e secret exclusivos; Execução usa sua tabela DynamoDB. Nenhum serviço deve acessar o banco de outro. Contratos não secretos ficam no SSM, com prontidão separada para filas e aplicações.

## ✅ Validação

- Conferir recursos, identidade e contratos reais na conta/região da janela.
- Validar rollout e imagem dos três workloads, HTTP/JWT e consulta privada da Auth.
- Executar o fluxo de negócio, compensação, idempotência e entrega condicionada ao pagamento.
- Comprovar checkout de teste, assinatura do Webhook e consulta financeira ao Mercado Pago; mocks não substituem esse aceite.
- Conferir ingestão Datadog, correlação, dashboards e alertas com recuperação. Ausência de dados não significa ausência de falha.
- Registrar os resultados reais no backlog e nas evidências, sem dados sensíveis.

## 🧹 Encerramento pela esteira

Destroy exige autorização e PR dedicado. Interromper entradas/produtores, reconciliar mensagens/outboxes/Sagas e respeitar os guardrails de cada owner. Remover Gateway, API/OS e Auth antes dos participantes e bancos; RDS precede Kubernetes na remoção e VPC fica por último. Confirmar cada dependência antes do passo seguinte.

Aplicar exclusivamente o plano destrutivo salvo e verificar ausência real, contratos SSM e state final. Não limpar filas com purge nem descartar evidências para passar no gate. Após destroy, preservar o controle; retornar para apply somente em nova janela autorizada, porque essa mudança pode provisionar novamente.

A [collection e o guia do Postman](../evidencias/postman.md) apresentam os cenários de negócio e as consultas assíncronas.


## Como avaliar um ambiente existente

Use a collection final e o ambiente Development, preencha o endpoint vigente do Gateway e os logins de teste nos valores locais. Um ambiente disponível não requer destroy nem novo apply para executar as chamadas. Não use o Runner em toda a collection: há esperas, pagamento interativo e caminhos alternativos.

| Situação | Procedimento |
| --- | --- |
| Ambiente saudável e state disponível | Usar as APIs. Apply só é necessário para uma implantação ou alteração autorizada. |
| Sessão Academy encerrada, recursos preservados | Renovar credenciais da mesma conta/região e verificar recursos/saúde. Recuperação de nós ou workloads, se necessária, passa pela esteira. |
| Reset removeu recursos, cache preservado | Conferir a ausência real e reconciliar pelo fluxo de recuperação de cada owner antes da nova subida. |
| Conta sem recursos e sem cache compatível | Executar a preparação inicial por destroy dedicado nos owners que a exigem; só depois autorizar apply na ordem das dependências. |
| Recursos existentes e cache ausente/corrompido | Recuperar o state e a chave corretos. Não assumir ambiente vazio ou destruir recursos como tentativa de correção. |

`TERRAFORM_ACTION=apply` representa a intenção de provisionar/atualizar; não é uma confirmação de disponibilidade. Uma alteração somente documental não reaplica a infraestrutura. Retornar de destroy para apply é uma mudança operacional e pode disparar provisionamento.

## Configuração dos serviços

| Componente | Preparação exigida para a janela |
| --- | --- |
| Oito repositórios | Credenciais temporárias da mesma janela em `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY` e `AWS_SESSION_TOKEN`, no Environment `development`. Conferir `AWS_REGION` e identidade da conta pelo runner autorizado. Presença/data de secret não comprovam validade. |
| Controle de execução | Revisar `infra/terraform/environments/dev/terraform-action.env`. A primeira subida exige retorno explícito para `apply` por PR, CI aprovado e janela autorizada. Habilitar `AWS_DEPLOY_ENABLED` somente para o owner que será executado. Manter promoções retidas; preparar `AUTO_PR_ENABLED=false` durante o aceite em develop. |
| Estados/planos protegidos | API, Execução, Orçamento/Pagamento e Auth exigem `TERRAFORM_STATE_KEY` correspondente aos seus próprios backups. Preservar chave, state e cache por conta; não gerar outra chave como tentativa de corrigir restauração. Em conta nova sem cache compatível, seguir a recuperação inicial já implementada pelo owner, com ausência confirmada e guardrails; não assumir state vazio ou copiar cache de outra conta. |
| JWT compartilhado | `JWT_SECRET` nos três serviços e `AUTH_LAMBDA_JWT_SECRET` na Lambda devem validar a mesma chave. Issuer `oficina-mecanica-auth` e audience `oficina-mecanica-api`; Execução exige `JWT_ISSUER`/`JWT_AUDIENCE` explícitos, Orçamento usa esses nomes com defaults, Auth usa `AUTH_LAMBDA_JWT_ISSUER`/`AUTH_LAMBDA_JWT_AUDIENCE`. Conferir a API pelo ConfigMap atual. Não imprimir chaves nem rotacioná-las sem coordenação. |
| Clientes privados | Mesmo `CLIENTES_INTERNOS_TOKEN` na API e Lambda, distinto do JWT. `CLIENTES_API_ENABLED=true` primeiro na API, depois na Lambda. A validação da Lambda deve confirmar consulta privada e ausência de connection string SQL. |
| RDS | Conferir `DB_USERNAME`; habilitar `ORCAMENTO_PAGAMENTO_DB_ENABLED=true` no preparo do banco próprio. O workflow atual exige Kubernetes pronto e usa Job no EKS para preparar e validar o banco SQL. Só avançar após `status/rds` e `status/rds-orcamento-pagamento` prontos e limpeza do Job/Secret temporários confirmada. |
| Workers | Execução usa `PROCESSAMENTO_REMOTO_HABILITADO`; API e Orçamento/Pagamento usam `PROCESSAMENTO_REMOTO_ENABLED`. São nomes diferentes e não intercambiáveis. Ativação requer fila OS publicada; API também exige filas dos dois participantes antes de habilitar seu worker. |
| Corte distribuído | `FLUXO_DISTRIBUIDO_ENABLED` na API, Orçamento/Pagamento e Gateway, respeitando a sequência do README. Na API, true exige worker e clientes privados. No Orçamento, true exige worker. No Gateway, true exige os guardrails de workloads/flags/Lambda/listeners já implementados. |
| Mercado Pago | `MERCADO_PAGO_ACCESS_TOKEN`, `MERCADO_PAGO_WEBHOOK_SECRET`, `MERCADO_PAGO_RECEBEDOR_ID` e `MERCADO_PAGO_URL_NOTIFICACAO`, com `MERCADO_PAGO_ATIVO=true` somente após configuração completa. URL HTTPS do Gateway efetivamente criado mais a rota `/api/v1/pagamentos/notificacoes/mercado-pago`; não inventar hostname. Validar conta vendedora, comprador e checkout de teste; configuração aceita pelo workflow não comprova pagamento. |
| Datadog | `DD_API_KEY` no Kubernetes e Auth, site da conta conferido (`DD_SITE` tem default `datadoghq.com` no workflow Kubernetes). Usar o workflow existente `datadog-observability.yml`; Agent/Cluster Agent não importam o painel Saga nem criam monitores. Importação, ingestão, baseline e disparo/recuperação permanecem etapas de aceite. |

Na primeira implantação, seguir a tabela de dez passos do README: API sem worker/corte e com clientes privados; Auth; Execução; Orçamento com pagamento desligado; API com worker/corte; Gateway; configuração do Webhook e ativação do pagamento. A consulta privada não usa a conexão SQL da Lambda. O JWT compartilhado exige mesma chave, issuer e audience; `CLIENTES_INTERNOS_TOKEN` é outra credencial.

Uma alteração de configuração pode exigir nova execução completa de um CD de apply já autorizado, gerando plano da nova tentativa. Não repetir apenas o job que aplica um plano antigo. Conferir a disponibilidade do artefato de imagem validado e não usar uma execução documental como substituto de deploy.

## Segurança e limites de recuperação

Reset da AWS Academy não substitui destroy controlado: pode deixar recursos, contratos ou state inconsistentes. Credenciais expiradas, acesso negado e falhas de rede não comprovam ausência. Preserve chaves, caches e dados; não faça purge de filas nem edição manual de state. Interrompa a recuperação diante de identidade divergente ou recursos órfãos.

O ambiente acadêmico compartilha EKS, instância RDS e identidade LabRole. Os bancos e credenciais de aplicação são próprios; a identidade de Pods e a disponibilidade física não têm isolamento de produção. Datadog de Kubernetes/APM não equivale a integração completa de métricas AWS/RDS/SQS/Lambda.
