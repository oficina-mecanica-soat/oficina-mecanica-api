# Architecture Decision Records (ADRs)

Registro das decisões arquiteturais do projeto, no padrão definido pela equipe desde a Fase 1.

## Padrão de escrita

Use ADR-0001 a ADR-0004 como referência. Toda ADR deve conter título, status, data, autores e as seis seções: Contexto e Problema; Fatores Decisivos (Drivers); Decisão Proposta; Justificativa; Consequências (Trade-offs), separando ganhos e perdas/riscos; e Referências. Explique o problema, os motivos da escolha e as limitações concretas. Diferencie exigência do enunciado de decisão da equipe e decisão aceita de implementação comprovada. Preserve datas, autoria e histórico; alterações de decisão exigem registro sucessor. Não deixe frases interrompidas, referências genéricas quando houver documento identificável ou seções vazias.

## Índice

| ADR | Título | Data | Status |
| --- | --- | --- | --- |
| [ADR-0001](adr-0001-clean-architecture-monolito.md) | Clean Architecture em monólito | 01/05/2026 | ✅ Aceito |
| [ADR-0002](adr-0002-linguagem-framework-dotnet.md) | Linguagem e framework (.NET) | 01/05/2026 | ✅ Aceito |
| [ADR-0003](adr-0003-banco-dados-sql-server.md) | Banco de dados relacional (SQL Server) | 01/05/2026 | ✅ Aceito |
| [ADR-0004](adr-0004-autenticacao-autorizacao-jwt.md) | Autenticação e autorização (JWT) | 01/05/2026 | ✅ Aceito |
| [ADR-0005](adr-0005-webhook-notificacao-decisao-orcamento.md) | Notificação externa de decisão de orçamento via webhook com token compartilhado | 07/07/2026 | ✅ Aceito |
| [ADR-0006](adr-0006-rds-single-az-sem-backup.md) | RDS single-AZ sem backup automatizado para ambiente de laboratório | 07/07/2026 | ✅ Aceito |
| [ADR-0007](adr-0007-topologia-rede-aws-vpc-nat-unico.md) | Topologia de rede AWS: VPC, 2 AZs, subnets públicas/privadas e NAT Gateway único | 07/07/2026 | ✅ Aceito |
| [ADR-0008](adr-0008-orquestracao-kubernetes-eks-terraform.md) | Orquestração com Kubernetes/EKS e Terraform como IaC | 08/07/2026 | ✅ Aceito |
| [ADR-0009](adr-0009-load-balancer-via-kubernetes-service.md) | Load Balancer provisionado via Kubernetes Service em vez de recurso Terraform explícito | 08/07/2026 | ✅ Aceito |
| [ADR-0010](adr-0010-pipeline-cicd-estagios-deploys-logicos.md) | Pipeline CI/CD em estágios com Git Flow automatizado e deploys lógicos | 09/07/2026 | ✅ Aceito |
| [ADR-0011](adr-0011-controle-apply-destroy-arquivo-versionado.md) | Controle de apply/destroy do Terraform via arquivo versionado com aprovação por PR | 11/07/2026 | ✅ Aceito |
| [ADR-0012](adr-0012-terraform-state-local-cache-github-actions.md) | Terraform state em backend local com cache do GitHub Actions | 11/07/2026 | ✅ Aceito |
| [ADR-0013](adr-0013-autoscaling-hpa-runtime-dotnet.md) | Autoscaling horizontal via HPA e ajuste de runtime .NET | 12/07/2026 | ✅ Aceito |
| [ADR-0014](adr-0014-organizacao-dos-entregaveis-da-fase-3-em-seis-esteiras.md) | Organização dos Entregáveis da Fase 3 em Seis Esteiras | 31/08/2026 | ✅ Aceito |
| [ADR-0015](adr-0015-uso-do-aws-api-gateway-como-porta-publica-unica.md) | Uso do AWS API Gateway como Porta Pública Única | 31/08/2026 | ✅ Aceito |
| [ADR-0016](adr-0016-migracao-do-load-balancer-publico-para-nlb-interno.md) | Migração do Load Balancer Público para NLB Interno | 31/08/2026 | ✅ Aceito |
| [ADR-0017](adr-0017-uso-de-vpc-link-para-integracao-privada-entre-api-gateway-e-kubernetes.md) | Uso de VPC Link para Integração Privada entre API Gateway e Kubernetes | 31/08/2026 | ✅ Aceito |
| [ADR-0018](adr-0018-execucao-da-lambda-de-autenticacao-dentro-da-vpc.md) | Execução da Lambda de Autenticação dentro da VPC | 31/08/2026 | ✅ Aceito |
| [ADR-0019](adr-0019-autenticacao-das-apis-por-jwt-emitido-pela-lambda.md) | Autenticação das APIs por JWT Emitido pela Lambda | 31/08/2026 | ✅ Aceito |
| [ADR-0020](adr-0020-inclusao-do-status-do-cliente-no-modelo-relacional.md) | Inclusão do Status do Cliente no Modelo Relacional | 31/08/2026 | ✅ Aceito |
| [ADR-0021](adr-0021-datadog-como-solucao-de-observabilidade.md) | Datadog como Solução de Observabilidade | 31/08/2026 | ✅ Aceito |
| [ADR-0022](adr-0022-uso-do-aws-systems-manager-parameter-store-para-compartilhamento-de-outputs-entre-esteiras.md) | Uso do AWS Systems Manager Parameter Store para Compartilhamento de Outputs entre Esteiras | 31/08/2026 | ✅ Aceito |
| [ADR-0023](adr-0023-separacao-ownership-infraestrutura-compartilhada-deploy-api.md) | Separação de ownership da infraestrutura compartilhada e deploy da API | 13/09/2026 | ✅ Aceito |
| [ADR-0024](adr-0024-evolucao-para-microsservicos.md) | Evolução para três microsserviços | 09/11/2026 | ✅ Aceito para implementação |
| [ADR-0025](adr-0025-bancos-e-mensageria-aws.md) | Bancos isolados e mensageria AWS | 09/11/2026 | ✅ Aceito para implementação |
| [ADR-0026](adr-0026-saga-orquestrada.md) | Saga orquestrada no serviço de OS | 09/11/2026 | ✅ Aceito para implementação |

## Origem

ADR-0001 a ADR-0004 foram criadas originalmente na Fase 1 do Tech Challenge e trazidas para este repositório na Fase 2. ADR-0005 a ADR-0013 registram decisões arquiteturais tomadas durante a própria Fase 2, **numeradas na ordem cronológica real em que foram tomadas** (conferida no histórico de commits do repositório, não na ordem em que foram documentadas): primeiro a API de webhook e as peças de infraestrutura AWS (RDS, rede, orquestração EKS, Load Balancer), depois o desenho do pipeline de CI/CD, e por último os ajustes operacionais de Terraform e o tuning de autoscaling, descobertos já com o ambiente rodando de verdade.

Cada ADR desta fase deixa explícito, na seção "Contexto e Problema", o que é **exigência literal do enunciado do Tech Challenge** e o que foi **decisão adicional da equipe** — várias peças de infraestrutura AWS (RDS, VPC, NAT Gateway, Load Balancer, EKS gerenciado) foram escolhas voluntárias para ir além do mínimo exigido (o enunciado aceita cluster Kubernetes local com a mesma nota), enquanto o webhook de orçamento e o HPA por CPU/memória são requisitos obrigatórios citados textualmente.

Fonte: [`docs/projeto/enunciado-fase-2-tech-challenge.pdf`](../../projeto/enunciado-fase-2-tech-challenge.pdf) (enunciado oficial da FIAP).
