# ADR-0023 — Separação de ownership da infraestrutura compartilhada e deploy da API

## Status

**Status:** ✅ Aceito
**Data:** 13/09/2026
**Autores:** Gabriel de Sousa Silva, Geovanna Monteiro Scabio

---

## 1. Contexto e Problema

A Fase 3 possui seis esteiras por responsabilidade. VPC, Kubernetes/EKS/ECR e RDS possuem ciclos de vida próprios e publicam contratos não secretos via SSM; a credencial do RDS permanece no AWS Secrets Manager.

## 2. Fatores Decisivos (Drivers)

- Separar o ciclo de vida da infraestrutura compartilhada do workload da API.
- Evitar ownership duplicado e preservar os contratos SSM.
- Reduzir o risco operacional da entrega sem migrar o ECR imediatamente.

## 3. Decisão Proposta

Este repositório deixa de criar VPC, EKS, RDS e ECR compartilhado. Ele consome os contratos SSM e resolve temporariamente o segredo do RDS no CD para formar uma variável sensível de conexão. A API mantém o build/publicação da imagem, o deploy e os recursos Kubernetes específicos da aplicação.

O ECR permanece temporariamente em `oficina-mecanica-infra-kubernetes` para reduzir risco próximo à entrega. A possível migração futura para o repositório da API está registrada no backlog.

## 4. Justificativa

A separação acompanha as responsabilidades dos repositórios já existentes e permite implantar a aplicação sem recriar rede, cluster ou banco. Manter temporariamente o ECR em Kubernetes reduz mudanças simultâneas e preserva a infraestrutura validada.

## 5. Consequências (Trade-offs)

### ✅ Positivo (Ganhos)

- States e pipelines independentes, com ownership explícito.
- Menor duplicidade de provisionamento e dependências documentadas.

### ❌ Negativo (Perdas/Riscos)

- A API depende da disponibilidade dos outputs e recursos compartilhados.
- O ECR permanece com ciclo de vida separado da aplicação; sua evolução exige planejamento coordenado.

Há menor duplicidade, states separados, pipelines independentes e ownership explícito. A ordem operacional passa a ser VPC → Kubernetes/EKS/ECR → RDS → API → Auth Lambda → API Gateway. Helm não é introduzido nesta mudança; a possível centralização futura de manifests fica no backlog.

Após o primeiro apply seguro da API desacoplada, os módulos legados, o workflow de import e o arquivo transicional de state foram removidos deste repositório. A API mantém apenas o workload Kubernetes e seus contratos de consumo.

## 6. Referências

[ADR-0022 — Contratos SSM](adr-0022-uso-do-aws-systems-manager-parameter-store-para-compartilhamento-de-outputs-entre-esteiras.md) e [RFC-0003 — Separação de esteiras](../rfcs/rfc-0003-separacao-de-esteiras-por-recurso-e-responsabilidade.md).
