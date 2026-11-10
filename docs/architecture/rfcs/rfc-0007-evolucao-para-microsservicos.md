# RFC-0007 — Evolução para microsserviços

## Status

Contrato de implementação definido em 09/11/2026 conforme ADR-0024, ADR-0025 e ADR-0026. Os itens inicialmente abertos abaixo são resolvidos pelas especificações seguintes. Mudanças incompatíveis exigem revisão deste RFC antes de implementação.

## Contexto

O enunciado da Fase 4 exige ao menos três microsserviços independentes, repositório, infraestrutura e banco por serviço, SQL e NoSQL, mensageria, Saga com compensação, testes e deploy em Kubernetes. A aplicação existente é um monólito modular com contextos de Atendimento, Administrativo, Gestão de Estoque e Gestão de Ordem de Serviço.

## Direção acordada

- Evoluir a aplicação para microsserviços, preservando as regras de domínio e avaliando a sucessão da ADR-0001.
- Utilizar DynamoDB e SQS; avaliar SNS apenas quando um evento precisar de múltiplos consumidores. Verificar as permissões de criação e operação no AWS Academy antes do provisionamento.
- Preservar a infraestrutura e as esteiras existentes como referência, observando independência e ownership de cada serviço.
- Manter bancos isolados por serviço, com ao menos um SQL e um NoSQL. Nenhum serviço acessa diretamente o banco de outro.

## Aspectos cobertos pelo contrato abaixo

- Delimitar serviços, responsabilidades, migração de dados, APIs e ownership de bancos sem perder os fluxos atuais.
- Definir Saga, estados, comandos, eventos, compensações, falhas, repetição e idempotência; validar a integração com Mercado Pago.
- Definir contratos, segurança, correlação, observabilidade, execução local, testes e fluxo completo de demonstração.
- Detalhar implantação e destruição, custos e limites do laboratório; avaliar o impacto sobre autenticação, API Gateway e consumidores existentes.

## Critérios de aceite

- Fronteiras, decisões, contratos e critérios de falha revisados e acordados; ADRs necessárias registradas sem apagar o histórico.
- Backlog dividido por dependências e responsáveis; cada serviço com CI/CD independente, testes, cobertura, documentação e evidências exigidas pelo enunciado.

## Contrato de mensagens v1

Envelope JSON: `mensagemId` (UUID), `tipo`, `versao` (1), `sagaId`, `ordemServicoId`, `correlationId`, `causationId`, `ocorridoEm` (UTC ISO-8601) e `dados`. IDs de domínio são UUIDs; valores monetários usam decimal BRL, nunca float. Não transportar JWT, CPF/CNPJ ou credenciais. Propagar também o contexto de trace suportado pelo Datadog. A chave de idempotência de negócio é operação + OS + versão, além de mensagemId.

| Origem → destino | Tipo e dados mínimos | Resultado para OS |
| --- | --- | --- |
| OS → Execução | PrepararExecucao: filialId, veiculoId, mecanicoId, itens [{itemId, quantidade}], servicos [{servicoId, descricao, valor}] | ExecucaoPreparada: preparacaoId, versao, itens reservados e diagnóstico; ou PreparacaoRecusada: codigoMotivo |
| OS → Orçamento/Pagamento | GerarOrcamento: versao, clienteId, itens e serviços com quantidade e valor unitário congelados | OrcamentoGerado: orcamentoId, versao, valorTotal |
| Orçamento/Pagamento → OS | OrcamentoDecidido: orcamentoId, versao, decisao (Aprovado/Recusado) | OS valida versão e estado antes de avançar |
| OS → Execução | AutorizarExecucao: preparacaoId, versaoOrcamento | ExecucaoIniciada; depois ExecucaoFinalizada com finalizadoEm |
| OS → Orçamento/Pagamento | CriarCobranca: orcamentoId, versao | CobrancaCriada; PagamentoConfirmado com pagamentoId e valor; ou PagamentoRecusado |
| OS → Execução | CancelarPreparacao: preparacaoId, motivo | PreparacaoCancelada, inclusive se não houver reserva |
| OS → Orçamento/Pagamento | CancelarOrcamento: orcamentoId ou OS + versao, motivo | OrcamentoCancelado, preservando histórico |

Publicações de resultado usam a fila de OS; comandos usam a fila do destinatário. Cada handler valida tipo/versão e dados; mensagem desconhecida vai para investigação via DLQ, sem processamento permissivo.

## Estados, compensação e recuperação

1. Abrir OS persiste Recebida e Saga Preparando; emite PrepararExecucao.
2. Execução realiza diagnóstico e reserva atômica; sem saldo, retorna recusa sem reserva parcial.
3. OS solicita orçamento; ao recebê-lo, aguarda decisão. Recusa aciona CancelarPreparacao e CancelarOrcamento. Só registra Saga Compensada após ambas as confirmações.
4. Aprovação da versão atual libera execução. Conclusão registra Finalizada e solicita cobrança.
5. Pagamento confirmado pelo provedor libera Entregar OS; consulta de pagamento é local ao serviço de cobrança, nunca ao banco dele por OS.
6. Falha permanente anterior à execução física aciona compensação. Falha de compensação permanece CompensacaoPendente, com repetição e alerta; não anunciar rollback concluído sem confirmação.

Estado da Saga e versão são persistidos separadamente do enum legado. Aplicar comparação de versão na transição; mensagens antigas/duplicadas não regridem estado. Resultado recebido após cancelamento não reabre execução e deve ser reconciliado.

Filas FIFO: MessageGroupId = ordemServicoId, deduplicação = mensagemId. Visibility timeout inicial 60 s, long polling 20 s, maxReceiveCount 5, retenção 4 dias e DLQ 14 dias; ajustar por medição. Operações longas renovam visibilidade. Outbox com repetição e backoff; expiração técnica e falha permanente geram ocorrência persistida. Reprocessamento de DLQ é operação controlada pela esteira e deve ser idempotente.

## HTTP, segurança e pagamentos

Preservar rotas públicas existentes da OS e autorização por papel/cliente. APIs novas usam /api/v1/orcamentos e /api/v1/pagamentos no serviço de cobrança e /api/v1/execucoes no serviço de execução. Atualizar Gateway, OpenAPI e Postman na mesma tarefa de integração. Operações assíncronas retornam 202 com identificador e endpoint de consulta; não devolver sucesso de negócio antes da confirmação.

Decisão de orçamento exige identidade autorizada e versão atual. Consulta de cliente para a Lambda é privada, autenticada por identidade técnica segregada, e não pode ficar acessível por rota pública do Gateway. Mantém o contrato público do RFC-0001.

Mercado Pago será integrado em ambiente de teste com credenciais próprias. Criar cobrança vinculada à OS e versão, persistir referência do provedor e chave idempotente antes da chamada; em resposta incerta consultar o provedor antes de repetir. Notificação não é prova de pagamento: validar autenticidade conforme API escolhida, consultar o pagamento no provedor e conferir referência, valor, moeda e estado. Duplicatas não confirmam duas vezes. Escolher e validar o produto de checkout no primeiro spike de integração, mantendo o contrato interno acima. Nenhuma cobrança real faz parte do teste.

## Infraestrutura, execução local e CI/CD

Cada serviço mantém Dockerfile, manifests, workload Terraform, CI/CD e README próprios; dados e filas seguem ADR-0025. Workloads no EKS existente, com validação JWT, health checks, requests/limits e observabilidade conforme API. Nenhum serviço acessa banco alheio. Recursos reais somente por esteira.

Ambiente local: SQL Server em Docker conforme projeto atual, DynamoDB Local e adaptador de mensageria substituível para testes de processo. Testes de integração com SQS real usam ambiente de teste provisionado pela esteira; não afirmar equivalência completa com execução local. Cada serviço deve rodar e testar isoladamente.

Replicar os workflows atuais, preservando detecção de escopo, validação de branches, quality gate, promoção por PR, publicação de imagem e validação de rollout. Adaptar exclusivamente o que pertence ao serviço. Não copiar o acoplamento de banco do monólito nem criar deploy manual.

## Evidências de aceite

- Fluxo BDD completo: OS → diagnóstico/reserva → orçamento aprovado → execução → pagamento de teste confirmado → entrega.
- Recusa libera saldo exatamente uma vez; falha ao gerar orçamento compensa reserva; compensação interrompida retoma após reinício.
- Mensagens duplicadas, resultado tardio, reinício entre commit e envio e falha do provedor têm testes.
- Cada serviço tem cobertura mínima de 80%, análise Sonar ou equivalente no CI e evidências reais no README.
- Traces/logs correlacionados, painel de erros da Saga, DLQ e health dos três serviços demonstrados.
- Vídeo de até 15 minutos, PDF com participantes, links, arquitetura e justificativas.
