# ADR-0025 — Bancos isolados e mensageria AWS

## Status

Aceita para implementação em 09/11/2026. Permissões de provisionamento do laboratório ainda serão verificadas pela esteira.

## Contexto e fatores decisivos

O enunciado exige SQL e NoSQL, bancos próprios, comunicação assíncrona e deploy Kubernetes. A equipe escolheu DynamoDB e SQS; o prazo favorece os serviços gerenciados já alinhados à AWS.

## Decisão

OS conserva seu SQL Server. Orçamento/Pagamento usa outro banco lógico SQL Server com credencial e migrations próprios; pode compartilhar a instância RDS acadêmica, mas não schema, credencial ou consultas. A infraestrutura de bancos continua no repositório RDS com outputs separados por serviço. Confirmar a viabilidade das credenciais isoladas no primeiro trabalho dessa frente; não substituir por credencial master compartilhada silenciosamente.

Execução usa DynamoDB, provisionado no próprio repositório do serviço, com capacidade sob demanda e chaves definidas pelos acessos: execução por OS, estoque por item e filial, reservas por OS e item. Leituras de lista usam índices explícitos; evitar Scan em operações normais. Escritas condicionais/versionamento protegem saldo e transições; reservas e marcadores de deduplicação são gravados atomicamente com o estado relacionado.

Cada consumidor é dono da fila SQS de entrada e sua DLQ, provisionadas no respectivo repositório. Três filas de entrada: OS recebe resultados, Orçamento/Pagamento recebe comandos, Execução recebe comandos. Usar FIFO agrupada por ordemServicoId; idempotência permanece obrigatória além da deduplicação do transporte. SNS fica fora da implantação inicial, pois cada mensagem possui um destinatário. Adotar somente mediante necessidade documentada de múltiplos assinantes.

Persistir estado e outbox na mesma transação local; um BackgroundService publica pendências e marca envio. Consumidor confirma mensagem somente após commit de estado, deduplicação e outbox. SQL usa transação EF; DynamoDB usa transação/condições. Não introduzir framework de eventos ou biblioteca compartilhada antes de uma necessidade comprovada.

## Infraestrutura e consequências

EKS, VPC, Gateway, Datadog e CI/CD mantêm os padrões vigentes. Kubernetes mantém ECR compartilhado durante a entrega, incluindo novos registries por serviço, conforme ADR-0023. SSM publica URLs/ARNs não secretos; Secrets Manager armazena material sensível. Identidade de workloads seguirá a capacidade real do laboratório e o padrão validado, sem credenciais em código ou logs.

Provisionamento, publicação e destruição reais exclusivamente pelas esteiras. Consultas locais são read-only. Apply na ordem rede/plataforma/bancos → recursos dos serviços → workloads → Gateway; destruir na ordem inversa respeitando filas/outbox pendentes e dados persistentes.

## Referências

- [DynamoDB: transações](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/transaction-apis.html)
- [SQS: visibility timeout](https://docs.aws.amazon.com/AWSSimpleQueueService/latest/SQSDeveloperGuide/sqs-visibility-timeout.html)
- ADR-0022, ADR-0023 e RFC-0005.

