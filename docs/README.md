# Documentação

Este é o índice mestre da documentação do projeto. Use este arquivo para navegar sem depender do contexto da conversa.

## Leitura recomendada

1. [`../README.md`](../README.md) para visão geral, execução local, CI/CD e entrega.
2. [`architecture/adrs/`](architecture/adrs/) para decisões arquiteturais e trade-offs AWS Academy.
3. [`deploy/github-actions.md`](deploy/github-actions.md) para entender a esteira e o Git Flow.
4. [`deploy/deploy-aws.md`](deploy/deploy-aws.md) para provisionar, validar e destruir o ambiente AWS.

## Índice por tema

| Tema | Caminho | Quando usar |
| --- | --- | --- |
| Visão geral do projeto | [`../README.md`](../README.md) | Apresentação principal para avaliadores e execução rápida. |
| Deploy e esteira | [`deploy/README.md`](deploy/README.md) | Entrada para guias operacionais de CI/CD, AWS e destroy. |
| GitHub Actions | [`deploy/github-actions.md`](deploy/github-actions.md) | Explicar CI, CD Development, release lógico e production lógico. |
| AWS real | [`deploy/deploy-aws.md`](deploy/deploy-aws.md) | Configurar GitHub Environment, rodar deploy e encerrar recursos. |
| Guardrails AWS Academy | [`deploy/aws-academy-guardrails.md`](deploy/aws-academy-guardrails.md) | Evitar gasto indevido e operar com segurança no Learner Lab. |
| Projeto | [`projeto/README.md`](projeto/README.md) | Entrada para pendências do projeto. |
| Backlog técnico | [`projeto/backlog-tecnico.md`](projeto/backlog-tecnico.md) | Estado e critérios da entrega atual, evidências e dívidas técnicas. |
| Evidências | [`evidencias/`](evidencias/) | Registrar prints e saídas reais de qualidade, segurança e infra. |
| Operação e recuperação | [`deploy/deploy-aws.md`](deploy/deploy-aws.md) | Configuração e recuperação do ambiente pelas esteiras oficiais. |
| Postman | [`evidencias/postman.md`](evidencias/postman.md) | Importar collections, configurar endereços e executar os cenários manualmente. |
| OpenAPI | [`openapi/`](openapi/) | Consultar contrato versionado da API. |
| C4 Model | [`architecture/diagrams/c4-model/README.md`](architecture/diagrams/c4-model/README.md) | Consultar a modelagem histórica e seus arquivos Structurizr/SVG; usar o diagrama distribuído para a evolução atual. |
| Diagramas finais | [`architecture/diagrams/`](architecture/diagrams/) | Guardar diagramas de infraestrutura AWS e de fluxo CI/CD. |
| Arquitetura distribuída | [SVG](architecture/diagrams/distributed-architecture.svg), [PNG](architecture/diagrams/distributed-architecture.png) e [draw.io](architecture/diagrams/distributed-architecture.drawio) | Visão geral dos serviços, dados próprios e comunicação para vídeo/PDF; não comprova implantação. |

## Índice de infraestrutura

| Área | Caminho | Papel |
| --- | --- | --- |
| Docker local | [`../README.md#execucao-local-docker-compose`](../README.md#execucao-local-docker-compose) | Comandos do Docker Compose local. |
| Kubernetes | [`../k8s/README.md`](../k8s/README.md) | Manifests Kubernetes (local e base para EKS). |
| Terraform da API | [`../infra/terraform/environments/dev/`](../infra/terraform/environments/dev/) | Workload Kubernetes que consome a infraestrutura compartilhada por SSM e Secrets Manager. |

## Regras

- Não versionar credenciais, tokens, kubeconfig, secrets ou outputs sensíveis.
- O ambiente AWS Academy é temporário: todo `apply` deve ter plano de `destroy`.
