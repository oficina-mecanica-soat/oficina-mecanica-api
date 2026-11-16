# ☁️ Deploy na AWS

O ambiente físico é `development`. Homologation e production continuam promoções lógicas do Git Flow. Toda criação, atualização e destruição de recursos reais ocorre exclusivamente nas esteiras oficiais; comandos locais são somente de consulta, validação e planejamento.

## 🧭 Ordem das esteiras

A [sequência canônica no README](../../README.md#deploy-aws) e o [roteiro integrado](../evidencias/validacao-integrada.md#ordem-de-ativação-coordenada) descrevem os dez passos das oito esteiras. Este guia não mantém uma ordem concorrente.

VPC precede Kubernetes; RDS depende de Kubernetes para preparar seu banco próprio. A API publica primeiro sua fila e consulta privada. Execução e Orçamento/Pagamento publicam suas entradas e usam a fila OS. A API então recebe as filas dos participantes. Auth usa a consulta privada, e o Gateway sobe uma única vez após os três serviços estarem saudáveis. A URL pública permite completar o Webhook e atualizar somente a configuração do pagamento.

As passagens adicionais da API e de Orçamento/Pagamento são dependências da implementação atual. Não exigem destroy, criação de novos serviços ou troca de arquitetura. Não iniciar jornadas antes da configuração final.

## 🔑 Configuração da janela

- Usar credenciais temporárias da mesma conta/região nos oito Environments `development`.
- Preservar a chave JWT compartilhada e o contrato de issuer/audience dos três serviços e Auth.
- Manter secrets no GitHub Environment ou Secrets Manager; nunca imprimir valores, state, connection strings, JWT ou assinatura de Webhook.
- Configurar flags e dependências conforme o roteiro canônico. `AWS_DEPLOY_ENABLED` autoriza a operação, `TERRAFORM_ACTION` escolhe apply/destroy e `AUTO_PR_ENABLED` controla promoções; não são opções equivalentes.
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

Ausência ou corrupção de cache não autoriza esquecer recursos. Sem cache, a recuperação inicial existente exige recuperação dedicada com inventário vazio comprovado, plano exclusivo de destroy e state final sem recursos gerenciados. Se houver recursos, recuperar o state correto antes de prosseguir. Preservar caches, chaves e evidências; não usar state rm/import/mv como atalho.

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

A validação detalhada e as limitações de cada cenário ficam no [roteiro integrado](../evidencias/validacao-integrada.md).
