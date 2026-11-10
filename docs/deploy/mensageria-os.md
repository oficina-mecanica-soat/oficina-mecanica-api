# Fila de resultados da OS — preparação da Fase 4

## Escopo e decisões

A [ADR-0025](../architecture/adrs/adr-0025-bancos-e-mensageria-aws.md) atribui a cada consumidor sua entrada e DLQ; a [RFC-0007](../architecture/rfcs/rfc-0007-evolucao-para-microsservicos.md) define resultados de Execução e Orçamento/Pagamento destinados à fila de OS. O serviço de OS permanece no repositório `oficina-mecanica-api`; esta entrega não cria outro serviço ou repositório.

Gabriel prepara a infraestrutura/contrato neste PR, conforme alinhamento com o responsável pelo projeto. Saga SQL, outbox, consumidor de resultados e transições de OS continuam na F4-06 de Geovanna. Não altera código .NET, rotas, eventos, banco ou comportamento público atual. Não ativa consumidor nem concede acesso IAM.

## Nomes propostos e ownership

Os nomes abaixo são a proposta deste PR, derivados do componente existente e do ambiente. A RFC define os parâmetros operacionais, não esses nomes físicos. Aprovar o PR aprova o contrato antes de provisionar.

| Recurso em development | Nome | Configuração |
| --- | --- | --- |
| Entrada de OS | `oficina-mecanica-api-development.fifo` | FIFO, visibility 60 s, long polling 20 s, retenção 4 dias e maxReceiveCount 5. |
| DLQ de OS | `oficina-mecanica-api-development-dlq.fifo` | FIFO, retenção 14 dias e redrive aceito somente da entrada própria. |

São duas filas próprias da API, com SSE gerenciado pelo SQS e sem nova chave KMS. Deduplicação por conteúdo fica desabilitada: produtores enviam `MessageGroupId=ordemServicoId` e `MessageDeduplicationId=mensagemId`, mantendo a idempotência persistida. Não cria SNS, EKS, ECR, RDS ou VPC. Não muda os recursos/contratos existentes dos owners compartilhados.

Nomes e contratos usam `ssm_environment_name`; as tags das novas filas seguem o ambiente informado, sem alterar as tags históricas do restante do workload. Homologation/production continuam promoções lógicas conforme a arquitetura atual; testes de parametrização não significam implantação desses ambientes.

## Contratos para Execução e Orçamento/Pagamento

Parâmetros SSM `String`, sem credenciais, sob `/oficina-mecanica/{ambiente}/api/`:

| Sufixo | Origem e consumidor |
| --- | --- |
| `entrada_queue_url` | URL da entrada de OS; destino dos produtores de resultados Execução e Orçamento/Pagamento. |
| `entrada_queue_arn` | ARN da entrada de OS; referência para a política de acesso na futura integração. |
| `dlq_url` | URL da DLQ de OS; investigação/reprocessamento controlado, sem publicação normal de resultados. |
| `dlq_arn` | ARN da DLQ de OS. |

Output Terraform `os_messaging_contracts` publica o mesmo mapa; `os_messaging_ssm_prefix` publica o prefixo. `/oficina-mecanica/{ambiente}/status/api=ready` depende dos quatro contratos e da política de redrive. Esse marcador indica somente disponibilidade da infraestrutura de mensageria: não comprova saúde HTTP, consumo, estado da Saga ou identidade IAM do workload.

Os produtores só poderão consumir os contratos após um apply autorizado publicar os recursos. Não apontar resultados à fila de entrada do Execução e não usar sua DLQ como destino normal. Configuração e identidade dos workloads serão tratadas no incremento de integração; não se presume que a role dos nodes ou a credencial do runner sejam adequadas para os Pods.

## CI/CD e laboratório

Preserva Git Flow, nomes dos jobs/checks, Quality gate, imagem validada no CI, plano salvo e state isolado por conta/ambiente. Acrescenta três testes Terraform com providers simulados no job já existente, sem nova esteira paralela. O deploy real permanece somente pelo workflow AWS.

`AWS_DEPLOY_ENABLED=false`, definido no repositório, bloqueia a chamada do deploy pelo CD; o workflow reutilizável também exige habilitação explícita. Mudança implantável sem deploy não abre promoção como se estivesse aplicada. Alterações apenas documentais preservam o fluxo existente. O controle `terraform-action.env` permanece inalterado e destroy continua exigindo alteração dedicada.

A restauração do state no job de aplicação agora precede sua validação e a preparação de destroy, como no Execução. Isso corrige a ordem anterior, que consultava o arquivo antes de restaurá-lo. Artefato de plano e caches gerados incluem a tentativa da execução, evitando colisão em reruns; o prefixo de restore continua restrito à mesma conta e ambiente, compatível com o histórico.

Após apply autorizado, a esteira comparará atributos FIFO/retenção/redrive e contratos SSM com as filas registradas no state. Antes do plano de destroy e imediatamente antes da aplicação, verifica mensagens visíveis, em voo e atrasadas na entrada e DLQ. Pendências, respostas inválidas e erros de acesso bloqueiam; só o erro específico de ausência permite recuperação. O contexto não sensível das filas acompanha o plano para verificar remoção e limpeza dos cinco parâmetros após esvaziar o state. Não faz purge nem redrive automático.

**Parar produtores/consumidores e preservar dados antes de autorizar destroy.** Contagens SQS são aproximadas; o guardrail não substitui a quiescência operacional. Esta entrega não consulta a futura outbox SQL nem certifica que a Saga está sem pendências: essa integração pertence à implementação da F4-06. Destruir as filas elimina mensagens e evidências ainda retidas; não é uma rotina automática deste PR.

Nenhuma consulta AWS, plano autenticado, apply ou destroy foi executado. Avisar Gabriel antes da janela para criar novo laboratório e atualizar environments. O merge prepara o contrato; não comprova disponibilidade dos recursos ou permissões reais.

## Validação

Terraform 1.15.8: formatação, inicialização sem backend e validate aprovados. Três cenários com providers AWS/Kubernetes simulados aprovados: configuração FIFO/redrive da RFC, contratos SSM e isolamento de nomes por ambiente. Nove testes Node aprovados, incluindo guardrails originais, mensagens pendentes, state parcial, metadados de filas/SSM e distinção entre ausência e AccessDenied/credencial expirada/rede/JSON inválido. actionlint 1.7.12 aprovou os workflows, sem ShellCheck/Pyflakes. Nenhum desses testes comprova SQS/IAM real.

```powershell
terraform fmt -check -recursive infra/terraform
terraform -chdir=infra/terraform/environments/dev init -backend=false -input=false
terraform -chdir=infra/terraform/environments/dev validate
terraform -chdir=infra/terraform/environments/dev test
```

O CI repete os gates completos da API, incluindo testes .NET/cobertura, Docker, manifests e Terraform. A próxima validação real exige janela autorizada e não conclui, isoladamente, a Saga ou o fluxo de negócio ponta a ponta.

## Organização das validações

Os nove testes operacionais continuam no CI, agora definidos diretamente em `ci.yml`, usando o mesmo conteúdo operacional incorporado em `aws-deploy.yml`. Nenhuma pasta de scripts é versionada. Os jobs e gates são preservados; arquivos auxiliares da execução existem apenas no diretório temporário do runner. O gate de estrutura é executado para todo PR, inclusive documental.
