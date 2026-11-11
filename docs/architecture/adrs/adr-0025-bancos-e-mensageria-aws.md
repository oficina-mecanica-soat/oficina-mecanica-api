# ADR-0025 — Bancos isolados e mensageria AWS

## Status

**Status:** ✅ Aceito para implementação
**Data:** 09/11/2026
**Autores:** Gabriel de Sousa Silva, Geovanna Monteiro Scabio

A decisão define a arquitetura alvo; implementação, provisionamento e evidências permanecem sujeitos às tarefas e validações do backlog.

---

## 1. Contexto e Problema

O enunciado exige SQL e NoSQL, bancos próprios, comunicação assíncrona e deploy Kubernetes. A equipe escolheu DynamoDB e SQS; o prazo favorece os serviços gerenciados já alinhados à AWS.

## 2. Fatores Decisivos (Drivers)

- Cumprir o uso de SQL e NoSQL com dados isolados por serviço.
- Adotar DynamoDB e SQS conforme escolha da equipe.
- Reduzir administração de infraestrutura e preservar os padrões AWS existentes.
- Tratar duplicatas, concorrência e falhas sem perder mudanças de negócio.

## 3. Decisão Proposta

OS conserva seu SQL Server. Orçamento/Pagamento usa outro banco lógico SQL Server com credencial e migrations próprios; pode compartilhar a instância RDS acadêmica, mas não schema, credencial ou consultas. A infraestrutura de bancos continua no repositório RDS com outputs separados por serviço. Confirmar a viabilidade das credenciais isoladas no primeiro trabalho dessa frente; não substituir por credencial master compartilhada silenciosamente.

Execução usa DynamoDB, provisionado no próprio repositório do serviço, com capacidade sob demanda e chaves definidas pelos acessos: execução por OS, estoque por item e filial, reservas por OS e item. Leituras de lista usam índices explícitos; evitar Scan em operações normais. Escritas condicionais/versionamento protegem saldo e transições; reservas e marcadores de deduplicação são gravados atomicamente com o estado relacionado.

Cada consumidor é dono da fila SQS de entrada e sua DLQ, provisionadas no respectivo repositório. Três filas de entrada: OS recebe resultados, Orçamento/Pagamento recebe comandos, Execução recebe comandos. Usar FIFO agrupada por ordemServicoId; idempotência permanece obrigatória além da deduplicação do transporte. SNS fica fora da implantação inicial, pois cada mensagem possui um destinatário. Adotar somente mediante necessidade documentada de múltiplos assinantes.

Persistir estado e outbox na mesma transação local; um BackgroundService publica pendências e marca envio. Consumidor confirma mensagem somente após commit de estado, deduplicação e outbox. SQL usa transação EF; DynamoDB usa transação/condições. Não introduzir framework de eventos ou biblioteca compartilhada antes de uma necessidade comprovada.

EKS, VPC, Gateway, Datadog e CI/CD mantêm os padrões vigentes. Kubernetes mantém ECR compartilhado durante a entrega, incluindo novos registries por serviço, conforme ADR-0023. SSM publica URLs/ARNs não secretos; Secrets Manager armazena material sensível. Identidade de workloads seguirá a capacidade real do laboratório e o padrão validado, sem credenciais em código ou logs.

Provisionamento, publicação e destruição reais exclusivamente pelas esteiras. Consultas locais são read-only. Apply na ordem rede/plataforma/bancos → recursos dos serviços → workloads → Gateway; destruir na ordem inversa respeitando filas/outbox pendentes e dados persistentes.

## 4. Justificativa

SQL Server preserva a persistência relacional existente e atende ao registro de orçamento e cobrança. DynamoDB atende aos acessos delimitados de execução e estoque, com condições e transações locais. SQS oferece transporte desacoplado; FIFO por OS auxilia a ordenação, sem substituir idempotência. Outbox reduz a janela de perda entre persistência e publicação.

## 5. Consequências (Trade-offs)

### ✅ Positivo (Ganhos)

- Uso de serviços gerenciados e aderência à infraestrutura AWS existente.
- Isolamento lógico e credenciais próprias por serviço.
- Publicação recuperável e processamento idempotente.

### ❌ Negativo (Perdas/Riscos)

- Compartilhar uma instância RDS não oferece isolamento físico nem de disponibilidade.
- Acesso DynamoDB depende de chaves e índices projetados para as consultas.
- IAM, custos e permissões do laboratório precisam ser comprovados pela esteira.
- Outbox, DLQ e compensações acrescentam operação e testes.

## 6. Exceção acadêmica de identidade do Execução — 11/11/2026

Gabriel aprovou o uso excepcional da identidade existente dos nodes (`LabRole`) pelo workload do Execução no laboratório AWS Academy. Esta decisão complementa a escolha de identidade condicionada à capacidade real do laboratório; não substitui o objetivo de identidade própria e menor privilégio fora desse ambiente. Não autoriza alteração da trust policy, ampliação de permissões ou criação de role, IRSA/EKS Pod Identity como efeito indireto.

O levantamento read-only não encontrou cluster EKS ativo em `us-east-1` ou role com trust para IRSA/EKS Pod Identity. A simulação da `LabRole` indicou acesso às operações DynamoDB/SQS exigidas pelos adaptadores. A simulação de administração IAM retornou negação implícita com contexto faltante, e a leitura das políticas da sessão foi negada; isso não comprova indisponibilidade universal desses recursos no Academy. A exceção é uma decisão explícita para este laboratório, não uma conclusão de que IAM isolado seja impossível em qualquer ambiente.

A identidade é compartilhada: não há isolamento IAM entre aplicações que consigam obter as credenciais do node, e as permissões efetivas da role podem exceder a matriz mínima do Execução. A separação de bancos, ausência de consultas cruzadas e configuração por serviço permanecem obrigatórias, mas não constituem barreira IAM contra outros Pods. O risco e a limitação devem constar das evidências finais. A concessão não se estende automaticamente a outros serviços.

A implementação deverá selecionar esse caminho explicitamente e verificar a identidade efetivamente obtida dentro do workload antes de habilitar o processamento remoto. Não aceitar como prova a identidade do runner, a presença do nome `LabRole`, um healthcheck HTTP ou apenas a simulação IAM. Ausência de credenciais, identidade divergente ou acesso negado devem bloquear a ativação; não usar modo local ou outra fonte de credenciais por fallback silencioso. Não copiar secrets AWS do GitHub para o Pod, persistir credenciais no Terraform ou registrá-las em logs.

O futuro teste autorizado deverá comprovar acesso à tabela própria, consumo/renovação/ACK da entrada e publicação na fila de OS, com reinício/outbox e duplicatas, conforme a matriz do README do Execução. Restrições de rede/metadados e a configuração do SDK precisam ser verificadas no Pod; este registro não altera esses controles. IRSA/Pod Identity e isolamento por workload ficam como evolução para um ambiente que permita a configuração apropriada.

Este ajuste é somente arquitetural. Não cria recursos, muda roles, habilita acesso a metadados, ativa o runtime ou autoriza apply/deploy/destroy. A implementação e a prova real seguem em PR/janela próprios, pelas esteiras existentes. `AWS_DEPLOY_ENABLED=false` permanece até autorização específica.

## 7. Referências

- [DynamoDB: transações](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/transaction-apis.html)
- [SQS: visibility timeout](https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-visibility-timeout.html)
- ADR-0022, ADR-0023 e RFC-0005.
