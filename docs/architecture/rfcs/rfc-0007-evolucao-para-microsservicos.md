# RFC-0007 — Evolução para microsserviços

## Status

Proposta inicial; contratos e fronteiras ainda precisam de validação antes da implementação integrada.

## Contexto

O enunciado da Fase 4 exige ao menos três microsserviços independentes, repositório, infraestrutura e banco por serviço, SQL e NoSQL, mensageria, Saga com compensação, testes e deploy em Kubernetes. A aplicação existente é um monólito modular com contextos de Atendimento, Administrativo, Gestão de Estoque e Gestão de Ordem de Serviço.

## Direção acordada

- Evoluir a aplicação para microsserviços, preservando as regras de domínio e avaliando a sucessão da ADR-0001.
- Utilizar DynamoDB e SQS; avaliar SNS apenas quando um evento precisar de múltiplos consumidores. Verificar as permissões de criação e operação no AWS Academy antes do provisionamento.
- Preservar a infraestrutura e as esteiras existentes como referência, observando independência e ownership de cada serviço.
- Manter bancos isolados por serviço, com ao menos um SQL e um NoSQL. Nenhum serviço acessa diretamente o banco de outro.

## Questões a resolver antes da implementação integrada

- Delimitar serviços, responsabilidades, migração de dados, APIs e ownership de bancos sem perder os fluxos atuais.
- Definir Saga, estados, comandos, eventos, compensações, falhas, repetição e idempotência; validar a integração com Mercado Pago.
- Definir contratos, segurança, correlação, observabilidade, execução local, testes e fluxo completo de demonstração.
- Detalhar implantação e destruição, custos e limites do laboratório; avaliar o impacto sobre autenticação, API Gateway e consumidores existentes.

## Critérios de aceite

- Fronteiras, decisões, contratos e critérios de falha revisados e acordados; ADRs necessárias registradas sem apagar o histórico.
- Backlog dividido por dependências e responsáveis; cada serviço com CI/CD independente, testes, cobertura, documentação e evidências exigidas pelo enunciado.
