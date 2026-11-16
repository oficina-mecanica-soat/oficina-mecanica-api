# Postman: contratos e validação manual

## Arquivos e finalidade

| Arquivo | Uso |
| --- | --- |
| [Collection distribuída](../postman/oficina-mecanica-distribuida.postman_collection.json) | Fluxo atual de OS, Execução e Orçamento/Pagamento; autenticação, compensação e testes negativos. Preparada a partir dos controllers e DTOs, não de uma exportação Swagger única. |
| [Collection completa histórica da API](../postman/oficina-mecanica-api.postman_collection.json) | Importação original do Swagger da API; útil para cadastros e consulta do contrato legado. Não representa sozinha o fluxo distribuído atual. |
| [Environment local](../postman/oficina-mecanica-local.postman_environment.json) | API em `http://localhost:5093`; endereços de Execução e Orçamento/Pagamento devem ser preenchidos conforme processos/portas realmente iniciados. |
| [Environment AWS development](../postman/oficina-mecanica-aws-dev.postman_environment.json) | `baseUrl` vazio para preencher com o Gateway vigente. Os três serviços usam esse endereço público. |

O [roteiro integrado](validacao-integrada.md) define pré-condições, cenários, observabilidade, evidências e gravação. A existência da collection não comprova que essas chamadas passaram no ambiente.

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
