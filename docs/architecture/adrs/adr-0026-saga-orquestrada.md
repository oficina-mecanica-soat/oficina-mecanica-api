# ADR-0026 — Saga orquestrada no serviço de OS

## Status

Aceita para implementação em 09/11/2026.

## Contexto

A transação local OS/estoque não atravessa bancos independentes. É necessário coordenar o fluxo, tratar duplicatas, falhas e compensações e demonstrar a recuperação.

## Decisão

OS coordena uma máquina de estados persistida, com outbox e deduplicação. A Saga cobre preparação da execução (diagnóstico/reserva), orçamento, decisão do cliente e liberação para execução; após finalização, Orçamento/Pagamento registra cobrança Mercado Pago e pagamento confirmado libera a entrega.

Preservar os valores do enum StatusOrdemServico existente. Estados de integração e pagamento pertencem a registros separados; Finalizada não significa paga. Entrega exige pagamento confirmado. Recusa de orçamento cancela preparação e libera reservas; falha antes de iniciar execução também compensa reservas e orçamento. Falhas técnicas transitórias provocam repetição, não cancelamento imediato.

Reparo executado não é reversível: após início físico, falhas técnicas ficam pendentes de recuperação e não desfazem estoque ou serviço ficticiamente. Estorno financeiro exige operação do provedor confirmada; nunca marcar devolução apenas porque a mensagem falhou.

## Consequências

Há mais estados e mensagens, mas um coordenador permite acompanhar recuperação e demonstrar a Saga. Aguardar aprovação/pagamento é espera de negócio, sem timeout técnico curto. Compensações precisam ser idempotentes e observáveis.

## Referências

RFC-0007 e ADR-0024/ADR-0025.

