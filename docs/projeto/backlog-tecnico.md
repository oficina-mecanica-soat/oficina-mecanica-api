# Backlog técnico

## Execução da Fase 4 — 09 a 17/11/2026

Fonte de arquitetura: ADR-0024 a ADR-0026 e RFC-0007. Esta seção prevalece para o escopo atual; itens históricos abaixo não viram trabalho automático. Responsável indica quem conduz a tarefa com seu Codex. Contratos são compartilhados; alterações incompatíveis devem ser alinhadas antes de implementação.

| ID | Responsável | Dependências | Entrega e aceite |
| --- | --- | --- | --- |
| F4-01 | Gabriel | — | Validar ambiente atual via esteiras e Postman; registrar impedimentos e evidências, sem provisionamento direto. |
| F4-02 | Geovanna | — | Revisar ADR-0024/25/26 e RFC-0007, conferir enunciado e fechar contratos usados pelas duas frentes. |
| F4-03 | Gabriel | F4-01, F4-02 | Criar repositório Execução com AGENTS, Clean Architecture, testes, Dockerfile, CI/CD e governança derivados das referências; CI verde e execução local. |
| F4-04 | Gabriel | F4-03 | Provisionar DynamoDB, fila de entrada e DLQ pela esteira; validar leitura/escrita/consumo pelo workload e publicar contratos SSM. Sem SNS inicialmente. |
| F4-05 | Gabriel | F4-03, F4-02 | Extrair diagnóstico, estoque, reserva/liberação e execução preservando regras e testes. Garantir reserva atômica, idempotência, outbox e compensação; consumir os contratos v1 do RFC. Pode usar doubles enquanto F4-04 ocorre. |
| F4-06 | Geovanna | F4-02 | Adaptar OS existente, estados persistidos da Saga, outbox, consumidor de resultados e testes; remover transação entre bancos de serviços. |
| F4-07 | Geovanna | F4-02 | Criar serviço Orçamento/Pagamento, CI/CD independente, banco SQL isolado e fila/DLQ pela esteira; snapshot de orçamento e decisão versionada. |
| F4-08 | Geovanna | F4-07 | Validar produto de checkout Mercado Pago com credenciais de teste; implementar cobrança, notificação verificada, consulta ao provedor e idempotência. Bloqueio de credenciais é P0 imediato. |
| F4-09 | Geovanna | F4-06, F4-07 | Adequar Lambda ao contrato privado de clientes, JWT nos três serviços e roteamento Gateway; testes 401/403 e acesso por cliente. |
| F4-10 | Gabriel | F4-05, F4-06, F4-07 | Integrar fluxo assíncrono, BDD e testes de duplicata, reinício, recusa e compensação; saldo e estados consistentes. Geovanna revisa o contrato e resultados. |
| F4-11 | Gabriel | F4-04, F4-05 | Instrumentar serviço Execução e fluxos distribuídos no Datadog; painéis, logs seguros e falhas da Saga/DLQ observáveis. Geovanna instrumenta OS e cobrança nas respectivas tarefas. |
| F4-12 | Ambos | F4-08 a F4-11 | Rodar deploy completo por esteiras; cobertura ≥80% por serviço, Sonar/equivalente e checks verdes; registrar links reais. |
| F4-13 | Geovanna | F4-12 | Atualizar C4/diagrama geral, README, OpenAPI/Postman, roteiro e PDF; Gabriel revisa execução seguindo os documentos. |
| F4-14 | Ambos | F4-13 | Gravar vídeo ≤15 min com sucesso, compensação, CI/CD e traces; conferir acesso aos repositórios e links; enviar até 17/11 23h59. |

### Marcos e coordenação

- 09/11: decisões e contratos revisáveis; ambiente atual; início dos dois scaffolds e código independente.
- 10–11/11: serviços isolados e infraestrutura mínima; spike Mercado Pago e permissões AWS resolvidos.
- 12–13/11: integração da Saga e compensações, autenticação/Gateway e pagamento de teste.
- 14–15/11: testes, cobertura, Sonar, observabilidade e deploy demonstrável.
- 16/11: documentação, PDF e gravação; congelar funcionalidades.
- 17/11: buffer, conferência de links e envio; evitar deixar gravação/upload para o limite.

Cada tarefa deve ter PR com ID, critérios de aceite, validações reais e referência de contrato. Gabriel pode iniciar F4-03 depois de F4-02; não precisa aguardar todas as frentes. Não editar a mesma branch; compartilhar alterações de contrato antes de avançar nos dois lados. Cada pessoa revisa seus diffs e checks mesmo usando bypass de aprovação. Promoções continuam automáticas conforme as esteiras.

### Alinhamento da infraestrutura da fila de OS — 10/11/2026

Gabriel prepara em PR separado a entrada FIFO/DLQ de resultados de OS e seus contratos SSM no repositório API, conforme alinhamento explícito do responsável pelo projeto. Saga, outbox SQL, consumidor de resultados e transições de OS continuam com Geovanna na F4-06; a preparação da fila não conclui essa tarefa. O contrato proposto e os limites de ativação estão em [Mensageria de OS](../deploy/mensageria-os.md). Recursos reais e IAM dependem da janela de laboratório autorizada.

### Cortes e riscos

Alinhamento da entrada HTTP da F4-05 em 10/11/2026: Gabriel prepara as ações manuais e consulta local do Execução no contrato detalhado da RFC-0007, preservando Administrador/Mecanico e os sufixos atuais. Inclui OpenAPI/Postman locais e testes de 401/403, commit antes de 202 e persistência. Não transfere a F4-09: emissores JWT, Gateway e corte das rotas antigas continuam coordenados com Geovanna. Não declarar integração pública ou AWS concluída por esse preparo.

Alinhamento adicional da F4-05 em 10/11/2026: o snapshot de serviços mantém `servicoId` (instância na OS) e inclui `servicoCatalogoId` (referência ao catálogo), conforme o DDD. A RFC-0007 detalha obrigatoriedade, repetição e limites de compatibilidade. Gabriel adapta Execução; Geovanna adapta o produtor OS na F4-06, antes do corte integrado F4-10. Não inferir igualdade entre os IDs nem consultar banco de outro serviço.

Esclarecimento da F4-05 alinhado com Gabriel em 10/11/2026: preservar o início manual pelo mecânico. `AutorizarExecucao` somente libera o trabalho; `ExecucaoIniciada` corresponde ao primeiro serviço efetivamente iniciado. Finalização física explícita exige todos os serviços finalizados, baixa e outbox atômicas. A barreira de compensação é o início físico, não o recebimento da autorização. A seção correspondente da RFC-0007 detalha a disputa cancelamento/início; implementação e corte público permanecem pendentes nas frentes responsáveis.

Não incluir SNS sem múltiplos consumidores, novos serviços de cadastro, framework compartilhado, migração de Git Flow, plataforma de templates ou hardening corporativo nesta entrega. Não cortar Saga real, integração Mercado Pago, isolamento de bancos, BDD, cobertura, CI/CD ou observabilidade exigidos.

Permissões DynamoDB/SQS, acesso Mercado Pago, conta Sonar e limite do laboratório são riscos P0 e devem ser resolvidos até 11/11. Compensação não pode ser simulada como sucesso. Provisionamento de bancos isolados precisa ser provado; qualquer inviabilidade muda ADR-0025 por decisão explícita, sem acesso cruzado a banco.

## Preparação da próxima entrega

Os itens abaixo são pontos de partida e não substituem decisões arquiteturais aprovadas. O enunciado exige ao menos três microsserviços independentes, bancos próprios com SQL e NoSQL, mensageria, Saga com compensação, cobertura mínima de 80% por serviço, BDD, CI/CD, Kubernetes e observabilidade.

| Prioridade | Frente | Trabalho e critério de aceite | Responsável inicial |
| --- | --- | --- | --- |
| P0 | Ambiente | Executar a solução atual e conhecer os fluxos no Postman; registrar bloqueios sem alterar infraestrutura remota. | Gabriel |
| P0 | Arquitetura | Definir limites dos serviços e evolução do monólito em ADR sucessora; detalhar contratos e Saga em RFC antes de integrar. | Geovanna |
| P0 | AWS | Verificar permissão de criação, uso e limpeza de DynamoDB e SQS no laboratório antes do provisionamento. SNS apenas se necessário. | A definir |
| P0 | Serviços | Planejar três serviços com bancos próprios, fluxo ponta a ponta e integração Mercado Pago; decompor por contratos aprovados. | A definir |
| P0 | Qualidade e entrega | Preservar esteiras existentes como referência; garantir testes, cobertura, BDD, Sonar, deploy, observabilidade, documentação, vídeo e PDF exigidos. | Ambos |

Este backlog guarda melhorias técnicas, itens de código e evoluções operacionais que não precisam entrar imediatamente na entrega principal. A ideia é separar bem o que é escopo do Tech Challenge do que é maturidade de produção para depois.

## Como usar

| Campo | Regra |
| --- | --- |
| Prioridade | `P0` obrigatório para a entrega, `P1` importante se couber, `P2` pós-entrega, `P3` oportunidade futura. |
| Horizonte | `Fase 3`, `Antes da demo`, `Pós-entrega` ou `Pesquisa`. |
| Status | `A fazer`, `Em andamento`, `Bloqueado`, `Concluído` ou `Não priorizado agora`. |
| Critério de aceite | Evidência objetiva de que o item saiu do backlog. |

## Visão de prioridade

| Prioridade | Significado | Decisão prática |
| --- | --- | --- |
| `P0` | Necessário para cumprir o Tech Challenge com segurança. | Fazer antes da entrega. |
| `P1` | Melhora clareza, demonstração ou confiabilidade. | Fazer se não ameaçar o prazo. |
| `P2` | Padrão mais próximo de empresa em produção. | Guardar para depois da entrega. |
| `P3` | Ideia técnica, pesquisa ou refinamento. | Reavaliar quando o produto estiver estável. |

## Escopo da Fase 3

| ID | Prioridade | Horizonte | Área | Item | Critério de aceite | Status |
| --- | --- | --- | --- | --- | --- | --- |
| `F3-001` | `P0` | Fase 3 | Autenticação | Criar uma Lambda de autenticação por documento, em repositório próprio, emitindo JWT somente para Cliente ativo. | `POST /auth/documento` valida CPF ou CNPJ, consulta por `Documento + TipoDocumento` e emite JWT com os claims aprovados; o cenário CPF foi adotado na demonstração. | Concluído |
| `F3-002` | `P0` | Fase 3 | API | Alinhar a API ao contrato único de JWT e manter a autorização dentro da aplicação. | JWT de Cliente usa `sub`, `cliente_id`, `role`, `jti`, `iss`, `aud` e expiração, sem documento ou hash. | Concluído |
| `F3-003` | `P0` | Fase 3 | Banco | Usar banco gerenciado no RDS, com credenciais e rede compatíveis com os consumidores da Fase 3. | RDS provisionado por Terraform, credenciais em Secrets Manager e contratos não sensíveis publicados no SSM. | Concluído |
| `F3-004` | `P0` | Fase 3 | Kubernetes | Executar a API em Kubernetes com escalabilidade. | API publicada no EKS, healthcheck funcional e HPA evidenciado. | Concluído |
| `F3-005` | `P0` | Fase 3 | API Gateway | Expor a entrada pública via API Gateway. | HTTP API roteia `POST /auth/documento` para Lambda e `ANY /api/{proxy+}` para a API no Kubernetes via VPC Link. | Concluído |
| `F3-006` | `P0` | Fase 3 | Terraform | Separar infraestrutura em repositórios/esteiras por recurso. | Seis repositórios possuem ownership explícito, Terraform/implementação, CI/CD e instruções operacionais. | Concluído |
| `F3-007` | `P0` | Fase 3 | CI/CD | Manter branch protegida, PR obrigatório e quality gate. | Esteiras e promoções Git Flow implementadas nos repositórios da solução. | Concluído |
| `F3-008` | `P0` | Pós-entrega | Observabilidade | Enviar logs, métricas e traces para Datadog. | API e Kubernetes possuem métricas, logs e traces funcionando. Auth Lambda está instrumentada, mas a ingestão Datadog da Lambda não foi evidenciada no ambiente acadêmico. | Em andamento / evolução pós-entrega |
| `F3-009` | `P0` | Pós-entrega | Observabilidade | Criar dashboards e alertas pedidos no enunciado. | Implementados: latência API, CPU, memória, healthcheck, error rate, logs ao vivo e dashboard Datadog. Restam métricas de negócio, alerta específico de falha de OS e visibilidade completa da Lambda. | Em andamento / evolução pós-entrega |
| `F3-010` | `P0` | Fase 3 | Documentação | Consolidar diagramas, ADRs/RFCs e documentação final. | READMEs, diagramas, ADRs/RFCs, execução, arquitetura e limitações finais estão documentados sem afirmar evidências inexistentes. | Concluído |
| `F3-011` | `P0` | Fase 3 | Rede | Garantir entrada pública única pelo API Gateway e backend privado da API. | Internet → API Gateway → VPC Link → NLB interno → NodePort → API no EKS; autenticação segue para a Auth Lambda. | Concluído |
| `F3-012` | `P0` | Fase 3 | Rede / Cutover | Remover a exposição pública direta da API após validação E2E do private ingress. | VPC Link, NLB interno, NodePort e rotas do Gateway validados; API Gateway é a entrada pública única. | Concluído |

## Setup inicial de novos repositórios

| ID | Prioridade | Horizonte | Área | Item | Critério de aceite | Status |
| --- | --- | --- | --- | --- | --- | --- |
| `SETUP-001` | `P0` | Fase 3 | Repositório | Criar repositório com nome padronizado `oficina-mecanica-*`. | Nome reflete o recurso e aparece no plano da Fase 3. | Em andamento |
| `SETUP-002` | `P0` | Fase 3 | Branches | Criar `main`, `develop` e `release` quando aplicável. | Branches existem antes do primeiro fluxo de PR. | Em andamento |
| `SETUP-003` | `P0` | Fase 3 | Branch protection | Aplicar proteção nas branches principais. | Dois rulesets ativos: Git Flow sem bypass exige PR, validação de origem, Quality gate e bloqueio de push direto, force push e deleção; aprovação humana separada permite bypass via PR para os integrantes autorizados e maintain/admin. Repetir o padrão em cada novo repositório e revisar alterações nos workflows de validação. | Em andamento |
| `SETUP-004` | `P0` | Fase 3 | CI/CD | Criar workflow de CI mínimo para cada tipo de repo. | PR executa validação compatível com o repositório: `.NET`, Terraform, Lambda ou manifests; a Lambda usa `ci.yml`, quality gate e jobs equivalentes às esteiras maduras. | Em andamento |
| `SETUP-005` | `P0` | Fase 3 | Documentação | Criar README inicial com objetivo, stack, execução e deploy. | README permite entender o papel do repo sem depender de conversa externa. | Em andamento |
| `SETUP-006` | `P1` | Antes da demo | Operação | Padronizar repository variables, secrets e environments. | Variáveis obrigatórias documentadas e cadastradas antes da esteira real. | A fazer |
| `SETUP-007` | `P1` | Antes da demo | Governança | Confirmar acesso do usuário `soat-architecture`. | Usuário aparece com acesso exigido em todos os repositórios da entrega. | A fazer |
| `SETUP-008` | `P0` | Fase 3 | CI/CD | Padronizar os novos fluxos de CI/CD pelo padrão maduro atual. | VPC, Kubernetes, API e a nova Lambda usam `ci.yml` com os estágios compatíveis (`🧪 CI Development`, `🔎 CI Release`, `🛡️ CI Production`, `🚀 CD Development`, `☁️ AWS Deploy`, `🔀 CD Release` e `🏁 CD Production`); o check obrigatório no ruleset corresponde ao Quality gate. A divergência legada do RDS é tratada em `PIPE-001` a `PIPE-004`, sem bloquear a Lambda. | Em andamento |
| `SETUP-009` | `P1` | Antes da demo | Documentação | Revisar acentuação e português dos textos das esteiras. | Workflows, READMEs e docs de todas as esteiras ficam legíveis em português, sem palavras sem acento por padronização manual ou mojibake. | A fazer |
| `SETUP-010` | `P1` | Antes da demo | Higiene | Completar `.gitignore` do repositório RDS. | Arquivos Terraform gerados, crash logs, overrides e arquivos de IDE ficam alinhados, sem remover as proteções já existentes. | A fazer |

## Backlog de código e qualidade

| ID | Prioridade | Horizonte | Área | Item | Critério de aceite | Status |
| --- | --- | --- | --- | --- | --- | --- |
| `CODE-001` | `P0` | Fase 3 | Domínio | Adicionar `StatusCliente` para suportar autenticação de Cliente por documento. | Aggregate/modelo, mapeamento EF, migration e snapshot são atualizados; seed/demo possui cliente ativo e inativo; testes cobrem os documentos CPF e CNPJ nos dois estados. A representação física do status é documentada antes da migration. | Concluído |
| `CODE-002` | `P0` | Fase 3 | Segurança | Evitar CPF/CNPJ puro em logs, respostas técnicas e JWT. | Testes e implementação garantem respostas sanitizadas e JWT sem documento ou hash. | Concluído |
| `CODE-003` | `P0` | Fase 3 | Testes | Cobrir fluxo de autenticação e autorização por documento. | Testes cobrem CPF/CNPJ, entradas inválidas, cliente inexistente/inativo, claims e autorização. | Concluído |
| `CODE-004` | `P1` | Pós-entrega | Contratos | Atualizar OpenAPI e Postman versionados para os fluxos da Fase 3. | Artefatos versionados ainda refletem a Fase 2; Swagger runtime representa a API atual. | Evolução pós-entrega |
| `CODE-005` | `P1` | Fase 3 | Observabilidade | Padronizar `X-Correlation-Id`, `dd.trace_id` e `dd.span_id`. | API e Lambda propagam correlação e a API injeta identificadores Datadog nos logs. | Concluído |
| `CODE-006` | `P2` | Pós-entrega | Banco | Mover migrations e seed inicial do startup da API para Kubernetes Job versionado. | Deploy da API não executa migration automaticamente no startup. | Não priorizado agora |
| `CODE-007` | `P2` | Pós-entrega | Banco | Avaliar lock distribuído para migrations concorrentes. | Estratégia definida, por exemplo com `sp_getapplock`, antes de escalar réplicas com migration automática. | Não priorizado agora |
| `CODE-008` | `P2` | Pós-entrega | Qualidade | Adicionar análise de dependências e vulnerabilidades. | Pipeline publica resultado de auditoria de pacotes sem bloquear indevidamente a entrega acadêmica. | Não priorizado agora |
| `CODE-009` | `P0` | Fase 3 | API | Criar a rota do próprio cliente para consultar suas ordens de serviço. | `GET /api/v1/clientes/me/ordens-servico` usa somente `cliente_id` validado do JWT e possui testes de autorização. | Concluído |
| `CODE-010` | `P1` | Antes da demo | Segredos locais | Substituir o segredo local versionado no manifesto Kubernetes. | `k8s/api-secret.yaml`, hoje com credencial local fixa versionada, é trocado por estratégia de exemplo/template sem segredo real ou local versionado, preservando a facilidade de execução local e as validações existentes. | A fazer |
| `CODE-011` | `P2` | Pós-entrega | Qualidade | Tratar vulnerabilidade reportada no SSH.NET 2025.1.0. | Atualização da dependência é avaliada com impactos e regressões antes da correção. | A fazer |

## Decisões e contratos pendentes da autenticação

| ID | Prioridade | Horizonte | Área | Item | Critério de aceite | Status |
| --- | --- | --- | --- | --- | --- | --- |
| `DEC-001` | `P0` | Fase 3 | Contrato JWT | Resolver a divergência entre o `cpf_hash` do ADR-0019 e o contrato do RFC-0001. | ADR-0019 e RFC-0001 definem o mesmo claim set: `sub = cliente_id`, `cliente_id`, `role`, `jti`, `iss`, `aud` e expiração; CPF/CNPJ e hashes de documento ficam fora do JWT. | Concluído |
| `DEC-002` | `P0` | Fase 3 | Contrato HTTP | Formalizar o contrato de `POST /auth/documento` e sua integração com o API Gateway. | HTTP API payload format 2.0 com `APIGatewayHttpApiV2ProxyRequest`; request por `documento`; `200` com token mínimo; `400` genérico para entrada inválida; `401` idêntico para inexistente/inativo; `503` genérico para RDS/Secrets Manager; sem exposição de documento ou infraestrutura. | Concluído |
| `RDS-001` | `P0` | Fase 3 | Segredos | Provisionar e publicar a referência das credenciais do RDS. | `oficina-mecanica-infra-rds` é o dono do segredo gerenciado no AWS Secrets Manager; o endpoint e o ARN do segredo são publicados pelo SSM em `/oficina-mecanica/development/rds/endpoint` e `/oficina-mecanica/development/rds/master_secret_arn`. API e Auth Lambda resolvem as credenciais no deploy, sem GitHub Secret de connection string da Lambda. | Concluído |
| `RDS-002` | `P0` | Fase 3 | Rede | Restringir o acesso SQL Server do RDS conforme ADR-0018. | RDS privado aceita TCP/1433 somente dos componentes autorizados; EKS e Auth Lambda usam regras por security group. | Concluído |

## Dependências P0 da autenticação

| Item | Repositório responsável | Depende de | Bloqueia |
| --- | --- | --- | --- |
| `DEC-001` | `oficina-mecanica-api` | — | — (concluído) |
| `DEC-002` | `oficina-mecanica-infra-api-gateway` com contrato revisado em `oficina-mecanica-api` | — | — (concluído) |
| `CODE-001` | `oficina-mecanica-api` | definição física do status | `F3-001`, `CODE-003`, `CODE-009` |
| `F3-002` | `oficina-mecanica-api` | contrato JWT consolidado em `DEC-001` | `CODE-003`, `CODE-009`, testes ponta a ponta |
| `RDS-001` | `oficina-mecanica-infra-rds` | decisão de publicar ARN não secreto via SSM | `F3-001`, testes ponta a ponta |
| `RDS-002` | `oficina-mecanica-infra-rds` (SG do RDS) e `oficina-mecanica-auth-lambda` (regra de integração) | SG do RDS publicado via SSM e SG próprio da Lambda | deploy funcional da Lambda |
| `F3-001` | `oficina-mecanica-auth-lambda` | `CODE-001`, `RDS-001` | `RDS-002`, `F3-005`, testes ponta a ponta |
| `CODE-009` | `oficina-mecanica-api` | `CODE-001`, `F3-002` | demonstração de autorização do cliente |
| `F3-005` | `oficina-mecanica-infra-api-gateway` | `F3-001`, endpoint privado da API | testes ponta a ponta |

## Dívida de CI/CD observada na auditoria

| ID | Prioridade | Horizonte | Área | Item | Critério de aceite | Status |
| --- | --- | --- | --- | --- | --- | --- |
| `PIPE-001` | `P2` | Pós-entrega | CI/CD | Convergir a organização do RDS para o padrão `ci.yml` adotado por API, VPC e Kubernetes. | A decisão de unificar ou manter os três workflows do RDS é registrada e, se unificada, preserva os quality gates sem regressão. | Não priorizado agora |
| `PIPE-002` | `P2` | Pós-entrega | CI/CD | Avaliar reutilização do mesmo `tfplan` no apply do RDS. | A esteira do RDS reutiliza o plano aprovado ou documenta tecnicamente por que precisa regenerá-lo. | Não priorizado agora |
| `PIPE-003` | `P2` | Pós-entrega | Documentação | Corrigir a referência de `infra-action.env` onde o padrão real é `terraform-action.env`. | RFCs, planos e READMEs citam o nome real, ou a alteração de nome é aprovada e aplicada de forma coordenada. | Não priorizado agora |
| `PIPE-004` | `P2` | Pós-entrega | Qualidade | Normalizar finais de linha no RDS, caso a alteração ainda exista após nova verificação. | O diff não contém alteração apenas de line ending e `.gitattributes` ou padrão equivalente evita recorrência. | Não priorizado agora |
| `PIPE-005` | `P2` | Pós-entrega / executar hoje somente se todos os P0 estiverem concluídos e houver folga | CI/CD / Git Flow | Corrigir definitivamente a política de promoção `develop -> release -> main` nos seis repositórios da Fase 3, preservando ancestralidade Git. | A configuração vulnerável identificada em `oficina-mecanica-api`, `oficina-mecanica-infra-vpc`, `oficina-mecanica-infra-kubernetes`, `oficina-mecanica-infra-rds`, `oficina-mecanica-auth-lambda` e `oficina-mecanica-infra-api-gateway` é corrigida conforme o diagnóstico detalhado da issue #268. `feature/* -> develop` pode continuar permitindo squash; `develop -> release` e `release -> main` devem usar merge commit real. Squash e rebase não devem ser usados em promoções entre branches de estágio enquanto os workflows dependerem de ancestralidade ou `git rev-list`. Preferir a branch fixa `release` neste projeto e remover `release/*` dos rulesets e workflows se releases versionadas não forem usadas; se `release/*` for mantido, corrigir o deadlock atual de required status checks aplicados na criação da branch. Não reconciliar nem regravar o histórico dos demais repositórios quando o grafo já estiver saudável. Validar ao final com `git merge-base --is-ancestor` e compare. | Não priorizado agora |

## Backlog operacional pós-entrega

### Itens não bloqueantes para a Fase 3

Os itens desta seção não bloqueiam a entrega atual da Fase 3.

### Estado aceito de secrets e configuração na entrega

- As credenciais do RDS têm fonte da verdade adequada: `username` e `password` são gerenciados pelo RDS no AWS Secrets Manager; o Parameter Store publica somente contratos não sensíveis, como endpoint, ARN, IDs, nomes e status.
- API e Auth Lambda obtêm endpoint e ARN do segredo via SSM e resolvem as credenciais no Secrets Manager durante o deploy. A Auth Lambda não utiliza `AUTH_LAMBDA_CONNECTION_STRING` como GitHub Secret.
- Por limitação operacional do AWS Academy/lab, `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY` e `AWS_SESSION_TOKEN` permanecem temporariamente no GitHub Environment para autenticar o runner de CI/CD na AWS. Não são credenciais da aplicação.
- O segredo de assinatura JWT continua no GitHub Environment durante a Fase 3. Esta é uma decisão temporária de entrega, não o estado alvo de produção.
- `AWS_REGION`, issuer, audience, expiração e nome da execution role podem permanecer como GitHub Variables na Fase 3; são configurações não sensíveis.

### Regra arquitetural para evolução futura

- **Parameter Store:** configuração e contratos não sensíveis, como IDs, ARNs, endpoints, nomes, status e parâmetros operacionais.
- **Secrets Manager:** material sensível, como passwords, signing keys, tokens e API keys.
- **GitHub:** código, workflows e a configuração mínima necessária ao bootstrap do CI/CD. Em produção, evitar secrets permanentes no GitHub quando OIDC/federação e Secrets Manager puderem substituí-los.

| ID | Prioridade | Horizonte | Área | Item | Critério de aceite | Status |
| --- | --- | --- | --- | --- | --- | --- |
| `OPS-009` | `P2` | Pós-entrega | Registry | Avaliar mover o ownership do ECR da API de `oficina-mecanica-infra-kubernetes` para `oficina-mecanica-api`, aproximando o registry do ciclo de vida da aplicação. | Decisão registrada e migração planejada sem interromper a entrega atual. | Não priorizado agora |
| `OPS-010` | `P2` | Pós-entrega | Kubernetes | Avaliar Helm ou manifests centralizados em `oficina-mecanica-infra-kubernetes` após a entrega. | Estratégia de workloads definida sem duplicação e com migração segura. | Não priorizado agora |
| `OPS-011` | `P2` | Pós-entrega | Segredos | Consumir secrets em runtime pelos workloads. | Auth Lambda recebe somente ARN/referência e consulta o Secrets Manager em runtime via AWS SDK/IAM; a API adota estratégia equivalente adequada ao Kubernetes. A evolução evita valores sensíveis em Terraform state e environment variables; `sensitive = true` oculta a apresentação, mas não remove o valor do state. | Não priorizado agora |
| `OPS-012` | `P2` | Pós-entrega | Solution | Migrar a solution `.sln` para `.slnx`. | Migração validada sem alterar o comportamento da aplicação. | Não priorizado agora |
| `OPS-013` | `P1` | Assim que possível após o primeiro apply seguro | Infraestrutura da API | Concluir a limpeza transicional da API desacoplada: remover módulos legados VPC/EKS/RDS/ECR e `state-ownership.tf`, deixando somente namespace, configmap, secret, deployment, service, HPA, providers/data sources e pipeline. | Após o primeiro apply seguro, não há ruído nem ownership de infraestrutura compartilhada na pasta `infra/terraform` da API. | Concluído |
| `OPS-014` | `P1` | Pós-entrega | Ambientes | Auditar hardcoded de ambiente e paths SSM em todas as esteiras. | Workflows reutilizáveis não apontam silenciosamente para development; a relação entre branch, GitHub Environment, pasta Terraform e paths SSM está parametrizada ou explicitamente documentada. | A fazer |
| `OPS-015` | `P2` | Pós-entrega | Kubernetes | Restringir o endpoint administrativo do EKS. | Avaliar `cluster_endpoint_public_access_cidrs`, eliminar `0.0.0.0/0` e usar endpoint privado e/ou runner self-hosted dentro da VPC em produção. Não bloqueia a Fase 3. | A fazer |
| `OPS-016` | `P2` | Pós-entrega | Rede | Evoluir a estratégia de egress privado. | Avaliar manter NAT Gateway único, usar NAT por AZ para alta disponibilidade ou substituir parte do tráfego por VPC Endpoints (ECR, S3, Secrets Manager, CloudWatch, STS e similares). O NAT atual permanece intencional na Fase 3: EKS e Lambda estão em subnets privadas e precisam de saída, inclusive para Secrets Manager e Datadog conforme ADR-0018. | A fazer |
| `OPS-017` | `P3` | Pós-entrega | Rede | Reavaliar subnets públicas após remover o Load Balancer público da API. | Não remover subnets na Fase 3. Hoje o NAT usa `public[0]`; a segunda subnet pode ser útil em uma evolução para NAT por AZ e não há benefício suficiente para alterar a topologia imediatamente antes da entrega. | A fazer |
| `OPS-018` | `P2` | Pós-entrega | Segredos | Centralizar a JWT signing key no AWS Secrets Manager. | Existe uma única fonte da verdade compartilhada por Auth Lambda e API; elimina duplicação entre `JWT_SECRET` e `AUTH_LAMBDA_JWT_SECRET`, não expõe valor em logs, documenta ownership e suporta rotação. O owner compartilhado deve ser definido antes da implementação para não criar dependência circular entre API e Auth Lambda. | Não priorizado agora |
| `OPS-019` | `P2` | Pós-entrega | Segredos | Migrar `WEBHOOK_TOKEN` para o AWS Secrets Manager. | Valor deixa o GitHub Secrets; referência/configuração é controlada e nenhum segredo aparece em código, tfvars versionados ou logs. | Não priorizado agora |
| `OPS-020` | `P2` | Pós-entrega | CI/CD | Migrar a autenticação GitHub Actions → AWS para OIDC. | GitHub OIDC Provider e IAM Role usam trust policy restrita a repositório, branch e environment, menor privilégio e credenciais STS temporárias; não permanecem access keys no GitHub. Pode ser inviável no AWS Academy/voclabs por restrições de IAM: as credenciais temporárias atuais são concessão operacional acadêmica, não desenho de produção. | Não priorizado agora |
| `OPS-021` | `P3` | Pós-entrega | Configuração | Centralizar configurações não sensíveis no Parameter Store. | Avaliar região quando aplicável, issuer, audience, expiração e demais configurações de ambiente no SSM. Não é requisito de segurança — GitHub Variables não são secrets — e busca governança/centralização sem competir com itens funcionais da Fase 3. | Não priorizado agora |
| `ECR-001` | `P2` | Pós-entrega | Registry | Criar Lifecycle Policy do ECR para imagens publicadas com tags imutáveis `sha-$GITHUB_SHA`. | Existe `aws_ecr_lifecycle_policy` que mantém as últimas 10–20 imagens `sha-*`, remove imagens untagged antigas após alguns dias, preserva rollback e não apaga imediatamente a imagem anterior ao deploy. | A fazer |
| `DOC-README-001` | `P2` | Pós-entrega / documentação final | Documentação | Revisar os READMEs dos seis repositórios para refletir o estado final real. | Conteúdo obsoleto foi corrigido sem reescrever a documentação madura. | Concluído |
| `DOC-README-002` | `P2` | Pós-entrega / documentação final | Documentação | Revisar links quebrados nos seis repositórios. | Links relativos alterados foram validados e referências removidas foram ajustadas. | Concluído |
| `DOC-README-003` | `P2` | Pós-entrega / documentação final | Documentação | Padronizar conteúdo mínimo dos READMEs sem apagar o estilo próprio de cada repositório. | Cada README documenta responsabilidade, dependências, execução, CI/CD, configuração e retorno ao README principal. | Concluído |
| `DOC-API-001` | `P2` | Pós-entrega / documentação final | Contratos | Atualizar OpenAPI e Postman após o código final. | Documentação cobre `POST /auth/documento`, `GET /api/v1/clientes/me/ordens-servico`, JWT/autorização e endpoints finais; referências ao LoadBalancer público e fluxos obsoletos foram removidas. | A fazer |
| `DOC-API-002` | `P2` | Pós-entrega / documentação final | Demonstração | Criar collection Postman exclusiva para o vídeo. | Collection contém health, autenticação por documento, `/me` sem JWT (`401`) e com JWT Cliente (`200`), rota Admin com JWT Cliente (`403`), abertura de OS e chamada para logs/traces; variável/script captura automaticamente o JWT de `/auth/documento` para as requests seguintes. | A fazer |

## Status atual da entrega

- VPC, Kubernetes/EKS, RDS, API, Auth Lambda e API Gateway possuem esteiras separadas e contratos SSM documentados.
- API Gateway é a entrada pública; a API no EKS é alcançada por VPC Link, NLB interno e NodePort.
- Autenticação por documento e rota protegida do próprio cliente estão implementadas.
- Datadog da API/Kubernetes possui métricas, logs e traces; a telemetria completa da Auth Lambda permanece como evolução.

| ID | Prioridade | Horizonte | Área | Item | Critério de aceite | Status |
| --- | --- | --- | --- | --- | --- | --- |
| `OPS-001` | `P2` | Pós-entrega | Hotfix | Definir fluxo `hotfix/*` a partir de `main`. | Hotfix entra por PR para `main`, passa por CI/aprovação e depois é sincronizado para `develop` e `release`. | Não priorizado agora |
| `OPS-002` | `P2` | Pós-entrega | Rollback | Criar rollback manual por versão/tag de imagem. | Esteira permite redeploy de uma versão anterior conhecida como boa. | Não priorizado agora |
| `OPS-003` | `P2` | Pós-entrega | Rollback | Avaliar rollback automático após falha de healthcheck. | Rollback automático é limitado a deploy de aplicação e nunca reverte banco de forma destrutiva sem aprovação humana. | Não priorizado agora |
| `OPS-004` | `P2` | Pós-entrega | Banco | Definir política de rollback de migrations. | Migrations possuem estratégia segura para forward fix, compatibilidade ou rollback manual controlado. | Não priorizado agora |
| `OPS-005` | `P2` | Pós-entrega | Infra | Evoluir RDS para Multi-AZ, backups e snapshot final. | Ambiente deixa de depender das simplificações da AWS Academy. | Não priorizado agora |
| `OPS-006` | `P2` | Pós-entrega | Segredos | Formalizar rotação de secrets e chaves JWT. | Segredos possuem dono, periodicidade e procedimento de rotação. | Não priorizado agora |
| `OPS-007` | `P2` | Pós-entrega | DNS | Trocar hostname bruto do Load Balancer por DNS amigável. | API usa domínio próprio com Route 53 ou provedor equivalente. | Não priorizado agora |
| `OPS-008` | `P3` | Pesquisa | Plataforma | Avaliar trunk-based development como alternativa ao Git Flow. | Decisão documentada somente se houver ganho real para o contexto do time. | Não priorizado agora |

## Evoluções pós-entrega identificadas na Fase 3

- evidenciar a telemetria completa da Auth Lambda no Datadog;
- medir o volume diário de ordens de serviço por evento de negócio;
- medir o tempo médio por status de Diagnóstico, Execução e Finalização;
- criar alerta específico de falha de processamento de ordem de serviço;
- atualizar a collection Postman versionada para os fluxos da Fase 3;
- atualizar o OpenAPI versionado, preservando o Swagger runtime como referência atual;
- persistir o histórico de transições de status necessário às métricas de negócio;
- criar monitores e alertas mais específicos conforme dados reais de operação;
- reavaliar as melhorias de maturidade P2/P3 já mantidas neste backlog.

## Fora do escopo imediato

Estes itens ficam registrados para não serem esquecidos, mas não devem competir com a entrega da Fase 3:

- rollback automático completo;
- fluxo formal de hotfix;
- banco Multi-AZ com política completa de backup;
- DNS com domínio próprio;
- rotação corporativa de segredos;
- esteira corporativa com change management;
- plataforma compartilhada de templates para todos os repositórios.

## Próxima revisão

Antes de iniciar uma nova frente, revisar primeiro os itens `P0`. Depois da entrega, reabrir os itens `P2` e decidir o que vira melhoria real do projeto e o que continua apenas como referência de arquitetura.

### Padronização obrigatória de estrutura — 10/11/2026

Por orientação explícita de Gabriel, as pastas de scripts foram removidas da API e do Execução. Lógica operacional e testes são mantidos nos workflows existentes; arquivos temporários são gerados fora do conteúdo versionado. O gate de Git Flow impede pastas de scripts e novos diretórios de topo em todo PR. Não acrescentar estruturas auxiliares sem alinhamento. Esta reorganização não altera recursos de infraestrutura, contratos de mensagens ou regras de negócio; AWS permanece desabilitada.
