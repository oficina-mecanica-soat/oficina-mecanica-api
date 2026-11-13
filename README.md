# 🔧 Oficina Mecânica API

API REST para atendimento, execução e acompanhamento de ordens de serviço em uma oficina mecânica.

Projeto desenvolvido para o **Tech Challenge — Fase 3 da Pós Tech FIAP em Arquitetura de Software**, com foco em Clean Architecture, modelagem de domínio, autenticação serverless, execução em Kubernetes, AWS, observabilidade e rastreabilidade de qualidade via CI/CD.

---

## 📌 Índice

- [✨ Visão geral](#visao-geral)
- [🧩 Repositórios da solução](#repositorios-da-solucao)
- [✅ Funcionalidades](#funcionalidades)
- [🏗️ Arquitetura](#arquitetura)
- [🧭 Documentação e diagramas](#documentacao-e-diagramas)
- [🧰 Tecnologias](#tecnologias)
- [📁 Estrutura do repositório](#estrutura-do-repositorio)
- [🚀 Execução local com Docker Compose](#execucao-local-docker-compose)
- [☸️ Execução local com Kubernetes](#execucao-local-kubernetes)
- [☁️ Deploy AWS](#deploy-aws)
- [🔁 CI/CD](#cicd)
- [🌿 Convenção Git Flow](#convencao-git-flow)
- [🧪 Testes e qualidade](#testes-e-qualidade)
- [🔐 Autenticação](#autenticacao)
- [📊 Observabilidade](#observabilidade)
- [📚 Swagger, OpenAPI e collection](#swagger-openapi-collection)
- [🗄️ Banco de dados e seed](#banco-de-dados-e-seed)

---

<a id="visao-geral"></a>

## ✨ Visão geral

A solução simula um sistema integrado para uma oficina mecânica, cobrindo o fluxo desde o atendimento inicial até a entrega do veículo ao cliente.

| Perfil | O que faz |
| --- | --- |
| 👩‍💼 **Atendente** | Cadastra clientes e veículos, abre ordens de serviço, acompanha orçamento e registra entrega. |
| 👨‍🔧 **Mecânico** | Inicia diagnóstico, define serviços, reserva peças, executa e finaliza serviços. |
| 👤 **Cliente** | Consulta o status das próprias ordens de serviço com JWT emitido pela Auth Lambda. |
| 🛠️ **Administrador** | Opera cadastros administrativos e apoia a gestão da oficina. |

---

<a id="repositorios-da-solucao"></a>

## 🧩 Repositórios da solução

Este README é a porta de entrada da solução. Cada infraestrutura possui repositório e pipeline próprios.

| Repositório | Responsabilidade |
| --- | --- |
| [`oficina-mecanica-api`](https://github.com/geoscabio/oficina-mecanica-api) | API .NET, regras de negócio, testes e workload Kubernetes. |
| [`oficina-mecanica-infra-vpc`](https://github.com/geoscabio/oficina-mecanica-infra-vpc) | Rede base, subnets, NAT e contratos de VPC no SSM. |
| [`oficina-mecanica-infra-kubernetes`](https://github.com/geoscabio/oficina-mecanica-infra-kubernetes) | EKS, ECR, NLB interno e Datadog no cluster. |
| [`oficina-mecanica-infra-rds`](https://github.com/geoscabio/oficina-mecanica-infra-rds) | SQL Server privado, credenciais gerenciadas e contratos de banco no SSM. |
| [`oficina-mecanica-auth-lambda`](https://github.com/geoscabio/oficina-mecanica-auth-lambda) | Autenticação por documento e emissão do JWT de Cliente. |
| [`oficina-mecanica-infra-api-gateway`](https://github.com/geoscabio/oficina-mecanica-infra-api-gateway) | HTTP API pública, VPC Link, integrações e access logs. |

---

<a id="funcionalidades"></a>

## ✅ Funcionalidades

| Área | Funcionalidades principais |
| --- | --- |
| 🔐 **Identidade** | Login, geração de JWT e autorização por perfil. |
| 🤝 **Atendimento** | Cadastro e consulta de clientes e veículos. |
| 🧾 **Administrativo** | Cadastro de mecânicos, serviços de catálogo e peças/insumos. |
| 📦 **Estoque** | Entrada, consulta, reserva, baixa e estorno de itens. |
| 🧰 **Ordem de Serviço** | Abertura, diagnóstico, orçamento, aprovação, execução, finalização, entrega, cancelamento e status. |
| 📊 **Indicadores** | Consulta de tempo médio de execução de serviços. |
| ❤️ **Healthcheck** | Endpoint `/api/health` usado por Docker, Kubernetes e infraestrutura. |

### Fluxo principal

1. Cliente solicita atendimento.
2. Atendente identifica ou cadastra cliente e veículo.
3. Atendente abre a ordem de serviço.
4. Mecânico inicia diagnóstico.
5. Mecânico define serviços e reserva peças/insumos.
6. Sistema calcula orçamento.
7. Atendente envia orçamento por canal externo.
8. Cliente aprova ou recusa.
9. Mecânico executa e finaliza serviços.
10. Sistema baixa estoque reservado.
11. Atendente entrega o veículo.

Fluxos alternativos como estoque insuficiente, reprovação de orçamento, cancelamento com estorno e conflito de atualização também foram tratados.

---

<a id="arquitetura"></a>

## 🏗️ Arquitetura

O projeto adota **Clean Architecture** em um **monólito modular**, preservando o domínio de detalhes externos como HTTP, Swagger, banco de dados e infraestrutura.

A arquitetura final da Fase 3 mantém o API Gateway como entrada pública única:

```text
Internet
  -> API Gateway HTTP API
      -> POST /auth/documento -> Auth Lambda -> RDS
      -> ANY /api/{proxy+} -> VPC Link -> NLB interno
         -> EKS NodePort -> API Pods -> RDS
```

O NLB é interno e encaminha tráfego ao Service `NodePort`; não há acesso público direto à API no EKS.

| Camada | Projeto | Responsabilidade |
| --- | --- | --- |
| 🌐 **API** | `OficinaMecanica.API` | Controllers, Swagger, autenticação, autorização, middlewares e healthcheck. |
| 🧠 **Application** | `OficinaMecanica.Application` | Use cases, DTOs, validações, mapeamentos e orquestração dos fluxos. |
| 💎 **Domain** | `OficinaMecanica.Domain` | Agregados, entidades, value objects, enums, regras de negócio e contratos. |
| 🧱 **Infrastructure** | `OficinaMecanica.Infrastructure` | EF Core, SQL Server, repositories, migrations, seed e serviços JWT. |

### Decisões aplicadas

| Decisão | Aplicação prática |
| --- | --- |
| **Monólito modular** | Deploy único com organização por contextos: Administrativo, Atendimento, Estoque e Ordem de Serviço. |
| **Clean Architecture** | Dependências apontam para dentro; domínio não depende de API ou Infrastructure. |
| **DDD tático** | Uso de agregados, entidades, value objects, enums e regras no domínio. |
| **Use Cases** | Regras de aplicação centralizadas na camada Application. |
| **Repository Pattern** | Contratos no domínio e implementação na infraestrutura. |
| **JWT + perfis** | Segurança baseada em autenticação Bearer e autorização por papel. |
| **Resiliência de dados** | RowVersion/concurrency tokens para evitar sobrescrita silenciosa. |

---

<a id="documentacao-e-diagramas"></a>

## 🧭 Documentação e diagramas

| Item | Caminho |
| --- | --- |
| 🧭 Índice mestre de documentação | [`docs/README.md`](docs/README.md) |
| 🧩 C4 Model oficial validado | [`docs/architecture/diagrams/c4-model`](docs/architecture/diagrams/c4-model) |
| ☁️ Diagrama de infraestrutura AWS (inclui Kubernetes/Docker) | [`docs/architecture/diagrams/aws`](docs/architecture/diagrams/aws) |
| 🔁 Diagrama de fluxo CI/CD | [`docs/architecture/diagrams/ci-cd`](docs/architecture/diagrams/ci-cd) |
| 🐳 Docker Compose local | [`docker-compose.yml`](docker-compose.yml) |
| ☸️ Manifests Kubernetes | [`k8s`](k8s) |
| 📄 Evidências de qualidade | [`docs/evidencias`](docs/evidencias) |
| 🚀 Guias de deploy | [`docs/deploy`](docs/deploy) |
| 📌 Gestão do projeto | [`docs/projeto`](docs/projeto) |

> Os diagramas C4 já estão finalizados e validados. Os diretórios AWS, Kubernetes, Docker e CI/CD foram reservados para receber os arquivos finais da entrega.

---

<a id="tecnologias"></a>

## 🧰 Tecnologias

| Categoria | Tecnologias |
| --- | --- |
| Linguagem e plataforma | C#, .NET 10, ASP.NET Core Web API |
| Banco de dados | SQL Server 2022 |
| ORM | Entity Framework Core |
| API docs | Swagger/OpenAPI com Swashbuckle |
| Segurança | JWT Bearer, autorização por perfis e headers HTTP de segurança |
| Validação e mapeamento | FluentValidation e AutoMapper |
| Testes | xUnit, FluentAssertions, Moq, Testcontainers, Respawn e Coverlet |
| DevOps | Docker, Docker Compose, Kubernetes e GitHub Actions |
| AWS | Terraform, ECR, EKS, RDS, VPC e Load Balancer |
| Qualidade | `dotnet format`, cobertura mínima, SonarQube e OWASP ZAP |

---

<a id="estrutura-do-repositorio"></a>

## 📁 Estrutura do repositório

```text
.
├── .github/workflows/              # Esteira CI/CD
├── docs/                           # Índice, guias, evidências e diagramas
├── infra/terraform/                # Infraestrutura AWS real
│   ├── environments/dev/           # Ambiente development
│   └── modules/                    # Módulos AWS: VPC, ECR, RDS e EKS
├── k8s/                            # Manifests Kubernetes
├── src/                            # Código de produção
├── tests/                          # Testes unitários e integração
├── Dockerfile                      # Build da imagem da API
├── docker-compose.yml              # Docker Compose local
└── OficinaMecanica.sln
```

---

<a id="execucao-local-docker-compose"></a>

## 🚀 Execução local com Docker Compose

### Pré-requisitos

- Docker Desktop em execução.
- .NET SDK 10 instalado para comandos locais fora do container.

### Subir ambiente local

```powershell
Copy-Item .env.example .env
docker compose --env-file .env -f docker-compose.yml up -d --build
```

O Docker Compose sobe:

- SQL Server em `localhost,14333`;
- API em `http://localhost:5093`;
- migrations e seed demo automaticamente.

### Validar API

```powershell
curl.exe http://localhost:5093/api/health
```

Resposta esperada:

```text
Healthy
```

### Acessos locais

| Recurso | URL |
| --- | --- |
| Swagger | `http://localhost:5093/swagger` |
| Healthcheck | `http://localhost:5093/api/health` |
| OpenAPI JSON | `http://localhost:5093/swagger/v1/swagger.json` |

### Parar ambiente

```powershell
docker compose --env-file .env -f docker-compose.yml down
```

Para recriar banco/volume do zero:

```powershell
docker compose --env-file .env -f docker-compose.yml down -v
docker compose --env-file .env -f docker-compose.yml up -d --build
```

---

<a id="execucao-local-kubernetes"></a>

## ☸️ Execução local com Kubernetes

### Pré-requisitos

- Docker Desktop com Kubernetes habilitado.
- `kubectl` apontando para o contexto local.

### Aplicar manifests

```powershell
kubectl apply -R -f k8s/
kubectl rollout status deployment/sqlserver -n oficina-mecanica --timeout=180s
kubectl rollout status deployment/oficina-mecanica-api -n oficina-mecanica --timeout=180s
```

### Acessar API por port-forward

```powershell
kubectl port-forward service/oficina-mecanica-api 5093:8080 -n oficina-mecanica
```

Depois acesse:

```text
http://localhost:5093/swagger
http://localhost:5093/api/health
```

### Validar recursos

```powershell
kubectl get all,hpa,ingress -n oficina-mecanica
kubectl describe hpa oficina-mecanica-api-hpa -n oficina-mecanica
```

---

<a id="deploy-aws"></a>

## ☁️ Deploy AWS

A infraestrutura AWS real é provisionada por Terraform para o ambiente `development`. Os estágios `homologation` e `production` permanecem como deploys lógicos via Git Flow, mantendo rastreabilidade por PR mesmo sem ambientes físicos separados.

> Regra operacional: qualquer recurso criado em ambiente temporário precisa ter `terraform destroy` planejado após a demonstração.

### Fluxo operacional

1. Provisionar a VPC pelo repositório `oficina-mecanica-infra-vpc`.
2. Provisionar Kubernetes/ECR e RDS por seus repositórios; essas duas esteiras podem evoluir em paralelo após a VPC.
3. Publicar a imagem e o workload da API por este repositório.
4. Provisionar a Auth Lambda.
5. Provisionar o API Gateway por último, quando NLB e Lambda estiverem disponíveis.
6. Instalar/validar o Datadog no EKS pelo workflow do repositório Kubernetes e validar a instrumentação da API.
7. Em cada repositório, integrar a branch de trabalho em `develop`; mudanças deployáveis executam o pipeline próprio e alterações exclusivamente documentais pulam a AWS.
8. Promover por PR para `release` e `main` somente após o estágio anterior concluir com sucesso.

Os comandos de execução, dependências, outputs SSM, secrets e procedimento de `destroy` estão documentados no README de cada repositório. A ordem de destruição é a inversa, mantendo a VPC por último.

### Controle apply/destroy pela esteira

O CD da AWS é controlado pelo arquivo:

```text
infra/terraform/environments/dev/terraform-action.env
```

Para subir ou atualizar a AWS:

```env
TERRAFORM_ACTION=apply
```

Para destruir a AWS criada pelo Terraform:

```env
TERRAFORM_ACTION=destroy
```

Como usar:

1. Abrir uma branch a partir da `develop`.
2. Alterar somente `infra/terraform/environments/dev/terraform-action.env`.
3. Abrir PR para `develop`.
4. Após o merge, o workflow `🚀 CD Development` roda automaticamente.
5. Ao finalizar o destroy, abrir outro PR voltando para `TERRAFORM_ACTION=apply`.

> Segurança: `TERRAFORM_ACTION=destroy` só é aceito quando o arquivo `terraform-action.env` foi alterado no próprio merge. Isso evita destruir recursos por acidente em pushes futuros.

### Configuração obrigatória para CD AWS

Antes de mergear em `develop`, configurar no GitHub em `Settings > Environments > development`.

Environment secrets:

| Nome | Valor esperado |
| --- | --- |
| `AWS_ACCESS_KEY_ID` | Copiar de `AWS Details` / credenciais CLI do AWS Academy. |
| `AWS_SECRET_ACCESS_KEY` | Copiar de `AWS Details` / credenciais CLI do AWS Academy. |
| `AWS_SESSION_TOKEN` | Copiar de `AWS Details` / credenciais CLI do AWS Academy. Expira a cada sessão. |
| `DB_PASSWORD` | Criar uma senha forte para o RDS SQL Server. |
| `JWT_SECRET` | Criar uma string aleatória com pelo menos 32 caracteres. |
| `WEBHOOK_TOKEN` | Criar uma string aleatória com pelo menos 32 caracteres. |

Environment variables:

| Nome | Valor esperado |
| --- | --- |
| `AWS_REGION` | `us-east-1` |
| `EKS_CLUSTER_ROLE_NAME` | `LabRole`, salvo se o AWS Academy informar outro nome. |
| `EKS_NODE_ROLE_NAME` | `LabRole`, salvo se o AWS Academy informar outro nome. |

> Nunca versionar esses valores em `.env`, YAML, Terraform ou README. O repositório documenta os nomes, mas os valores ficam somente no GitHub Environment.

Guias:

- [`docs/deploy/aws-academy-guardrails.md`](docs/deploy/aws-academy-guardrails.md)
- [`docs/deploy/deploy-aws.md`](docs/deploy/deploy-aws.md)
- [`docs/deploy/github-actions.md`](docs/deploy/github-actions.md)

---

<a id="cicd"></a>

## 🔁 CI/CD

Os workflows ficam em [`.github/workflows/`](.github/workflows/) e foram separados por etapa para deixar o fluxo claro.

### Quando roda automaticamente

| Evento | O que acontece |
| --- | --- |
| `🧪 CI Development` | Em `pull_request` para `develop`, valida build, format, testes, cobertura, Docker e Kubernetes. |
| `🔎 CI Release` | Em `pull_request` para `release` ou `release/**`, valida build, format, testes, cobertura, Docker e Kubernetes. |
| `🛡️ CI Production` | Em `pull_request` para `main`, valida build, format, testes, cobertura, Docker e Kubernetes. |
| `🚀 CD Development` | Em `push` na `develop`, executa deploy AWS real somente para mudanças deployable e abre PR para `release`. |
| `☁️ AWS Deploy` | Chamado pelo CD de desenvolvimento, executa `apply` ou `destroy` da API na AWS conforme controle versionado. |
| `🔀 CD Release` | Em `push` na `release` ou `release/**`, registra deploy lógico em `homologation` e abre PR para `main`. |
| `🏁 CD Production` | Em `push` na `main`, registra deploy lógico em `production`. |

### Bloqueios de qualidade

A esteira falha se:

- algum teste falhar;
- algum teste for ignorado durante a coleta de cobertura;
- a cobertura global de linhas ficar abaixo de **90%**;
- `dotnet format --verify-no-changes` detectar formatação pendente;
- manifests Kubernetes não passarem no dry-run.

### Fluxo Git Flow automatizado

```text
feature/* -> PR develop -> deploy development quando necessário -> PR release -> deploy homologation -> PR main -> deploy production
```

O PR de branch de trabalho para `develop` é manual para economizar GitHub Actions no plano gratuito. Depois do merge em `develop`, a automação abre o próximo PR somente após o deploy do estágio anterior passar.

O deploy AWS real de `development` executa automaticamente após merge/push na `develop` somente quando há mudança deployable. Alterações de documentação, Markdown ou workflow seguem pelo Git Flow sem aplicar AWS. Os PRs automáticos de `develop -> release` e `release -> main` só executam com `AUTO_PR_ENABLED=true`.

Branches `develop`, `release`, `release/*` e `main` devem usar branch protection para bloquear commit direto e exigir PR com aprovação e status checks quando o plano do GitHub permitir.

> Observação: o repositório utiliza rulesets/branch protection configurados para `develop`, `release`, `release/*` e `main`. Em função das limitações do GitHub Free para repositórios privados, essas regras ficam documentadas e configuradas, mas a aplicação automática requer GitHub Team/Enterprise ou repositório público.

<a id="convencao-git-flow"></a>

### Convenção Git Flow

- Branches de trabalho nascem a partir de `develop` e seguem prefixos como `feature/*`, `bugfix/*`, `docs/*`, `test/*`, `ci/*` e `chore/*`.
- O fluxo padrão é `branch de trabalho -> PR develop -> deploy development -> PR release -> deploy homologation -> PR main -> deploy production`.
- `develop`, `release` e `main` não recebem commit direto; toda integração deve passar por PR, revisão e checks obrigatórios.
- Commits e PRs seguem Conventional Commits: `<type>(scope): <description>`, por exemplo `feat(api): add healthcheck endpoint`.

---

<a id="testes-e-qualidade"></a>

## 🧪 Testes e qualidade

### Comandos principais

```powershell
dotnet build OficinaMecanica.sln --no-restore
dotnet format OficinaMecanica.sln --verify-no-changes --no-restore
dotnet test OficinaMecanica.sln --no-build
```

### Cobertura

```powershell
dotnet test OficinaMecanica.sln `
  --configuration Release `
  --no-build `
  --collect:"XPlat Code Coverage" `
  --settings tests/sonarqube.runsettings `
  --results-directory TestResults `
  --logger "trx"
```

Evidência atual:

- **424 testes aprovados**
- **0 testes ignorados**
- **91.2% de cobertura global de linhas**
- CI configurado para bloquear cobertura abaixo de **90%**

Arquivos de evidência:

| Evidência | Caminho |
| --- | --- |
| Build, testes e cobertura | [`docs/evidencias/build-test.md`](docs/evidencias/build-test.md) |
| SonarQube | [`docs/evidencias/sonarqube.md`](docs/evidencias/sonarqube.md) |
| OWASP ZAP | [`docs/evidencias/owasp-zap.md`](docs/evidencias/owasp-zap.md) |
| Kubernetes HPA | [`docs/evidencias/kubernetes-hpa.md`](docs/evidencias/kubernetes-hpa.md) |
| Terraform apply/destroy | [`docs/evidencias/terraform-apply.md`](docs/evidencias/terraform-apply.md) |

---

<a id="autenticacao"></a>

## 🔐 Autenticação

Para usuários internos, permanece disponível o login da API:

```http
POST /api/v1/identidade/autenticacao/login
```

Usuários demo:

| Perfil | Login | Senha |
| --- | --- | --- |
| Administrador | `admin` | `admin123` |
| Atendente | `atendente` | `atendente123` |
| Mecânico | `mecanico` | `mecanico123` |

No Swagger, clique em **Authorize** e informe:

```text
Bearer <token>
```

Consulta pública de status não exige autenticação.

Na Fase 3, clientes autenticam pelo ponto de entrada público:

```http
POST /auth/documento
```

O cenário demonstrado usa CPF. A Auth Lambda valida o documento e o status do cliente, consulta o RDS e emite um JWT de Cliente sem transportar CPF/CNPJ. Esse token autoriza a rota protegida:

```http
GET /api/v1/clientes/me/ordens-servico
```

Essa rota obtém a identidade exclusivamente do claim `cliente_id`; não aceita substituir o cliente por parâmetro de rota ou query string.

---

<a id="observabilidade"></a>

## 📊 Observabilidade

A observabilidade da aplicação e do cluster usa Datadog:

- Agent e Cluster Agent no EKS;
- APM, traces e logs correlacionados da API;
- métricas de CPU e memória;
- latência P95, error rate e healthcheck;
- logs ao vivo e dashboard da entrega.

A Auth Lambda possui layers e variáveis de instrumentação Datadog e armazena a API key no AWS Secrets Manager. Entretanto, a ingestão de logs e traces da Lambda não foi evidenciada de ponta a ponta no ambiente acadêmico; essa lacuna permanece registrada como evolução pós-entrega. O API Gateway publica access logs no CloudWatch, sem afirmar a existência de um Datadog Forwarder que não foi validado.

---

<a id="swagger-openapi-collection"></a>

## 📚 Swagger, OpenAPI e collection

| Item | Caminho |
| --- | --- |
| Swagger local | `http://localhost:5093/swagger` |
| OpenAPI local | `http://localhost:5093/swagger/v1/swagger.json` |
| OpenAPI versionado (referência da Fase 2) | [`docs/openapi/oficina-mecanica-openapi.json`](docs/openapi/oficina-mecanica-openapi.json) |
| Collection Postman (referência da Fase 2) | [`docs/postman/oficina-mecanica-api.postman_collection.json`](docs/postman/oficina-mecanica-api.postman_collection.json) |
| Environments Postman | [`local`](docs/postman/oficina-mecanica-local.postman_environment.json) · [`AWS dev`](docs/postman/oficina-mecanica-aws-dev.postman_environment.json) |

O Swagger runtime é gerado pela aplicação atual e permanece a fonte mais fiel para explorar os endpoints executados. O OpenAPI e a collection Postman versionados ainda refletem a Fase 2 e não incluem integralmente o fluxo da Fase 3; sua atualização final está registrada como evolução pós-entrega. Não os use como evidência de cobertura do endpoint `POST /auth/documento`.

---

<a id="banco-de-dados-e-seed"></a>

## 🗄️ Banco de dados e seed

O projeto usa SQL Server com Entity Framework Core.

Configurações relevantes:

| Configuração | Função |
| --- | --- |
| `Database:ApplyMigrationsOnStartup` | Aplica migrations ao iniciar a API. |
| `Database:SeedDemoData` | Carrega dados demo para avaliação local. |

No Docker Compose local, o SQL Server fica disponível em:

```text
localhost,14333
```

Credenciais locais ficam no arquivo `.env`, criado a partir de `.env.example`.

---

<a id="observacoes"></a>

## 🏗️ Responsabilidade de infraestrutura da API

Este repositório mantém a aplicação principal .NET, Dockerfile, CI, build e publicação da imagem Docker, deploy no Kubernetes e os recursos Kubernetes específicos da aplicação enquanto a estratégia atual usa Terraform neste repositório.

VPC, subnets, NAT/Internet Gateway, rotas, EKS, RDS e o ECR compartilhado não são criados aqui. A API depende de `oficina-mecanica-infra-vpc`, `oficina-mecanica-infra-kubernetes` (EKS/ECR via SSM) e `oficina-mecanica-infra-rds` (endpoint e ARN do segredo via SSM). Auth Lambda e API Gateway são componentes separados.

As seis esteiras são: API, Auth Lambda, infra-vpc, infra-kubernetes, infra-rds e infra-api-gateway. Os quatro repositórios obrigatórios da Fase 3 são o mínimo; a separação adicional por responsabilidade é aceita quando ownership e dependências estão documentados.

Oficina Mecânica — API: https://github.com/geoscabio/oficina-mecanica-api 

Oficina Mecânica — Auth Lambda: https://github.com/geoscabio/oficina-mecanica-auth-lambda 

Oficina Mecânica — Infra Kubernetes: https://github.com/geoscabio/oficina-mecanica-infra-kubernetes 

Oficina Mecânica — Infra RDS: https://github.com/geoscabio/oficina-mecanica-infra-rds 

Oficina Mecânica — Infra VPC: https://github.com/geoscabio/oficina-mecanica-infra-vpc 

Oficina Mecânica — Infra API Gateway: https://github.com/geoscabio/oficina-mecanica-infra-api-gateway 

Helm não foi introduzido nesta mudança para reduzir risco. A evolução para Helm/manifests centralizados e a possível migração futura do ECR para a API estão registradas no backlog técnico.

Os módulos legados de VPC, EKS, RDS e ECR, o workflow de import e o arquivo transicional de state foram removidos após o primeiro apply seguro. Dockerfile e Docker Compose local permanecem independentes da infraestrutura AWS compartilhada.

- Não versionar credenciais, tokens, kubeconfig, secrets ou outputs sensíveis.
- Ambientes AWS temporários devem ser destruídos após a demonstração.
- O deploy para `main` deve usar branch protection e aprovação obrigatória de PR.
- O projeto prioriza rastreabilidade e simplicidade operacional, sem depender de contexto externo para ser compreendido.

### Reconciliação de destroy após reset do laboratório

O destroy restaura o cache Terraform existente e usa uma configuração temporária sem data sources de contratos SSM. O state original não é editado nem descartado: o provider confirma os recursos ausentes durante o plano de destruição. Somente o plano salvo, sem criação ou atualização, pode ser aplicado. Ao terminar, a esteira verifica que o state não contém recursos gerenciados antes de persistir o cache final. Falhas de autenticação, permissão ou rede bloqueiam a operação.

Na API, vínculos Kubernetes só podem ser retirados por blocos declarativos removed com destroy=false após ResourceNotFoundException confirmar que o EKS registrado no state não existe. Com EKS existente, permanece a configuração normal de destroy. Não há uso de state rm, import ou mv.

O retorno isolado de terraform-action.env para apply ainda é reconhecido como mudança de infraestrutura pelo detector de CD; não faça esse retorno enquanto não existir separação segura entre rearmar e provisionar. Preserve destroy após a reconciliação e só autorize nova subida em tarefa própria.

#### State isolado por conta AWS

As chaves de cache incluem o ID da conta retornado pela autenticação AWS, antes do ambiente e da execução. A restauração só pesquisa esse escopo, sem fallback para chaves históricas sem conta. Os caches anteriores permanecem preservados como histórico do laboratório encerrado 058264466885, inclusive quando contêm referências que a conta atual não consegue consultar. AccessDenied não é convertido em ausência.

Sem cache no escopo atual, a esteira bloqueia apply. Um destroy dedicado só pode iniciar state novo depois de consultas read-only confirmarem que não existem recursos ou contratos do projeto na conta atual. Recursos internos do laboratório são preservados. Depois disso, o plano exclusivo de destroy e a validação de state vazio produzem o cache usado pelas próximas execuções da mesma conta. Em laboratório com recursos existentes e cache ausente, é necessário recuperar o state correto; o bootstrap não libera provisionamento automático.

## Mensageria de OS — preparação da Fase 4

A infraestrutura da entrada FIFO de resultados da OS e de sua DLQ está preparada neste repositório, conforme ADR-0025/RFC-0007. Os nomes propostos, quatro contratos SSM sob `/oficina-mecanica/{ambiente}/api/`, status de infraestrutura, validações e recuperação estão em [Mensageria de OS](docs/deploy/mensageria-os.md). O contrato `entrada_queue_url` é o futuro destino dos resultados de Execução e Orçamento/Pagamento.

Este incremento não implementa nem ativa Saga, consumidor ou outbox SQL; essa parte continua com Geovanna na F4-06. Não altera rotas ou regras de negócio .NET. `status/api=ready` indica somente infraestrutura de mensageria, sem afirmar prontidão da aplicação ou da Saga.

**AWS continua desabilitada.** A variável de repositório `AWS_DEPLOY_ENABLED=false` impede provisionamento pelo CD e promoção de alteração implantável como se tivesse ocorrido deploy; o workflow reutilizável exige habilitação explícita. `terraform-action.env` permanece inalterado. Nenhum recurso foi criado nesta preparação. Antes de precisar do laboratório, avisar Gabriel para criá-lo e atualizar os environments.

Validações locais: Terraform 1.15.8 com três testes simulados, formatação/validate e nove testes Node aprovados; workflows conferidos com actionlint. O CI repete os gates existentes. Não confundir mocks com prova de acesso SQS pelo workload.

## Preparação da subida da API — F4-04

O controle da API retorna para `TERRAFORM_ACTION=apply`, mantendo `AWS_DEPLOY_ENABLED=false` até a janela autorizada. O workflow reutilizável exige habilitação explícita e a branch `develop`; arquivo de controle ausente, ação vazia ou inválida bloqueiam a operação, sem assumir `apply`. Destroy continua exigindo alteração dedicada no controle.

Na futura execução autorizada, a esteira consome os contratos de Kubernetes/ECR e RDS, restaura e valida o state da conta e gera um plano salvo. Publica a mesma imagem produzida pela CI do PR integrado, conferindo a árvore de código, e aplica o plano da mesma tentativa, sem replanejar no job de aplicação. Mantém a validação de ownership, as verificações de filas/SSM e o rollout existente.

O apply previsto inclui namespace, ConfigMap, Secret, Deployment, Service NodePort e HPA da API, além da entrada FIFO/DLQ de OS, redrive e contratos SSM já definidos. Não recria VPC, EKS, ECR ou RDS. A configuração atual aplica migrations e seed demo ao iniciar a API; isso faz parte da futura implantação, não desta preparação. `/status/api=ready` confirma a infraestrutura de mensageria, não a saúde HTTP ou a integração da Saga.

Esta preparação não ativa o Execução remoto, não provisiona Lambda/Gateway e não altera contratos ou regras de negócio. A prova real de acesso DynamoDB/SQS pelo Pod do Execução permanece pendente da F4-04. Nenhum recurso AWS é criado por este PR enquanto o bloqueio permanecer ativo.

## Estrutura obrigatória dos PRs

Validações operacionais e seus testes ficam nos YAMLs existentes de CI/CD. Não são versionadas pastas de scripts nem diretórios auxiliares adicionais. Trechos necessários à execução são gerados somente no diretório temporário do runner; não são enviados como conteúdo do repositório ou artefato de scripts.

O job obrigatório de Git Flow verifica essa estrutura em todos os PRs, inclusive documentais: bloqueia qualquer pasta chamada `scripts` e diretórios de topo fora da estrutura aprovada do projeto. Funcionalidades devem seguir as camadas existentes. Exceções de estrutura precisam de alinhamento explícito, sem remover o gate para contornar a regra. `AGENTS.md` e as orientações locais registram o mesmo requisito.

## Orientações para trabalho com Codex

### Integração local entre OS e Orçamento

A Saga já possui processamento local de resultados de orçamento, recebimentos idempotentes e comandos na outbox SQL. Após uma preparação registrada, `SolicitarOrcamentoSagaUseCase` recebe o snapshot congelado e persiste `GerarOrcamento`; `ProcessadorResultadoOrcamento` confere geração/decisão/cancelamento e grava os próximos comandos atomicamente. Aprovação autoriza o Execução sem iniciar o reparo físico; recusa aguarda confirmações de cancelamento do orçamento e da preparação.

Os contratos e limites estão no [RFC de microsserviços](docs/architecture/rfcs/rfc-0007-evolucao-para-microsservicos.md). O publicador aceita um transporte explícito; o substituto por arquivos foi usado com bancos SQL separados para validar o conteúdo real das mensagens. Não existe worker novo ativo no host, adaptador SQS ou chamada desses casos de uso pelas rotas antigas neste incremento. A construção do snapshot a partir da OS, o recebimento de preparação, o corte das rotas e a implantação continuam na integração seguinte.

As suítes `MensagensSagaTests`, `PublicacaoOutboxTests` e `ContratoMensagemTests` verificam persistência, reentrega, validação estrita e retomada. Uma tentativa que falha deve descartar seu escopo de DI antes da reentrega; testes locais não comprovam FIFO/DLQ, acesso AWS ou execução física de estoque.

O `AGENTS.md` mantém as regras obrigatórias e aponta para o procedimento canônico em `oficina-mecanica-api/.agents/skills/oficina-mecanica/SKILL.md`. A skill do Execução é somente um encaminhamento, evitando duas versões do mesmo fluxo. READMEs e documentos de arquitetura continuam sendo as fontes de decisões e comportamento do projeto.

Ao abrir um novo chat, prefira a raiz do repositório correto. Se o chat foi aberto fora do Git, peça a leitura explícita do `AGENTS.md` e da skill antes de implementar; entrar na pasta por um comando não garante descoberta automática. A verificação deste ajuste confirma os arquivos e os caminhos, sem instalar skill pessoal ou mudar a configuração do Codex. O gate do CI continua sendo o bloqueio verificável de estrutura, complementando as instruções.
