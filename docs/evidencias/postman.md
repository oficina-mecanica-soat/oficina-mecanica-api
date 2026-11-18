# Postman: contratos e validação manual

## Collection final consolidada

Use [Oficina Mecânica — solução completa](../postman/oficina-mecanica-completa.postman_collection.json) no workspace **Oficina Mecânica**. A collection reúne as 63 operações OpenAPI atuais dos três serviços, a autenticação da Lambda, healthchecks, contrato privado e roteiro de demonstração: 93 requisições incluindo os exemplos repetidos nos fluxos. As collections anteriores permanecem como referências históricas; não precisam ser importadas junto da final.

O workspace organiza os recursos no Postman; a entrega portável são os três JSONs: collection, [Local](../postman/oficina-mecanica-local.postman_environment.json) e [Development](../postman/oficina-mecanica-aws-dev.postman_environment.json). Gabriel pode importar os mesmos arquivos e preencher seus valores locais. Não é necessário recriar as consultas manualmente.

| Pasta | Finalidade |
| --- | --- |
| 00 · Comece aqui | Saúde dos hosts e orientação inicial. |
| 01 · Identidade | Login do operador, login do mecânico e autenticação do Cliente pela Lambda; captura dos respectivos JWTs. |
| 02 · OS e cadastros | Catálogo completo da API por área: clientes, veículos, mecânicos, catálogos, ordens e consultas históricas. |
| 03 · Execução | Entrada idempotente de estoque, preparação e ações físicas do mecânico. |
| 04 · Orçamento e Pagamento | Consultas, decisão, pagamento e teste negativo do Webhook. Mutações diretas de orçamento indicam o bloqueio no modo distribuído. |
| 05 · Demonstração | Passos numerados de autenticação, estoque, abertura, aprovação, trabalho físico, pagamento e entrega; recusa/cancelamento em outra OS. |
| 07 · Compatibilidade legada | Mutações anteriores, protegidas por `permitirRotasLegadas=false`; no modo distribuído o resultado esperado é 409. |
| 08 · Contratos privados | Consulta de cliente usada pela Lambda, fora do Gateway; protegida por `permitirRotasInternas=false`. |

VPC, EKS, ECR, RDS, filas e Gateway não são aplicações de negócio com rotas a cadastrar no Postman. Seus ciclos de vida continuam nas esteiras oficiais; a collection não contém comandos administrativos AWS.

### Ambientes e endereços

| Variável | Local | Development |
| --- | --- | --- |
| `baseUrl` / `osBaseUrl` | `http://localhost:5093` | Preencher `baseUrl` com o endpoint HTTPS vigente do Gateway; `osBaseUrl` referencia esse valor. |
| `execucaoBaseUrl` | `http://localhost:5094` | Referencia `baseUrl`, que encaminha `/api/v1/execucoes`. |
| `orcamentoBaseUrl` | `http://localhost:5095` | Referencia `baseUrl`, que encaminha orçamento/pagamento. |
| `authBaseUrl` | Vazio: requer host HTTP de emulação da Lambda explicitamente preparado. | Referencia `baseUrl`; caminho `/auth/documento`. |
| `osInternaBaseUrl` | Host da API, somente quando a consulta técnica estiver habilitada. | Vazio: exige acesso privado suportado; não usar o Gateway. |
| `execucaoHealthBaseUrl` / `orcamentoHealthBaseUrl` | Portas 5094/5095 dos próprios participantes. | Vazias: os healthchecks privados não são publicados por essas rotas do Gateway. Usar validação privada da esteira. |

As portas locais seguem os comandos dos READMEs. A collection não inicia processos, Docker, banco, worker ou emulador da Lambda. Configurar os três hosts com chaves JWT compatíveis, bancos próprios e modos locais/distribuídos coerentes antes da demonstração local. Uma OS não percorre a Saga apenas porque três endpoints de saúde responderam.

### Variáveis, dados e captura

- `login`/`senha` e `loginMecanico`/`senhaMecanico`: preencher somente nos valores locais do ambiente. Os logins capturam `token` e `tokenMecanico`; a Lambda captura `tokenCliente`. Nenhum script imprime o token no console.
- `documento` e `documentoCliente`: documento sintético válido e coerente com o cliente preparado. O login por documento exige cliente ativo. Não usar dados pessoais reais no vídeo.
- `clienteId`, `veiculoId`, `mecanicoId`, `servicoCatalogoId` e `pecaInsumoCatalogoId`: UUIDs dos cadastros; os POSTs de cadastro do catálogo final capturam o ID retornado. Para outra pessoa/entidade, substituir conscientemente os valores.
- `filialId`: UUID da filial usada pelo estoque e pela OS. `quantidadeEntrada` prepara a entrada; `quantidade` é a necessidade da jornada. O estoque da API é histórico: abastecer o owner Execução.
- `ordemServicoId`, `sagaId`, `preparacaoId`, `orcamentoId`, `versaoOrcamento` e `pagamentoId`: a demonstração captura os IDs da abertura/consulta de andamento. Repetir a consulta até o estado esperado; não preencher IDs fictícios para pular uma espera.
- Uma abertura 202 na demonstração limpa IDs de Saga/preparação/orçamento/pagamento e operações da jornada anterior antes de capturar a nova OS, evitando mostrar o pagamento de outra jornada. Não repetir a abertura para demonstrar idempotência: ela representa outra OS.
- `servicoId`: ID da instância do serviço no snapshot, diferente de `servicoCatalogoId`. O roteiro de uma linha pode capturá-lo; para múltiplos serviços, selecionar cada instância e concluir todas antes de finalizar a execução.
- `operacao*Id`: UUID estável por intenção; scripts geram apenas quando vazio. Para repetir a mesma ação, conservar ID e corpo. Ao mudar de OS/serviço ou iniciar outra intenção, limpar os IDs correspondentes. Não reutilizar a operação de início na finalização.
- `correlationId`: gerado quando vazio e enviado como `X-Correlation-Id`; conservar na jornada para investigação no Datadog. Não é segredo nem confirmação de processamento.
- Parâmetros opcionais de query vêm desabilitados no catálogo; habilitar os filtros desejados. `pagina` e `tamanhoPagina` têm defaults explícitos. Corpos mantêm tipos JSON, inclusive quantidades/valores numéricos; conferir validadores e dados relacionados.
- `permitirExclusoes=false`: bloqueia DELETE por padrão; habilitar somente para dados de teste descartáveis após revisar o ID. Os bloqueios da collection não substituem autorização da aplicação.

Tokens, senhas, documentos e o token técnico privado estão vazios nos arquivos versionados. Não colocar `JWT_SECRET`, Access Token do Mercado Pago ou assinatura do Webhook no Postman. O export do environment pode conter valores locais: revisar e limpar antes de compartilhar. O tipo secret ajuda a ocultar a tela, mas não substitui essa conferência.

### Demonstração sem improviso

1. Selecionar o ambiente, testar a saúde pública e executar os logins necessários na pasta 01.
2. Preparar cadastros coerentes no catálogo da API; registrar estoque no Execução. Repetir a mesma entrada com o mesmo ID não soma novamente.
3. Na pasta 05, abrir uma OS distribuída: esperar 202 e Location; consultar andamento até preparação e orçamento disponíveis. Consultar os dois owners.
4. Aprovar o orçamento e repetir a decisão com o mesmo ID. Aguardar autorização; mostrar que o serviço ainda não foi iniciado automaticamente.
5. Com JWT do mecânico, iniciar e finalizar cada serviço; depois finalizar a execução. Consultar andamento até cobrança disponível. Antes da confirmação financeira, a entrega deve ser recusada.
6. Consultar pagamento, abrir `enderecoCheckout` no navegador e pagar apenas com comprador de teste do Mercado Pago. Aguardar notificação assinada e consulta ao provedor; conferir pagamento Confirmado e `pagamentoConfirmado=true` antes de entregar.
7. Em outra OS preparada e ainda sem início físico, recusar orçamento **ou** solicitar cancelamento. Aguardar ambas as compensações e conferir o estado no Execução/Orçamento. Não executar os dois caminhos como sequência do fluxo feliz.
8. Demonstrar segurança com 401 sem token, 403 de Cliente na área administrativa e 409 da mutação legada. A chamada Webhook sem assinatura é negativa e deve retornar 401; não é mecanismo de confirmação manual.

No `consultar-status`, OS com Saga informa `execucaoDistribuida=true`, `preparacaoId` e `servicos=[]`. O estado físico atual está no GET autenticado do Execução; o snapshot da OS serve para identidade/valores históricos, não para afirmar que o serviço ainda está Pendente após o reparo. Ordens legadas preservam o detalhe local.

Não executar a collection inteira no Runner. Há mutações, exclusões, esperas e caminhos incompatíveis. As asserções de cada etapa verificam o contrato HTTP; não transformam 202 em conclusão de negócio nem substituem a prova real dos bancos, filas e observabilidade.

## Referências anteriores — collections históricas

As instruções abaixo descrevem os arquivos anteriores. Para a entrega final, usar a collection consolidada e o procedimento acima.

| Arquivo | Uso |
| --- | --- |
| [Collection distribuída](../postman/oficina-mecanica-distribuida.postman_collection.json) | Fluxo atual de OS, Execução e Orçamento/Pagamento; autenticação, compensação e testes negativos. Preparada a partir dos controllers e DTOs, não de uma exportação Swagger única. |
| [Collection completa histórica da API](../postman/oficina-mecanica-api.postman_collection.json) | Importação original do Swagger da API; útil para cadastros e consulta do contrato legado. Não representa sozinha o fluxo distribuído atual. |
| [Environment local](../postman/oficina-mecanica-local.postman_environment.json) | API em `http://localhost:5093`; endereços de Execução e Orçamento/Pagamento devem ser preenchidos conforme processos/portas realmente iniciados. |
| [Environment AWS development](../postman/oficina-mecanica-aws-dev.postman_environment.json) | `baseUrl` vazio para preencher com o Gateway vigente. Os três serviços usam esse endereço público. |

O [roteiro integrado](../deploy/deploy-aws.md) define pré-condições, cenários, observabilidade, evidências e gravação. A existência da collection não comprova que essas chamadas passaram no ambiente.

## Importação e preparação

1. Importar a collection distribuída e o environment desejado. A collection histórica pode ser importada separadamente para preparar os cadastros que ainda pertencem à API.
2. Preencher `baseUrl` sem barra final. Na AWS usar HTTPS do Gateway, não Load Balancer público antigo. Localmente, preencher `execucaoBaseUrl` e `orcamentoBaseUrl`; `osBaseUrl` referencia `baseUrl`.
3. Preencher em valores locais `login`/`senha`, `loginMecanico`/`senhaMecanico` e documento do cliente de teste. Não presumir que todos os perfis compartilham a mesma credencial.
4. Informar `clienteId`, `veiculoId`, `mecanicoId`, `filialId`, `servicoCatalogoId`, `pecaInsumoCatalogoId` e `quantidade`. Veículo, cliente, catálogo e saldo precisam estar coerentes. O exemplo usa um serviço e uma peça; adaptar para cenários sem peças ou com vários serviços.
5. Executar os logins: atendente/administrador preenche `token`; mecânico preenche `tokenMecanico`; `/auth/documento` preenche `tokenCliente` a partir de `accessToken`. O último endpoint exige a Lambda pelo Gateway ou equivalente local explicitamente preparado.
6. Executar abertura e consultas manualmente. O modo distribuído deve retornar `202`, não `201`; consultar até a Saga disponibilizar os IDs da preparação/orçamento/pagamento. Nunca inventar os IDs.

## Organização dos cenários

- Pastas 01 a 04: autenticação, abertura, aprovação, trabalho manual e pagamento/entrega. A aprovação não inicia o trabalho do mecânico.
- Pasta 05: caminhos alternativos, em outra OS antes do início físico. Recusa e cancelamento não são passos posteriores à entrega nem devem ser executados ambos indiscriminadamente.
- IDs `operacao*Id` são gerados uma vez por ação e preservados nas repetições. Ao iniciar nova jornada ou trocar `servicoId`, limpar os IDs de operação anteriores. Não confundir instância de serviço com ID de catálogo.
- A consulta de andamento captura IDs recebidos. A consulta de pagamento mostra `enderecoCheckout`; abrir manualmente o endereço sandbox e usar as contas de teste do provedor. A collection não contém assinatura de webhook, access token Mercado Pago ou chamada que confirme pagamento manualmente.
- Não executar a collection inteira com Runner: há esperas assíncronas, pagamento interativo e cenários mutuamente exclusivos. Os scripts validam o status esperado da etapa, não substituem a conferência do estado persistido, saldo e efeitos únicos.

## Segurança e evidências

Os environments versionados têm tokens, senhas, documentos e IDs vazios. O tipo `secret` ajuda a ocultar valores na interface, mas não garante remoção em uma exportação: revisar o arquivo antes de compartilhar. Limpar valores locais, cabeçalhos Authorization, cookies, assinaturas e respostas sensíveis de prints/relatórios.

A [evidência de HPA](kubernetes-hpa.md) refere-se ao teste histórico com a collection da API e ao ambiente daquela execução. Ela permanece histórica; não demonstra implantação ou desempenho dos três serviços atuais.

Antes de abrir uma OS com peças, executar a entrada de estoque no Execução com filial/item válidos e perfil Administrador ou Atendente. A collection mantém `operacaoEntradaEstoqueId` entre repetições: repetir o mesmo conteúdo não soma novamente; para outra entrada, escolher outro UUID. A quantidade do exemplo é dez unidades e deve ser adequada ao cenário, sem abastecer o estoque legado de OS.
